using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Exchanges the provider authorization code for tokens.
  ///
  /// The callback is a cross-site navigation started by the provider, so the session cookie is not
  /// guaranteed to be present and the acting operator is resolved from the pending correlator instead
  /// of from the request identity. The correlator is single use: a repeated callback is rejected
  /// rather than replayed.
  /// </summary>
  public class CompleteBrokerAuthorization : IRequest<BrokerAuthorizationResultDto>
  {
    public string Code { get; set; }

    /// <summary>
    /// Optional: when present it must match the pending attempt, otherwise the request is rejected.
    /// </summary>
    public string CorrelationId { get; set; }
  }
}
