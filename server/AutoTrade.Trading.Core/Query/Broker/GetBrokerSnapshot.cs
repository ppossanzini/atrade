using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Broker
{
  /// <summary>Reads the latest provider account, instrument and reconciliation snapshot.</summary>
  public class GetBrokerSnapshot : IRequest<BrokerSnapshotDto>
  {
  }
}
