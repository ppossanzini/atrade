using AutoTrade.Trading.Core.Enums;

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
    }
}
