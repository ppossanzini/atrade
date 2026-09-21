using System;

namespace AutoTrade.Trading.Core.Dto
{
    public class SessionDto
    {
        public Guid SessionId { get; set; }
        public Guid OperatorId { get; set; }
        public string UserName { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
