using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class TradingAccountStatusDto
  {
    public long BrokerAccountId { get; set; }
    public TradingEnvironment Environment { get; set; }
    public BrokerConnectionState ConnectionState { get; set; }
    public bool IsTradingEnabled { get; set; }
    public DateTime? LastBrokerSyncUtc { get; set; }
    public DateTime? LastReconciledUtc { get; set; }
  }
}
