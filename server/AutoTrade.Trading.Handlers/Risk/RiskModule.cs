using AutoTrade.Trading.Handlers.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers
{
  /// <summary>
  /// Risk tier registration. The engine is stateless: its only dependencies are the configured thresholds
  /// and the clock, so a single instance is safe to share.
  /// </summary>
  public static class RiskModule
  {
    public static IServiceCollection AddTradingRisk(this IServiceCollection services, IConfiguration configuration)
    {
      RiskThresholds thresholds = RiskThresholdsFactory.FromConfiguration(configuration);
      RiskSizingThresholds sizingThresholds = RiskSizingThresholdsFactory.FromConfiguration(configuration);

      services.AddSingleton(thresholds);
      services.AddSingleton(sizingThresholds);
      services.AddSingleton<RiskEngine>();

      return services;
    }
  }
}
