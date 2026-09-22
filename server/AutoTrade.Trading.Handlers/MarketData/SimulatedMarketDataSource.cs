using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.MarketData
{
    /// <summary>
    /// Source used for development and demo while the broker integration waits for the application to be
    /// approved. It produces a coherent capture from configured profiles and a seeded variation, so a
    /// capture is reproducible and a gate path can be forced on purpose (spread near a limit, symbol
    /// unavailable, volatility spike).
    ///
    /// It implements the same seam as the broker source: nothing above it knows which one is active, and
    /// replacing it is a configuration change plus one implementation.
    /// </summary>
    public class SimulatedMarketDataSource(MarketDataOptions options, TimeProvider timeProvider) : IMarketDataSource
    {
        private const uint SeedFallback = 0x9E3779B9u;

        private int _cycle;

        public Task<MarketDataCapture> CaptureAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
        {
            SimulatedMarketDataOptions settings = options.Simulated;

            if (options.Provider != MarketDataProviderKind.Simulated || settings == null)
            {
                return Task.FromResult(MarketDataCapture.Unavailable());
            }

            int cycle = Interlocked.Increment(ref _cycle);
            List<SymbolCapture> captures = new List<SymbolCapture>();

            if (symbols != null)
            {
                foreach (SymbolRequest request in symbols)
                {
                    captures.Add(CaptureSymbol(settings, request, cycle, timeProvider.GetUtcNow().UtcDateTime));
                }
            }

            return Task.FromResult(new MarketDataCapture
            {
                IsAvailable = true,
                CapturedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Account = new AccountCapture
                {
                    Equity = settings.Equity,
                    Balance = settings.Balance > 0 ? settings.Balance : settings.Equity,
                    RealizedPnlToday = settings.RealizedPnlToday,
                    UnrealizedPnl = settings.UnrealizedPnl,
                    Environment = TradingEnvironment.Demo,
                    Currency = settings.AccountCurrency
                },
                Symbols = captures
            });
        }

        /// <summary>
        /// Describes the instruments from the configured profiles. A symbol with neither an explicit profile nor
        /// a market fallback is absent from the answer, which is how the source reports "this instrument is not
        /// offered" without inventing a specification.
        /// </summary>
        public Task<List<SymbolSpecification>> DescribeAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
        {
            SimulatedMarketDataOptions settings = options.Simulated;
            List<SymbolSpecification> specifications = new List<SymbolSpecification>();

            if (options.Provider != MarketDataProviderKind.Simulated || settings == null || symbols == null)
            {
                return Task.FromResult(specifications);
            }

            foreach (SymbolRequest request in symbols)
            {
                SimulatedSymbolOptions profile = ResolveProfile(settings, request);

                if (profile == null)
                {
                    continue;
                }

                specifications.Add(new SymbolSpecification
                {
                    Symbol = request.Symbol,
                    Market = profile.Market,
                    MinVolume = profile.MinVolume,
                    StepVolume = profile.StepVolume,
                    MaxVolume = profile.MaxVolume,
                    LotSize = profile.LotSize,
                    PipSizePerUnit = profile.PipSizePerUnit,
                    ProfitCurrency = profile.ProfitCurrency,
                    IsTradable = profile.IsTradable
                });
            }

            return Task.FromResult(specifications);
        }

        private static SymbolCapture CaptureSymbol(SimulatedMarketDataOptions settings, SymbolRequest request, int cycle, DateTime now)
        {
            SimulatedSymbolOptions profile = ResolveProfile(settings, request);

            // A symbol without a profile and without a market fallback is not quoted at all: it stays absent
            // rather than being priced from a value nobody chose.
            if (profile == null)
            {
                return new SymbolCapture
                {
                    Symbol = request.Symbol,
                    Price = null,
                    SpreadPips = null,
                    VolatilityPercent = null,
                    IsTradable = false
                };
            }

            double factor = NextFactor(settings.Seed, request.Symbol, cycle, settings.JitterPercent);

            return new SymbolCapture
            {
                Symbol = request.Symbol,
                Price = Math.Round(profile.BasePrice * factor, 5),
                SpreadPips = Math.Round(profile.SpreadPips * factor, 3),
                VolatilityPercent = Math.Round(profile.VolatilityPercent * factor, 3),
                IsTradable = profile.IsTradable,
                Bars = BuildBars(profile, request, cycle, now)
            };
        }

        private static List<MarketBar> BuildBars(SimulatedSymbolOptions profile, SymbolRequest request, int cycle, DateTime now)
        {
            List<MarketBar> bars = new List<MarketBar>();
            double trend = ((StableHash(request.Symbol) % 7) - 3) * 0.0002;
            double previous = profile.BasePrice * (1 + trend * cycle / 100);

            for (int index = 96; index >= 1; index--)
            {
                double open = previous;
                double close = open + trend + Math.Sin(index + cycle) * profile.BasePrice * 0.00005;
                double high = Math.Max(open, close) + profile.BasePrice * 0.0001;
                double low = Math.Min(open, close) - profile.BasePrice * 0.0001;
                bars.Add(new MarketBar
                {
                    TimeFrame = TimeFrame.M15,
                    ClosedAtUtc = now.AddMinutes(-15 * index),
                    Open = open, High = high, Low = low, Close = close, Volume = 1000 + index
                });
                previous = close;
            }

            return bars;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                return Math.Abs(hash);
            }
        }

        private static SimulatedSymbolOptions ResolveProfile(SimulatedMarketDataOptions settings, SymbolRequest request)
        {
            if (settings.Symbols != null && !string.IsNullOrWhiteSpace(request.Symbol) && settings.Symbols.TryGetValue(request.Symbol, out SimulatedSymbolOptions explicitProfile))
            {
                return explicitProfile;
            }

            return null;
        }

        /// <summary>
        /// Variation of one symbol in one cycle. It is a seeded mixer instead of <see cref="Random"/> so the
        /// same seed always yields the same sequence, independently of the runtime version, and so two
        /// consecutive cycles differ instead of moving together.
        /// </summary>
        private static double NextFactor(int seed, string symbol, int cycle, double jitterPercent)
        {
            if (jitterPercent <= 0)
            {
                return 1;
            }

            uint state = unchecked((uint)seed);

            if (!string.IsNullOrEmpty(symbol))
            {
                foreach (char character in symbol.ToUpperInvariant())
                {
                    state = unchecked((state * 16777619u) ^ character);
                }
            }

            state = unchecked(state + ((uint)cycle * 2654435761u));

            // splitmix32 finalizer: the cycle has to reach the high bits, because those are the ones the
            // fraction uses.
            state ^= state >> 16;
            state = unchecked(state * 0x7FEB352Du);
            state ^= state >> 15;
            state = unchecked(state * 0x846CA68Bu);
            state ^= state >> 16;

            if (state == 0)
            {
                state = SeedFallback;
            }

            double unit = state / (double)uint.MaxValue;
            double offset = (unit * 2) - 1;

            return 1 + (offset * jitterPercent / 100);
        }
    }
}
