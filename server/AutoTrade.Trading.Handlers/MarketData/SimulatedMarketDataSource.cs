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
          captures.Add(CaptureSymbol(settings, request, cycle));
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
          Environment = TradingEnvironment.Demo
        },
        Symbols = captures
      });
    }

    private static SymbolCapture CaptureSymbol(SimulatedMarketDataOptions settings, SymbolRequest request, int cycle)
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
        IsTradable = profile.IsTradable
      };
    }

    private static SimulatedSymbolOptions ResolveProfile(SimulatedMarketDataOptions settings, SymbolRequest request)
    {
      if (settings.Symbols != null && !string.IsNullOrWhiteSpace(request.Symbol) && settings.Symbols.TryGetValue(request.Symbol, out SimulatedSymbolOptions explicitProfile))
      {
        return explicitProfile;
      }

      if (settings.DefaultByMarket != null && settings.DefaultByMarket.TryGetValue(request.Market, out SimulatedSymbolOptions marketProfile))
      {
        return marketProfile;
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
