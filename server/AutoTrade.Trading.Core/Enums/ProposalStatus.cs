namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Lifecycle of a proposal. Routing decides the state at generation: a gate that does not allow is
    /// never waiting for a decision, and a decision never turns a blocked proposal into an executable one.
    /// </summary>
    public enum ProposalStatus
    {
        /// <summary>Waiting for the operator, and decidable only in a mode that asks the operator.</summary>
        NeedsReview = 0,

        /// <summary>Routed forward automatically by the mode; never a permission to bypass the gate.</summary>
        AutoApproved = 1,

        /// <summary>A gate blocked it. Terminal for routing purposes and never decidable.</summary>
        Blocked = 2,

        Approved = 3,

        Rejected = 4,

        Suspended = 5,

        /// <summary>The validity window elapsed before a decision.</summary>
        Expired = 6
    }
}
