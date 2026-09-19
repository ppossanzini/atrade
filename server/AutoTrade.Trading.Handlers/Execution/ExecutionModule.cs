using System;
using AutoTrade.Trading.Handlers.MarketData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>
  /// Execution tier registration. It is called by the handlers module so the composition root keeps a single
  /// registration entrypoint per tier.
  /// </summary>
  public static class ExecutionModule
  {
    public static IServiceCollection AddTradingExecution(this IServiceCollection services, IConfiguration configuration)
    {
      ExecutionOptions options = ExecutionOptionsFactory.FromConfiguration(configuration);
      services.AddSingleton(options);

      if (options.Provider == ExecutionProviderKind.Simulated)
      {
        services.AddSingleton<IExecutionGateway, SimulatedExecutionGateway>();
      }
      else
      {
        services.AddSingleton<IExecutionGateway, UnconfiguredExecutionGateway>();
      }

      return services;
    }

    /// <summary>
    /// Fail-closed cross-checks of the selected provider. The broker gateway does not exist yet, so selecting
    /// it aborts startup instead of leaving an application that believes it can send orders.
    /// </summary>
    public static void EnsureProviderIsUsable(ExecutionOptions options)
    {
      if (options == null)
      {
        throw new ArgumentNullException(nameof(options));
      }

      if (options.Provider == ExecutionProviderKind.Ctrader)
      {
        throw new InvalidOperationException("Trading:Execution:Provider is Ctrader, but the broker gateway is not available yet. Set it to None or Simulated, or complete the broker integration first.");
      }

      if (options.Provider == ExecutionProviderKind.Simulated && options.Simulated == null)
      {
        throw new InvalidOperationException("Trading:Execution:Provider is Simulated but Trading:Execution:Simulated is missing, so no order behaviour is defined.");
      }
    }
  }

  /// <summary>
  /// The gateway in force when no provider is configured. StartExecution refuses before reaching it, so this
  /// exists only to keep resolution honest: if an order is ever attempted without a provider, it fails loudly
  /// instead of silently pretending an outcome.
  /// </summary>
  public class UnconfiguredExecutionGateway : IExecutionGateway
  {
    public System.Threading.Tasks.Task<OrderDispatchResult> SendAsync(OrderRequest request, System.Threading.CancellationToken cancellationToken)
    {
      throw new InvalidOperationException("No execution provider is configured, so no order can be sent.");
    }

    public System.Threading.Tasks.Task<OrderQueryResult> QueryAsync(string clientOrderId, string symbol, System.Threading.CancellationToken cancellationToken)
    {
      throw new InvalidOperationException("No execution provider is configured, so no order can be queried.");
    }
  }
}
