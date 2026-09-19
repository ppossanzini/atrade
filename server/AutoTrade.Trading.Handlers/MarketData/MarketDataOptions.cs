using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// Which source produces market and account captures. <see cref="None"/> is the default and means no
  /// data at all: the risk gates then block exactly as they do without a feed, so a deployment cannot
  /// start with invented values because nobody configured a source.
  /// </summary>
  public enum MarketDataProviderKind
  {
    None = 0,
    Simulated = 1,
    Ctrader = 2
  }

  /// <summary>
  /// Configuration of the market data seam. The provider is chosen once at startup; nothing downstream
  /// reads this class, so the rest of the application consumes a capture without knowing where it came
  /// from and swapping the source is a configuration change plus one implementation.
  /// </summary>
  public class MarketDataOptions
  {
    public MarketDataProviderKind Provider { get; set; }

    public SimulatedMarketDataOptions Simulated { get; set; }
  }

  /// <summary>
  /// Profile of a simulated capture. The seed makes a capture reproducible, so a demo or a test can be
  /// replayed and a gate path can be forced deliberately.
  /// </summary>
  public class SimulatedMarketDataOptions
  {
    public int Seed { get; set; }

    /// <summary>Relative amplitude of the cycle to cycle variation, in percent.</summary>
    public double JitterPercent { get; set; }

    public double Equity { get; set; }

    public double Balance { get; set; }

    public double RealizedPnlToday { get; set; }

    public double UnrealizedPnl { get; set; }

    /// <summary>Currency of the simulated account, used to decide whether a conversion would be needed.</summary>
    public string AccountCurrency { get; set; }

    /// <summary>Explicit symbol profiles, keyed by symbol.</summary>
    public Dictionary<string, SimulatedSymbolOptions> Symbols { get; set; }

    /// <summary>Fallback profile per market, used for symbols without an explicit profile.</summary>
    public Dictionary<MarketKind, SimulatedSymbolOptions> DefaultByMarket { get; set; }
  }

  /// <summary>
  /// Values of one simulated symbol. They are deliberately plain numbers: the simulation describes a
  /// market, it does not model one.
  /// </summary>
  public class SimulatedSymbolOptions
  {
    public MarketKind Market { get; set; }

    public double BasePrice { get; set; }

    public double SpreadPips { get; set; }

    public double VolatilityPercent { get; set; }

    public bool IsTradable { get; set; }

    /// <summary>Smallest volume the provider accepts for this instrument.</summary>
    public int MinVolume { get; set; }

    public int StepVolume { get; set; }

    public int MaxVolume { get; set; }

    public int LotSize { get; set; }

    /// <summary>Price value of one pip for one unit of volume.</summary>
    public double PipSizePerUnit { get; set; }

    public string ProfitCurrency { get; set; }

    /// <summary>
    /// True when this profile was materialised as a fallback for a whole market, so an explicit symbol
    /// entry still wins over it.
    /// </summary>
    public bool IsMarketDefault { get; set; }
  }
}
