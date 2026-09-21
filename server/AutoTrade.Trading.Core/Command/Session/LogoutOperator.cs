using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Session
{
    public class LogoutOperator : IRequest<Unit>
    {
        public Guid OperatorId { get; set; }
        public string SessionToken { get; set; }
    }
}
