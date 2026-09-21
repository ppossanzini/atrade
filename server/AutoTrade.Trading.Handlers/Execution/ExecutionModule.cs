using System;
using System.Linq;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.Protocol;
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
            IConfigurationSection reconciliationSection = configuration.GetSection("Trading:Execution:Reconciliation");
            bool.TryParse(reconciliationSection["Enabled"], out bool reconciliationEnabled);
            int.TryParse(reconciliationSection["IntervalSeconds"], out int reconciliationIntervalSeconds);
            services.AddSingleton(new ExecutionReconciliationOptions
            {
                IsEnabled = reconciliationEnabled,
                IntervalSeconds = reconciliationIntervalSeconds
            });
            services.AddScoped<IExecutionReconciliationService, ExecutionReconciliationService>();

            if (options.Provider == ExecutionProviderKind.Simulated)
            {
                services.AddSingleton<IExecutionGateway, SimulatedExecutionGateway>();
            }
            else if (options.Provider == ExecutionProviderKind.Ctrader)
            {
                services.AddScoped<IExecutionGateway, CtraderExecutionGateway>();
            }
            else
            {
                services.AddSingleton<IExecutionGateway, UnconfiguredExecutionGateway>();
            }

            return services;
        }

        /// <summary>
        /// Fail-closed cross-checks of the selected provider. Live execution is an explicit promotion from the
        /// default demo environment, and it also requires the OAuth scope that permits trading.
        /// </summary>
        public static void EnsureProviderIsUsable(ExecutionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Provider == ExecutionProviderKind.Ctrader)
            {
                if (options.Ctrader == null)
                {
                    throw new InvalidOperationException("Trading:Execution:Ctrader is missing, so the cTrader execution promotion guard is undefined.");
                }
            }

            if (options.Provider == ExecutionProviderKind.Simulated && options.Simulated == null)
            {
                throw new InvalidOperationException("Trading:Execution:Provider is Simulated but Trading:Execution:Simulated is missing, so no order behaviour is defined.");
            }
        }

        public static void EnsureProviderIsUsable(ExecutionOptions options, BrokerOptions brokerOptions)
        {
            EnsureProviderIsUsable(options);

            if (options.Provider != ExecutionProviderKind.Ctrader)
            {
                return;
            }

            if (brokerOptions == null)
            {
                throw new InvalidOperationException("cTrader execution requires broker configuration.");
            }

            if (brokerOptions.Environment == AutoTrade.Trading.Core.Enums.TradingEnvironment.Live && !options.Ctrader.AllowLive)
            {
                throw new InvalidOperationException("Live cTrader execution is disabled by the promotion guard. Set Trading:Execution:Ctrader:AllowLive=true only after an explicit live promotion.");
            }

            bool tradingScope = (brokerOptions.Scope ?? string.Empty)
              .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
              .Any(item => string.Equals(item, BrokerOptions.TradingScope, StringComparison.OrdinalIgnoreCase));

            if (!tradingScope)
            {
                throw new InvalidOperationException("cTrader execution requires a broker OAuth scope containing 'trading'. Re-authorize the account with the trading scope before enabling order sending.");
            }
        }

        public static void EnsureReconciliationIsUsable(ExecutionOptions options, ExecutionReconciliationOptions reconciliationOptions)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Provider != ExecutionProviderKind.Ctrader)
            {
                return;
            }

            if (reconciliationOptions == null || !reconciliationOptions.IsEnabled || reconciliationOptions.IntervalSeconds <= 0)
            {
                throw new InvalidOperationException("cTrader execution requires Trading:Execution:Reconciliation:Enabled=true and a positive IntervalSeconds value.");
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
