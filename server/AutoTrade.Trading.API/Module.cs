using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.API
{
  /// <summary>
  /// Presentation-tier registration. Controllers stay stateless: they bind HTTP, dispatch exactly
  /// one contract through the mediator, and translate the outcome into a transport response.
  /// </summary>
  public static class Module
  {
    public static IServiceCollection AddTradingApi(this IServiceCollection services)
    {
      // Enums travel as names so the HTTP contract stays readable for clients and humans.
      services
        .AddControllersWithViews()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

      return services;
    }
  }
}
