namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Why a decision was refused or applied. Refusals are as important as applications: the operator must
    /// learn whether a proposal was no longer decidable, expired, or had become riskier since generation.
    /// </summary>
    public enum ProposalDecisionOutcome
    {
        Applied = 0,

        NotFound = 1,

        /// <summary>The status or the current mode does not admit a decision.</summary>
        NotDecidable = 2,

        /// <summary>The validity window elapsed; the proposal is expired and never forwarded.</summary>
        Expired = 3,

        /// <summary>The gate was re-evaluated and is no longer an explicit allow.</summary>
        GateRegressed = 4,

        /// <summary>A decision was already recorded on this proposal.</summary>
        AlreadyDecided = 5
    }
}
