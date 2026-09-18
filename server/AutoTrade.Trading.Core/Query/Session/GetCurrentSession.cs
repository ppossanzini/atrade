using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Session
{
  public class GetCurrentSession : IRequest<SessionDto>
  {
    public Guid OperatorId { get; set; }
    public string SessionToken { get; set; }
  }
}
