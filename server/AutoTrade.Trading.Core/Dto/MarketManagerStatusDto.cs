using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class MarketManagerStatusDto
  {
    public MarketManagerMode Mode { get; set; }
    public bool IsAnalysisRunning { get; set; }
  }
}
