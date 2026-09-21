using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Basket;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.Basket
{
    /// <summary>
    /// Covers the basket registry and draft lifecycle. Each update scope is verified to write only
    /// the properties it owns, and every rejected operation is verified to leave the store untouched.
    /// </summary>
    public class BasketCommandHandlerTests
    {
        private static TradingTestContext CreateContext()
        {
            return new TradingTestContext(new Dictionary<string, string>());
        }

        private static BasketCompositionLegDto CreateLeg(
          string symbol,
          int weight,
          double riskCap,
          bool isSelected = true,
          LegDirection direction = LegDirection.Long,
          TimeFrame timeFrame = TimeFrame.H1)
        {
            return new BasketCompositionLegDto
            {
                Symbol = symbol,
                Direction = direction,
                TimeFrame = timeFrame,
                Weight = weight,
                RiskCap = riskCap,
                IsSelected = isSelected
            };
        }

        private static async Task<Guid> CreateBasketAsync(TradingTestContext context, string name)
        {
            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = name, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            return result.BasketId;
        }

        private static async Task<BasketOperationResult> SetCompositionAsync(TradingTestContext context, Guid basketId, params BasketCompositionLegDto[] legs)
        {
            return await context.Hikyaku.Send(
              new UpdateBasketComposition
              {
                  BasketId = basketId,
                  Legs = legs.ToList(),
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);
        }

        private static async Task<BasketOperationResult> SetPolicyAsync(TradingTestContext context, Guid basketId, BasketPolicyDto policy)
        {
            return await context.Hikyaku.Send(
              new UpdateBasketPolicy
              {
                  BasketId = basketId,
                  Policy = policy,
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);
        }

        private static async Task<BasketOperationResult> ArchiveAsync(TradingTestContext context, Guid basketId)
        {
            return await context.Hikyaku.Send(
              new ArchiveBasket { BasketId = basketId, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);
        }

        [Fact]
        public async Task CreateBasket_WithValidName_CreatesBasketWithPrototypeDefaultPolicyAndJournals()
        {
            using TradingTestContext context = CreateContext();
            Guid operatorId = Guid.CreateVersion7();

            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = "  Majors Carry  ", OperatorId = operatorId },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
            Assert.NotEqual(Guid.Empty, result.BasketId);

            var basket = Assert.Single(context.Db.Baskets);
            Assert.Equal("Majors Carry", basket.Name);
            Assert.Null(basket.ArchivedAtUtc);
            Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, basket.CreatedAtUtc);

            var policy = Assert.Single(context.Db.BasketDraftPolicies);
            Assert.Equal(basket.Id, policy.BasketId);
            Assert.Equal(FailurePolicy.MinimumCoverage, policy.FailurePolicy);
            Assert.Equal(75, policy.MinimumCoverage);
            Assert.Equal(0.8, policy.RiskPerBasket);
            Assert.Equal(2.5, policy.DailyLossLimit);

            Assert.Empty(context.Db.BasketDraftLegs);

            var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketCreated);
            Assert.Equal(basket.Id, journalEvent.EntityId);
            Assert.Equal(operatorId, journalEvent.ActorId);
        }

        [Fact]
        public async Task CreateBasket_WhenNameIsBlank_ReturnsInvalidInputAndWritesNothing()
        {
            using TradingTestContext context = CreateContext();

            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = "   ", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Equal(Guid.Empty, result.BasketId);
            Assert.Empty(context.Db.Baskets);
            Assert.Empty(context.Db.BasketDraftPolicies);
            Assert.Empty(context.Db.JournalEvents);
        }

        [Fact]
        public async Task CreateBasket_WhenNameExceedsLimit_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();

            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = new string('A', 129), OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Empty(context.Db.Baskets);
        }

        [Fact]
        public async Task CreateBasket_WhenNameIsAlreadyUsedByLiveBasket_ReturnsConflict()
        {
            using TradingTestContext context = CreateContext();
            await CreateBasketAsync(context, "Majors Carry");

            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = "majors carry", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Conflict, result.Outcome);
            Assert.Single(context.Db.Baskets);
        }

        [Fact]
        public async Task CreateBasket_WhenPreviousBasketWithSameNameIsArchived_IsAllowed()
        {
            using TradingTestContext context = CreateContext();
            Guid firstBasketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult archiveResult = await ArchiveAsync(context, firstBasketId);
            Assert.Equal(BasketOperationOutcome.Applied, archiveResult.Outcome);

            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = "Majors Carry", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
            Assert.Equal(2, context.Db.Baskets.Count());
        }

        [Fact]
        public async Task UpdateBasketIdentity_WritesOnlyTheNameScope()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));
            await SetPolicyAsync(context, basketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = 90,
                RiskPerBasket = 1.2,
                DailyLossLimit = 3.5
            });

            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketIdentity { BasketId = basketId, Name = " Majors Carry v2 ", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            var basket = Assert.Single(context.Db.Baskets);
            Assert.Equal("Majors Carry v2", basket.Name);

            var leg = Assert.Single(context.Db.BasketDraftLegs);
            Assert.Equal("EURUSD", leg.Symbol);
            Assert.Equal(100, leg.Weight);

            var policy = Assert.Single(context.Db.BasketDraftPolicies);
            Assert.Equal(FailurePolicy.AllOrNothing, policy.FailurePolicy);
            Assert.Equal(90, policy.MinimumCoverage);
            Assert.Equal(1.2, policy.RiskPerBasket);
            Assert.Equal(3.5, policy.DailyLossLimit);
        }

        [Fact]
        public async Task UpdateBasketIdentity_WhenNameIsTakenByAnotherLiveBasket_ReturnsConflictAndKeepsOriginalName()
        {
            using TradingTestContext context = CreateContext();
            Guid firstBasketId = await CreateBasketAsync(context, "Majors Carry");
            await CreateBasketAsync(context, "Commodities");

            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketIdentity { BasketId = firstBasketId, Name = "Commodities", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Conflict, result.Outcome);
            Assert.Equal("Majors Carry", context.Db.Baskets.Single(item => item.Id == firstBasketId).Name);
        }

        [Fact]
        public async Task UpdateBasketIdentity_WhenNameIsUnchangedForSameBasket_IsAllowed()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketIdentity { BasketId = basketId, Name = "Majors Carry", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
        }

        [Fact]
        public async Task UpdateBasketIdentity_WhenBasketIsArchived_ReturnsInvalidState()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await ArchiveAsync(context, basketId);

            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketIdentity { BasketId = basketId, Name = "Renamed", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
            Assert.Equal("Majors Carry", context.Db.Baskets.Single().Name);
        }

        [Fact]
        public async Task UpdateBasketIdentity_WhenBasketIsMissing_ReturnsNotFound()
        {
            using TradingTestContext context = CreateContext();

            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketIdentity { BasketId = Guid.CreateVersion7(), Name = "Renamed", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
        }

        [Fact]
        public async Task UpdateBasketComposition_ReplacesLegsAndNormalizesSymbols()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("GBPUSD", 100, 2.0));
            await SetPolicyAsync(context, basketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.RequireConfirmation,
                MinimumCoverage = 80,
                RiskPerBasket = 1.1,
                DailyLossLimit = 4.0
            });

            BasketOperationResult result = await SetCompositionAsync(
              context,
              basketId,
              CreateLeg(" eurusd ", 60, 1.5, isSelected: true, direction: LegDirection.Short),
              CreateLeg("XAUUSD", 40, 0.8, isSelected: true, timeFrame: TimeFrame.M5),
              CreateLeg("USDJPY", 25, 0.6, isSelected: false));

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            List<AutoTrade.Trading.Handlers.Model.BasketDraftLeg> legs = context.Db.BasketDraftLegs.OrderBy(item => item.Symbol).ToList();
            Assert.Equal(3, legs.Count);
            Assert.Equal(new[] { "EURUSD", "USDJPY", "XAUUSD" }, legs.Select(item => item.Symbol).ToArray());

            var eurUsd = legs.Single(item => item.Symbol == "EURUSD");
            Assert.Equal(60, eurUsd.Weight);
            Assert.Equal(1.5, eurUsd.RiskCap);
            Assert.Equal(LegDirection.Short, eurUsd.Direction);
            Assert.True(eurUsd.IsSelected);

            var xauUsd = legs.Single(item => item.Symbol == "XAUUSD");
            Assert.Equal(TimeFrame.M5, xauUsd.TimeFrame);

            var unselected = legs.Single(item => item.Symbol == "USDJPY");
            Assert.False(unselected.IsSelected);

            // Policy scope is owned elsewhere and must survive a composition update untouched.
            var policy = Assert.Single(context.Db.BasketDraftPolicies);
            Assert.Equal(FailurePolicy.RequireConfirmation, policy.FailurePolicy);
            Assert.Equal(80, policy.MinimumCoverage);
            Assert.Equal(1.1, policy.RiskPerBasket);
            Assert.Equal(4.0, policy.DailyLossLimit);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenSelectedWeightsDoNotTotalOneHundred_ReturnsInvalidInputAndKeepsPreviousLegs()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("GBPUSD", 100, 2.0));

            int compositionJournalCountBefore = context.Db.JournalEvents.Count(item => item.Kind == JournalEventKind.BasketCompositionUpdated);

            BasketOperationResult result = await SetCompositionAsync(
              context,
              basketId,
              CreateLeg("EURUSD", 60, 1.5),
              CreateLeg("XAUUSD", 30, 0.8));

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);

            var leg = Assert.Single(context.Db.BasketDraftLegs);
            Assert.Equal("GBPUSD", leg.Symbol);
            Assert.Equal(compositionJournalCountBefore, context.Db.JournalEvents.Count(item => item.Kind == JournalEventKind.BasketCompositionUpdated));
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenNoLegIsSelected_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5, isSelected: false));

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Empty(context.Db.BasketDraftLegs);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenLegsAreEmpty_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetCompositionAsync(context, basketId);

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Empty(context.Db.BasketDraftLegs);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenSymbolsAreDuplicated_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetCompositionAsync(
              context,
              basketId,
              CreateLeg("EURUSD", 50, 1.5),
              CreateLeg("eurusd", 50, 1.5));

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Empty(context.Db.BasketDraftLegs);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenRiskCapIsOutOfRange_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 5.5));

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
            Assert.Empty(context.Db.BasketDraftLegs);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenBasketIsArchived_ReturnsInvalidStateAndKeepsPreviousLegs()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("GBPUSD", 100, 2.0));
            await ArchiveAsync(context, basketId);

            BasketOperationResult result = await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

            Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
            Assert.Equal("GBPUSD", context.Db.BasketDraftLegs.Single().Symbol);
        }

        [Fact]
        public async Task UpdateBasketComposition_WhenBasketIsMissing_ReturnsNotFound()
        {
            using TradingTestContext context = CreateContext();

            BasketOperationResult result = await SetCompositionAsync(context, Guid.CreateVersion7(), CreateLeg("EURUSD", 100, 1.5));

            Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
        }

        [Fact]
        public async Task UpdateBasketPolicy_WritesOnlyThePolicyScope()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

            BasketOperationResult result = await SetPolicyAsync(context, basketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = 95,
                RiskPerBasket = 2.0,
                DailyLossLimit = 6.0
            });

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            var policy = Assert.Single(context.Db.BasketDraftPolicies);
            Assert.Equal(FailurePolicy.AllOrNothing, policy.FailurePolicy);
            Assert.Equal(95, policy.MinimumCoverage);
            Assert.Equal(2.0, policy.RiskPerBasket);
            Assert.Equal(6.0, policy.DailyLossLimit);

            Assert.Equal("Majors Carry", context.Db.Baskets.Single().Name);
            Assert.Equal("EURUSD", context.Db.BasketDraftLegs.Single().Symbol);
        }

        [Theory]
        [InlineData(49, 0.8, 2.5)]
        [InlineData(101, 0.8, 2.5)]
        [InlineData(75, 0.05, 2.5)]
        [InlineData(75, 10.5, 2.5)]
        [InlineData(75, 0.8, 0.05)]
        [InlineData(75, 0.8, 20.5)]
        public async Task UpdateBasketPolicy_WhenValuesAreOutOfRange_ReturnsInvalidInputAndKeepsPreviousPolicy(int coverage, double riskPerBasket, double dailyLossLimit)
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetPolicyAsync(context, basketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = coverage,
                RiskPerBasket = riskPerBasket,
                DailyLossLimit = dailyLossLimit
            });

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);

            var policy = Assert.Single(context.Db.BasketDraftPolicies);
            Assert.Equal(FailurePolicy.MinimumCoverage, policy.FailurePolicy);
            Assert.Equal(75, policy.MinimumCoverage);
            Assert.Equal(0.8, policy.RiskPerBasket);
            Assert.Equal(2.5, policy.DailyLossLimit);
            Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketPolicyUpdated);
        }

        [Fact]
        public async Task UpdateBasketPolicy_WhenPolicyIsMissing_ReturnsInvalidInput()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await SetPolicyAsync(context, basketId, null);

            Assert.Equal(BasketOperationOutcome.InvalidInput, result.Outcome);
        }

        [Fact]
        public async Task UpdateBasketPolicy_WhenBasketIsArchived_ReturnsInvalidState()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await ArchiveAsync(context, basketId);

            BasketOperationResult result = await SetPolicyAsync(context, basketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = 95,
                RiskPerBasket = 2.0,
                DailyLossLimit = 6.0
            });

            Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
            Assert.Equal(FailurePolicy.MinimumCoverage, context.Db.BasketDraftPolicies.Single().FailurePolicy);
        }

        [Fact]
        public async Task UpdateBasketPolicy_WhenBasketIsMissing_ReturnsNotFound()
        {
            using TradingTestContext context = CreateContext();

            BasketOperationResult result = await SetPolicyAsync(context, Guid.CreateVersion7(), new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = 95,
                RiskPerBasket = 2.0,
                DailyLossLimit = 6.0
            });

            Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
        }

        [Fact]
        public async Task ArchiveBasket_WithoutActiveVersion_StampsArchiveAndJournals()
        {
            using TradingTestContext context = CreateContext();
            Guid operatorId = Guid.CreateVersion7();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            BasketOperationResult result = await context.Hikyaku.Send(
              new ArchiveBasket { BasketId = basketId, OperatorId = operatorId },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            var basket = Assert.Single(context.Db.Baskets);
            Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, basket.ArchivedAtUtc);
            Assert.Equal(basket.ArchivedAtUtc, basket.UpdatedAtUtc);

            var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketArchived);
            Assert.Equal(operatorId, journalEvent.ActorId);
        }

        [Fact]
        public async Task ArchiveBasket_WhenBasketHoldsTheActiveVersion_ReturnsInvalidStateAndJournalsRejection()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

            PublishBasketVersionResult publishResult = await context.Hikyaku.Send(
              new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            await context.Hikyaku.Send(
              new ActivateBasketVersion { BasketId = basketId, VersionId = publishResult.VersionId, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            BasketOperationResult result = await ArchiveAsync(context, basketId);

            Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
            Assert.Null(context.Db.Baskets.Single().ArchivedAtUtc);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketOperationRejected);
            Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketArchived);
        }

        [Fact]
        public async Task ArchiveBasket_WhenBasketIsAlreadyArchived_ReturnsInvalidState()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await ArchiveAsync(context, basketId);

            BasketOperationResult result = await ArchiveAsync(context, basketId);

            Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
            Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketArchived);
        }

        [Fact]
        public async Task ArchiveBasket_WhenBasketIsMissing_ReturnsNotFound()
        {
            using TradingTestContext context = CreateContext();

            BasketOperationResult result = await ArchiveAsync(context, Guid.CreateVersion7());

            Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
        }

        [Fact]
        public async Task CloneBasket_CopiesDraftCompositionAndPolicyUnderTheNewName()
        {
            using TradingTestContext context = CreateContext();
            Guid sourceBasketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(
              context,
              sourceBasketId,
              CreateLeg("EURUSD", 60, 1.5),
              CreateLeg("XAUUSD", 40, 0.8));
            await SetPolicyAsync(context, sourceBasketId, new BasketPolicyDto
            {
                FailurePolicy = FailurePolicy.AllOrNothing,
                MinimumCoverage = 90,
                RiskPerBasket = 1.4,
                DailyLossLimit = 5.0
            });

            CreateBasketResult result = await context.Hikyaku.Send(
              new CloneBasket { SourceBasketId = sourceBasketId, Name = "Majors Carry Copy", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
            Assert.NotEqual(sourceBasketId, result.BasketId);

            var clone = context.Db.Baskets.Single(item => item.Id == result.BasketId);
            Assert.Equal("Majors Carry Copy", clone.Name);

            List<AutoTrade.Trading.Handlers.Model.BasketDraftLeg> cloneLegs = context.Db.BasketDraftLegs
              .Where(item => item.BasketId == result.BasketId)
              .OrderBy(item => item.Symbol)
              .ToList();

            Assert.Equal(2, cloneLegs.Count);
            Assert.Equal(new[] { "EURUSD", "XAUUSD" }, cloneLegs.Select(item => item.Symbol).ToArray());
            Assert.Equal(100, cloneLegs.Sum(item => item.Weight));
            Assert.NotEqual(cloneLegs[0].Id, Guid.Empty);

            var clonePolicy = context.Db.BasketDraftPolicies.Single(item => item.BasketId == result.BasketId);
            Assert.Equal(FailurePolicy.AllOrNothing, clonePolicy.FailurePolicy);
            Assert.Equal(90, clonePolicy.MinimumCoverage);
            Assert.Equal(1.4, clonePolicy.RiskPerBasket);
            Assert.Equal(5.0, clonePolicy.DailyLossLimit);

            // The source draft must remain untouched and independent from the copy.
            Assert.Single(context.Db.BasketDraftPolicies.Where(item => item.BasketId == sourceBasketId));
            Assert.Equal(2, context.Db.BasketDraftLegs.Count(item => item.BasketId == sourceBasketId));

            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketCloned);
        }

        [Fact]
        public async Task CloneBasket_WhenSourceBasketIsMissing_ReturnsNotFound()
        {
            using TradingTestContext context = CreateContext();

            CreateBasketResult result = await context.Hikyaku.Send(
              new CloneBasket { SourceBasketId = Guid.CreateVersion7(), Name = "Copy", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
            Assert.Empty(context.Db.Baskets);
        }

        [Fact]
        public async Task CloneBasket_WhenNameIsTaken_ReturnsConflictAndWritesNothing()
        {
            using TradingTestContext context = CreateContext();
            Guid sourceBasketId = await CreateBasketAsync(context, "Majors Carry");
            await CreateBasketAsync(context, "Commodities");

            CreateBasketResult result = await context.Hikyaku.Send(
              new CloneBasket { SourceBasketId = sourceBasketId, Name = "Commodities", OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Conflict, result.Outcome);
            Assert.Equal(2, context.Db.Baskets.Count());
        }
    }
}
