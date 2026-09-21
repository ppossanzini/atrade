using System;
using System.Globalization;
using AutoTrade.Trading.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Risk
{
    /// <summary>
    /// Risk settings that stay in deployment configuration. The leg limits do not live here: what spread or
    /// volatility is acceptable is a decision about a leg, so it travels with the leg through the draft, the
    /// version and the proposal, exactly like the stop distance. What remains is the snapshot validity window,
    /// which describes the cadence of the feed and not a decision about a basket.
    ///
    /// The value is nullable on purpose: null means "not decided yet", and the engine turns that into a
    /// blocking gate instead of a permissive default.
    /// </summary>
    public class RiskThresholds
    {
        public int? SnapshotMaxAgeSeconds { get; set; }

        public bool IsConfigured
        {
            get { return SnapshotMaxAgeSeconds.HasValue; }
        }
    }

    public static class RiskThresholdsFactory
    {
        public static RiskThresholds FromConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return new RiskThresholds
            {
                SnapshotMaxAgeSeconds = ParsePositiveInt(configuration[RiskConfigurationKeys.SnapshotMaxAgeSeconds])
            };
        }

        private static int? ParsePositiveInt(string value)
        {
            int parsed;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
            {
                return parsed;
            }

            return null;
        }
    }
}
