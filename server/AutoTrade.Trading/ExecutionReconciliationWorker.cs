using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Handlers.Execution;
using Hikyaku;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading
{
  /// <summary>
  /// Periodically dispatches the reconciliation command in a fresh scope. Registration and the interval are
  /// intentionally owned by the composition root so deployments can keep the worker disabled by default.
  /// </summary>
  public sealed class ExecutionReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    ExecutionReconciliationOptions options,
    ILogger<ExecutionReconciliationWorker> logger) : BackgroundService
  {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      if (!options.IsEnabled || options.IntervalSeconds <= 0)
      {
        logger.LogInformation("Execution reconciliation worker is disabled.");
        return;
      }

      using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          using IServiceScope scope = scopeFactory.CreateScope();
          IHikyaku hikyaku = scope.ServiceProvider.GetRequiredService<IHikyaku>();
          await hikyaku.Send(new ReconcileExecutions(), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          break;
        }
        catch (Exception error)
        {
          // An unavailable broker must keep the account fail-closed while the next tick retries the read.
          logger.LogError(error, "Execution reconciliation failed; no order was retried.");
        }

        try
        {
          await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
      }
    }
  }
}
