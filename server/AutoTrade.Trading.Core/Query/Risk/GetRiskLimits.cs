using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Risk
{
  /// <summary>Reports the configured risk thresholds, including which ones are still missing.</summary>
  public class GetRiskLimits : IRequest<RiskLimitsDto>
  {
  }
}
