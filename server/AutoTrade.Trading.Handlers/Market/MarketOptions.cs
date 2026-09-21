using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Market
{
    /// <summary>
    /// Timing of the analysis cycle. Neither value has a default in code: the cycle does not start until the
    /// operator has decided how often to analyse and how long a proposal stays valid.
    /// </summary>
    public class MarketOptions
    {
        public int CycleSeconds { get; set; }

        public int ProposalTtlSeconds { get; set; }

        public bool IsConfigured
        {
            get { return CycleSeconds > 0 && ProposalTtlSeconds > 0; }
        }
    }

    public static class MarketOptionsFactory
    {
        private const string SectionKey = "Trading:Market";

        public static MarketOptions FromConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return new MarketOptions
            {
                CycleSeconds = ReadInt(configuration, "CycleSeconds"),
                ProposalTtlSeconds = ReadInt(configuration, "ProposalTtlSeconds")
            };
        }

        private static int ReadInt(IConfiguration configuration, string key)
        {
            string value = configuration[SectionKey + ":" + key];

            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Configuration value '{SectionKey}:{key}' is not an integer: '{value}'.");
        }
    }
}
