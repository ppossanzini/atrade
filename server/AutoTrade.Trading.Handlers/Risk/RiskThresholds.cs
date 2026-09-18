using System;
using System.Collections.Generic;
using System.Globalization;
using AutoTrade.Trading.Core.Configuration;
using AutoTrade.Trading.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// Leg limits of one market. Every value is nullable on purpose: null means "not decided yet", and the
  /// engine turns that into a blocking gate instead of a permissive default.
  /// </summary>
  public class MarketLegLimits
  {
    public double? LegSpreadMaxPips { get; set; }

    public double? LegVolatilityMaxPercent { get; set; }

    public bool IsFullyConfigured
    {
      get { return LegSpreadMaxPips.HasValue && LegVolatilityMaxPercent.HasValue; }
    }
  }

  /// <summary>
  /// Risk thresholds. Leg limits are per market: a spread that is normal on an index is unusable on a
  /// major FX pair, and a single global value would either block healthy legs or admit unhealthy ones.
  /// The snapshot window is global because a version carries a single market snapshot.
  /// </summary>
  public class RiskThresholds
  {
    private static readonly MarketLegLimits UnconfiguredMarket = new MarketLegLimits();

    public int? SnapshotMaxAgeSeconds { get; set; }

    public Dictionary<MarketKind, MarketLegLimits> Markets { get; set; }

    /// <summary>
    /// Limits in force for one market. A market that was never configured yields an unconfigured set,
    /// so the leg gates block instead of borrowing another market's limit.
    /// </summary>
    public MarketLegLimits ForMarket(MarketKind market)
    {
      MarketLegLimits limits;

      if (Markets != null && Markets.TryGetValue(market, out limits) && limits != null)
      {
        return limits;
      }

      return UnconfiguredMarket;
    }

    public bool IsFullyConfigured
    {
      get
      {
        if (!SnapshotMaxAgeSeconds.HasValue)
        {
          return false;
        }

        // Every declared market must have both leg limits, not just the ones currently in use.
        foreach (MarketKind market in Enum.GetValues(typeof(MarketKind)))
        {
          if (!ForMarket(market).IsFullyConfigured)
          {
            return false;
          }
        }

        return true;
      }
    }
  }

  public static class RiskThresholdsFactory
  {
    public static RiskThresholds FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      Dictionary<MarketKind, MarketLegLimits> markets = new Dictionary<MarketKind, MarketLegLimits>();

      foreach (MarketKind market in Enum.GetValues(typeof(MarketKind)))
      {
        markets.Add(market, new MarketLegLimits
        {
          LegSpreadMaxPips = ParsePositiveDouble(configuration[RiskConfigurationKeys.LegSpreadMaxPips(market)]),
          LegVolatilityMaxPercent = ParsePositiveDouble(configuration[RiskConfigurationKeys.LegVolatilityMaxPercent(market)])
        });
      }

      return new RiskThresholds
      {
        SnapshotMaxAgeSeconds = ParsePositiveInt(configuration[RiskConfigurationKeys.SnapshotMaxAgeSeconds]),
        Markets = markets
      };
    }

    private static int? ParsePositiveInt(string value)
    {
      int parsed;

      if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
      {
        return parsed;
      }

      return null;
    }

    private static double? ParsePositiveDouble(string value)
    {
      double parsed;

      if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
      {
        return parsed;
      }

      return null;
    }
  }
}
