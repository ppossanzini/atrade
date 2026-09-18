using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Read-only rule: an authorization callback must carry both the code and a correlator issued by
  /// this server. Validating the pair here keeps the rule out of the command handler.
  /// </summary>
  public class ValidateBrokerAuthorizationCallback : IRequest<bool>
  {
    public string Code { get; set; }
    public string CorrelationId { get; set; }
  }
}
