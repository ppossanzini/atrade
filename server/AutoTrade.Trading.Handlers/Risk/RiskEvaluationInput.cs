using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// One leg as the risk engine sees it. Market values are nullable because absence is a first class case:
  /// a leg without spread or volatility cannot be judged, and an unjudged leg must not be executed.
  /// </summary>
  public class RiskEvaluationLeg
  {
    public string Symbol { get; set; }

    /// <summary>Market the symbol belongs to. The leg limits are resolved from it, never assumed.</summary>
    public MarketKind Market { get; set; }

    public int Weight { get; set; }

    public double? SpreadPips { get; set; }

    public double? VolatilityPercent { get; set; }

    /// <summary>
    /// Whether the leg can actually be prepared for execution. Coverage is computed from this flag, so a
    /// leg that cannot be prepared reduces coverage instead of silently disappearing.
    /// </summary>
    public bool IsExecutable { get; set; }
  }

  /// <summary>
  /// Complete input of a risk evaluation: application state plus the market snapshot values. The engine
  /// reads nothing else, which is what makes it deterministic and testable by table.
  /// </summary>
  public class RiskEvaluationInput
  {
    public bool HasActiveVersion { get; set; }

    public bool KillSwitchEngaged { get; set; }

    public int VersionNumber { get; set; }

    /// <summary>Null when no market snapshot is available at all.</summary>
    public DateTime? MarketDataCapturedAtUtc { get; set; }

    public FailurePolicy FailurePolicy { get; set; }

    public int MinimumCoverage { get; set; }

    /// <summary>Risk per basket limit, as a percentage, from the active version policy.</summary>
    public double RiskPerBasketLimit { get; set; }

    /// <summary>Daily loss limit, as a percentage, from the active version policy.</summary>
    public double DailyLossLimit { get; set; }

    /// <summary>Current basket risk as a percentage, or null when it cannot be computed.</summary>
    public double? BasketRiskPercent { get; set; }

    /// <summary>Current daily loss as a percentage, or null when it cannot be computed.</summary>
    public double? DailyLossPercent { get; set; }

    public List<RiskEvaluationLeg> Legs { get; set; }
  }
}
