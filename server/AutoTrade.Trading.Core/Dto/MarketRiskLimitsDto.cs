using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Leg limits in force for one market. Null means the threshold is not configured for that market, and
  /// the corresponding gate blocks: a missing decision is never filled in with another market's value.
  /// </summary>
  public class MarketRiskLimitsDto
  {
    public MarketKind Market { get; set; }

    public double? LegSpreadMaxPips { get; set; }

    public double? LegVolatilityMaxPercent { get; set; }

    public bool IsConfigured { get; set; }
  }
}
