namespace AutoTrade.Trading.Handlers.Execution
{
    /// <summary>Composition-root supplied scheduling options for the reconciliation worker.</summary>
    public sealed class ExecutionReconciliationOptions
    {
        public bool IsEnabled { get; set; }

        public int IntervalSeconds { get; set; }
    }
}
