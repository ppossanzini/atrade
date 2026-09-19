using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.MarketData
{
  /// <summary>
  /// The simulated source stands in for the broker while the integration is blocked, so the properties it
  /// has to guarantee are the ones the rest of the application relies on: a coherent capture, values that
  /// stay inside the configured profile, and reproducibility from the seed.
  /// </summary>
  public class SimulatedMarketDataSourceTests
  {
    private static MarketDataOptions Options(int seed, double jitterPercent, bool withProfiles = true)
    {
      SimulatedMarketDataOptions simulated = new SimulatedMarketDataOptions
      {
        Seed = seed,
        JitterPercent = jitterPercent,
        Equity = 10000,
        Balance = 10000,
        RealizedPnlToday = 0,
        UnrealizedPnl = 0,
        Symbols = new Dictionary<string, SimulatedSymbolOptions>(StringComparer.OrdinalIgnoreCase),
        DefaultByMarket = new Dictionary<MarketKind, SimulatedSymbolOptions>()
      };

      if (withProfiles)
      {
        simulated.Symbols["EURUSD"] = new SimulatedSymbolOptions
        {
          Market = MarketKind.Fx,
          BasePrice = 1.085,
          SpreadPips = 0.8,
          VolatilityPercent = 0.22,
          IsTradable = true
        };

        simulated.DefaultByMarket[MarketKind.Metal] = new SimulatedSymbolOptions
        {
          Market = MarketKind.Metal,
          BasePrice = 2650,
          SpreadPips = 28,
          VolatilityPercent = 0.55,
          IsTradable = true,
          IsMarketDefault = true
        };
      }

      return new MarketDataOptions
      {
        Provider = MarketDataProviderKind.Simulated,
        Simulated = simulated
      };
    }

    private static List<SymbolRequest> FxAndMetal()
    {
      return new List<SymbolRequest>
      {
        new SymbolRequest { Symbol = "EURUSD", Market = MarketKind.Fx },
        new SymbolRequest { Symbol = "XAUUSD", Market = MarketKind.Metal }
      };
    }

    private static async Task<MarketDataCapture> CaptureAsync(MarketDataOptions options, IReadOnlyList<SymbolRequest> symbols, FakeTimeProvider clock = null)
    {
      FakeTimeProvider timeProvider = clock ?? new FakeTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
      SimulatedMarketDataSource source = new SimulatedMarketDataSource(options, timeProvider);

      return await source.CaptureAsync(symbols, CancellationToken.None);
    }

    [Fact]
    public async Task CaptureAsync_WithoutSimulatedProvider_IsUnavailable()
    {
      MarketDataOptions options = Options(11, 5);
      options.Provider = MarketDataProviderKind.None;

      MarketDataCapture capture = await CaptureAsync(options, FxAndMetal());

      Assert.False(capture.IsAvailable);
      Assert.Null(capture.CapturedAtUtc);
      Assert.Null(capture.Account);
      Assert.Empty(capture.Symbols);
    }

    [Fact]
    public async Task CaptureAsync_ReportsTheInstantAndTheAccountOfTheSameCapture()
    {
      FakeTimeProvider clock = new FakeTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

      MarketDataCapture capture = await CaptureAsync(Options(11, 0), FxAndMetal(), clock);

      Assert.True(capture.IsAvailable);
      Assert.Equal(clock.GetUtcNow().UtcDateTime, capture.CapturedAtUtc);
      Assert.Equal(10000, capture.Account.Equity);
      Assert.Equal(TradingEnvironment.Demo, capture.Account.Environment);
      Assert.Equal(2, capture.Symbols.Count);
    }

    [Fact]
    public async Task CaptureAsync_OverridesTheMarketDefaultWithTheExplicitProfile()
    {
      MarketDataCapture capture = await CaptureAsync(Options(11, 0), FxAndMetal());

      SymbolCapture eurUsd = capture.Symbols[0];
      SymbolCapture gold = capture.Symbols[1];

      Assert.Equal(1.085, eurUsd.Price);
      Assert.Equal(0.8, eurUsd.SpreadPips);
      Assert.Equal(2650, gold.Price);
      Assert.Equal(28, gold.SpreadPips);
    }

    [Fact]
    public async Task CaptureAsync_WithoutAnyProfile_LeavesTheSymbolUnquoted()
    {
      MarketDataCapture capture = await CaptureAsync(Options(11, 0), new List<SymbolRequest>
      {
        new SymbolRequest { Symbol = "BTCUSD", Market = MarketKind.Fx }
      }, null);

      // FX is not configured as a market default in this fixture, so nothing is quoted.
      MarketDataCapture furtherCapture = capture;

      Assert.Single(furtherCapture.Symbols);
      Assert.Null(furtherCapture.Symbols[0].SpreadPips);
      Assert.Null(furtherCapture.Symbols[0].VolatilityPercent);
      Assert.False(furtherCapture.Symbols[0].IsTradable);
    }

    [Fact]
    public async Task CaptureAsync_WithTheSameSeed_IsReproducible()
    {
      MarketDataCapture first = await CaptureAsync(Options(20260919, 6), FxAndMetal());
      MarketDataCapture second = await CaptureAsync(Options(20260919, 6), FxAndMetal());

      Assert.Equal(first.Symbols[0].SpreadPips, second.Symbols[0].SpreadPips);
      Assert.Equal(first.Symbols[1].VolatilityPercent, second.Symbols[1].VolatilityPercent);
    }

    [Fact]
    public async Task CaptureAsync_WithZeroJitter_ReturnsTheConfiguredValues()
    {
      MarketDataCapture capture = await CaptureAsync(Options(7, 0), FxAndMetal());

      Assert.Equal(1.085, capture.Symbols[0].Price);
      Assert.Equal(0.22, capture.Symbols[0].VolatilityPercent);
    }

    [Fact]
    public async Task CaptureAsync_KeepsEveryValueInsideTheConfiguredAmplitude()
    {
      SimulatedMarketDataSource source = new SimulatedMarketDataSource(Options(99, 6), new FakeTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

      for (int cycle = 0; cycle < 50; cycle++)
      {
        MarketDataCapture capture = await source.CaptureAsync(FxAndMetal(), CancellationToken.None);
        SymbolCapture eurUsd = capture.Symbols[0];

        Assert.InRange(eurUsd.SpreadPips.Value, 0.8 * 0.94, 0.8 * 1.06);
        Assert.InRange(eurUsd.VolatilityPercent.Value, 0.22 * 0.94, 0.22 * 1.06);
      }
    }

    [Fact]
    public async Task CaptureAsync_AcrossCycles_VariesTheValues()
    {
      SimulatedMarketDataSource source = new SimulatedMarketDataSource(Options(5, 6), new FakeTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

      MarketDataCapture first = await source.CaptureAsync(FxAndMetal(), CancellationToken.None);
      MarketDataCapture second = await source.CaptureAsync(FxAndMetal(), CancellationToken.None);

      Assert.NotEqual(first.Symbols[0].SpreadPips, second.Symbols[0].SpreadPips);
    }
  }
}
