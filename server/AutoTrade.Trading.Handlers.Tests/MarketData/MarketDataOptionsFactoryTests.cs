using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.MarketData;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.MarketData
{
  /// <summary>
  /// The source is chosen by configuration and a wrong value must abort startup instead of degrading to
  /// "no data": a typo in a deployment cannot be allowed to silence the feed without anybody noticing.
  /// </summary>
  public class MarketDataOptionsFactoryTests
  {
    private static MarketDataOptions FromSettings(Dictionary<string, string> settings)
    {
      IConfiguration configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(settings)
        .Build();

      return MarketDataOptionsFactory.FromConfiguration(configuration);
    }

    [Fact]
    public void FromConfiguration_WithoutTheSection_UsesNoSource()
    {
      MarketDataOptions options = FromSettings(new Dictionary<string, string>());

      Assert.Equal(MarketDataProviderKind.None, options.Provider);
    }

    [Fact]
    public void FromConfiguration_ReadsTheProviderCaseInsensitively()
    {
      MarketDataOptions options = FromSettings(new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "simulated" }
      });

      Assert.Equal(MarketDataProviderKind.Simulated, options.Provider);
    }

    [Fact]
    public void FromConfiguration_WithAnUnknownProvider_IsRejected()
    {
      ArgumentNullException missingConfiguration = Assert.Throws<ArgumentNullException>(() => MarketDataOptionsFactory.FromConfiguration(null));
      InvalidOperationException unknownProvider = Assert.Throws<InvalidOperationException>(() => FromSettings(new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "Mock" }
      }));

      Assert.Equal("configuration", missingConfiguration.ParamName);
      Assert.Contains("Mock", unknownProvider.Message);
    }

    [Fact]
    public void FromConfiguration_ReadsSymbolProfilesAndMarketDefaults()
    {
      MarketDataOptions options = FromSettings(new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "Simulated" },
        { "Trading:MarketData:Simulated:Seed", "42" },
        { "Trading:MarketData:Simulated:JitterPercent", "5" },
        { "Trading:MarketData:Simulated:Equity", "10000" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:Market", "Fx" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:BasePrice", "1.085" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:SpreadPips", "0.8" },
        { "Trading:MarketData:Simulated:Symbols:EURUSD:VolatilityPercent", "0.22" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:Market", "Metal" },
        { "Trading:MarketData:Simulated:Symbols:XAUUSD:IsTradable", "false" },
        { "Trading:MarketData:Simulated:DefaultByMarket:Index:BasePrice", "5000" }
      });

      Assert.Equal(42, options.Simulated.Seed);
      Assert.Equal(5, options.Simulated.JitterPercent);
      Assert.Equal(10000, options.Simulated.Equity);
      Assert.Equal(1.085, options.Simulated.Symbols["EURUSD"].BasePrice);
      Assert.True(options.Simulated.Symbols["EURUSD"].IsTradable);
      Assert.False(options.Simulated.Symbols["XAUUSD"].IsTradable);
      Assert.Equal(5000, options.Simulated.DefaultByMarket[MarketKind.Index].BasePrice);
    }

    [Fact]
    public void FromConfiguration_WithAnUnknownMarketDefault_IsRejected()
    {
      InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => FromSettings(new Dictionary<string, string>
      {
        { "Trading:MarketData:Provider", "Simulated" },
        { "Trading:MarketData:Simulated:DefaultByMarket:Crypto:BasePrice", "100" }
      }));

      Assert.Contains("Crypto", error.Message);
    }

    [Fact]
    public void EnsureSourceIsUsable_RejectsTheBrokerSourceUntilItExists()
    {
      MarketDataOptions options = new MarketDataOptions { Provider = MarketDataProviderKind.Ctrader };

      Assert.Throws<InvalidOperationException>(() => MarketDataModule.EnsureSourceIsUsable(options, new BrokerOptions()));
    }

    [Fact]
    public void EnsureSourceIsUsable_AcceptsNoneAndSimulated()
    {
      MarketDataModule.EnsureSourceIsUsable(new MarketDataOptions { Provider = MarketDataProviderKind.None }, new BrokerOptions());
      MarketDataModule.EnsureSourceIsUsable(new MarketDataOptions { Provider = MarketDataProviderKind.Simulated }, new BrokerOptions());
    }
  }
}
