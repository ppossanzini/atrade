using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Operations
{
  public class EngageKillSwitch : IRequest<KillSwitchChangeResult>
  {
    public Guid OperatorId { get; set; }
    public string Reason { get; set; }
  }
}
