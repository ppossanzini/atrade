using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>Frozen policy of a published version, which is the strategy the version follows.</summary>
    [Table("BasketVersionPolicy")]
    public class BasketVersionPolicy
    {
        [Key]
        public Guid Id { get; set; }

        public Guid VersionId { get; set; }

        /// <summary>Declared entry rule, frozen with the version.</summary>
        public EntryMode EntryMode { get; set; }

        public FailurePolicy FailurePolicy { get; set; }

        public int MinimumCoverage { get; set; }

        public double RiskPerBasket { get; set; }

        public double DailyLossLimit { get; set; }

        public StrategyCombinationMode CombinationMode { get; set; }

        public int MinimumAgreement { get; set; }

        public int MinimumStrategyConfidence { get; set; }

        public StrategyConflictPolicy ConflictPolicy { get; set; }
    }
}
