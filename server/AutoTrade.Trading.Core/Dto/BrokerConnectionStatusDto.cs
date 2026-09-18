using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Broker connectivity status. No secret is ever part of this contract: it reports only whether
  /// the application is configured and whether a stored authorization is still usable.
  /// </summary>
  public class BrokerConnectionStatusDto
  {
    public bool IsClientConfigured { get; set; }
    public TradingEnvironment Environment { get; set; }
    public string RedirectUri { get; set; }
    public bool IsAuthorized { get; set; }
    public DateTime? AuthorizedAtUtc { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public bool IsAccessTokenExpired { get; set; }
    public long? CtidTraderAccountId { get; set; }
    public string LastError { get; set; }
  }
}
