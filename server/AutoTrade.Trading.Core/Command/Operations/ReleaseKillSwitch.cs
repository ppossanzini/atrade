using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Operations
{
    public class ReleaseKillSwitch : IRequest<KillSwitchChangeResult>
    {
        public Guid OperatorId { get; set; }
    }
}
