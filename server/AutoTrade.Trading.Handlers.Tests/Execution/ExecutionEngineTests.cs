using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Execution;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Xunit;
using ExecutionEntity = AutoTrade.Trading.Handlers.Model.Execution;

namespace AutoTrade.Trading.Handlers.Tests.Execution
{
    /// <summary>
    /// The execution engine end to end, through the mediator and the simulated gateway. What is pinned here is
    /// the part that must never be wrong: nothing is sent before it is persisted, one leg at a time, an event is
    /// applied once, silence never becomes a rejection, and coverage is measured from what was really filled.
    /// </summary>
    public class ExecutionEngineTests
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Dictionary<string, string> Settings(string eurUsdBehaviour = "Filled", string goldBehaviour = "PartialFill", double goldFillRatio = 0.6, bool duplicateGoldEvent = false)
        {
            return new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "Simulated" },
        { "Trading:MarketData:Simulated:Seed", "11" },
        { "Trading:MarketData:Simulated:JitterPercent", "0" },
        { "Trading:MarketData:Simulated:Equity", "10000" },
        { "Trading:MarketData:Simulated:AccountCurrency", "USD" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:Market", "Fx" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:BasePrice", "1.085" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:SpreadPips", "0.8" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:VolatilityPercent", "0.2" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:MinVolume", "1000" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:StepVolume", "1000" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:PipSizePerUnit", "0.0001" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:ProfitCurrency", "USD" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:Market", "Metal" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:BasePrice", "2650" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:SpreadPips", "28" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:VolatilityPercent", "0.5" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:MinVolume", "1" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:StepVolume", "1" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:PipSizePerUnit", "0.01" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:ProfitCurrency", "USD" },
        { "Trading:Execution:Provider", "Simulated" },
        { "Trading:Execution:Simulated:DefaultBehaviour", "Filled" },
        { "Trading:Execution:Simulated:Symbols:EURUSD:Behaviour", eurUsdBehaviour },
        { "Trading:Execution:Simulated:Symbols:XAUUSD:Behaviour", goldBehaviour },
        { "Trading:Execution:Simulated:Symbols:XAUUSD:FillRatio", goldFillRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        { "Trading:Execution:Simulated:Symbols:XAUUSD:DuplicateEvent", duplicateGoldEvent ? "true" : "false" }
      };
        }

        /// <summary>Seeds an approved proposal of a version with two sized legs, which is what the engine starts from.</summary>
        private static (Guid ProposalId, Guid ExecutionId) SeedApprovedProposal(TradingTestContext context, FailurePolicy policy = FailurePolicy.MinimumCoverage, int minimumCoverage = 75, bool killSwitchEngaged = false)
        {
            Guid basketId = Guid.CreateVersion7();
            Guid versionId = Guid.CreateVersion7();
            Guid proposalId = Guid.CreateVersion7();

            context.Db.KillSwitchStates.Add(new KillSwitchState { Id = 1, IsEngaged = killSwitchEngaged });
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
                FailurePolicy = policy,
                MinimumCoverage = minimumCoverage,
                RiskPerBasket = 3,
                DailyLossLimit = 2.5
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
                RiskCap = 1.5,
                StopDistancePips = 20
            });

            context.Db.BasketVersionLegs.Add(new BasketVersionLeg
            {
                Id = Guid.CreateVersion7(),
                VersionId = versionId,
                Ordinal = 1,
                Symbol = "XAUUSD",
                Market = MarketKind.Metal,
                Direction = LegDirection.Short,
                TimeFrame = TimeFrame.M5,
                Weight = 40,
                RiskCap = 0.8,
                StopDistancePips = 500
            });

            context.Db.Proposals.Add(new Proposal
            {
                Id = proposalId,
                BasketId = basketId,
                BasketVersionId = versionId,
                VersionNumber = 1,
                Action = ProposalAction.Entry,
                Gate = RiskGateVerdict.Allow,
                Status = ProposalStatus.Approved,
                Confidence = 100,
                ExpectedRiskPercent = 2.3,
                ProposedAtUtc = Start,
                ExpiresAtUtc = Start.AddMinutes(5),
                DecidedAtUtc = Start,
                DecidedByOperatorId = Guid.Empty,
                Rationale = "entry|v1",
                CycleSequence = 1
            });

            context.Db.SaveChanges();

            return (proposalId, Guid.Empty);
        }

        private static TradingTestContext CreateContext(string eurUsdBehaviour = "Filled", string goldBehaviour = "PartialFill", double goldFillRatio = 0.6, bool duplicateGoldEvent = false)
        {
            TradingTestContext context = new TradingTestContext(Settings(eurUsdBehaviour, goldBehaviour, goldFillRatio, duplicateGoldEvent));
            context.Clock.Set(Start);

            return context;
        }

        [Fact]
        public async Task Start_SizesBothLegsAndFillsThemInTheFrozenOrder()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.Applied, result.Outcome);

            List<ExecutionLeg> legs = context.Db.ExecutionLegs.Where(item => item.ExecutionId == result.ExecutionId).OrderBy(item => item.Ordinal).ToList();

            Assert.Equal(2, legs.Count);

            // EURUSD: 150 USD at risk / (20 pips * 0.0001) = 75.000 units. XAUUSD: 80 / (500 * 0.01) = 16 units.
            Assert.Equal(75000, legs[0].VolumeUnits);
            Assert.Equal(16, legs[1].VolumeUnits);
            Assert.Equal(ExecutionLegStatus.Filled, legs[0].Status);
            Assert.Equal(75000, legs[0].FilledVolumeUnits);
            Assert.All(legs, leg => Assert.StartsWith("AT-", leg.ClientOrderId));
            Assert.Equal(2, legs.Select(leg => leg.ClientOrderId).Distinct().Count());
        }

        [Fact]
        public async Task Start_PersistsAnAcceptedEventPerLegAndOneRowPerBrokerEvent()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            List<BrokerEvent> events = context.Db.BrokerEvents.Where(item => item.ExecutionId == result.ExecutionId).ToList();

            // Accepted + filled for EURUSD, accepted + partial for XAUUSD.
            Assert.Equal(4, events.Count);
            Assert.Equal(4, events.Select(item => item.BrokerEventId).Distinct().Count());
        }

        [Fact]
        public async Task Start_WithADuplicateEvent_CountsTheFillOnce()
        {
            using TradingTestContext context = CreateContext(goldBehaviour: "Filled", duplicateGoldEvent: true);
            (Guid proposalId, _) = SeedApprovedProposal(context);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            ExecutionLeg gold = context.Db.ExecutionLegs.Single(item => item.ExecutionId == result.ExecutionId && item.Symbol == "XAUUSD");

            // The repeated event is stored and journalled, but the volume is not counted twice.
            Assert.Equal(16, gold.FilledVolumeUnits);
            Assert.Equal(ExecutionLegStatus.Filled, gold.Status);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerEventDuplicateIgnored);
        }

        [Fact]
        public async Task Start_WithAPartialFillThatSatisfiesThePolicy_CompletesPartial()
        {
            using TradingTestContext context = CreateContext(goldFillRatio: 0.6);
            (Guid proposalId, _) = SeedApprovedProposal(context, FailurePolicy.MinimumCoverage, minimumCoverage: 75);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            // 75.000 filled of 75.016 planned is 99%, above the 75% the policy requires.
            Assert.Equal(ExecutionStatus.CompletedPartial, result.Status);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionCompletedPartial);
        }

        [Fact]
        public async Task Start_WithAPartialFillThatBreaksAllOrNothing_RequiresACompensation()
        {
            using TradingTestContext context = CreateContext(goldFillRatio: 0.6);
            (Guid proposalId, _) = SeedApprovedProposal(context, FailurePolicy.AllOrNothing);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionStatus.CompensationRequired, result.Status);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionCompensationRequired);
        }

        [Fact]
        public async Task Start_WhenTheProviderDoesNotAnswer_StopsInReconciliationAndSendsNothingElse()
        {
            using TradingTestContext context = CreateContext(eurUsdBehaviour: "NoResponse", goldBehaviour: "Filled");
            (Guid proposalId, _) = SeedApprovedProposal(context);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            List<ExecutionLeg> legs = context.Db.ExecutionLegs.Where(item => item.ExecutionId == result.ExecutionId).OrderBy(item => item.Ordinal).ToList();

            Assert.Equal(ExecutionStatus.ReconciliationRequired, result.Status);
            Assert.Equal(ExecutionLegStatus.TimedOut, legs[0].Status);

            // The second leg was never sent: an unknown outcome must not be compounded by another order.
            Assert.Equal(ExecutionLegStatus.Pending, legs[1].Status);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionReconciliationRequired);
        }

        [Fact]
        public async Task Start_WithARejectedFirstLegAndAllOrNothing_DoesNotSendTheSecond()
        {
            using TradingTestContext context = CreateContext(eurUsdBehaviour: "Rejected", goldBehaviour: "Filled");
            (Guid proposalId, _) = SeedApprovedProposal(context, FailurePolicy.AllOrNothing);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            ExecutionLeg gold = context.Db.ExecutionLegs.Single(item => item.ExecutionId == result.ExecutionId && item.Symbol == "XAUUSD");

            Assert.Equal(ExecutionStatus.CompensationRequired, result.Status);
            Assert.Equal(ExecutionLegStatus.Pending, gold.Status);
        }

        [Fact]
        public async Task Start_WithoutAStopDistance_SendsNothingAndWritesNothing()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context);

            BasketVersionLeg gold = context.Db.BasketVersionLegs.Single(item => item.Symbol == "XAUUSD");
            gold.StopDistancePips = 0;
            context.Db.SaveChanges();

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.NotConfigured, result.Outcome);
            Assert.Contains("XAUUSD", result.Reason);
            Assert.Contains("stop_distance_not_configured", result.Reason);

            // Nothing was written, so the operator can fix the leg and retry: no execution, no legs, no orders.
            Assert.Empty(context.Db.Executions);
            Assert.Empty(context.Db.ExecutionLegs);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionStartRefused);
        }

        [Fact]
        public async Task Start_TwiceOnTheSameProposal_IsRefused()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context);

            await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);
            ExecutionStartResultDto second = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.AlreadyExecuted, second.Outcome);
            Assert.Single(context.Db.Executions.Where(item => item.ProposalId == proposalId));

            // The refused attempt is part of the record: the operator asked and the answer was no.
            JournalEvent refusal = Assert.Single(context.Db.JournalEvents.Where(item => item.Kind == JournalEventKind.ExecutionStartRefused));
            Assert.Contains("proposal_already_executed", refusal.Payload);
            Assert.Equal(proposalId, refusal.EntityId);
        }

        [Fact]
        public async Task Start_OnAProposalThatIsNotApproved_IsNotAuthorized()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context);

            Proposal proposal = context.Db.Proposals.Single(item => item.Id == proposalId);
            proposal.Status = ProposalStatus.NeedsReview;
            context.Db.SaveChanges();

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.NotAuthorized, result.Outcome);
            Assert.Empty(context.Db.Executions);

            JournalEvent refusal = Assert.Single(context.Db.JournalEvents.Where(item => item.Kind == JournalEventKind.ExecutionStartRefused));
            Assert.Contains("proposal_not_approved:NeedsReview", refusal.Payload);
            Assert.Equal(proposalId, refusal.EntityId);
        }

        [Fact]
        public async Task Start_WithTheKillSwitchEngaged_IsBlocked()
        {
            using TradingTestContext context = CreateContext();
            (Guid proposalId, _) = SeedApprovedProposal(context, killSwitchEngaged: true);

            ExecutionStartResultDto result = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.Blocked, result.Outcome);
            Assert.Empty(context.Db.Executions);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionBlocked);
        }

        [Fact]
        public async Task Compensation_CreatesANewExecutionWithTheOppositeSideAndClosesTheOriginal()
        {
            using TradingTestContext context = CreateContext(goldFillRatio: 0.6);
            (Guid proposalId, _) = SeedApprovedProposal(context, FailurePolicy.AllOrNothing);

            ExecutionStartResultDto original = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);
            Assert.Equal(ExecutionStatus.CompensationRequired, original.Status);

            ExecutionStartResultDto compensation = await context.Hikyaku.Send(new ConfirmCompensation
            {
                ExecutionId = original.ExecutionId,
                OperatorId = Guid.CreateVersion7(),
                Reason = "Fill parziale: chiudo l'esposizione residua."
            }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.Applied, compensation.Outcome);

            List<ExecutionLeg> compensationLegs = context.Db.ExecutionLegs.Where(item => item.ExecutionId == compensation.ExecutionId).OrderBy(item => item.Ordinal).ToList();

            Assert.Equal(2, compensationLegs.Count);

            // Every compensating leg mirrors the filled volume on the opposite side, with its own client order id.
            Assert.All(compensationLegs, leg => Assert.True(leg.FilledVolumeUnits > 0));
            Assert.Equal(LegDirection.Short, compensationLegs.Single(item => item.Symbol == "EURUSD").Direction);
            Assert.Equal(LegDirection.Long, compensationLegs.Single(item => item.Symbol == "XAUUSD").Direction);
            Assert.Equal(75000, compensationLegs.Single(item => item.Symbol == "EURUSD").VolumeUnits);

            // The plan asked for 16, the provider filled 9: the compensation mirrors what really happened, not the intent.
            Assert.Equal(9, compensationLegs.Single(item => item.Symbol == "XAUUSD").VolumeUnits);

            // The provider fills gold partially again, so the compensation is itself incomplete and says so: a
            // compensation is a new sequence exposed to the same market, never an atomic rollback.
            Assert.Equal(ExecutionLegStatus.Filled, compensationLegs.Single(item => item.Symbol == "EURUSD").Status);
            Assert.Equal(ExecutionLegStatus.PartiallyFilled, compensationLegs.Single(item => item.Symbol == "XAUUSD").Status);
            Assert.Equal(ExecutionStatus.CompensationRequired, compensation.Status);

            ExecutionEntity stored = context.Db.Executions.Single(item => item.Id == original.ExecutionId);
            ExecutionEntity compensating = context.Db.Executions.Single(item => item.Id == compensation.ExecutionId);

            Assert.Equal(ExecutionStatus.CompletedPartial, stored.Status);
            Assert.Equal(original.ExecutionId, compensating.CompensationOfExecutionId);
            Assert.Null(compensating.ProposalId);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.ExecutionCompensationConfirmed);
        }

        [Fact]
        public async Task Compensation_OnAnExecutionThatDoesNotNeedIt_IsRefused()
        {
            using TradingTestContext context = CreateContext(goldBehaviour: "Filled");
            (Guid proposalId, _) = SeedApprovedProposal(context);

            ExecutionStartResultDto original = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            ExecutionStartResultDto compensation = await context.Hikyaku.Send(new ConfirmCompensation
            {
                ExecutionId = original.ExecutionId,
                OperatorId = Guid.CreateVersion7(),
                Reason = "Non serve."
            }, CancellationToken.None);

            Assert.Equal(ExecutionOutcome.Conflict, compensation.Outcome);
            Assert.Equal("not_in_compensation_required", compensation.Reason);
        }

        [Fact]
        public async Task QueueAndDetail_ReportMeasuredCoverageAndTheEventHistory()
        {
            using TradingTestContext context = CreateContext(goldFillRatio: 0.6);
            (Guid proposalId, _) = SeedApprovedProposal(context, FailurePolicy.AllOrNothing);

            ExecutionStartResultDto original = await context.Hikyaku.Send(new StartExecution { ProposalId = proposalId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

            List<ExecutionSummaryDto> queue = await context.Hikyaku.Send(new GetExecutionQueue(), CancellationToken.None);
            ExecutionSummaryDto row = Assert.Single(queue);

            Assert.Equal("Momentum", row.BasketName);
            Assert.Equal(ExecutionStatus.CompensationRequired, row.Status);
            Assert.True(row.NeedsCompensation);
            Assert.Equal(1, row.FilledLegCount);
            Assert.True(row.Coverage is > 0 and < 100);

            // The queue carries the correlation, so an operator reading the list can reach the proposal behind the
            // exposure without opening the detail first.
            Assert.Equal(proposalId, row.ProposalId);

            ExecutionDetailDto detail = await context.Hikyaku.Send(new GetExecutionDetail { ExecutionId = original.ExecutionId }, CancellationToken.None);

            Assert.Equal(2, detail.Legs.Count);
            Assert.NotEmpty(detail.Events);
            Assert.Equal(proposalId, detail.ProposalId);
            Assert.All(detail.Events, item => Assert.False(string.IsNullOrWhiteSpace(item.Kind.ToString())));
        }
    }
}
