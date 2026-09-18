using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  [Table("TradingAccount")]
  public class TradingAccount
  {
    [Key]
    public Guid Id { get; set; }

    public long BrokerAccountId { get; set; }

    public TradingEnvironment Environment { get; set; }

    public bool IsTradingEnabled { get; set; }

    public BrokerConnectionState ConnectionState { get; set; }

    public DateTime? LastBrokerSyncUtc { get; set; }

    public DateTime? LastReconciledUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
  }
}
