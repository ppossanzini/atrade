using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    public class KillSwitchChangeResult
    {
        public KillSwitchChangeOutcome Outcome { get; set; }
        public bool IsEngaged { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }
}
