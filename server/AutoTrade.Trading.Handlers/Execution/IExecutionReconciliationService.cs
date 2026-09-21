using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;

namespace AutoTrade.Trading.Handlers.Execution
{
    public interface IExecutionReconciliationService
    {
        Task<ExecutionReconciliationResultDto> ReconcileAsync(CancellationToken cancellationToken);
    }
}
