using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Market;
using AutoTrade.Trading.Handlers.Market;
using Hikyaku;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading
{
    /// <summary>
    /// Drives the analysis cycle. It owns no rule: it wakes up on the configured interval and asks the cycle
    /// command to run, so the worker, a test and any future scheduler execute exactly the same logic.
    ///
    /// Without the configured timing the worker stops immediately instead of choosing a pace of its own.
    /// </summary>
    public class AnalysisCycleService(IServiceScopeFactory scopeFactory, MarketOptions marketOptions, ILogger<AnalysisCycleService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!marketOptions.IsConfigured)
            {
                logger.LogWarning("The analysis cycle is not configured. Set Trading:Market:CycleSeconds and Trading:Market:ProposalTtlSeconds to enable it.");

                return;
            }

            TimeSpan interval = TimeSpan.FromSeconds(marketOptions.CycleSeconds);

            logger.LogInformation("Analysis cycle configured every {Seconds} seconds with a proposal lifetime of {TtlSeconds} seconds.", marketOptions.CycleSeconds, marketOptions.ProposalTtlSeconds);

            using PeriodicTimer timer = new PeriodicTimer(interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    IHikyaku hikyaku = scope.ServiceProvider.GetRequiredService<IHikyaku>();

                    await hikyaku.Send(new RunAnalysisCycle(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception error)
                {
                    // A failing cycle must not kill the worker: the next iteration re-reads the state and the journal
                    // keeps the technical failure out of the functional record.
                    logger.LogError(error, "An analysis cycle failed and will be retried on the next interval.");
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
