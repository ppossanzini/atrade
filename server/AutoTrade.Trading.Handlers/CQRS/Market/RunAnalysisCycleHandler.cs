using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Market;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Market;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Risk;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BasketVersionEntity = AutoTrade.Trading.Handlers.Model.BasketVersion;

namespace AutoTrade.Trading.Handlers.CQRS.Market
{
  /// <summary>
  /// One iteration of the analysis cycle. It is the only place where a proposal is born, and it is where the
  /// three rules that keep a proposal honest are applied: nothing is proposed without a captured market,
  /// the gate is the engine's own verdict, and routing never turns a non-allow into a permission.
  /// </summary>
  public class RunAnalysisCycleHandler(DB db, IMarketDataSource marketDataSource, RiskEngine engine, IProposalSource proposalSource, IJournalWriter journalWriter, MarketOptions marketOptions, TimeProvider timeProvider)
    : IRequestHandler<RunAnalysisCycle, AnalysisCycleResultDto>
  {
    private const int ActiveVersionSlotId = 1;
    private const int KillSwitchSingletonId = 1;
    private const int MarketManagerStateId = 1;

    public async Task<AnalysisCycleResultDto> Handle(RunAnalysisCycle request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      await ExpireStaleProposalsAsync(now, cancellationToken);

      MarketManagerState state = await db.MarketManagerStates
        .FirstOrDefaultAsync(item => item.Id == MarketManagerStateId, cancellationToken);

      if (state == null)
      {
        return Skipped("MarketManagerStateMissing");
      }

      if (!state.IsAnalysisRunning)
      {
        return Skipped("AnalysisStopped");
      }

      ActiveBasketVersion activeVersion = await db.ActiveBasketVersions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == ActiveVersionSlotId, cancellationToken);

      if (activeVersion == null)
      {
        return Skipped("NoActiveVersion");
      }

      BasketVersionEntity version = await db.BasketVersions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == activeVersion.VersionId, cancellationToken);

      if (version == null)
      {
        return Skipped("ActiveVersionNotFound");
      }

