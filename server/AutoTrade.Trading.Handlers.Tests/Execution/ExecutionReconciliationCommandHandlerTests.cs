using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Handlers.CQRS.Execution;
using AutoTrade.Trading.Handlers.Execution;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Execution
{
  public class ExecutionReconciliationCommandHandlerTests
  {
    [Fact]
    public async Task Handle_DelegatesToReconciliationService()
    {
      ExecutionReconciliationResultDto expected = new ExecutionReconciliationResultDto
      {
        IsClean = true,
        ExecutionCount = 2,
        ResolvedExecutionCount = 2
      };
      StubReconciliationService service = new StubReconciliationService(expected);
      ExecutionReconciliationCommandHandler handler = new ExecutionReconciliationCommandHandler(service);

      ExecutionReconciliationResultDto actual = await handler.Handle(new ReconcileExecutions(), CancellationToken.None);

      Assert.Same(expected, actual);
      Assert.True(service.WasCalled);
    }

    private sealed class StubReconciliationService(ExecutionReconciliationResultDto result) : IExecutionReconciliationService
    {
      public bool WasCalled { get; private set; }

      public Task<ExecutionReconciliationResultDto> ReconcileAsync(CancellationToken cancellationToken)
      {
        WasCalled = true;
        return Task.FromResult(result);
      }
    }
  }
}
