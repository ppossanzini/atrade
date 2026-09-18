using System;
using System.Net.Http;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers
{
  /// <summary>
  /// Broker tier registration. It is called by the handlers module so the composition root keeps a
  /// single registration entrypoint per tier.
  /// </summary>
  public static class BrokerModule
  {
    private const int BrokerHttpTimeoutSeconds = 30;

    public static IServiceCollection AddTradingBroker(this IServiceCollection services, IConfiguration configuration)
    {
      BrokerOptions options = BrokerOptionsFactory.FromConfiguration(configuration);
      services.AddSingleton(options);

      // A configured broker without a usable key must not be able to reach the token store, and an
      // unconfigured broker must not break resolution of unrelated requests. The guard turns the first
      // case into a startup failure and this registration turns the second into an explicit error on use.
      byte[] tokenKey;

      if (BrokerTokenProtector.TryCreateKey(options.TokenKey, out tokenKey))
      {
        services.AddSingleton<IBrokerTokenProtector>(new BrokerTokenProtector(tokenKey));
      }
      else
      {
        services.AddSingleton<IBrokerTokenProtector, UnconfiguredBrokerTokenProtector>();
      }

      // A single long lived client serves the fixed provider endpoint, so there is no per request socket
      // churn and no handler rotation to manage.
      services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(BrokerHttpTimeoutSeconds) });
      services.AddSingleton<IBrokerTokenClient, BrokerTokenClient>();
      services.AddScoped<IBrokerAuthorizationCorrelator, BrokerAuthorizationCorrelator>();

      return services;
    }
  }
}
