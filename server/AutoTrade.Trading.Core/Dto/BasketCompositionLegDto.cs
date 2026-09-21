using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Operator-owned leg definition. Analysis values (score, correlation, volatility, spread, leg
    /// status) are produced by the analysis pipeline and are deliberately not part of this contract.
    /// </summary>
    public class BasketCompositionLegDto
    {
        public string Symbol { get; set; }
        public LegDirection Direction { get; set; }
        public TimeFrame TimeFrame { get; set; }
        public int Weight { get; set; }
        public double RiskCap { get; set; }

        /// <summary>
        /// Stop distance of the leg, in pips, chosen by the operator for this instrument inside this basket.
        /// It belongs to the composition and not to a deployment setting: how far a leg may run against us is a
        /// decision about that leg, and it is frozen into the version like every other leg property.
        /// </summary>
        public double StopDistancePips { get; set; }

        /// <summary>
        /// Widest spread tolerated on this leg, in pips. Zero means the operator has not decided it yet and the
        /// corresponding gate blocks; the value belongs to the leg because what is normal on an index is
        /// unusable on a major pair, and it is frozen into the version like every other leg property.
        /// </summary>
        public double MaxSpreadPips { get; set; }

        /// <summary>
        /// Highest volatility tolerated on this leg, as a percentage. Zero means not decided and blocks.
        /// </summary>
        public double MaxVolatilityPercent { get; set; }

        public bool IsSelected { get; set; }
    }
}
