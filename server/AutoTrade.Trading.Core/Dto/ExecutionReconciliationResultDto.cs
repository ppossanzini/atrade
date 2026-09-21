using System;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Technical acknowledgement of one reconciliation pass. It is deliberately a summary, not an execution
    /// resource: callers must query the execution queue/detail for the current state.
    /// </summary>
    public class ExecutionReconciliationResultDto
    {
        public bool IsClean { get; set; }

        public int ExecutionCount { get; set; }

        public int ResolvedExecutionCount { get; set; }

        public int UnresolvedExecutionCount { get; set; }

        public int AppliedEventCount { get; set; }

        public int DuplicateEventCount { get; set; }

        public DateTime? ReconciledAtUtc { get; set; }

        public string ErrorCode { get; set; }
    }
}
