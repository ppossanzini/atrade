using System;

namespace AutoTrade.Trading.Core.Dto
{
  public class OperationalStatusDto
  {
    public DateTime ServerTimeUtc { get; set; }
    public KillSwitchStatusDto KillSwitch { get; set; }
    public TradingAccountStatusDto Account { get; set; }
    public MarketManagerStatusDto MarketManager { get; set; }
  }
}
