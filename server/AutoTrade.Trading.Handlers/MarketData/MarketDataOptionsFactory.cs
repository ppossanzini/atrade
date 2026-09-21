using System;
using System.Collections.Generic;
using System.Globalization;
using AutoTrade.Trading.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.MarketData
{
    /// <summary>
    /// Reads the market data configuration. An unrecognised provider name aborts startup instead of
    /// degrading to "no data": a typo in a deployment must not silently disable the feed.
    /// </summary>
    public static class MarketDataOptionsFactory
    {
        private const string ProviderKey = "Trading:MarketData:Provider";
        private const string SimulatedSectionKey = "Trading:MarketData:Simulated";

        public static MarketDataOptions FromConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            MarketDataOptions options = new MarketDataOptions
            {
                Provider = ParseProvider(configuration[ProviderKey], ProviderKey),
                Simulated = ReadSimulated(configuration.GetSection(SimulatedSectionKey))
            };

            return options;
        }

        private static MarketDataProviderKind ParseProvider(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return MarketDataProviderKind.None;
            }

            if (Enum.TryParse(value.Trim(), true, out MarketDataProviderKind provider))
            {
                return provider;
            }

            throw new InvalidOperationException($"Configuration value '{key}' is not a known market data provider: '{value}'. Use None, Simulated or Ctrader.");
        }

        private static SimulatedMarketDataOptions ReadSimulated(IConfigurationSection section)
        {
            SimulatedMarketDataOptions options = new SimulatedMarketDataOptions
            {
                Seed = ReadInt(section, "Seed", 0),
                JitterPercent = ReadDouble(section, "JitterPercent", 0),
                Equity = ReadDouble(section, "Equity", 0),
                Balance = ReadDouble(section, "Balance", 0),
                RealizedPnlToday = ReadDouble(section, "RealizedPnlToday", 0),
                UnrealizedPnl = ReadDouble(section, "UnrealizedPnl", 0),
                AccountCurrency = section["AccountCurrency"],
                Symbols = new Dictionary<string, SimulatedSymbolOptions>(StringComparer.OrdinalIgnoreCase),
                DefaultByMarket = new Dictionary<MarketKind, SimulatedSymbolOptions>()
            };

            foreach (IConfigurationSection symbol in section.GetSection("Symbols").GetChildren())
            {
                options.Symbols[symbol.Key] = ReadSymbol(symbol, false);
            }

            foreach (IConfigurationSection market in section.GetSection("DefaultByMarket").GetChildren())
            {
                if (!Enum.TryParse(market.Key, true, out MarketKind marketKind))
                {
                    throw new InvalidOperationException($"Configuration key '{SimulatedSectionKey}:DefaultByMarket:{market.Key}' is not a known market. Use Fx, Metal or Index.");
                }

                SimulatedSymbolOptions profile = ReadSymbol(market, true);
                profile.Market = marketKind;
                options.DefaultByMarket[marketKind] = profile;
            }

            return options;
        }

        private static SimulatedSymbolOptions ReadSymbol(IConfigurationSection section, bool isMarketDefault)
        {
            return new SimulatedSymbolOptions
            {
                Market = ReadEnum(section, "Market", MarketKind.Fx),
                BasePrice = ReadDouble(section, "BasePrice", 0),
                SpreadPips = ReadDouble(section, "SpreadPips", 0),
                VolatilityPercent = ReadDouble(section, "VolatilityPercent", 0),
                IsTradable = ReadBool(section, "IsTradable", true),
                MinVolume = ReadInt(section, "MinVolume", 0),
                StepVolume = ReadInt(section, "StepVolume", 0),
                MaxVolume = ReadInt(section, "MaxVolume", 0),
                LotSize = ReadInt(section, "LotSize", 0),
                PipSizePerUnit = ReadDouble(section, "PipSizePerUnit", 0),
                ProfitCurrency = section["ProfitCurrency"],
                IsMarketDefault = isMarketDefault
            };
        }

        private static MarketKind ReadEnum(IConfigurationSection section, string key, MarketKind fallback)
        {
            string value = section[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (Enum.TryParse(value.Trim(), true, out MarketKind parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not a known market: '{value}'. Use Fx, Metal or Index.");
        }

        private static double ReadDouble(IConfigurationSection section, string key, double fallback)
        {
            string value = section[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not a number: '{value}'.");
        }

        private static int ReadInt(IConfigurationSection section, string key, int fallback)
        {
            string value = section[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not an integer: '{value}'.");
        }

        private static bool ReadBool(IConfigurationSection section, string key, bool fallback)
        {
            string value = section[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not a boolean: '{value}'.");
        }
    }
}
