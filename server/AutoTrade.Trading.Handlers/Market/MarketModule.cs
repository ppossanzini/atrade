using AutoTrade.Trading.Handlers.Market;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers
{
    /// <summary>
    /// Market Manager tier registration. It is called by the handlers module so the composition root keeps a
    /// single registration entrypoint per tier.
    /// </summary>
    public static class MarketModule
    {
        public static IServiceCollection AddTradingMarket(this IServiceCollection services, IConfiguration configuration)
        {
            MarketOptions options = MarketOptionsFactory.FromConfiguration(configuration);

            services.AddSingleton(options);
            services.AddSingleton<IProposalSource, DeterministicProposalSource>();

            return services;
        }
    }
}
