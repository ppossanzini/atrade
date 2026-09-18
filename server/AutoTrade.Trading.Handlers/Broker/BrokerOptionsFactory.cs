using System;
using AutoTrade.Trading.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Broker
{
  /// <summary>
  /// Maps the broker configuration section onto <see cref="BrokerOptions"/>. The mapping is explicit
  /// so defaults and enum parsing stay visible, and so the tier keeps its current configuration style.
  /// </summary>
  public static class BrokerOptionsFactory
  {
    public static BrokerOptions FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      IConfigurationSection section = configuration.GetSection(BrokerOptions.SectionName);

      BrokerOptions options = new BrokerOptions
      {
        ClientId = section["ClientId"],
        ClientSecret = section["ClientSecret"],
        TokenKey = section["TokenKey"],
        RedirectUri = section["RedirectUri"],
        Environment = ParseEnvironment(section["Environment"]),
        Scope = ValueOrDefault(section["Scope"], BrokerOptions.AccountsScope),
        AuthorizationEndpoint = ValueOrDefault(section["AuthorizationEndpoint"], BrokerOptions.DefaultAuthorizationEndpoint),
        TokenEndpoint = ValueOrDefault(section["TokenEndpoint"], BrokerOptions.DefaultTokenEndpoint)
      };

      int lifetimeMinutes;
      if (int.TryParse(section["CorrelationLifetimeMinutes"], out lifetimeMinutes) && lifetimeMinutes > 0)
      {
        options.CorrelationLifetimeMinutes = lifetimeMinutes;
      }

      return options;
    }

    /// <summary>
    /// The default environment is Demo, so a missing or unparsable value can never silently point the
    /// application at a live account.
    /// </summary>
    private static TradingEnvironment ParseEnvironment(string value)
    {
      TradingEnvironment parsed;

      if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out parsed))
      {
        return parsed;
      }

      return TradingEnvironment.Demo;
    }

    private static string ValueOrDefault(string value, string fallback)
    {
      return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
  }
}
