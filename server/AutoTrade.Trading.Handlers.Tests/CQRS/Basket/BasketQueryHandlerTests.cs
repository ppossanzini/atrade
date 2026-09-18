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

namespace AutoTrade.Trading.Handlers.Tests.CQRS.Basket
{
  /// <summary>
  /// Registry reads. Status is never stored, so these tests pin the derivation rules: archived wins,
  /// otherwise the basket holding the single active version is active and everything else is inactive.
  /// </summary>
  public class BasketQueryHandlerTests
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
      MarketKind market = MarketKind.Fx,
      TimeFrame timeFrame = TimeFrame.H1)
    {
      return new BasketCompositionLegDto
      {
        Symbol = symbol,
        Market = market,
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

    private static async Task SetCompositionAsync(TradingTestContext context, Guid basketId, params BasketCompositionLegDto[] legs)
    {
      BasketOperationResult result = await context.Hikyaku.Send(
        new UpdateBasketComposition
        {
          BasketId = basketId,
          Legs = legs.ToList(),
          OperatorId = Guid.CreateVersion7()
        },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
    }

    private static async Task<Guid> PublishVersionAsync(TradingTestContext context, Guid basketId)
    {
      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

      return result.VersionId;
    }

    private static async Task ActivateVersionAsync(TradingTestContext context, Guid basketId, Guid versionId)
    {
      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = basketId, VersionId = versionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
    }

    [Fact]
    public async Task GetBaskets_WhenNoVersionIsActive_ReportsEveryBasketAsInactive()
    {
      using TradingTestContext context = CreateContext();
      await CreateBasketAsync(context, "Majors Carry");
      Guid secondBasketId = await CreateBasketAsync(context, "Commodities");
      await SetCompositionAsync(
        context,
        secondBasketId,
        CreateLeg("XAUUSD", 60, 0.8, market: MarketKind.Metal),
        CreateLeg("XAGUSD", 40, 0.5, market: MarketKind.Metal));

      List<BasketSummaryDto> summaries = await context.Hikyaku.Send(new GetBaskets(), CancellationToken.None);

      Assert.Equal(2, summaries.Count);
      Assert.All(summaries, item => Assert.Equal(BasketStatus.Inactive, item.Status));
      Assert.All(summaries, item => Assert.Equal(0, item.ActiveVersionNumber));
      Assert.All(summaries, item => Assert.Equal(0, item.LatestVersionNumber));
      Assert.Equal(new[] { "Commodities", "Majors Carry" }, summaries.Select(item => item.Name).ToArray());

      BasketSummaryDto commoditySummary = summaries.Single(item => item.BasketId == secondBasketId);
      Assert.Equal(2, commoditySummary.SelectedLegCount);
      Assert.Equal(100, commoditySummary.TotalWeight);
    }

    [Fact]
    public async Task GetBaskets_ReportsActiveAndLatestVersionNumbers()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      Guid firstVersionId = await PublishVersionAsync(context, basketId);
      await PublishVersionAsync(context, basketId);
      await ActivateVersionAsync(context, basketId, firstVersionId);

      List<BasketSummaryDto> summaries = await context.Hikyaku.Send(new GetBaskets(), CancellationToken.None);

      BasketSummaryDto summary = Assert.Single(summaries);
      Assert.Equal(BasketStatus.Active, summary.Status);
      Assert.Equal(1, summary.ActiveVersionNumber);
      Assert.Equal(2, summary.LatestVersionNumber);
    }

    [Fact]
    public async Task GetBaskets_WhenOnlyOneBasketHoldsTheActiveVersion_KeepsTheOthersInactive()
    {
      using TradingTestContext context = CreateContext();
      Guid activeBasketId = await CreateBasketAsync(context, "Majors Carry");
      Guid otherBasketId = await CreateBasketAsync(context, "Commodities");
      await SetCompositionAsync(context, activeBasketId, CreateLeg("EURUSD", 100, 1.5));
      await SetCompositionAsync(context, otherBasketId, CreateLeg("XAUUSD", 100, 0.8, market: MarketKind.Metal));

      Guid versionId = await PublishVersionAsync(context, activeBasketId);
      await ActivateVersionAsync(context, activeBasketId, versionId);

      List<BasketSummaryDto> summaries = await context.Hikyaku.Send(new GetBaskets(), CancellationToken.None);

      Assert.Equal(BasketStatus.Active, summaries.Single(item => item.BasketId == activeBasketId).Status);
      Assert.Equal(BasketStatus.Inactive, summaries.Single(item => item.BasketId == otherBasketId).Status);
    }

    [Fact]
    public async Task GetBaskets_WhenActiveBasketIsArchived_StatusStaysArchived()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      Guid versionId = await PublishVersionAsync(context, basketId);
      await ActivateVersionAsync(context, basketId, versionId);

      // Archive is guarded against a live active version, so the archived state is seeded directly
      // to prove that derivation gives archiving precedence over the active pointer.
      var basket = context.Db.Baskets.Single();
      basket.ArchivedAtUtc = context.Clock.GetUtcNow().UtcDateTime;
      await context.Db.SaveChangesAsync(CancellationToken.None);

      List<BasketSummaryDto> summaries = await context.Hikyaku.Send(new GetBaskets(), CancellationToken.None);

      Assert.Equal(BasketStatus.Archived, Assert.Single(summaries).Status);
    }

    [Fact]
    public async Task GetBasketById_ReturnsTheDraftScopeAndTheActivePointer()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(
        context,
        basketId,
        CreateLeg("EURUSD", 60, 1.5, direction: LegDirection.Short),
        CreateLeg("XAUUSD", 40, 0.8, market: MarketKind.Metal, timeFrame: TimeFrame.M15));
      await context.Hikyaku.Send(
        new UpdateBasketPolicy
        {
          BasketId = basketId,
          Policy = new BasketPolicyDto
          {
            FailurePolicy = FailurePolicy.AllOrNothing,
            MinimumCoverage = 90,
            RiskPerBasket = 1.4,
            DailyLossLimit = 5.0
          },
          OperatorId = Guid.CreateVersion7()
        },
        CancellationToken.None);

      Guid versionId = await PublishVersionAsync(context, basketId);
      await ActivateVersionAsync(context, basketId, versionId);

      BasketDetailDto detail = await context.Hikyaku.Send(new GetBasketById { BasketId = basketId }, CancellationToken.None);

      Assert.NotNull(detail);
      Assert.Equal(basketId, detail.BasketId);
      Assert.Equal("Majors Carry", detail.Name);
      Assert.Equal(BasketStatus.Active, detail.Status);
      Assert.Equal(versionId, detail.ActiveVersionId);
      Assert.Equal(1, detail.ActiveVersionNumber);
      Assert.Equal(1, detail.LatestVersionNumber);

      Assert.Equal(2, detail.DraftLegs.Count);

      BasketCompositionLegDto eurUsd = detail.DraftLegs.Single(item => item.Symbol == "EURUSD");
      Assert.Equal(60, eurUsd.Weight);
      Assert.Equal(1.5, eurUsd.RiskCap);
      Assert.Equal(LegDirection.Short, eurUsd.Direction);
      Assert.True(eurUsd.IsSelected);

      BasketCompositionLegDto xauUsd = detail.DraftLegs.Single(item => item.Symbol == "XAUUSD");
      Assert.Equal(MarketKind.Metal, xauUsd.Market);
      Assert.Equal(TimeFrame.M15, xauUsd.TimeFrame);

      Assert.NotNull(detail.DraftPolicy);
      Assert.Equal(FailurePolicy.AllOrNothing, detail.DraftPolicy.FailurePolicy);
      Assert.Equal(90, detail.DraftPolicy.MinimumCoverage);
      Assert.Equal(1.4, detail.DraftPolicy.RiskPerBasket);
      Assert.Equal(5.0, detail.DraftPolicy.DailyLossLimit);
    }

    [Fact]
    public async Task GetBasketById_WhenNoVersionIsActive_ReportsZeroCountersAndNullActiveVersion()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");

      BasketDetailDto detail = await context.Hikyaku.Send(new GetBasketById { BasketId = basketId }, CancellationToken.None);

      Assert.NotNull(detail);
      Assert.Equal(BasketStatus.Inactive, detail.Status);
      Assert.Null(detail.ActiveVersionId);
      Assert.Equal(0, detail.ActiveVersionNumber);
      Assert.Equal(0, detail.LatestVersionNumber);
      Assert.Empty(detail.DraftLegs);
      Assert.NotNull(detail.DraftPolicy);
    }

    [Fact]
    public async Task GetBasketById_WhenBasketIsMissing_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();

      BasketDetailDto detail = await context.Hikyaku.Send(new GetBasketById { BasketId = Guid.CreateVersion7() }, CancellationToken.None);

      Assert.Null(detail);
    }

    [Fact]
    public async Task GetActiveBasket_WhenNoVersionIsActive_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();
      await CreateBasketAsync(context, "Majors Carry");

      BasketDetailDto detail = await context.Hikyaku.Send(new GetActiveBasket(), CancellationToken.None);

      Assert.Null(detail);
    }

    [Fact]
    public async Task GetActiveBasket_ReturnsTheBasketHoldingTheActiveVersion()
    {
      using TradingTestContext context = CreateContext();
      Guid idleBasketId = await CreateBasketAsync(context, "Commodities");
      Guid activeBasketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, activeBasketId, CreateLeg("EURUSD", 100, 1.5));

      Guid versionId = await PublishVersionAsync(context, activeBasketId);
      await ActivateVersionAsync(context, activeBasketId, versionId);

      BasketDetailDto detail = await context.Hikyaku.Send(new GetActiveBasket(), CancellationToken.None);

      Assert.NotNull(detail);
      Assert.Equal(activeBasketId, detail.BasketId);
      Assert.NotEqual(idleBasketId, detail.BasketId);
      Assert.Equal(BasketStatus.Active, detail.Status);
      Assert.Equal(versionId, detail.ActiveVersionId);
    }

    [Fact]
    public async Task GetActiveBasket_WhenSwitchingTheActiveVersion_FollowsTheSinglePointer()
    {
      using TradingTestContext context = CreateContext();
      Guid firstBasketId = await CreateBasketAsync(context, "Majors Carry");
      Guid secondBasketId = await CreateBasketAsync(context, "Commodities");
      await SetCompositionAsync(context, firstBasketId, CreateLeg("EURUSD", 100, 1.5));
      await SetCompositionAsync(context, secondBasketId, CreateLeg("XAUUSD", 100, 0.8, market: MarketKind.Metal));

      Guid firstVersionId = await PublishVersionAsync(context, firstBasketId);
      Guid secondVersionId = await PublishVersionAsync(context, secondBasketId);

      await ActivateVersionAsync(context, firstBasketId, firstVersionId);
      await ActivateVersionAsync(context, secondBasketId, secondVersionId);

      BasketDetailDto detail = await context.Hikyaku.Send(new GetActiveBasket(), CancellationToken.None);

      Assert.NotNull(detail);
      Assert.Equal(secondBasketId, detail.BasketId);

      var activeSlot = Assert.Single(context.Db.ActiveBasketVersions);
      Assert.Equal(secondBasketId, activeSlot.BasketId);
      Assert.Equal(secondVersionId, activeSlot.VersionId);
    }
  }
}
