using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Version-level policy. Defaults confirmed by the operator from the validated prototype:
  /// MinimumCoverage, 75% minimum coverage, 0.8% risk per basket, 2.5% daily loss limit.
  /// </summary>
  public class BasketPolicyDto
  {
    public FailurePolicy FailurePolicy { get; set; }
    public int MinimumCoverage { get; set; }
    public double RiskPerBasket { get; set; }
    public double DailyLossLimit { get; set; }
  }
}
