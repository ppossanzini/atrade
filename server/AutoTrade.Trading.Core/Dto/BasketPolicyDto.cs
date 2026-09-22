using AutoTrade.Trading.Core.Enums;
using System.Collections.Generic;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Version-level policy, which is what the operator configures as the strategy of the basket.
    /// Defaults confirmed by the operator from the validated prototype: RegimeMomentum entry,
    /// MinimumCoverage, 75% minimum coverage, 0.8% risk per basket, 2.5% daily loss limit.
    /// </summary>
    public class BasketPolicyDto
    {
        /// <summary>Declared entry rule of the strategy. Recorded and frozen, never silently defaulted.</summary>
        public EntryMode EntryMode { get; set; }

        public FailurePolicy FailurePolicy { get; set; }
        public int MinimumCoverage { get; set; }
        public double RiskPerBasket { get; set; }
        public double DailyLossLimit { get; set; }

        public StrategyCombinationMode CombinationMode { get; set; }

        public int MinimumAgreement { get; set; }

        public int MinimumStrategyConfidence { get; set; }

        public StrategyConflictPolicy ConflictPolicy { get; set; }

        public List<StrategyComponentDto> Components { get; set; }
    }

    public class StrategyComponentDto
    {
        public StrategyComponentType Type { get; set; }

        public bool Enabled { get; set; }

        public int Weight { get; set; }

        public TimeFrame TimeFrame { get; set; }

        public string ParametersJson { get; set; }
    }
}
