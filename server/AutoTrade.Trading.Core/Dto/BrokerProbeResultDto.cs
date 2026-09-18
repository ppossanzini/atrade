using System.Collections.Generic;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Result of a connectivity probe. Tokens are never part of this contract: it reports which step
  /// succeeded and, when one failed, the provider error code and description.
  /// </summary>
  public class BrokerProbeResultDto
  {
    public bool IsAuthenticated { get; set; }

    public string ErrorCode { get; set; }

    public string Description { get; set; }

    public bool HasStoredGrant { get; set; }

    public bool HasAccountList { get; set; }

    public int AccountCount { get; set; }

    public List<long> CtidTraderAccountIds { get; set; }
  }
}
