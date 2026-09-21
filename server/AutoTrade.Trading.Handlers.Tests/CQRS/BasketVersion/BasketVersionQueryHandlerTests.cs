using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Basket;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Basket;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.BasketVersion
{
    /// <summary>
    /// Version history reads. The stored snapshot holds no status, so these tests pin the derivation
    /// rules across the whole lifecycle: published, active, superseded and archived.
    /// </summary>
    public class BasketVersionQueryHandlerTests
    {
        private static TradingTestContext CreateContext()
        {
            return new TradingTestContext(new Dictionary<string, string>());
        }

        private static async Task<Guid> CreateBasketAsync(TradingTestContext context, string name)
        {
            CreateBasketResult result = await context.Hikyaku.Send(
              new CreateBasket { Name = name, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            return result.BasketId;
        }

        private static async Task SetCompositionAsync(TradingTestContext context, Guid basketId, string symbol)
        {
            BasketOperationResult result = await context.Hikyaku.Send(
              new UpdateBasketComposition
              {
                  BasketId = basketId,
                  Legs = new List<BasketCompositionLegDto>
                {
            new BasketCompositionLegDto
            {
              Symbol = symbol,
              Direction = LegDirection.Long,
              TimeFrame = TimeFrame.H1,
              Weight = 100,
              RiskCap = 1.5,
              IsSelected = true
            }
                },
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
        }

        private static async Task<PublishBasketVersionResult> PublishVersionAsync(TradingTestContext context, Guid basketId, string note)
        {
            PublishBasketVersionResult result = await context.Hikyaku.Send(
              new PublishBasketVersion { BasketId = basketId, Note = note, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

            return result;
        }

        private static async Task ActivateVersionAsync(TradingTestContext context, Guid basketId, Guid versionId)
        {
            BasketOperationResult result = await context.Hikyaku.Send(
              new ActivateBasketVersion { BasketId = basketId, VersionId = versionId, OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
        }

        [Fact]
        public async Task GetBasketVersions_WhenNoVersionIsActive_ReportsEveryVersionAsPublished()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, "EURUSD");

            await PublishVersionAsync(context, basketId, "v1");
            await PublishVersionAsync(context, basketId, "v2");

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = basketId },
              CancellationToken.None);

            Assert.Equal(2, versions.Count);
            Assert.All(versions, item => Assert.Equal(BasketVersionStatus.Published, item.Status));

            // Newest first.
            Assert.Equal(new[] { 2, 1 }, versions.Select(item => item.Number).ToArray());
            Assert.Equal("v2", versions[0].Note);
            Assert.Equal("v1", versions[1].Note);
        }

        [Fact]
        public async Task GetBasketVersions_MarksOnlyTheActiveVersionAsActiveAndTheRestAsSuperseded()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, "EURUSD");

            PublishBasketVersionResult first = await PublishVersionAsync(context, basketId, "v1");
            PublishBasketVersionResult second = await PublishVersionAsync(context, basketId, "v2");
            await PublishVersionAsync(context, basketId, "v3");

            await ActivateVersionAsync(context, basketId, first.VersionId);

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = basketId },
              CancellationToken.None);

            Assert.Equal(BasketVersionStatus.Active, versions.Single(item => item.VersionId == first.VersionId).Status);
            Assert.Equal(BasketVersionStatus.Superseded, versions.Single(item => item.VersionId == second.VersionId).Status);
            Assert.Equal(2, versions.Count(item => item.Status == BasketVersionStatus.Superseded));
            Assert.Equal(3, versions.Count);
        }

        [Fact]
        public async Task GetBasketVersions_WhenActiveVersionIsSwitched_MovesTheActiveStatus()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, "EURUSD");

            PublishBasketVersionResult first = await PublishVersionAsync(context, basketId, "v1");
            PublishBasketVersionResult second = await PublishVersionAsync(context, basketId, "v2");

            await ActivateVersionAsync(context, basketId, first.VersionId);
            await ActivateVersionAsync(context, basketId, second.VersionId);

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = basketId },
              CancellationToken.None);

            Assert.Equal(BasketVersionStatus.Active, versions.Single(item => item.VersionId == second.VersionId).Status);
            Assert.Equal(BasketVersionStatus.Superseded, versions.Single(item => item.VersionId == first.VersionId).Status);
        }

        [Fact]
        public async Task GetBasketVersions_ScopesTheActiveStatusToTheBasketBeingRead()
        {
            using TradingTestContext context = CreateContext();
            Guid activeBasketId = await CreateBasketAsync(context, "Majors Carry");
            Guid otherBasketId = await CreateBasketAsync(context, "Commodities");
            await SetCompositionAsync(context, activeBasketId, "EURUSD");
            await SetCompositionAsync(context, otherBasketId, "XAUUSD");

            PublishBasketVersionResult activePublish = await PublishVersionAsync(context, activeBasketId, "v1");
            await PublishVersionAsync(context, otherBasketId, "v1");

            await ActivateVersionAsync(context, activeBasketId, activePublish.VersionId);

            List<BasketVersionDto> otherVersions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = otherBasketId },
              CancellationToken.None);

            // The other basket never had an active version, so its history stays published instead of
            // inheriting the active status of a different basket.
            Assert.Equal(BasketVersionStatus.Published, Assert.Single(otherVersions).Status);
        }

        [Fact]
        public async Task GetBasketVersions_WhenBasketIsArchived_ReportsEveryVersionAsArchived()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");
            await SetCompositionAsync(context, basketId, "EURUSD");

            await PublishVersionAsync(context, basketId, "v1");
            PublishBasketVersionResult second = await PublishVersionAsync(context, basketId, "v2");

            var basket = context.Db.Baskets.Single();
            basket.ArchivedAtUtc = context.Clock.GetUtcNow().UtcDateTime;
            await context.Db.SaveChangesAsync(CancellationToken.None);

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = basketId },
              CancellationToken.None);

            Assert.All(versions, item => Assert.Equal(BasketVersionStatus.Archived, item.Status));
            Assert.Equal(second.VersionId, versions[0].VersionId);
        }

        [Fact]
        public async Task GetBasketVersions_WhenBasketHasNoVersions_ReturnsAnEmptyList()
        {
            using TradingTestContext context = CreateContext();
            Guid basketId = await CreateBasketAsync(context, "Majors Carry");

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = basketId },
              CancellationToken.None);

            Assert.Empty(versions);
        }

        [Fact]
        public async Task GetBasketVersions_WhenBasketIsMissing_ReturnsAnEmptyList()
        {
            using TradingTestContext context = CreateContext();

            List<BasketVersionDto> versions = await context.Hikyaku.Send(
              new GetBasketVersionsByBasket { BasketId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Empty(versions);
        }
    }
}
