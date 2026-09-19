using System;

namespace AutoTrade.Trading.Core.Dto
{
  public class OperationalStatusDto
  {
    public DateTime ServerTimeUtc { get; set; }
    public KillSwitchStatusDto KillSwitch { get; set; }
    public TradingAccountStatusDto Account { get; set; }
    public MarketManagerStatusDto MarketManager { get; set; }

    /// <summary>Semantic memory in force, so its absence is a reported state and not a silent one.</summary>
    public EvidenceStoreStatusDto EvidenceStore { get; set; }
  }
}
