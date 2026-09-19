using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Market;
using AutoTrade.Trading.Handlers.Market;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;

namespace AutoTrade.Trading.Handlers.CQRS.Market
{
  /// <summary>
  /// Read side of the Market Manager. Decidability is computed per read from the current mode, so the queue
  /// cannot claim a proposal is decidable because it once was. The stored status is reported as it is: the
  /// effective expiry is conveyed by the deadline and by decidability, not by rewriting the record here.
  /// </summary>
  public class ProposalQueryHandler(DB db, TimeProvider timeProvider)
    : IRequestHandler<GetProposalQueue, List<ProposalSummaryDto>>,
      IRequestHandler<GetProposalDetail, ProposalDetailDto>
  {
    private const int MarketManagerStateId = 1;
    private const int QueueLimit = 200;

    public async Task<List<ProposalSummaryDto>> Handle(GetProposalQueue request, CancellationToken cancellationToken)
    {
      MarketManagerMode mode = await ReadModeAsync(cancellationToken);
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      List<Proposal> proposals = await db.Proposals
        .AsNoTracking()
        .OrderByDescending(item => item.ProposedAtUtc)
        .Take(QueueLimit)
        .ToListAsync(cancellationToken);

      Dictionary<Guid, string> basketNames = await ReadBasketNamesAsync(proposals, cancellationToken);
      List<ProposalSummaryDto> queue = new List<ProposalSummaryDto>();

      foreach (Proposal proposal in proposals)
      {
        queue.Add(new ProposalSummaryDto
        {
          ProposalId = proposal.Id,
          BasketId = proposal.BasketId,
          BasketName = basketNames.TryGetValue(proposal.BasketId, out string name) ? name : null,
          VersionNumber = proposal.VersionNumber,
          Action = proposal.Action,
          Status = proposal.Status,
          Gate = proposal.Gate,
          Confidence = proposal.Confidence,
          ExpectedRiskPercent = proposal.ExpectedRiskPercent,
          ProposedAtUtc = proposal.ProposedAtUtc,
          ExpiresAtUtc = proposal.ExpiresAtUtc,
          IsDecidable = AnalysisRules.IsDecidable(proposal.Status, mode, proposal.ExpiresAtUtc, now)
        });
      }

      return queue;
    }

    public async Task<ProposalDetailDto> Handle(GetProposalDetail request, CancellationToken cancellationToken)
    {
      MarketManagerMode mode = await ReadModeAsync(cancellationToken);
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      Proposal proposal = await db.Proposals
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.ProposalId, cancellationToken);

      if (proposal == null)
      {
        return null;
      }

      string basketName = await db.Baskets
        .AsNoTracking()
        .Where(item => item.Id == proposal.BasketId)
        .Select(item => item.Name)
        .FirstOrDefaultAsync(cancellationToken);

      DateTime? snapshotCapturedAtUtc = null;

      if (proposal.SnapshotId.HasValue)
      {
        snapshotCapturedAtUtc = await db.MarketSnapshots
          .AsNoTracking()
          .Where(item => item.Id == proposal.SnapshotId.Value)
          .Select(item => (DateTime?)item.CapturedAtUtc)
          .FirstOrDefaultAsync(cancellationToken);
      }

      List<ProposalLeg> legs = await db.ProposalLegs
        .AsNoTracking()
        .Where(item => item.ProposalId == proposal.Id)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      List<GateEvaluation> gates = await db.GateEvaluations
        .AsNoTracking()
        .Where(item => item.ProposalId == proposal.Id)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      ProposalDetailDto detail = new ProposalDetailDto
      {
        ProposalId = proposal.Id,
        BasketId = proposal.BasketId,
        BasketName = basketName,
        VersionNumber = proposal.VersionNumber,
        SnapshotId = proposal.SnapshotId,
        SnapshotCapturedAtUtc = snapshotCapturedAtUtc,
        Action = proposal.Action,
        Status = proposal.Status,
        Gate = proposal.Gate,
        Confidence = proposal.Confidence,
        ExpectedRiskPercent = proposal.ExpectedRiskPercent,
        ProposedAtUtc = proposal.ProposedAtUtc,
        ExpiresAtUtc = proposal.ExpiresAtUtc,
        DecidedAtUtc = proposal.DecidedAtUtc,
        DecidedByOperatorId = proposal.DecidedByOperatorId,
        DecisionReason = proposal.DecisionReason,
        Rationale = proposal.Rationale,
        CycleSequence = proposal.CycleSequence,
        IsDecidable = AnalysisRules.IsDecidable(proposal.Status, mode, proposal.ExpiresAtUtc, now),
        Legs = new List<ProposalLegDto>(),
        Gates = new List<RiskGateResultDto>()
      };

      foreach (ProposalLeg leg in legs)
      {
        detail.Legs.Add(new ProposalLegDto
        {
          Symbol = leg.Symbol,
          Market = leg.Market,
          Direction = leg.Direction,
          Weight = leg.Weight,
          RiskCap = leg.RiskCap,
          StopDistancePips = leg.StopDistancePips
        });
      }

      foreach (GateEvaluation gate in gates)
      {
        detail.Gates.Add(new RiskGateResultDto
        {
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

      return detail;
    }

    private async Task<MarketManagerMode> ReadModeAsync(CancellationToken cancellationToken)
    {
      MarketManagerState state = await db.MarketManagerStates
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == MarketManagerStateId, cancellationToken);

      // Fail-closed on the read side too: an unreadable mode is reported as the one that asks the most of the
      // operator, not as the one that decides on its own.
      return state != null ? state.Mode : MarketManagerMode.Manual;
    }

    private async Task<Dictionary<Guid, string>> ReadBasketNamesAsync(List<Proposal> proposals, CancellationToken cancellationToken)
    {
      List<Guid> basketIds = new List<Guid>();

      foreach (Proposal proposal in proposals)
      {
        if (!basketIds.Contains(proposal.BasketId))
        {
          basketIds.Add(proposal.BasketId);
        }
      }

      List<BasketEntity> baskets = await db.Baskets
        .AsNoTracking()
        .Where(item => basketIds.Contains(item.Id))
        .ToListAsync(cancellationToken);

      Dictionary<Guid, string> names = new Dictionary<Guid, string>();

      foreach (BasketEntity basket in baskets)
      {
        names[basket.Id] = basket.Name;
      }

      return names;
    }
  }
}
