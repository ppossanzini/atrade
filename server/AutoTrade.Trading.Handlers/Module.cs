using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using MapZilla;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers
{
  /// <summary>
  /// Handlers-tier registration. The composition root is responsible for the mediator registration
  /// and for invoking this module.
  /// </summary>
  public static class Module
  {
    public static IServiceCollection AddTradingHandlers(this IServiceCollection services, IConfiguration configuration)
    {
      services.AddDbContext<DB>(options => options.UseSqlite(configuration.GetConnectionString("Trading")));
      services.AddScoped<IJournalWriter, JournalWriter>();
      services.AddScoped<IPasswordHasher<Operator>, PasswordHasher<Operator>>();
      services.AddScoped<TradingDatabaseInitializer>();
      services.AddMapZilla(new[] { typeof(MappingProfile).Assembly });
      services.AddTradingBroker(configuration);
      services.AddTradingMarketData(configuration);
      services.AddTradingRisk(configuration);

      return services;
    }
  }
}
