namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Configured risk thresholds, surfaced so the operator can see which limits are actually in force.
  /// A null value means the threshold is not configured, and the corresponding gate therefore blocks.
  /// </summary>
  public class RiskLimitsDto
  {
    public int? SnapshotMaxAgeSeconds { get; set; }

    public double? LegSpreadMaxPips { get; set; }

    public double? LegVolatilityMaxPercent { get; set; }

    public bool IsFullyConfigured { get; set; }
  }
}
