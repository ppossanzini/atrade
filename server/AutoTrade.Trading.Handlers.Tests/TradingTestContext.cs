using System;
using System.Collections.Generic;
using AutoTrade.Trading.Handlers;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using MapZilla;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers.Tests
{
  /// <summary>
  /// Wires the real mediator, handlers, EF provider, mapping profile and password hasher so the
  /// tests exercise the production pipeline instead of isolated stand-ins.
  /// </summary>
  internal sealed class TradingTestContext : IDisposable
  {
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public TradingTestContext(IDictionary<string, string> settings)
    {
      IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
      Clock = new FakeTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

      ServiceCollection services = new ServiceCollection();
      services.AddSingleton(configuration);
      services.AddSingleton<TimeProvider>(Clock);
      services.AddLogging();
      services.AddDbContext<DB>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
      services.AddScoped<IJournalWriter, JournalWriter>();
      services.AddScoped<IPasswordHasher<Operator>, PasswordHasher<Operator>>();
      services.AddMapZilla(new[] { typeof(MappingProfile).Assembly });
      services.AddHikyaku(hikyaku => hikyaku.RegisterServicesFromAssembly(typeof(Module).Assembly));

      _provider = services.BuildServiceProvider();
      _scope = _provider.CreateScope();
      Db = _scope.ServiceProvider.GetRequiredService<DB>();
    }

    public FakeTimeProvider Clock { get; }

    public DB Db { get; }

    public IHikyaku Hikyaku
    {
      get { return _scope.ServiceProvider.GetRequiredService<IHikyaku>(); }
    }

    private IPasswordHasher<Operator> PasswordHasher
    {
      get { return _scope.ServiceProvider.GetRequiredService<IPasswordHasher<Operator>>(); }
    }

    public Operator CreateOperator(string userName, string password, bool isActive)
    {
      Operator operatorEntity = new Operator
      {
        Id = Guid.CreateVersion7(),
        UserName = userName,
        IsActive = isActive,
        FailedAttempts = 0,
        LockedUntilUtc = null,
        CreatedAtUtc = Clock.GetUtcNow().UtcDateTime,
        LastLoginAtUtc = null
      };

      operatorEntity.PasswordHash = PasswordHasher.HashPassword(operatorEntity, password);

      Db.Operators.Add(operatorEntity);
      Db.SaveChanges();

      return operatorEntity;
    }

    public void Dispose()
    {
      _scope.Dispose();
      _provider.Dispose();
    }
  }

  internal sealed class FakeTimeProvider : TimeProvider
  {
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTime utcNow)
    {
      _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow()
    {
      return _utcNow;
    }

    public void Advance(TimeSpan amount)
    {
      _utcNow = _utcNow.Add(amount);
    }
  }
}
