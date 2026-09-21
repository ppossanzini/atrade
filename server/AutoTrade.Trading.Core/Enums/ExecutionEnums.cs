namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Lifecycle of one execution. The states are the ones the functional analysis fixed: an execution never
    /// returns to a previous state, and `ReconciliationRequired` is a visible state rather than a wait.
    /// </summary>
    public enum ExecutionStatus
    {
        Pending = 0,

        /// <summary>Legs are being prepared and sent, one at a time.</summary>
        Dispatching = 1,

        /// <summary>An order was sent and its outcome is not known yet.</summary>
        AwaitingBroker = 2,

        /// <summary>A timeout or a disconnection happened: nothing is sent again until the outcome is known.</summary>
        ReconciliationRequired = 3,

        /// <summary>A policy was violated and the operator must decide about the residual exposure.</summary>
        CompensationRequired = 4,

        Compensating = 5,

        CompletedNominal = 6,

        CompletedPartial = 7,

        /// <summary>A gate or a configuration refused to start it: nothing was sent.</summary>
        Blocked = 8,

        Failed = 9
    }

    /// <summary>
    /// Lifecycle of one leg. `TimedOut` is not a rejection: the order may exist at the broker, which is why
    /// the execution moves to reconciliation instead of treating the leg as failed.
    /// </summary>
    public enum ExecutionLegStatus
    {
        Pending = 0,

        Dispatched = 1,

        Accepted = 2,

        PartiallyFilled = 3,

        Filled = 4,

        Rejected = 5,

        TimedOut = 6,

        ReconciliationRequired = 7,

        Compensated = 8
    }

    /// <summary>
    /// Why a start or a compensation was refused or applied. Refusals carry the same weight as applications:
    /// the operator must know whether nothing was sent, or something was sent and needs reconciliation.
    /// </summary>
    public enum ExecutionOutcome
    {
        Applied = 0,

        NotFound = 1,

        /// <summary>The proposal is not approved, so no execution may be created from it.</summary>
        NotAuthorized = 2,

        /// <summary>The proposal already produced an execution: one proposal, one execution.</summary>
        AlreadyExecuted = 3,

        /// <summary>The allowed symbols, the minimum volume or the default volume are not configured.</summary>
        NotConfigured = 4,

        /// <summary>A gate blocked the start, for example the kill switch.</summary>
        Blocked = 5,

        /// <summary>The current state does not admit the operation.</summary>
        Conflict = 6
    }

    /// <summary>
    /// What the broker reported about one order. A timeout is deliberately **not** part of this vocabulary:
    /// it is the absence of an event, and recording it as a broker statement would be a lie.
    /// </summary>
    public enum ExecutionEventKind
    {
        Unknown = 0,

        OrderAccepted = 1,

        OrderRejected = 2,

        OrderPartiallyFilled = 3,

        OrderFilled = 4,

        OrderCancelled = 5
    }
}
