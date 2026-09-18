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
      // AddControllersWithViews is required for the built-in antiforgery token filters
      // used by the mutating endpoints ([ValidateAntiForgeryToken]).
      services.AddControllersWithViews();

      return services;
    }
  }
}
