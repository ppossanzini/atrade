using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Result of a completed or revoked authorization. Detail carries a provider error description,
  /// never a token.
  /// </summary>
  public class BrokerAuthorizationResultDto
  {
    public BrokerAuthorizationOutcome Outcome { get; set; }
    public long? CtidTraderAccountId { get; set; }
    public string Detail { get; set; }
  }
}
