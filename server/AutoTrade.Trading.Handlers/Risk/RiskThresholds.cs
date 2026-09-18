using System;
using System.Globalization;
using AutoTrade.Trading.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// Risk thresholds. Every one of them is nullable on purpose: null means "not decided yet", and the
  /// engine turns that into a blocking gate instead of a permissive default.
  /// </summary>
  public class RiskThresholds
  {
    public int? SnapshotMaxAgeSeconds { get; set; }

    public double? LegSpreadMaxPips { get; set; }

    public double? LegVolatilityMaxPercent { get; set; }

    public bool IsFullyConfigured
    {
      get { return SnapshotMaxAgeSeconds.HasValue && LegSpreadMaxPips.HasValue && LegVolatilityMaxPercent.HasValue; }
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

      return new RiskThresholds
      {
        SnapshotMaxAgeSeconds = ParsePositiveInt(configuration[RiskConfigurationKeys.SnapshotMaxAgeSeconds]),
        LegSpreadMaxPips = ParsePositiveDouble(configuration[RiskConfigurationKeys.LegSpreadMaxPips]),
        LegVolatilityMaxPercent = ParsePositiveDouble(configuration[RiskConfigurationKeys.LegVolatilityMaxPercent])
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
