using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Handlers.Execution;
using Hikyaku;

namespace AutoTrade.Trading.Handlers.CQRS.Execution
{
    public sealed class ExecutionReconciliationCommandHandler(IExecutionReconciliationService service)
      : IRequestHandler<ReconcileExecutions, ExecutionReconciliationResultDto>
    {
        public Task<ExecutionReconciliationResultDto> Handle(ReconcileExecutions request, CancellationToken cancellationToken)
        {
            return service.ReconcileAsync(cancellationToken);
        }
    }
}
