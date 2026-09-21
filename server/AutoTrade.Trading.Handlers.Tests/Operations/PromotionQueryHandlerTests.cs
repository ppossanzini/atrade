using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Operations;
using AutoTrade.Trading.Handlers.Model;
using Xunit;
using ExecutionEntity = AutoTrade.Trading.Handlers.Model.Execution;

namespace AutoTrade.Trading.Handlers.Tests.Operations
{
    /// <summary>
    /// The promotion gate is read-only and it must never claim more than it measured. The interesting cases are
    /// the measurable requirement and the one that needs a person: a requirement the application cannot decide
    /// stays open, and an empty history never counts as a passed period.
    /// </summary>
    public class PromotionQueryHandlerTests
    {
        private static TradingTestContext CreateContext()
        {
            return new TradingTestContext(new Dictionary<string, string>());
        }

        private static async Task<PromotionStatusDto> ReadAsync(TradingTestContext context)
        {
            return await context.Hikyaku.Send(new GetPromotionStatus(), CancellationToken.None);
        }

        private static PromotionRequirementDto RequirementOf(PromotionStatusDto status, string key)
        {
            return Assert.Single(status.Requirements, item => item.Key == key);
        }

        private static void SeedExecution(TradingTestContext context, ExecutionStatus status)
        {
            context.Db.Executions.Add(new ExecutionEntity
            {
                Id = Guid.CreateVersion7(),
                ProposalId = null,
                BasketId = Guid.CreateVersion7(),
                BasketVersionId = Guid.CreateVersion7(),
                VersionNumber = 1,
                Status = status,
                FailurePolicy = FailurePolicy.MinimumCoverage,
                MinimumCoverage = 75,
                Coverage = 100,
                CreatedAtUtc = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc)
            });

            // The handler reads the store, not the change tracker, so a seeded row must be persisted to be seen.
            context.Db.SaveChanges();
        }

        [Fact]
        public async Task Promotion_ReportsEveryRequirementOfTheGate()
        {
            using TradingTestContext context = CreateContext();

            PromotionStatusDto status = await ReadAsync(context);

            Assert.Equal(7, status.Requirements.Count);
            Assert.Equal(status.Requirements.Count, status.Requirements.Select(item => item.Key).Distinct().Count());
        }

        [Fact]
        public async Task Promotion_WithoutAnyExecution_DoesNotCountAsACleanPeriod()
        {
            using TradingTestContext context = CreateContext();

            PromotionStatusDto status = await ReadAsync(context);

            // Zero divergences out of zero executions proves nothing: the requirement asks for a period of running.
            PromotionRequirementDto reconciliation = RequirementOf(status, "reconciliation_clean");
            Assert.Equal(PromotionRequirementState.NotSatisfied, reconciliation.State);
            Assert.Contains("completed=0", reconciliation.Evidence);
        }

        [Fact]
        public async Task Promotion_WithCompletedExecutionsAndNoDivergence_IsSatisfied()
        {
            using TradingTestContext context = CreateContext();
            SeedExecution(context, ExecutionStatus.CompletedNominal);
            SeedExecution(context, ExecutionStatus.CompletedPartial);

            PromotionStatusDto status = await ReadAsync(context);

            PromotionRequirementDto reconciliation = RequirementOf(status, "reconciliation_clean");
            Assert.Equal(PromotionRequirementState.Satisfied, reconciliation.State);
            Assert.Contains("unresolved=0", reconciliation.Evidence);
            Assert.Contains("completed=2", reconciliation.Evidence);
        }

        [Fact]
        public async Task Promotion_WithAnUnresolvedDivergence_IsNotSatisfiedEvenWhenExecutionsCompleted()
        {
            using TradingTestContext context = CreateContext();
            SeedExecution(context, ExecutionStatus.CompletedNominal);
            SeedExecution(context, ExecutionStatus.ReconciliationRequired);

            PromotionStatusDto status = await ReadAsync(context);

            PromotionRequirementDto reconciliation = RequirementOf(status, "reconciliation_clean");
            Assert.Equal(PromotionRequirementState.NotSatisfied, reconciliation.State);
            Assert.Contains("unresolved=1", reconciliation.Evidence);
        }

        [Fact]
        public async Task Promotion_LeavesTheRequirementsThatNeedAPersonOpen()
        {
            using TradingTestContext context = CreateContext();

            PromotionStatusDto status = await ReadAsync(context);

            // An approval, a drill and a review are not measurements: reporting them as satisfied because nothing
            // contradicts them would be the one failure mode this panel exists to prevent.
            foreach (string key in new[] { "demo_period_criteria", "recovery_drill", "security_review", "adr_and_approval" })
            {
                Assert.Equal(PromotionRequirementState.NotVerifiable, RequirementOf(status, key).State);
                Assert.Null(RequirementOf(status, key).Evidence);
            }
        }

        [Fact]
        public async Task Promotion_ReportsTheKillSwitchEngagementsAsEvidenceOfTheRollbackProcedure()
        {
            using TradingTestContext context = CreateContext();

            SeedExecution(context, ExecutionStatus.CompletedNominal);

            context.Db.JournalEvents.Add(new JournalEvent
            {
                Id = Guid.CreateVersion7(),
                Sequence = 1,
                CorrelationId = Guid.CreateVersion7(),
                Kind = JournalEventKind.KillSwitchEngaged,
                ActorType = JournalActorType.Operator,
                ActorId = Guid.CreateVersion7(),
                EntityType = "KillSwitch",
                EntityId = null,
                Payload = "reason=drill",
                OccurredAtUtc = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc)
            });

            context.Db.SaveChanges();

            PromotionStatusDto status = await ReadAsync(context);

            PromotionRequirementDto rollback = RequirementOf(status, "rollback_procedure");
            Assert.Equal(PromotionRequirementState.NotVerifiable, rollback.State);
            Assert.Equal("killSwitchEngagements=1", rollback.Evidence);
        }

        [Fact]
        public async Task Promotion_IsNeverEligibleWhileARequirementIsOpen()
        {
            using TradingTestContext context = CreateContext();
            SeedExecution(context, ExecutionStatus.CompletedNominal);

            PromotionStatusDto status = await ReadAsync(context);

            // Even the measurable requirement is satisfied here, and live stays closed: there is no partial
            // eligibility and no command in the API that can open the gate.
            Assert.False(status.IsLiveEligible);
        }

        [Fact]
        public async Task Promotion_ReportsTheAccountEnvironmentOrNothingAtAll()
        {
            using TradingTestContext context = CreateContext();

            PromotionStatusDto withoutAccount = await ReadAsync(context);
            Assert.Null(withoutAccount.CurrentEnvironment);

            context.Db.TradingAccounts.Add(new TradingAccount
            {
                Id = Guid.CreateVersion7(),
                BrokerAccountId = 12345,
                Environment = TradingEnvironment.Demo,
                IsTradingEnabled = true,
                ConnectionState = BrokerConnectionState.Connected,
                CreatedAtUtc = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc)
            });

            context.Db.SaveChanges();

            PromotionStatusDto withAccount = await ReadAsync(context);
            Assert.Equal("Demo", withAccount.CurrentEnvironment);
            Assert.Equal("environment=Demo", RequirementOf(withAccount, "live_parameters_approved").Evidence);
        }
    }
}
