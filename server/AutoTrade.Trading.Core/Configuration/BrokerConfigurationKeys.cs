namespace AutoTrade.Trading.Core.Configuration
{
  /// <summary>
  /// Configuration keys of the broker section. They live in the contracts project so every tier reads
  /// the same literal: the presentation tier needs the return URI and must not duplicate the key.
  /// </summary>
  public static class BrokerConfigurationKeys
  {
    public const string Section = "Trading:Broker";

    public const string ClientId = "Trading:Broker:ClientId";

    public const string ClientSecret = "Trading:Broker:ClientSecret";

    public const string TokenKey = "Trading:Broker:TokenKey";

    public const string RedirectUri = "Trading:Broker:RedirectUri";

    /// <summary>Client route the provider callback sends the browser to once the exchange is done.</summary>
    public const string ReturnUri = "Trading:Broker:ReturnUri";

    public const string Environment = "Trading:Broker:Environment";

    public const string Scope = "Trading:Broker:Scope";

    public const string AuthorizationEndpoint = "Trading:Broker:AuthorizationEndpoint";

    public const string TokenEndpoint = "Trading:Broker:TokenEndpoint";

    public const string CorrelationLifetimeMinutes = "Trading:Broker:CorrelationLifetimeMinutes";

    /// <summary>Development affordance: enables the Playground token import.</summary>
    public const string AllowManualTokenImport = "Trading:Broker:AllowManualTokenImport";

    /// <summary>Diagnostic affordance: enables the connectivity probe.</summary>
    public const string AllowDiagnostics = "Trading:Broker:AllowDiagnostics";
  }
}
