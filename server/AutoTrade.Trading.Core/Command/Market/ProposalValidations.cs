using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Market
{
    /// <summary>
    /// Validation type: the requested mode must be a declared mode.
    /// </summary>
    public class ValidateMarketManagerMode : IRequest<bool>
    {
        public string Mode { get; set; }
    }

    /// <summary>
    /// Validation type: an analysis state change must be a real change and must come from an operator.
    /// </summary>
    public class ValidateAnalysisStateChange : IRequest<bool>
    {
        public bool IsRunning { get; set; }

        public bool CurrentIsRunning { get; set; }

        public Guid OperatorId { get; set; }
    }
}
