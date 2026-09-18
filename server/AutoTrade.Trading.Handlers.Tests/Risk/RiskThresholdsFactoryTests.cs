using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Risk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Risk
{
  /// <summary>
  /// The per-market limits are read from configuration by hand, so the reading itself is pinned: a missing,
  /// empty or nonsensical value must stay unconfigured and therefore block, never fall back to a default
  /// or to another market's limit.
  /// </summary>
  public class RiskThresholdsFactoryTests
  {
    private static RiskThresholds FromSettings(Dictionary<string, string> settings)
    {
      IConfiguration configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(settings)
        .Build();

      return RiskThresholdsFactory.FromConfiguration(configuration);
    }

    [Fact]
    public void FromConfiguration_NullConfiguration_IsRejected()
    {
      Assert.Throws<ArgumentNullException>(() => RiskThresholdsFactory.FromConfiguration(null));
    }

    [Fact]
    public void FromConfiguration_ReadsTheLimitsOfEachMarketSeparately()
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:SnapshotMaxAgeSeconds", "90" },
        { "Trading:Risk:Markets:Fx:LegSpreadMaxPips", "1.5" },
        { "Trading:Risk:Markets:Fx:LegVolatilityMaxPercent", "12" },
        { "Trading:Risk:Markets:Metal:LegSpreadMaxPips", "4" },
        { "Trading:Risk:Markets:Metal:LegVolatilityMaxPercent", "30" },
        { "Trading:Risk:Markets:Index:LegSpreadMaxPips", "3" },
        { "Trading:Risk:Markets:Index:LegVolatilityMaxPercent", "25" }
      });

      Assert.Equal(90, thresholds.SnapshotMaxAgeSeconds);
      Assert.Equal(1.5, thresholds.ForMarket(MarketKind.Fx).LegSpreadMaxPips);
      Assert.Equal(12, thresholds.ForMarket(MarketKind.Fx).LegVolatilityMaxPercent);
      Assert.Equal(4, thresholds.ForMarket(MarketKind.Metal).LegSpreadMaxPips);
      Assert.Equal(30, thresholds.ForMarket(MarketKind.Metal).LegVolatilityMaxPercent);
      Assert.Equal(3, thresholds.ForMarket(MarketKind.Index).LegSpreadMaxPips);
      Assert.Equal(25, thresholds.ForMarket(MarketKind.Index).LegVolatilityMaxPercent);
      Assert.True(thresholds.IsFullyConfigured);
    }

    [Fact]
    public void FromConfiguration_WithoutAnyRiskSection_LeavesEverythingUnconfigured()
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>());

      Assert.Null(thresholds.SnapshotMaxAgeSeconds);

      foreach (MarketKind market in Enum.GetValues(typeof(MarketKind)))
      {
        MarketLegLimits limits = thresholds.ForMarket(market);

        Assert.Null(limits.LegSpreadMaxPips);
        Assert.Null(limits.LegVolatilityMaxPercent);
      }

      Assert.False(thresholds.IsFullyConfigured);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void FromConfiguration_WithANonPositiveOrInvalidLimit_LeavesItUnconfigured(string value)
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:Markets:Fx:LegSpreadMaxPips", value },
        { "Trading:Risk:Markets:Fx:LegVolatilityMaxPercent", value }
      });

      MarketLegLimits limits = thresholds.ForMarket(MarketKind.Fx);

      Assert.Null(limits.LegSpreadMaxPips);
      Assert.Null(limits.LegVolatilityMaxPercent);
    }
  }
}
