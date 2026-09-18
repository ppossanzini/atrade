using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Exchanges the provider authorization code for tokens. The correlator is single use: a repeated
  /// callback is rejected instead of replayed.
  /// </summary>
  public class CompleteBrokerAuthorization : IRequest<BrokerAuthorizationResultDto>
  {
    public string Code { get; set; }
    public string CorrelationId { get; set; }
    public Guid OperatorId { get; set; }
  }
}
