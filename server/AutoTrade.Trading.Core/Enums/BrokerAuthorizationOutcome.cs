namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Result of an authorization operation. The vocabulary mirrors the basket operations so the
  /// controller can map every outcome to a single HTTP status.
  /// </summary>
  public enum BrokerAuthorizationOutcome
  {
    Applied = 0,
    NotConfigured = 1,
    InvalidRequest = 2,
    InvalidCorrelation = 3,
    ProviderRejected = 4,
    Failed = 5
  }
}
