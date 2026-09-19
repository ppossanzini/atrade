using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Risk
{
  /// <summary>
  /// The sizing is where risk becomes an order, so every refusal is pinned: an input the model cannot read
  /// must produce no volume at all, and a computed volume is never rounded up to reach the instrument minimum.
  /// </summary>
  public class PositionSizingCalculatorTests
  {
    [Fact]
    public void Compute_TurnsRiskIntoAVolumeRoundedDownToTheStep()
    {
      // 10.000 USD of capital, 1.5% at risk, 20 pips of stop, 0.0001 USD per pip per unit:
      // 150 / (20 * 0.0001) = 75.000 units, rounded down to the 1.000 step.
      SizingResult result = PositionSizingCalculator.Compute(Input(equity: 10000, riskCap: 1.5, stopPips: 20, minVolume: 1000, stepVolume: 1000));

      Assert.True(result.IsSized);
      Assert.Equal(75000, result.VolumeUnits);
      Assert.Equal("sized", result.Reason);
    }

    [Fact]
    public void Compute_RoundsDownAndNeverUpToReachTheMinimum()
    {
      // 100 USD at risk, 20 pips, 0.0001 per pip: 5.000 units, below the instrument minimum of 100.000.
      SizingResult result = PositionSizingCalculator.Compute(Input(equity: 10000, riskCap: 1.0, stopPips: 20, minVolume: 100000, stepVolume: 1000));

      Assert.False(result.IsSized);
      Assert.Equal(0, result.VolumeUnits);
      Assert.Equal(PositionSizingCalculator.BelowMinimum, result.Reason);
      Assert.Equal(50000, result.IdealVolumeUnits);
    }

    [Fact]
    public void Compute_RefusesAVolumeAboveTheInstrumentMaximum()
    {
      SizingResult result = PositionSizingCalculator.Compute(Input(equity: 1000000, riskCap: 5, stopPips: 20, minVolume: 1000, stepVolume: 1000, maxVolume: 100000));

      Assert.False(result.IsSized);
      Assert.Equal(PositionSizingCalculator.AboveMaximum, result.Reason);
    }

    [Fact]
    public void Compute_WithoutAStopDistance_HasNoVolume()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: null);
      SizingResult result = PositionSizingCalculator.Compute(input);

      Assert.False(result.IsSized);
      Assert.Equal(PositionSizingCalculator.StopNotConfigured, result.Reason);
    }

    [Fact]
    public void Compute_ForAnInstrumentTheProviderDidNotDescribe_HasNoVolume()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20);
      input.Specification = null;

      Assert.Equal(PositionSizingCalculator.SymbolNotDescribed, PositionSizingCalculator.Compute(input).Reason);
    }

    [Fact]
    public void Compute_ForAnInstrumentTheProviderReportsAsUntradable_HasNoVolume()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20);
      input.Specification.IsTradable = false;

      Assert.Equal(PositionSizingCalculator.SymbolNotTradable, PositionSizingCalculator.Compute(input).Reason);
    }

    [Fact]
    public void Compute_WithoutEquity_HasNoVolume()
    {
      Assert.Equal(PositionSizingCalculator.EquityUnavailable, PositionSizingCalculator.Compute(Input(equity: 0, riskCap: 1.5, stopPips: 20)).Reason);
    }

    [Fact]
    public void Compute_WithoutAPipSizeFromTheProvider_HasNoVolume()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20);
      input.Specification.PipSizePerUnit = 0;

      Assert.Equal(PositionSizingCalculator.PipSizeUnavailable, PositionSizingCalculator.Compute(input).Reason);
    }

    [Fact]
    public void Compute_WithoutAStep_UsesTheMinimumAsTheStepAndStillSizes()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20, minVolume: 1000, stepVolume: 0);

      SizingResult result = PositionSizingCalculator.Compute(input);

      Assert.True(result.IsSized);
      Assert.Equal(75000, result.VolumeUnits);
    }

    [Fact]
    public void Compute_InTheAccountCurrency_DoesNotNeedAConversion()
    {
      SizingResult result = PositionSizingCalculator.Compute(Input(equity: 10000, riskCap: 1.5, stopPips: 20, profitCurrency: "USD", accountCurrency: "USD"));

      Assert.True(result.IsSized);
    }

    [Fact]
    public void Compute_WhenAConversionIsNeeded_WithoutARateHasNoVolume()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20, profitCurrency: "GBP", accountCurrency: "USD");

      Assert.Equal(PositionSizingCalculator.ConversionUnavailable, PositionSizingCalculator.Compute(input).Reason);
    }

    [Fact]
    public void Compute_WhenAConversionIsNeeded_UsesTheProvidedRate()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20, profitCurrency: "GBP", accountCurrency: "USD");
      input.ConversionRate = 1.25;

      SizingResult result = PositionSizingCalculator.Compute(input);

      // 75.000 units of a GBP instrument are worth 1.25 times as much in the account currency, so the size
      // that keeps the same risk is smaller.
      Assert.True(result.IsSized);
      Assert.Equal(60000, result.VolumeUnits);
    }

    [Fact]
    public void Compute_WithoutBothCurrencies_RequiresARateRatherThanAssumingNone()
    {
      SizingInput input = Input(equity: 10000, riskCap: 1.5, stopPips: 20);
      input.AccountCurrency = null;

      Assert.Equal(PositionSizingCalculator.ConversionUnavailable, PositionSizingCalculator.Compute(input).Reason);
    }

    private static SizingInput Input(double equity, double riskCap, double? stopPips, int minVolume = 1000, int stepVolume = 1000, int maxVolume = 0, string profitCurrency = "USD", string accountCurrency = "USD")
    {
      return new SizingInput
      {
        Equity = equity,
        AccountCurrency = accountCurrency,
        RiskCapPercent = riskCap,
        StopDistancePips = stopPips,
        Specification = new SymbolSpecification
        {
          Symbol = "EURUSD",
          Market = MarketKind.Fx,
          MinVolume = minVolume,
          StepVolume = stepVolume,
          MaxVolume = maxVolume,
          LotSize = 100000,
          PipSizePerUnit = 0.0001,
          ProfitCurrency = profitCurrency,
          IsTradable = true
        }
      };
    }
  }

  /// <summary>
  /// The stop distance belongs to the risk model, so its reading is pinned too: a missing value stays missing
  /// and a market never borrows another market's stop.
  /// </summary>
  public class RiskSizingThresholdsFactoryTests
  {
    private static RiskSizingThresholds FromSettings(Dictionary<string, string> settings)
    {
      IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

      return RiskSizingThresholdsFactory.FromConfiguration(configuration);
    }

    [Fact]
    public void FromConfiguration_WithoutTheSection_IsNotConfigured()
    {
      RiskSizingThresholds thresholds = FromSettings(new Dictionary<string, string>());

      Assert.False(thresholds.IsConfigured);
      Assert.Null(thresholds.ForLeg("EURUSD", MarketKind.Fx));
    }

    [Fact]
    public void ForLeg_PrefersTheSymbolOverrideOverTheMarketValue()
    {
      RiskSizingThresholds thresholds = FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:Sizing:DefaultByMarket:Fx:StopDistancePips", "20" },
        { "Trading:Risk:Sizing:Symbols:EURUSD:StopDistancePips", "12" }
      });

      Assert.Equal(12, thresholds.ForLeg("EURUSD", MarketKind.Fx));
      Assert.Equal(20, thresholds.ForLeg("GBPUSD", MarketKind.Fx));
      Assert.Null(thresholds.ForLeg("XAUUSD", MarketKind.Metal));
    }

    [Fact]
    public void FromConfiguration_WithAnUnknownMarket_IsRejected()
    {
      Assert.Throws<InvalidOperationException>(() => FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:Sizing:DefaultByMarket:Crypto:StopDistancePips", "10" }
      }));
    }
  }
}
