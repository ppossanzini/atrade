using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// A symbol the caller needs a capture for. The market travels with the symbol because the source may
  /// have to fall back to market level values, and because it never has to guess a classification.
  /// </summary>
  public class SymbolRequest
  {
    public string Symbol { get; set; }

    public MarketKind Market { get; set; }
  }

  /// <summary>
  /// What one symbol looks like in a capture. Every measurement is nullable because absence is a real
  /// case: a symbol the source cannot quote must not become a zero.
  /// </summary>
  public class SymbolCapture
  {
    public string Symbol { get; set; }

    public double? Price { get; set; }

    public double? SpreadPips { get; set; }

    public double? VolatilityPercent { get; set; }

    /// <summary>Whether the symbol can be traded right now. Coverage is computed from it.</summary>
    public bool IsTradable { get; set; }
  }

  /// <summary>
  /// Account facts of the same capture. They belong to the capture because a risk decision must not mix
  /// a market instant with an account instant taken elsewhere.
  /// </summary>
  public class AccountCapture
  {
    public double Equity { get; set; }

    public double Balance { get; set; }

    public double RealizedPnlToday { get; set; }

    public double UnrealizedPnl { get; set; }

    public TradingEnvironment Environment { get; set; }
  }

  /// <summary>
  /// One coherent capture: a single instant, the requested symbols and the account facts. When the source
  /// cannot deliver, everything is absent and <see cref="IsAvailable"/> is false, so the consumer blocks
  /// instead of inventing values.
  /// </summary>
  public class MarketDataCapture
  {
    public bool IsAvailable { get; set; }

    public System.DateTime? CapturedAtUtc { get; set; }

    public AccountCapture Account { get; set; }

    public List<SymbolCapture> Symbols { get; set; }

    public static MarketDataCapture Unavailable()
    {
      return new MarketDataCapture
      {
        IsAvailable = false,
        CapturedAtUtc = null,
        Account = null,
        Symbols = new List<SymbolCapture>()
      };
    }
  }

  /// <summary>
  /// The seam between the application and whoever supplies market and account data. Behind it live the
  /// simulated source used for development and the broker source that arrives with the approved
  /// integration; above it nothing knows which one is active.
  /// </summary>
  public interface IMarketDataSource
  {
    Task<MarketDataCapture> CaptureAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken);
  }
}
