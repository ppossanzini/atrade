using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Discards the locally stored authorization. The provider keeps its own grant, so this is a local
  /// forget and must not be presented as a server-side revocation.
  /// </summary>
  public class RevokeBrokerAuthorization : IRequest<BrokerAuthorizationResultDto>
  {
    public Guid OperatorId { get; set; }
  }
}
