using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Broker
{
  /// <summary>
  /// Broker application settings. The client secret and the token key are secrets: they are read from
  /// configuration (environment variables, secret store or the untracked local file) and are never
  /// logged. The endpoints are configurable so tests can point them at a local server.
  /// </summary>
  public class BrokerOptions
  {
    public const string SectionName = "Trading:Broker";

    /// <summary>Consent page from the official documentation.</summary>
    public const string DefaultAuthorizationEndpoint = "https://id.ctrader.com/my/settings/openapi/grantingaccess/";

    /// <summary>Token endpoint from the official documentation.</summary>
    public const string DefaultTokenEndpoint = "https://openapi.ctrader.com/apps/token";

    /// <summary>Read-only scope: account and statistics access without trading operations.</summary>
    public const string AccountsScope = "accounts";

    /// <summary>Full scope, required only when execution is in play.</summary>
    public const string TradingScope = "trading";

    public string ClientId { get; set; }

    public string ClientSecret { get; set; }

    public string TokenKey { get; set; }

    public string RedirectUri { get; set; }

    public TradingEnvironment Environment { get; set; }

    public string Scope { get; set; } = AccountsScope;

    public string AuthorizationEndpoint { get; set; } = DefaultAuthorizationEndpoint;

    public string TokenEndpoint { get; set; } = DefaultTokenEndpoint;

    /// <summary>A provider authorization code expires in one minute, so the correlator is short lived too.</summary>
    public int CorrelationLifetimeMinutes { get; set; } = 10;

    public bool IsClientConfigured
    {
      get { return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret); }
    }

    public bool HasTokenKey
    {
      get { return !string.IsNullOrWhiteSpace(TokenKey); }
    }
  }
}
