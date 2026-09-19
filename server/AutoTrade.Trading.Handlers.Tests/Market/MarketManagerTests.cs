using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Market;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Market;
using AutoTrade.Trading.Handlers.Market;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Market
{
  /// <summary>
  /// The Market Manager seen through the mediator: what the cycle proposes, how the mode routes it, and what
  /// happens when the operator decides. The tests run the production pipeline, with the simulated source
  /// behind the market data seam, so the same code path that runs in the host is exercised here.
  /// </summary>
  public class MarketManagerTests
  {
    private static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Dictionary<string, string> Settings(string mode = "Supervised", bool running = true, double spreadLimit = 1.5, double ttlSeconds = 300)
    {
      return new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "Simulated" },
        { "Trading:MarketData:Simulated:Seed", "7" },
        { "Trading:MarketData:Simulated:JitterPercent", "0" },
        { "Trading:MarketData:Simulated:Equity", "10000" },
        { "Trading:MarketData:Simulated:RealizedPnlToday", "0" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:Market", "Fx" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:SpreadPips", "0.8" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:VolatilityPercent", "0.2" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:Market", "Metal" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:SpreadPips", "28" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:VolatilityPercent", "0.5" },
        { "Trading:MarketData:Simulated:Symbols:USDJPY:Market", "Fx" },
        { "Trading:MarketData:Simulated:Symbols:USDJPY:SpreadPips", "0.7" },
        { "Trading:MarketData:Simulated:Symbols:USDJPY:VolatilityPercent", "0.25" },
        { "Trading:MarketData:Simulated:Symbols:USDJPY:IsTradable", "false" },
        { "Trading:Risk:SnapshotMaxAgeSeconds", "60" },
        { "Trading:Risk:Markets:Fx:LegSpreadMaxPips", spreadLimit.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        { "Trading:Risk:Markets:Fx:LegVolatilityMaxPercent", "0.35" },
        { "Trading:Risk:Markets:Metal:LegSpreadMaxPips", "40" },
        { "Trading:Risk:Markets:Metal:LegVolatilityMaxPercent", "0.8" },
        { "Trading:Risk:Markets:Index:LegSpreadMaxPips", "5" },
        { "Trading:Risk:Markets:Index:LegVolatilityMaxPercent", "0.6" },
        { "Trading:Market:CycleSeconds", "30" },
        { "Trading:Market:ProposalTtlSeconds", ttlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) }
      };
    }

    private static TradingTestContext CreateContext(string mode = "Supervised", bool running = true, double spreadLimit = 1.5, double ttlSeconds = 300)
    {
      TradingTestContext context = new TradingTestContext(Settings(mode, running, spreadLimit, ttlSeconds));
      context.Clock.Set(Start);

      return context;
    }

    /// <summary>Seeds a released kill switch, the manager state, an active version and its frozen policy.</summary>
    private static Guid SeedActiveVersion(
      TradingTestContext context,
      string mode = "Supervised",
      bool running = true,
      double riskPerBasket = 2.0,
      double dailyLossLimit = 2.5,
      string secondSymbol = "XAUUSD",
      MarketKind secondMarket = MarketKind.Metal,
      FailurePolicy failurePolicy = FailurePolicy.MinimumCoverage,
      int minimumCoverage = 75)
    {
      context.Db.KillSwitchStates.Add(new KillSwitchState { Id = 1, IsEngaged = false });
      context.Db.MarketManagerStates.Add(new MarketManagerState
      {
        Id = 1,
        Mode = Enum.Parse<MarketManagerMode>(mode),
        IsAnalysisRunning = running,
        UpdatedAtUtc = Start
      });

      Guid basketId = Guid.CreateVersion7();
      Guid versionId = Guid.CreateVersion7();

      context.Db.Baskets.Add(new Basket { Id = basketId, Name = "Momentum", CreatedAtUtc = Start, UpdatedAtUtc = Start });
      context.Db.BasketVersions.Add(new BasketVersion
      {
        Id = versionId,
        BasketId = basketId,
        Number = 1,
        Note = "seed",
        CreatedAtUtc = Start,
        CreatedByOperatorId = Guid.Empty,
        PublishedAtUtc = Start
      });

      context.Db.BasketVersionPolicies.Add(new BasketVersionPolicy
      {
        Id = Guid.CreateVersion7(),
        VersionId = versionId,
        FailurePolicy = failurePolicy,
        MinimumCoverage = minimumCoverage,
        RiskPerBasket = riskPerBasket,
        DailyLossLimit = dailyLossLimit
      });

      context.Db.BasketVersionLegs.Add(new BasketVersionLeg
      {
        Id = Guid.CreateVersion7(),
        VersionId = versionId,
        Ordinal = 0,
        Symbol = "EURUSD",
        Market = MarketKind.Fx,
        Direction = LegDirection.Long,
        TimeFrame = TimeFrame.H1,
        Weight = 60,
        RiskCap = 0.5
      });

      context.Db.BasketVersionLegs.Add(new BasketVersionLeg
      {
        Id = Guid.CreateVersion7(),
        VersionId = versionId,
        Ordinal = 1,
        Symbol = secondSymbol,
        Market = secondMarket,
        Direction = LegDirection.Short,
        TimeFrame = TimeFrame.M5,
        Weight = 40,
        RiskCap = 0.3
      });

      context.Db.ActiveBasketVersions.Add(new ActiveBasketVersion
      {
        Id = 1,
        BasketId = basketId,
        VersionId = versionId,
        ActivatedAtUtc = Start,
        ActivatedByOperatorId = Guid.Empty
      });

      context.Db.SaveChanges();

      return versionId;
    }

    [Fact]
    public async Task Cycle_WhenTheAnalysisIsStopped_ProposesNothing()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context, running: false);

      AnalysisCycleResultDto result = await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Assert.False(result.Proposed);
      Assert.Equal("AnalysisStopped", result.Reason);
      Assert.Empty(context.Db.Proposals);
    }

    [Fact]
    public async Task Cycle_WithoutAMarketSnapshot_ProposesNothing()
    {
      using TradingTestContext context = new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Market:CycleSeconds", "30" },
        { "Trading:Market:ProposalTtlSeconds", "300" }
      });
      context.Clock.Set(Start);
      SeedActiveVersion(context);

      AnalysisCycleResultDto result = await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      // AC-05: a cycle that cannot capture the market stays silent instead of proposing something unjudged.
      Assert.False(result.Proposed);
      Assert.Equal("NoMarketSnapshot", result.Reason);
      Assert.Empty(context.Db.Proposals);
      Assert.Empty(context.Db.MarketSnapshots);
    }

    [Fact]
    public async Task Cycle_WithAnAllowedGate_InSupervisedMode_IsRoutedForwardAutomatically()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context);

      AnalysisCycleResultDto result = await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Proposal proposal = Assert.Single(context.Db.Proposals);

      Assert.True(result.Proposed);
      Assert.Equal(RiskGateVerdict.Allow, proposal.Gate);
      Assert.Equal(ProposalStatus.AutoApproved, proposal.Status);
      Assert.Equal(ProposalAction.Entry, proposal.Action);
      Assert.Equal(Start.AddSeconds(300), proposal.ExpiresAtUtc);
      Assert.NotNull(proposal.SnapshotId);
      Assert.Equal(100, proposal.Confidence);
      Assert.Equal(0.8, proposal.ExpectedRiskPercent);

      // The proposal keeps both its own legs and the gate rows it was routed on.
      Assert.Equal(2, context.Db.ProposalLegs.Count(item => item.ProposalId == proposal.Id));
      Assert.Equal(10, context.Db.GateEvaluations.Count(item => item.ProposalId == proposal.Id));
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalGenerated);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalAutoApproved);
    }

    [Fact]
    public async Task Cycle_WithAnAllowedGate_InManualMode_WaitsForTheOperator()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Proposal proposal = Assert.Single(context.Db.Proposals);

      Assert.Equal(ProposalStatus.NeedsReview, proposal.Status);
      Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalAutoApproved);
    }

    [Fact]
    public async Task Cycle_WithAReviewGate_NeverRoutesForwardInAnyMode()
    {
      // A symbol that cannot be traded and a policy that requires a confirmation for partial coverage produce
      // the review verdict, which is where the modes differ the most.
      using TradingTestContext context = CreateContext("Automatic");
      SeedActiveVersion(
        context,
        "Automatic",
        secondSymbol: "USDJPY",
        secondMarket: MarketKind.Fx,
        failurePolicy: FailurePolicy.RequireConfirmation,
        minimumCoverage: 50);

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Proposal proposal = Assert.Single(context.Db.Proposals);

      Assert.Equal(RiskGateVerdict.Review, proposal.Gate);
      Assert.Equal(ProposalStatus.NeedsReview, proposal.Status);
      Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalAutoApproved);
    }

    [Fact]
    public async Task Cycle_WithABlockedGate_RecordsABlockedProposalThatNobodyCanDecide()
    {
      using TradingTestContext context = CreateContext("Manual", spreadLimit: 0.49);
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Proposal proposal = Assert.Single(context.Db.Proposals);

      Assert.Equal(RiskGateVerdict.Block, proposal.Gate);
      Assert.Equal(ProposalStatus.Blocked, proposal.Status);
      Assert.False(AnalysisRules.IsDecidable(proposal.Status, MarketManagerMode.Manual, proposal.ExpiresAtUtc, Start));

      ProposalDecisionResultDto refused = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7()
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.NotDecidable, refused.Outcome);
    }

    [Fact]
    public async Task Cycle_PersistsTheMarketCaptureTheProposalWasJudgedOn()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context);

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      MarketSnapshot snapshot = Assert.Single(context.Db.MarketSnapshots);

      Assert.Equal(Start, snapshot.CapturedAtUtc);
      Assert.Equal(10000, snapshot.AccountEquity);

      List<MarketSnapshotLeg> legs = context.Db.MarketSnapshotLegs
        .Where(item => item.SnapshotId == snapshot.Id)
        .OrderBy(item => item.Ordinal)
        .ToList();

      Assert.Equal(2, legs.Count);
      Assert.Equal(0.8, legs[0].SpreadPips);
      Assert.Equal(28, legs[1].SpreadPips);
      Assert.True(legs[0].IsTradable);
    }

    [Fact]
    public async Task Decide_InAutomaticMode_IsRefusedSoTheOperatorChoosesTheModeOnPurpose()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      // The same proposal becomes undecidable as soon as the mode that asks the operator is left.
      await context.Hikyaku.Send(new SetMarketManagerMode { Mode = MarketManagerMode.Automatic, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      ProposalDecisionResultDto result = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7()
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.NotDecidable, result.Outcome);
      Assert.Equal(ProposalStatus.NeedsReview, context.Db.Proposals.Single(item => item.Id == proposal.Id).Status);
    }

    [Fact]
    public async Task Decide_ApprovesAReviewableProposalAndRecordsWhoDecided()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);
      Guid operatorId = Guid.CreateVersion7();

      ProposalDecisionResultDto result = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = operatorId
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.Applied, result.Outcome);
      Assert.Equal(ProposalStatus.Approved, result.Status);

      Proposal stored = context.Db.Proposals.Single(item => item.Id == proposal.Id);

      Assert.Equal(operatorId, stored.DecidedByOperatorId);
      Assert.Equal(Start, stored.DecidedAtUtc);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalApproved);
    }

    [Fact]
    public async Task Decide_RefusesAnApprovalWhenTheGateNoLongerAllows()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      // Engaging the kill switch makes the current gate a block, so the approval the operator was about to
      // give no longer describes the situation.
      KillSwitchState killSwitch = context.Db.KillSwitchStates.Single(item => item.Id == 1);
      killSwitch.IsEngaged = true;
      context.Db.SaveChanges();

      ProposalDecisionResultDto result = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7()
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.GateRegressed, result.Outcome);
      Assert.Equal(ProposalStatus.NeedsReview, context.Db.Proposals.Single(item => item.Id == proposal.Id).Status);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalDecisionRefused);
    }

    [Fact]
    public async Task Decide_RejectingStaysPossibleEvenWhenTheGateWorsened()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      KillSwitchState killSwitch = context.Db.KillSwitchStates.Single(item => item.Id == 1);
      killSwitch.IsEngaged = true;
      context.Db.SaveChanges();

      ProposalDecisionResultDto result = await context.Hikyaku.Send(new RejectProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7(),
        Reason = "Mercato troppo instabile."
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.Applied, result.Outcome);
      Assert.Equal(ProposalStatus.Rejected, result.Status);
      Assert.Equal("Mercato troppo instabile.", context.Db.Proposals.Single(item => item.Id == proposal.Id).DecisionReason);
    }

    [Fact]
    public async Task Decide_Twice_ReportsThatADecisionAlreadyExists()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      await context.Hikyaku.Send(new SuspendProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7(),
        Reason = "Attendo la chiusura di Londra."
      }, CancellationToken.None);

      ProposalDecisionResultDto second = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7()
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.AlreadyDecided, second.Outcome);
    }

    [Fact]
    public async Task Cycle_ExpiresAProposalThatRanOutOfTime_AndRecordsIt()
    {
      using TradingTestContext context = CreateContext("Manual", ttlSeconds: 60);
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      context.Clock.Advance(TimeSpan.FromSeconds(61));

      AnalysisCycleResultDto result = await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      Assert.True(result.Proposed);
      Assert.Equal(ProposalStatus.Expired, context.Db.Proposals.Single(item => item.Id == proposal.Id).Status);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ProposalExpired && item.EntityId == proposal.Id);

      ProposalDecisionResultDto refused = await context.Hikyaku.Send(new ApproveProposal
      {
        ProposalId = proposal.Id,
        OperatorId = Guid.CreateVersion7()
      }, CancellationToken.None);

      Assert.Equal(ProposalDecisionOutcome.AlreadyDecided, refused.Outcome);
    }

    [Fact]
    public async Task Queue_ReportsDecidabilityFromTheCurrentModeInsteadOfTheStoredStatus()
    {
      using TradingTestContext context = CreateContext("Manual");
      SeedActiveVersion(context, "Manual");

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);

      List<ProposalSummaryDto> queue = await context.Hikyaku.Send(new GetProposalQueue(), CancellationToken.None);

      ProposalSummaryDto row = Assert.Single(queue);

      Assert.Equal("Momentum", row.BasketName);
      Assert.True(row.IsDecidable);

      await context.Hikyaku.Send(new SetMarketManagerMode { Mode = MarketManagerMode.Supervised, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      // Supervised still asks the operator, so the proposal stays decidable; the mode only changes forwarding.
      List<ProposalSummaryDto> supervised = await context.Hikyaku.Send(new GetProposalQueue(), CancellationToken.None);

      Assert.True(Assert.Single(supervised).IsDecidable);

      await context.Hikyaku.Send(new SetMarketManagerMode { Mode = MarketManagerMode.Automatic, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      List<ProposalSummaryDto> automatic = await context.Hikyaku.Send(new GetProposalQueue(), CancellationToken.None);

      Assert.False(Assert.Single(automatic).IsDecidable);
    }

    [Fact]
    public async Task Detail_ReturnsTheGateRowsAndTheSnapshotOfTheProposal()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context);

      await context.Hikyaku.Send(new RunAnalysisCycle(), CancellationToken.None);
      Proposal proposal = Assert.Single(context.Db.Proposals);

      ProposalDetailDto detail = await context.Hikyaku.Send(new GetProposalDetail { ProposalId = proposal.Id }, CancellationToken.None);

      Assert.Equal("Momentum", detail.BasketName);
      Assert.Equal(Start, detail.SnapshotCapturedAtUtc);
      Assert.Equal(10, detail.Gates.Count);
      Assert.Equal(2, detail.Legs.Count);
      Assert.StartsWith("entry|v1|", detail.Rationale);
    }

    [Fact]
    public async Task AnalysisState_CannotStartWithoutAConfiguredCycleOrAnActiveVersion()
    {
      using TradingTestContext context = new TradingTestContext(new Dictionary<string, string>());
      context.Clock.Set(Start);

      context.Db.KillSwitchStates.Add(new KillSwitchState { Id = 1, IsEngaged = false });
      context.Db.MarketManagerStates.Add(new MarketManagerState { Id = 1, Mode = MarketManagerMode.Supervised, IsAnalysisRunning = false, UpdatedAtUtc = Start });
      context.Db.SaveChanges();

      bool started = await context.Hikyaku.Send(new SetAnalysisState { IsRunning = true, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      Assert.False(started);
      Assert.False(context.Db.MarketManagerStates.Single(item => item.Id == 1).IsAnalysisRunning);
    }

    [Fact]
    public async Task AnalysisState_StartAndStopAreJournalled()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context, running: false);
      Guid operatorId = Guid.CreateVersion7();

      bool started = await context.Hikyaku.Send(new SetAnalysisState { IsRunning = true, OperatorId = operatorId }, CancellationToken.None);
      bool stopped = await context.Hikyaku.Send(new SetAnalysisState { IsRunning = false, OperatorId = operatorId }, CancellationToken.None);

      Assert.True(started);
      Assert.True(stopped);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.AnalysisStarted);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.AnalysisStopped);
    }

    [Fact]
    public async Task Mode_ChangeIsJournalledOnlyWhenItChangesSomething()
    {
      using TradingTestContext context = CreateContext();
      SeedActiveVersion(context);
      Guid operatorId = Guid.CreateVersion7();

      await context.Hikyaku.Send(new SetMarketManagerMode { Mode = MarketManagerMode.Supervised, OperatorId = operatorId }, CancellationToken.None);

      Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.MarketManagerModeChanged);

      await context.Hikyaku.Send(new SetMarketManagerMode { Mode = MarketManagerMode.Manual, OperatorId = operatorId }, CancellationToken.None);

      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.MarketManagerModeChanged);
      Assert.Equal(MarketManagerMode.Manual, context.Db.MarketManagerStates.Single(item => item.Id == 1).Mode);
    }
  }
}
