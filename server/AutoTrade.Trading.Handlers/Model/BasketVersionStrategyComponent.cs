using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    [Table("BasketVersionStrategyComponent")]
    public class BasketVersionStrategyComponent
    {
        [Key]
        public Guid Id { get; set; }
        public Guid VersionId { get; set; }
        public int Ordinal { get; set; }
        public StrategyComponentType Type { get; set; }
        public bool Enabled { get; set; }
        public int Weight { get; set; }
        public TimeFrame TimeFrame { get; set; }
        [StringLength(1024)]
        public string ParametersJson { get; set; }
    }
}
