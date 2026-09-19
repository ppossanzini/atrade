using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Basket;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.BasketVersion
{
  /// <summary>
  /// Publication freezes the selected draft into an immutable snapshot ordered by risk priority;
  /// activation only moves the single active-version pointer.
  /// </summary>
  public class BasketVersionCommandHandlerTests
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

    [Fact]
    public async Task PublishBasketVersion_FreezesSelectedLegsOrderedByRiskPriorityAndCopiesThePolicy()
    {
      using TradingTestContext context = CreateContext();
      Guid operatorId = Guid.CreateVersion7();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(
        context,
        basketId,
        CreateLeg("EURUSD", 40, 1.5),
        CreateLeg("GBPUSD", 30, 2.0, direction: LegDirection.Short),
        CreateLeg("XAUUSD", 30, 0.8, market: MarketKind.Metal, timeFrame: TimeFrame.M15),
        CreateLeg("USDJPY", 10, 0.6, isSelected: false));

      await context.Hikyaku.Send(
        new UpdateBasketPolicy
        {
          BasketId = basketId,
          Policy = new BasketPolicyDto
          {
            EntryMode = EntryMode.MeanReversion,
            FailurePolicy = FailurePolicy.AllOrNothing,
            MinimumCoverage = 90,
            RiskPerBasket = 1.4,
            DailyLossLimit = 5.0
          },
          OperatorId = operatorId
        },
        CancellationToken.None);

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = " first release ", OperatorId = operatorId },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);
      Assert.NotEqual(Guid.Empty, result.VersionId);
      Assert.Equal(1, result.Number);

      var version = Assert.Single(context.Db.BasketVersions);
      Assert.Equal(basketId, version.BasketId);
      Assert.Equal(1, version.Number);
      Assert.Equal(" first release ", version.Note);
      Assert.Equal(operatorId, version.CreatedByOperatorId);
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, version.PublishedAtUtc);

      // Only selected legs are snapshotted, ordered by risk cap descending then symbol.
      var versionLegs = context.Db.BasketVersionLegs.OrderBy(item => item.Ordinal).ToList();
      Assert.Equal(3, versionLegs.Count);
      Assert.Equal(new[] { "GBPUSD", "EURUSD", "XAUUSD" }, versionLegs.Select(item => item.Symbol).ToArray());
      Assert.Equal(new[] { 0, 1, 2 }, versionLegs.Select(item => item.Ordinal).ToArray());
      Assert.DoesNotContain(versionLegs, item => item.Symbol == "USDJPY");

      var gbpUsd = versionLegs[0];
      Assert.Equal(2.0, gbpUsd.RiskCap);
      Assert.Equal(30, gbpUsd.Weight);
      Assert.Equal(LegDirection.Short, gbpUsd.Direction);

      var xauUsd = versionLegs[2];
      Assert.Equal(MarketKind.Metal, xauUsd.Market);
      Assert.Equal(TimeFrame.M15, xauUsd.TimeFrame);

      var versionPolicy = Assert.Single(context.Db.BasketVersionPolicies);
      Assert.Equal(version.Id, versionPolicy.VersionId);

      // The declared entry rule is part of the strategy, so it is frozen with the version like every other rule.
      Assert.Equal(EntryMode.MeanReversion, versionPolicy.EntryMode);
      Assert.Equal(FailurePolicy.AllOrNothing, versionPolicy.FailurePolicy);
      Assert.Equal(90, versionPolicy.MinimumCoverage);
      Assert.Equal(1.4, versionPolicy.RiskPerBasket);
      Assert.Equal(5.0, versionPolicy.DailyLossLimit);

      var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketVersionPublished);
      Assert.Equal(version.Id, journalEvent.EntityId);
      Assert.Equal(operatorId, journalEvent.ActorId);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenRepeated_NumbersVersionsSequentiallyAndKeepsEverySnapshot()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      PublishBasketVersionResult first = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      PublishBasketVersionResult second = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v2", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(1, first.Number);
      Assert.Equal(2, second.Number);
      Assert.Equal(2, context.Db.BasketVersions.Count());
      Assert.Equal(new[] { 1, 2 }, context.Db.BasketVersions.OrderBy(item => item.Number).Select(item => item.Number).ToArray());
    }

    [Fact]
    public async Task PublishBasketVersion_DoesNotTrackLaterDraftEdits()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      await SetCompositionAsync(context, basketId, CreateLeg("GBPUSD", 100, 2.0));

      var frozenLeg = Assert.Single(context.Db.BasketVersionLegs.Where(item => item.VersionId == result.VersionId));
      Assert.Equal("EURUSD", frozenLeg.Symbol);
      Assert.Equal(1.5, frozenLeg.RiskCap);

      Assert.Equal("GBPUSD", Assert.Single(context.Db.BasketDraftLegs).Symbol);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenDraftIsEmpty_IsRejectedAndWritesNoVersion()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Equal(Guid.Empty, result.VersionId);
      Assert.Equal(0, result.Number);
      Assert.Empty(context.Db.BasketVersions);
      Assert.Empty(context.Db.BasketVersionLegs);
      Assert.Empty(context.Db.BasketVersionPolicies);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketOperationRejected);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenNoLegIsSelected_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");

      // Seeded directly because the composition command refuses a draft with no selected leg, so
      // this proves the publication gate defends itself instead of trusting the draft to be valid.
      context.Db.BasketDraftLegs.Add(new AutoTrade.Trading.Handlers.Model.BasketDraftLeg
      {
        Id = Guid.CreateVersion7(),
        BasketId = basketId,
        Symbol = "EURUSD",
        Market = MarketKind.Fx,
        Direction = LegDirection.Long,
        TimeFrame = TimeFrame.H1,
        Weight = 100,
        RiskCap = 1.5,
        IsSelected = false
      });

      await context.Db.SaveChangesAsync(CancellationToken.None);

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.BasketVersions);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenSelectedWeightsNoLongerTotalOneHundred_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      // Seeded directly because the composition command refuses an unbalanced draft, so this proves
      // the publication gate defends itself instead of trusting the draft to be valid.
      var leg = context.Db.BasketDraftLegs.Single();
      leg.Weight = 90;
      await context.Db.SaveChangesAsync(CancellationToken.None);

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.BasketVersions);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenBasketIsArchived_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));
      await context.Hikyaku.Send(new ArchiveBasket { BasketId = basketId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.BasketVersions);
    }

    [Fact]
    public async Task PublishBasketVersion_WhenBasketIsMissing_ReturnsNotFound()
    {
      using TradingTestContext context = CreateContext();

      PublishBasketVersionResult result = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = Guid.CreateVersion7(), Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ActivateBasketVersion_StoresTheOperatorOnTheSingleActivePointer()
    {
      using TradingTestContext context = CreateContext();
      Guid operatorId = Guid.CreateVersion7();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      PublishBasketVersionResult publishResult = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = operatorId },
        CancellationToken.None);

      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = basketId, VersionId = publishResult.VersionId, OperatorId = operatorId },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

      var activeSlot = Assert.Single(context.Db.ActiveBasketVersions);
      Assert.Equal(1, activeSlot.Id);
      Assert.Equal(basketId, activeSlot.BasketId);
      Assert.Equal(publishResult.VersionId, activeSlot.VersionId);
      Assert.Equal(operatorId, activeSlot.ActivatedByOperatorId);
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, activeSlot.ActivatedAtUtc);

      var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketVersionActivated);
      Assert.Equal(publishResult.VersionId, journalEvent.EntityId);
    }

    [Fact]
    public async Task ActivateBasketVersion_WhenSwitchingBaskets_LeavesExactlyOneActivePointer()
    {
      using TradingTestContext context = CreateContext();
      Guid firstBasketId = await CreateBasketAsync(context, "Majors Carry");
      Guid secondBasketId = await CreateBasketAsync(context, "Commodities");
      await SetCompositionAsync(context, firstBasketId, CreateLeg("EURUSD", 100, 1.5));
      await SetCompositionAsync(context, secondBasketId, CreateLeg("XAUUSD", 100, 0.8, market: MarketKind.Metal));

      PublishBasketVersionResult firstPublish = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = firstBasketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      PublishBasketVersionResult secondPublish = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = secondBasketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = firstBasketId, VersionId = firstPublish.VersionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = secondBasketId, VersionId = secondPublish.VersionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      var activeSlot = Assert.Single(context.Db.ActiveBasketVersions);
      Assert.Equal(secondBasketId, activeSlot.BasketId);
      Assert.Equal(secondPublish.VersionId, activeSlot.VersionId);
    }

    [Fact]
    public async Task ActivateBasketVersion_WhenVersionBelongsToAnotherBasket_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid firstBasketId = await CreateBasketAsync(context, "Majors Carry");
      Guid secondBasketId = await CreateBasketAsync(context, "Commodities");
      await SetCompositionAsync(context, firstBasketId, CreateLeg("EURUSD", 100, 1.5));

      PublishBasketVersionResult publishResult = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = firstBasketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = secondBasketId, VersionId = publishResult.VersionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.ActiveBasketVersions);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BasketOperationRejected);
    }

    [Fact]
    public async Task ActivateBasketVersion_WhenVersionIsUnknown_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");

      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = basketId, VersionId = Guid.CreateVersion7(), OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.ActiveBasketVersions);
    }

    [Fact]
    public async Task ActivateBasketVersion_WhenBasketIsMissing_IsRejected()
    {
      using TradingTestContext context = CreateContext();

      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = Guid.CreateVersion7(), VersionId = Guid.CreateVersion7(), OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.ActiveBasketVersions);
    }

    [Fact]
    public async Task ActivateBasketVersion_WhenBasketIsArchived_IsRejected()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await CreateBasketAsync(context, "Majors Carry");
      await SetCompositionAsync(context, basketId, CreateLeg("EURUSD", 100, 1.5));

      PublishBasketVersionResult publishResult = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      await context.Hikyaku.Send(new ArchiveBasket { BasketId = basketId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      BasketOperationResult result = await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = basketId, VersionId = publishResult.VersionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.InvalidState, result.Outcome);
      Assert.Empty(context.Db.ActiveBasketVersions);
    }
  }
}
