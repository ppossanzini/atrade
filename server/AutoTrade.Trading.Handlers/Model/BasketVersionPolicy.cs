using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>Frozen policy of a published version.</summary>
  [Table("BasketVersionPolicy")]
  public class BasketVersionPolicy
  {
    [Key]
    public Guid Id { get; set; }

    public Guid VersionId { get; set; }

    public FailurePolicy FailurePolicy { get; set; }

    public int MinimumCoverage { get; set; }

    public double RiskPerBasket { get; set; }

    public double DailyLossLimit { get; set; }
  }
}
