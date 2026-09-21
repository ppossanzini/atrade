namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Mode change request. The mode arrives as text so an unknown value is refused by the endpoint instead of
    /// being bound to a default member of the enumeration.
    /// </summary>
    public class MarketManagerModeChangeDto
    {
        public string Mode { get; set; }
    }

    /// <summary>Start or stop request for the continuous analysis.</summary>
    public class AnalysisStateChangeDto
    {
        public bool IsRunning { get; set; }
    }

    /// <summary>
    /// Operator decision payload. The reason is mandatory for refusals and suspensions, and the endpoint checks
    /// it before the command is dispatched, so a decision without a reason never reaches the handlers.
    /// </summary>
    public class ProposalDecisionDto
    {
        public string Reason { get; set; }
    }
}

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Compensation request. The reason is mandatory: closing real exposure without saying why is not auditable.
    /// </summary>
    public class CompensationRequestDto
    {
        public string Reason { get; set; }
    }
}
