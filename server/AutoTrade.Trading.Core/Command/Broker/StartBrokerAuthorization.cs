using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Starts the OAuth consent flow. The handler issues a single-use correlator and returns the
  /// provider URL the operator must visit.
  /// </summary>
  public class StartBrokerAuthorization : IRequest<BrokerAuthorizationStartDto>
  {
    public Guid OperatorId { get; set; }
  }
}
