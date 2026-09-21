using System;
using AutoTrade.Trading.Handlers.MarketData;

namespace AutoTrade.Trading.Handlers.Risk
{
    /// <summary>
    /// Everything the sizing needs: the risk model inputs (capital, risk cap, stop distance) and the provider
    /// facts about the instrument. Nothing here is optional by accident: absence is what makes the size
    /// unavailable, and an unavailable size is never replaced by a guess.
    /// </summary>
    public class SizingInput
    {
        public double Equity { get; set; }

        public string AccountCurrency { get; set; }

        /// <summary>Risk cap of the leg, as a percentage of capital, from the active version.</summary>
        public double RiskCapPercent { get; set; }

        /// <summary>Stop distance in pips, decided by the risk model.</summary>
        public double? StopDistancePips { get; set; }

        /// <summary>Instrument facts, or null when the provider did not describe the symbol.</summary>
        public SymbolSpecification Specification { get; set; }

        /// <summary>
        /// Rate from the instrument profit currency to the account currency. Required only when the two differ;
        /// when it is missing the size is refused instead of being computed with an assumed rate.
        /// </summary>
        public double? ConversionRate { get; set; }
    }

    /// <summary>
    /// Outcome of the sizing. <see cref="Reason"/> is always filled, also on success, so the journal can record
    /// why a volume came out that way.
    /// </summary>
    public class SizingResult
    {
        public bool IsSized { get; set; }

        public int VolumeUnits { get; set; }

        /// <summary>The volume before rounding down, kept for diagnosis: a refusal must be explainable.</summary>
        public double IdealVolumeUnits { get; set; }

        public string Reason { get; set; }
    }

    /// <summary>
    /// Turns risk into a volume, deterministically.
    ///
    /// The rule is the approved one: the amount at risk is the capital multiplied by the leg risk cap, divided
    /// by the money value of the stop distance, then rounded **down** to the instrument step. The size is never
    /// rounded up to reach the instrument minimum: an order that cannot respect the risk model is not sent, and
    /// the reason travels with the refusal.
    /// </summary>
    public static class PositionSizingCalculator
    {
        public const string StopNotConfigured = "stop_distance_not_configured";
        public const string SymbolNotDescribed = "symbol_not_described";
        public const string SymbolNotTradable = "symbol_not_tradable";
        public const string EquityUnavailable = "equity_unavailable";
        public const string PipSizeUnavailable = "pip_size_unavailable";
        public const string StepUnavailable = "volume_step_unavailable";
        public const string ConversionUnavailable = "conversion_rate_unavailable";
        public const string BelowMinimum = "volume_below_instrument_minimum";
        public const string AboveMaximum = "volume_above_instrument_maximum";

        public static SizingResult Compute(SizingInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!input.StopDistancePips.HasValue || input.StopDistancePips.Value <= 0)
            {
                return Refused(StopNotConfigured);
            }

            SymbolSpecification specification = input.Specification;

            if (specification == null)
            {
                return Refused(SymbolNotDescribed);
            }

            if (!specification.IsTradable)
            {
                return Refused(SymbolNotTradable);
            }

            if (input.Equity <= 0)
            {
                return Refused(EquityUnavailable);
            }

            if (specification.PipSizePerUnit <= 0)
            {
                return Refused(PipSizeUnavailable);
            }

            int step = specification.StepVolume > 0 ? specification.StepVolume : specification.MinVolume;

            if (step <= 0)
            {
                return Refused(StepUnavailable);
            }

            if (RequiresConversion(input) && !input.ConversionRate.HasValue)
            {
                return Refused(ConversionUnavailable);
            }

            double conversion = RequiresConversion(input) ? input.ConversionRate.Value : 1;

            double riskAmount = input.Equity * input.RiskCapPercent / 100;
            double valuePerUnit = input.StopDistancePips.Value * specification.PipSizePerUnit;
            double ideal = riskAmount / valuePerUnit / conversion;
            int volume = (int)(Math.Floor(ideal / step) * step);

            if (volume < specification.MinVolume)
            {
                return new SizingResult
                {
                    IsSized = false,
                    IdealVolumeUnits = Math.Round(ideal, 3),
                    VolumeUnits = 0,
                    Reason = BelowMinimum
                };
            }

            if (specification.MaxVolume > 0 && volume > specification.MaxVolume)
            {
                return new SizingResult
                {
                    IsSized = false,
                    IdealVolumeUnits = Math.Round(ideal, 3),
                    VolumeUnits = 0,
                    Reason = AboveMaximum
                };
            }

            return new SizingResult
            {
                IsSized = true,
                IdealVolumeUnits = Math.Round(ideal, 3),
                VolumeUnits = volume,
                Reason = "sized"
            };
        }

        private static bool RequiresConversion(SizingInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Specification.ProfitCurrency) || string.IsNullOrWhiteSpace(input.AccountCurrency))
            {
                // Without both currencies the question cannot be answered, so the answer is "a rate would be needed"
                // and the sizing stops: assuming no conversion is how a size gets multiplied by the wrong rate.
                return true;
            }

            return !string.Equals(input.Specification.ProfitCurrency, input.AccountCurrency, StringComparison.OrdinalIgnoreCase);
        }

        private static SizingResult Refused(string reason)
        {
            return new SizingResult
            {
                IsSized = false,
                VolumeUnits = 0,
                Reason = reason
            };
        }
    }
}
