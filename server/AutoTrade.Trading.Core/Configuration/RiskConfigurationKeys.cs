using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Configuration
{
  /// <summary>
  /// Configuration keys of the risk section. No threshold has a default in code: an unconfigured limit
  /// makes its gate block, so a missing decision can never become a permissive one.
  ///
  /// The leg limits are per market because one value cannot describe both a major FX pair and an index.
  /// The snapshot validity window stays global: a version carries a single market snapshot, so there is
  /// nothing to differentiate until snapshots become per market.
  /// </summary>
  public static class RiskConfigurationKeys
  {
    public const string Section = "Trading:Risk";

    public const string SnapshotMaxAgeSeconds = "Trading:Risk:SnapshotMaxAgeSeconds";

    public const string MarketsSection = "Trading:Risk:Markets";

    public static string MarketSection(MarketKind market)
    {
      return MarketsSection + ":" + market;
    }

    public static string LegSpreadMaxPips(MarketKind market)
    {
      return MarketSection(market) + ":LegSpreadMaxPips";
    }

    public static string LegVolatilityMaxPercent(MarketKind market)
    {
      return MarketSection(market) + ":LegVolatilityMaxPercent";
    }
  }
}
