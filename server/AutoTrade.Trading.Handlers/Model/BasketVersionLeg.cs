using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// Frozen leg of a published version. Only the selected legs are stored, because a published
  /// version describes exactly the composition that would be executed.
  /// </summary>
  [Table("BasketVersionLeg")]
  public class BasketVersionLeg
  {
    [Key]
    public Guid Id { get; set; }

    public Guid VersionId { get; set; }

    public int Ordinal { get; set; }

    [Required]
    [StringLength(32)]
    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public LegDirection Direction { get; set; }

    public TimeFrame TimeFrame { get; set; }

    public int Weight { get; set; }

    public double RiskCap { get; set; }
  }
}
