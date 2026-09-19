using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// What the broker reported, recorded once. The broker event identity is the dedup key: an event already
  /// seen is ignored and never applied twice, which is what keeps a reconnect from doubling a fill.
  /// </summary>
  [Table("BrokerEvent")]
  public class BrokerEvent
  {
    [Key]
    public Guid Id { get; set; }

    public Guid? ExecutionId { get; set; }

    public Guid? LegId { get; set; }

    /// <summary>Identity assigned by the broker. Null only for events this system produced itself.</summary>
    [StringLength(128)]
    public string BrokerEventId { get; set; }

    public ExecutionEventKind Kind { get; set; }

    [StringLength(32)]
    public string Symbol { get; set; }

    [StringLength(512)]
    public string Payload { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
  }
}
