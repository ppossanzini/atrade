using System;

namespace AutoTrade.Trading.Core.Dto
{
    public class KillSwitchStatusDto
    {
        public bool IsEngaged { get; set; }
        public DateTime? ChangedAtUtc { get; set; }
        public Guid? ChangedByOperatorId { get; set; }
        public string Reason { get; set; }
    }
}
