namespace AutoTrade.Trading.Core.Configuration
{
  /// <summary>
  /// Configuration keys of the risk section. No threshold has a default in code: an unconfigured limit
  /// makes its gate block, so a missing decision can never become a permissive one.
  /// </summary>
  public static class RiskConfigurationKeys
  {
    public const string Section = "Trading:Risk";

    public const string SnapshotMaxAgeSeconds = "Trading:Risk:SnapshotMaxAgeSeconds";

    public const string LegSpreadMaxPips = "Trading:Risk:LegSpreadMaxPips";

    public const string LegVolatilityMaxPercent = "Trading:Risk:LegVolatilityMaxPercent";
  }
}
