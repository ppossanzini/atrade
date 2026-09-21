using System;
using AutoTrade.Trading.Handlers.Broker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// Market data tier registration. It is called by the handlers module so the composition root keeps a
  /// single registration entrypoint per tier.
  /// </summary>
  public static class MarketDataModule
  {
    public static IServiceCollection AddTradingMarketData(this IServiceCollection services, IConfiguration configuration)
    {
      MarketDataOptions options = MarketDataOptionsFactory.FromConfiguration(configuration);
      services.AddSingleton(options);

      switch (options.Provider)
      {
        case MarketDataProviderKind.Ctrader:
          services.AddScoped<IMarketDataSource, CtraderMarketDataSource>();
          break;

        case MarketDataProviderKind.Simulated:
          services.AddSingleton<IMarketDataSource, SimulatedMarketDataSource>();
          break;
        default:
          // None is the supported default, and Ctrader is rejected by the guard before the host starts.
          services.AddSingleton<IMarketDataSource, UnavailableMarketDataSource>();
          break;
      }

      return services;
    }

    /// <summary>
    /// Cross-checks the selected source against the broker configuration. Fail-closed: a source that
    /// cannot work aborts startup instead of leaving an application that believes it has a feed.
    /// </summary>
    public static void EnsureSourceIsUsable(MarketDataOptions options, BrokerOptions brokerOptions)
    {
      if (options == null)
      {
        throw new ArgumentNullException(nameof(options));
      }

      if (options.Provider == MarketDataProviderKind.Ctrader)
      {
        if (brokerOptions == null || !brokerOptions.IsClientConfigured)
        {
          throw new InvalidOperationException("Trading:MarketData:Provider is Ctrader, but Trading:Broker:ClientId and Trading:Broker:ClientSecret are not configured.");
        }
      }
    }
  }
}
