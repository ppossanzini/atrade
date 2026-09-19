using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>Editable leg of a basket draft. Owned exclusively by the composition update.</summary>
  [Table("BasketDraftLeg")]
  public class BasketDraftLeg
  {
    [Key]
    public Guid Id { get; set; }

    public Guid BasketId { get; set; }

    [Required]
    [StringLength(32)]
    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public LegDirection Direction { get; set; }

    public TimeFrame TimeFrame { get; set; }

    public int Weight { get; set; }

    public double RiskCap { get; set; }

    /// <summary>Stop distance of the leg in pips, decided with the leg and published with the version.</summary>
    public double StopDistancePips { get; set; }

    public bool IsSelected { get; set; }
  }
}
