using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class LoginOperatorResult
  {
    public LoginOutcome Outcome { get; set; }
    public Guid OperatorId { get; set; }
    public Guid SessionId { get; set; }
    public string SessionToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
  }
}
