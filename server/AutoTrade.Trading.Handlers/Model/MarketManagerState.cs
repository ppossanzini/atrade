using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  [Table("MarketManagerState")]
  public class MarketManagerState
  {
    [Key]
    public int Id { get; set; }

    public MarketManagerMode Mode { get; set; }

    public bool IsAnalysisRunning { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
  }
}
