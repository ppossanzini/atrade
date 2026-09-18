using System;
using AutoTrade.Trading.Core.Configuration;
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

      BrokerOptions options = new BrokerOptions
      {
        ClientId = configuration[BrokerConfigurationKeys.ClientId],
        ClientSecret = configuration[BrokerConfigurationKeys.ClientSecret],
        TokenKey = configuration[BrokerConfigurationKeys.TokenKey],
        RedirectUri = configuration[BrokerConfigurationKeys.RedirectUri],
        ReturnUri = configuration[BrokerConfigurationKeys.ReturnUri],
        Environment = ParseEnvironment(configuration[BrokerConfigurationKeys.Environment]),
        Scope = ValueOrDefault(configuration[BrokerConfigurationKeys.Scope], BrokerOptions.AccountsScope),
        AuthorizationEndpoint = ValueOrDefault(configuration[BrokerConfigurationKeys.AuthorizationEndpoint], BrokerOptions.DefaultAuthorizationEndpoint),
        TokenEndpoint = ValueOrDefault(configuration[BrokerConfigurationKeys.TokenEndpoint], BrokerOptions.DefaultTokenEndpoint),
        AllowManualTokenImport = ParseBool(configuration[BrokerConfigurationKeys.AllowManualTokenImport]),
        AllowDiagnostics = ParseBool(configuration[BrokerConfigurationKeys.AllowDiagnostics])
      };

      int lifetimeMinutes;
      if (int.TryParse(configuration[BrokerConfigurationKeys.CorrelationLifetimeMinutes], out lifetimeMinutes) && lifetimeMinutes > 0)
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

    /// <summary>Anything other than an explicit true leaves the affordance disabled.</summary>
    private static bool ParseBool(string value)
    {
      bool parsed;

      return !string.IsNullOrWhiteSpace(value) && bool.TryParse(value, out parsed) && parsed;
    }
  }
}
