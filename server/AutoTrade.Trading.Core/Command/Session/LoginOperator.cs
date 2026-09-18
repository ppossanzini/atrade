using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Session
{
  public class LoginOperator : IRequest<LoginOperatorResult>
  {
    public string UserName { get; set; }
    public string Password { get; set; }
  }
}
