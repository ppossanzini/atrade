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
  /// Read-only validation handlers. They are the single definition of every basket rule, so they
  /// are verified directly; they must never mutate the store or emit journal entries.
  /// </summary>
  public class BasketValidationHandlerTests
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

    private static BasketPolicyDto CreatePolicy(int minimumCoverage = 75, double riskPerBasket = 0.8, double dailyLossLimit = 2.5)
    {
      return new BasketPolicyDto
      {
        FailurePolicy = FailurePolicy.MinimumCoverage,
        MinimumCoverage = minimumCoverage,
        RiskPerBasket = riskPerBasket,
        DailyLossLimit = dailyLossLimit
      };
    }

    private static async Task<Guid> SeedBasketAsync(TradingTestContext context, string name)
    {
      CreateBasketResult result = await context.Hikyaku.Send(
        new CreateBasket { Name = name, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(BasketOperationOutcome.Applied, result.Outcome);

      return result.BasketId;
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Majors Carry", true)]
    public async Task ValidateBasketNamePresence_RequiresANonBlankNameWithinTheLengthLimit(string name, bool expected)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketNamePresence { Name = name }, CancellationToken.None);

      Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ValidateBasketNamePresence_AcceptsExactlyTheLengthLimit()
    {
      using TradingTestContext context = CreateContext();

      bool atLimit = await context.Hikyaku.Send(new ValidateBasketNamePresence { Name = new string('A', 128) }, CancellationToken.None);
      bool overLimit = await context.Hikyaku.Send(new ValidateBasketNamePresence { Name = new string('A', 129) }, CancellationToken.None);

      Assert.True(atLimit);
      Assert.False(overLimit);
    }

    [Fact]
    public async Task ValidateBasketNameUniqueness_RejectsANameUsedByALiveBasketIgnoringCase()
    {
      using TradingTestContext context = CreateContext();
      await SeedBasketAsync(context, "Majors Carry");

      bool result = await context.Hikyaku.Send(new ValidateBasketNameUniqueness { Name = "MAJORS CARRY" }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketNameUniqueness_IgnoresTheBasketBeingUpdated()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");

      bool result = await context.Hikyaku.Send(
        new ValidateBasketNameUniqueness { Name = "Majors Carry", ExcludedBasketId = basketId },
        CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketNameUniqueness_FreesTheNameOfAnArchivedBasket()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");

      await context.Hikyaku.Send(new ArchiveBasket { BasketId = basketId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      bool result = await context.Hikyaku.Send(new ValidateBasketNameUniqueness { Name = "Majors Carry" }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketNameUniqueness_RejectsABlankName()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketNameUniqueness { Name = "  " }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionWeights_AcceptsSelectedWeightsTotallingOneHundred()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 60, 1.5),
          CreateLeg("XAUUSD", 40, 0.8)
        }
      }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionWeights_IgnoresUnselectedLegsWhenTotalling()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 60, 1.5),
          CreateLeg("XAUUSD", 40, 0.8),
          CreateLeg("USDJPY", 25, 0.6, isSelected: false)
        }
      }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionWeights_RejectsAnEmptyOrFullyUnselectedComposition()
    {
      using TradingTestContext context = CreateContext();

      bool noLegs = await context.Hikyaku.Send(new ValidateBasketCompositionWeights { Legs = new List<BasketCompositionLegDto>() }, CancellationToken.None);
      bool noneSelected = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 100, 1.5, isSelected: false)
        }
      }, CancellationToken.None);

      Assert.False(noLegs);
      Assert.False(noneSelected);
    }

    [Theory]
    [InlineData(90, 5)]
    [InlineData(60, 50)]
    public async Task ValidateBasketCompositionWeights_RejectsSelectedWeightsThatDoNotTotalOneHundred(int firstLegWeight, int secondLegWeight)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", firstLegWeight, 1.5),
          CreateLeg("XAUUSD", secondLegWeight, 0.8)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionWeights_RejectsASelectedLegWithNoWeight()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 0, 1.5),
          CreateLeg("XAUUSD", 100, 0.8)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionWeights_RejectsAWeightAboveTheTarget()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 101, 1.5)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionSymbols_AcceptsDistinctUsableSymbols()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 60, 1.5),
          CreateLeg("XAUUSD", 40, 0.8)
        }
      }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketCompositionSymbols_RejectsDuplicatedSymbolsIgnoringCaseAndPadding()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 50, 1.5),
          CreateLeg(" eurusd ", 50, 1.5)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456")]
    public async Task ValidateBasketCompositionSymbols_RejectsSymbolsThatAreBlankOrTooLong(string symbol)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg(symbol, 100, 1.5)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.04)]
    [InlineData(5.01)]
    public async Task ValidateBasketCompositionSymbols_RejectsRiskCapsOutsideTheAllowedRange(double riskCap)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 100, riskCap)
        }
      }, CancellationToken.None);

      Assert.False(result);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(5.0)]
    public async Task ValidateBasketCompositionSymbols_AcceptsTheRiskCapRangeBoundaries(double riskCap)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto>
        {
          CreateLeg("EURUSD", 100, riskCap)
        }
      }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketPolicyValues_RejectsAMissingPolicy()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketPolicyValues { Policy = null }, CancellationToken.None);

      Assert.False(result);
    }

    [Theory]
    [InlineData(75, 0.8, 2.5, true)]
    [InlineData(50, 0.1, 0.1, true)]
    [InlineData(100, 10.0, 20.0, true)]
    [InlineData(49, 0.8, 2.5, false)]
    [InlineData(101, 0.8, 2.5, false)]
    [InlineData(75, 0.09, 2.5, false)]
    [InlineData(75, 10.1, 2.5, false)]
    [InlineData(75, 0.8, 0.09, false)]
    [InlineData(75, 0.8, 20.1, false)]
    public async Task ValidateBasketPolicyValues_EnforcesTheDocumentedBounds(int coverage, double riskPerBasket, double dailyLossLimit, bool expected)
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(
        new ValidateBasketPolicyValues { Policy = CreatePolicy(coverage, riskPerBasket, dailyLossLimit) },
        CancellationToken.None);

      Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ValidateBasketArchivable_AcceptsALiveBasketWithoutActiveVersion()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");

      bool result = await context.Hikyaku.Send(new ValidateBasketArchivable { BasketId = basketId }, CancellationToken.None);

      Assert.True(result);
    }

    [Fact]
    public async Task ValidateBasketArchivable_RejectsAnUnknownBasket()
    {
      using TradingTestContext context = CreateContext();

      bool result = await context.Hikyaku.Send(new ValidateBasketArchivable { BasketId = Guid.CreateVersion7() }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketArchivable_RejectsAnAlreadyArchivedBasket()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");

      await context.Hikyaku.Send(new ArchiveBasket { BasketId = basketId, OperatorId = Guid.CreateVersion7() }, CancellationToken.None);

      bool result = await context.Hikyaku.Send(new ValidateBasketArchivable { BasketId = basketId }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task ValidateBasketArchivable_RejectsABasketHoldingTheActiveVersion()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");
      await context.Hikyaku.Send(
        new UpdateBasketComposition
        {
          BasketId = basketId,
          Legs = new List<BasketCompositionLegDto> { CreateLeg("EURUSD", 100, 1.5) },
          OperatorId = Guid.CreateVersion7()
        },
        CancellationToken.None);

      PublishBasketVersionResult publishResult = await context.Hikyaku.Send(
        new PublishBasketVersion { BasketId = basketId, Note = "v1", OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      await context.Hikyaku.Send(
        new ActivateBasketVersion { BasketId = basketId, VersionId = publishResult.VersionId, OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      bool result = await context.Hikyaku.Send(new ValidateBasketArchivable { BasketId = basketId }, CancellationToken.None);

      Assert.False(result);
    }

    [Fact]
    public async Task Validations_AreReadOnlyAndEmitNoJournalEntries()
    {
      using TradingTestContext context = CreateContext();
      Guid basketId = await SeedBasketAsync(context, "Majors Carry");

      int journalCountBefore = context.Db.JournalEvents.Count();

      await context.Hikyaku.Send(new ValidateBasketNamePresence { Name = "Majors Carry" }, CancellationToken.None);
      await context.Hikyaku.Send(new ValidateBasketNameUniqueness { Name = "Majors Carry" }, CancellationToken.None);
      await context.Hikyaku.Send(new ValidateBasketCompositionWeights
      {
        Legs = new List<BasketCompositionLegDto> { CreateLeg("EURUSD", 100, 1.5) }
      }, CancellationToken.None);
      await context.Hikyaku.Send(new ValidateBasketCompositionSymbols
      {
        Legs = new List<BasketCompositionLegDto> { CreateLeg("EURUSD", 100, 1.5) }
      }, CancellationToken.None);
      await context.Hikyaku.Send(new ValidateBasketPolicyValues { Policy = CreatePolicy() }, CancellationToken.None);
      await context.Hikyaku.Send(new ValidateBasketArchivable { BasketId = basketId }, CancellationToken.None);

      Assert.Equal(journalCountBefore, context.Db.JournalEvents.Count());
      Assert.Single(context.Db.Baskets);
      Assert.Single(context.Db.BasketDraftPolicies);
      Assert.Empty(context.Db.BasketDraftLegs);
      Assert.Empty(context.Db.BasketVersions);
    }
  }
}
