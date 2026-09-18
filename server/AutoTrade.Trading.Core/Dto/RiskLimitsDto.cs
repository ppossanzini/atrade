using System.Collections.Generic;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Configured risk thresholds, surfaced so the operator can see which limits are actually in force.
  /// Leg limits are reported per market; a null value means the threshold is not configured, and the
  /// corresponding gate therefore blocks.
  /// </summary>
  public class RiskLimitsDto
  {
    public int? SnapshotMaxAgeSeconds { get; set; }

    /// <summary>One entry per market of the catalogue, configured or not, so nothing stays invisible.</summary>
    public List<MarketRiskLimitsDto> Markets { get; set; }

    public bool IsFullyConfigured { get; set; }
  }
}
