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

        public List<MarketBar> Bars { get; set; } = new List<MarketBar>();
    }

    /// <summary>Closed OHLC bar supplied by the market provider; open bars are never used by strategies.</summary>
    public class MarketBar
    {
        public TimeFrame TimeFrame { get; set; }
        public System.DateTime ClosedAtUtc { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
    }

    /// <summary>
    /// Account facts of the same capture. They belong to the capture because a risk decision must not mix
    /// a market instant with an account instant taken elsewhere, and because the position size depends on the
    /// capital and on the account currency rather than on a number configured by the application.
    /// </summary>
    public class AccountCapture
    {
        public double Equity { get; set; }

        public double Balance { get; set; }

        public double RealizedPnlToday { get; set; }

        public double UnrealizedPnl { get; set; }

        public TradingEnvironment Environment { get; set; }

        /// <summary>Currency the account is denominated in, used to decide whether a conversion is needed.</summary>
        public string Currency { get; set; }
    }

    /// <summary>
    /// What the provider says about one instrument: the facts that decide whether and how much can be sent.
    /// They are not operating policy and are never configured as such, because they describe the instrument
    /// and change with it (lot size, leverage and contract size are all folded into these numbers).
    /// </summary>
    public class SymbolSpecification
    {
        public string Symbol { get; set; }

        public MarketKind Market { get; set; }

        /// <summary>Smallest volume the provider accepts for this instrument, in broker units.</summary>
        public int MinVolume { get; set; }

        /// <summary>Increment every volume must be a multiple of.</summary>
        public int StepVolume { get; set; }

        /// <summary>Largest volume the provider accepts, or 0 when it does not publish one.</summary>
        public int MaxVolume { get; set; }

        /// <summary>Units in one lot, as the provider defines them.</summary>
        public int LotSize { get; set; }

        /// <summary>Price value of one pip for one unit of volume: the bridge between pips and money.</summary>
        public double PipSizePerUnit { get; set; }

        /// <summary>Currency the instrument settles in. When it differs from the account currency, a rate is needed.</summary>
        public string ProfitCurrency { get; set; }

        public bool IsTradable { get; set; }
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
    /// simulated source used for development and the cTrader source enabled after application approval;
    /// above it nothing knows which one is active.
    /// </summary>
    public interface IMarketDataSource
    {
        Task<MarketDataCapture> CaptureAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken);

        /// <summary>
        /// Describes the instruments the provider offers. A symbol missing from the answer is not tradable, and
        /// the application never substitutes a specification of its own.
        /// </summary>
        Task<List<SymbolSpecification>> DescribeAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken);
    }
}
