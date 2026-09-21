using System;
using System.Collections.Generic;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
    /// <summary>
    /// Opens a real connection to the broker endpoint to verify transport, application authentication and,
    /// when a stored grant exists, the account list. It is a diagnostic: it never writes state and never
    /// returns a credential.
    /// </summary>
    public class ProbeBrokerConnection : IRequest<AutoTrade.Trading.Core.Dto.BrokerProbeResultDto>
    {
        public Guid OperatorId { get; set; }
    }
}
