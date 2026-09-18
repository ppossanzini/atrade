using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Read-only rule: the broker application credentials must be configured before any authorization
  /// can start. Returns false when the broker is not configured at all.
  /// </summary>
  public class ValidateBrokerClientConfiguration : IRequest<bool>
  {
  }
}
