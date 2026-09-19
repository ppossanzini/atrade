using System;
using System.Collections.Generic;
using System.Globalization;
using AutoTrade.Trading.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// The stop distance of the risk model, which is the only sizing input that is a decision rather than a
  /// provider fact. Per market, with a per symbol override, and deliberately without a default: without a
  /// stop distance there is no size, and no size means no order.
  /// </summary>
  public class RiskSizingThresholds
  {
    private readonly Dictionary<MarketKind, double> _byMarket;
    private readonly Dictionary<string, double> _bySymbol;

    public RiskSizingThresholds(Dictionary<MarketKind, double> byMarket, Dictionary<string, double> bySymbol)
    {
      _byMarket = byMarket ?? new Dictionary<MarketKind, double>();
      _bySymbol = bySymbol ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsConfigured
    {
      get { return _byMarket.Count > 0 || _bySymbol.Count > 0; }
    }

    /// <summary>
    /// Stop distance for a leg: the symbol override wins, otherwise the market value, otherwise nothing.
    /// A market never borrows another market's stop, for the same reason it never borrows its limits.
    /// </summary>
    public double? ForLeg(string symbol, MarketKind market)
    {
      if (!string.IsNullOrWhiteSpace(symbol) && _bySymbol.TryGetValue(symbol, out double symbolValue))
      {
        return symbolValue;
      }

      return _byMarket.TryGetValue(market, out double marketValue) ? marketValue : (double?)null;
    }
  }

  public static class RiskSizingThresholdsFactory
  {
    private const string SectionKey = "Trading:Risk:Sizing";

    public static RiskSizingThresholds FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      Dictionary<MarketKind, double> byMarket = new Dictionary<MarketKind, double>();
      Dictionary<string, double> bySymbol = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

      foreach (IConfigurationSection market in configuration.GetSection(SectionKey + ":DefaultByMarket").GetChildren())
      {
        if (!Enum.TryParse(market.Key, true, out MarketKind marketKind))
        {
          throw new InvalidOperationException($"Configuration key '{SectionKey}:DefaultByMarket:{market.Key}' is not a known market. Use Fx, Metal or Index.");
        }

        if (TryRead(market["StopDistancePips"], SectionKey + ":DefaultByMarket:" + market.Key + ":StopDistancePips", out double value))
        {
          byMarket[marketKind] = value;
        }
      }

      foreach (IConfigurationSection symbol in configuration.GetSection(SectionKey + ":Symbols").GetChildren())
      {
        if (TryRead(symbol["StopDistancePips"], SectionKey + ":Symbols:" + symbol.Key + ":StopDistancePips", out double value))
        {
          bySymbol[symbol.Key] = value;
        }
      }

      return new RiskSizingThresholds(byMarket, bySymbol);
    }

    private static bool TryRead(string raw, string key, out double value)
    {
      value = 0;

      if (string.IsNullOrWhiteSpace(raw))
      {
        return false;
      }

      if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
      {
        value = parsed;

        return true;
      }

      throw new InvalidOperationException($"Configuration value '{key}' is not a number: '{raw}'.");
    }
  }
}