      BasketVersionPolicy policy = await db.BasketVersionPolicies
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.VersionId == version.Id, cancellationToken);

      List<BasketVersionLeg> legs = await db.BasketVersionLegs
        .AsNoTracking()
        .Where(item => item.VersionId == version.Id)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      bool killSwitchEngaged = await db.KillSwitchStates
        .AsNoTracking()
        .AnyAsync(item => item.Id == KillSwitchSingletonId && item.IsEngaged, cancellationToken);

      RiskCandidate candidate = AnalysisRules.BuildCandidate(true, version.Number, policy, legs, killSwitchEngaged);
      MarketDataCapture capture = await marketDataSource.CaptureAsync(AnalysisRules.CreateSymbolRequests(candidate), cancellationToken);

      // AC-05: without a valid market snapshot no proposal is generated at all. A proposal that cannot be
      // judged is not a proposal, so the cycle reports why it stayed silent instead of recording a fiction.
      if (capture == null || !capture.IsAvailable || !capture.CapturedAtUtc.HasValue)
      {
        return Skipped("NoMarketSnapshot");
      }

      RiskEvaluationInput input = RiskInputFactory.Create(candidate, capture);
      RiskDecisionDto decision = engine.Evaluate(input);

      MarketSnapshot snapshot = PersistSnapshot(capture, legs, cancellationToken);
      ProposalCandidate proposalCandidate = proposalSource.Create(capture, input);

      Proposal proposal = new Proposal
      {
        Id = Guid.CreateVersion7(),
        BasketId = activeVersion.BasketId,
        BasketVersionId = version.Id,
        VersionNumber = version.Number,
        SnapshotId = snapshot.Id,
        Action = proposalCandidate.Action,
        EntryMode = policy != null ? policy.EntryMode : EntryMode.RegimeMomentum,
        Gate = decision.Verdict,
        Status = AnalysisRules.Route(state.Mode, decision.Verdict),
        Confidence = proposalCandidate.Confidence,
        ExpectedRiskPercent = input.BasketRiskPercent ?? 0,
        ProposedAtUtc = now,
        ExpiresAtUtc = now.AddSeconds(marketOptions.ProposalTtlSeconds),
        Rationale = proposalCandidate.Rationale,
        CycleSequence = await NextCycleSequenceAsync(cancellationToken)
      };

      db.Proposals.Add(proposal);
      PersistProposalLegs(proposal, legs);
      PersistGateEvaluations(proposal, decision);

      state.LastCycleAtUtc = now;
      state.UpdatedAtUtc = now;

      await db.SaveChangesAsync(cancellationToken);

      string payload = string.Format(
        "proposal={0};action={1};gate={2};status={3};version={4};confidence={5};risk={6}",
        proposal.Id, proposal.Action, proposal.Gate, proposal.Status, proposal.VersionNumber, proposal.Confidence, proposal.ExpectedRiskPercent);

      await journalWriter.AppendSystemEventAsync(JournalEventKind.ProposalGenerated, "Proposal", proposal.Id, payload, cancellationToken);

      if (proposal.Status == ProposalStatus.AutoApproved)
      {
        await journalWriter.AppendSystemEventAsync(JournalEventKind.ProposalAutoApproved, "Proposal", proposal.Id, payload, cancellationToken);
      }

      return new AnalysisCycleResultDto
      {
        Proposed = true,
        CycleSequence = proposal.CycleSequence,
        Status = proposal.Status,
        Gate = proposal.Gate
      };
    }

    private async Task ExpireStaleProposalsAsync(DateTime now, CancellationToken cancellationToken)
    {
      List<Proposal> stale = await db.Proposals
        .Where(item => (item.Status == ProposalStatus.NeedsReview || item.Status == ProposalStatus.AutoApproved) && item.ExpiresAtUtc <= now)
        .ToListAsync(cancellationToken);

      if (stale.Count == 0)
      {
        return;
      }

      foreach (Proposal proposal in stale)
      {
        proposal.Status = ProposalStatus.Expired;
      }

      await db.SaveChangesAsync(cancellationToken);

      foreach (Proposal proposal in stale)
      {
        await journalWriter.AppendSystemEventAsync(JournalEventKind.ProposalExpired, "Proposal", proposal.Id, "expired=" + proposal.ExpiresAtUtc.ToString("O"), cancellationToken);
      }
    }

    private MarketSnapshot PersistSnapshot(MarketDataCapture capture, List<BasketVersionLeg> legs, CancellationToken cancellationToken)
    {
      MarketSnapshot snapshot = new MarketSnapshot
      {
        Id = Guid.CreateVersion7(),
        CapturedAtUtc = capture.CapturedAtUtc.Value,
        AccountEquity = capture.Account != null ? capture.Account.Equity : 0,
        AccountBalance = capture.Account != null ? capture.Account.Balance : 0,
        RealizedPnlToday = capture.Account != null ? capture.Account.RealizedPnlToday : 0,
        UnrealizedPnl = capture.Account != null ? capture.Account.UnrealizedPnl : 0
      };

      db.MarketSnapshots.Add(snapshot);

      // The snapshot stores the values of the version's own legs, so the record of a proposal is exactly what
      // that proposal was judged on and not the whole feed of the moment.
      int ordinal = 0;

      foreach (BasketVersionLeg leg in legs)
      {
        SymbolCapture quote = FindSymbol(capture, leg.Symbol);

        db.MarketSnapshotLegs.Add(new MarketSnapshotLeg
        {
          Id = Guid.CreateVersion7(),
          SnapshotId = snapshot.Id,
          Ordinal = ordinal++,
          Symbol = leg.Symbol,
          Market = leg.Market,
          Price = quote != null ? quote.Price : null,
          SpreadPips = quote != null ? quote.SpreadPips : null,
          VolatilityPercent = quote != null ? quote.VolatilityPercent : null,
          IsTradable = quote != null && quote.IsTradable
        });
      }

      return snapshot;
    }

    private static SymbolCapture FindSymbol(MarketDataCapture capture, string symbol)
    {
      foreach (SymbolCapture quote in capture.Symbols)
      {
        if (string.Equals(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
          return quote;
        }
      }

      return null;
    }

    private void PersistProposalLegs(Proposal proposal, List<BasketVersionLeg> legs)
    {
      int ordinal = 0;

      foreach (BasketVersionLeg leg in legs)
      {
        db.ProposalLegs.Add(new ProposalLeg
        {
          Id = Guid.CreateVersion7(),
          ProposalId = proposal.Id,
          Ordinal = ordinal++,
          Symbol = leg.Symbol,
          Market = leg.Market,
          Direction = leg.Direction,
          Weight = leg.Weight,
          RiskCap = leg.RiskCap,
          StopDistancePips = leg.StopDistancePips,
          MaxSpreadPips = leg.MaxSpreadPips,
          MaxVolatilityPercent = leg.MaxVolatilityPercent
        });
      }
    }

    private void PersistGateEvaluations(Proposal proposal, RiskDecisionDto decision)
    {
      int ordinal = 0;

      foreach (RiskGateResultDto gate in decision.Gates)
      {
        db.GateEvaluations.Add(new GateEvaluation
        {
          Id = Guid.CreateVersion7(),
          ProposalId = proposal.Id,
          Ordinal = ordinal++,
          Code = gate.Code,
          Verdict = gate.Verdict,
          Subject = gate.Subject,
          Market = gate.Market,
          ObservedValue = gate.ObservedValue,
          ThresholdValue = gate.ThresholdValue,
          Unit = gate.Unit,
          EvaluatedAtUtc = gate.EvaluatedAtUtc,
          Detail = gate.Detail
        });
      }
    }

    private async Task<int> NextCycleSequenceAsync(CancellationToken cancellationToken)
    {
      int? last = await db.Proposals.Select(item => (int?)item.CycleSequence).MaxAsync(cancellationToken);

      return (last ?? 0) + 1;
    }

    private static AnalysisCycleResultDto Skipped(string reason)
    {
      return new AnalysisCycleResultDto
      {
        Proposed = false,
        Reason = reason
      };
    }
  }
}
