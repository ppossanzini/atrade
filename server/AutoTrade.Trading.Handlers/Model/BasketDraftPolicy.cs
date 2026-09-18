using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>Editable policy of a basket draft. Owned exclusively by the policy update.</summary>
  [Table("BasketDraftPolicy")]
  public class BasketDraftPolicy
  {
    [Key]
    public Guid Id { get; set; }

    public Guid BasketId { get; set; }

    public FailurePolicy FailurePolicy { get; set; }

    public int MinimumCoverage { get; set; }

    public double RiskPerBasket { get; set; }

    public double DailyLossLimit { get; set; }
  }
}
