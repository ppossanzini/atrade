using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// The market and account instance a proposal was judged on. It is persisted so a decision can always be
  /// replayed against the values that produced it, instead of being explained only by the gate wording.
  /// </summary>
  [Table("MarketSnapshot")]
  public class MarketSnapshot
  {
    [Key]
    public Guid Id { get; set; }

    public DateTime CapturedAtUtc { get; set; }

    public double AccountEquity { get; set; }

    public double AccountBalance { get; set; }

    public double RealizedPnlToday { get; set; }

    public double UnrealizedPnl { get; set; }
  }

  /// <summary>One symbol of a snapshot. A measurement that was not available stays null.</summary>
  [Table("MarketSnapshotLeg")]
  public class MarketSnapshotLeg
  {
    [Key]
    public Guid Id { get; set; }

    public Guid SnapshotId { get; set; }

    public int Ordinal { get; set; }

    [Required]
    [StringLength(32)]
    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public double? Price { get; set; }

    public double? SpreadPips { get; set; }

    public double? VolatilityPercent { get; set; }

    public bool IsTradable { get; set; }
  }
}
