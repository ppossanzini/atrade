using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Execution
{
    /// <summary>
    /// Reads every execution with an unknown broker outcome and applies only the provider state that can be
    /// correlated to its persisted client order id. It never sends an order.
    /// </summary>
    public class ReconcileExecutions : IRequest<ExecutionReconciliationResultDto>
    {
    }
}
