using System;
using System.Collections.Generic;
using AutoTrade.Trading.Handlers;
using AutoTrade.Trading.Handlers.Analysis;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Evidence;
using AutoTrade.Trading.Handlers.Execution;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Tests.Broker;
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

      // The broker tier is registered for real, including the token protector, so tests exercise the same
      // cryptography as production; only the provider endpoint is replaced by the fake transport.
      services.AddTradingBroker(configuration);
      services.AddSingleton<IBrokerTokenClient>(TokenClient);

      // The market data tier is registered for real too, so the configured provider decides which source
      // the handlers use exactly as it does in production.
      services.AddTradingMarketData(configuration);

      // The risk and market tiers keep the same registration as the host, so the tests exercise the real
      // engine, the real threshold reading and the real routing rules instead of stand-ins.
      services.AddTradingRisk(configuration);
      services.AddTradingMarket(configuration);

      // The execution tier too: the configured provider decides which order gateway the handler receives,
      // exactly as it does in production.
      services.AddTradingExecution(configuration);

      // And the evidence tier: with no section configured it resolves to the explicitly unavailable store.
      services.AddTradingEvidence(configuration);

      // The analysis tier is registered the same way, with the local model replaced by a stand-in: a test must
      // not depend on a model being installed on the machine running it. The transport is covered separately.
      services.AddTradingAnalysis(configuration);
      services.AddSingleton<IOllamaAnalysisClient>(AnalysisClient);

      // And the embedding tier: with no section configured it resolves to the explicitly unavailable source, so
      // a test never needs a checkpoint on disk.
      services.AddTradingEmbedding(configuration);

      // The episode writer is replaced rather than configured: remembering is covered by asserting what the
      // application tried to remember, not by needing a real store and a real model in a unit test.
      services.AddSingleton<IOperationalEpisodeWriter>(EpisodeWriter);

      _provider = services.BuildServiceProvider();
      _scope = _provider.CreateScope();
      Db = _scope.ServiceProvider.GetRequiredService<DB>();
    }

    public FakeBrokerTokenClient TokenClient { get; } = new FakeBrokerTokenClient();

    /// <summary>Analysis model stand-in, so a status test can choose whether a model is in force.</summary>
    public Tests.Analysis.FakeAnalysisClient AnalysisClient { get; } = new Tests.Analysis.FakeAnalysisClient();

    /// <summary>Episodes the application tried to remember during this test.</summary>
    public Tests.Evidence.FakeOperationalEpisodeWriter EpisodeWriter { get; } = new Tests.Evidence.FakeOperationalEpisodeWriter();

    /// <summary>Exposed so tests can prove what actually reached the database.</summary>
    public AutoTrade.Trading.Handlers.Broker.IBrokerTokenProtector TokenProtector
    {
      get { return _scope.ServiceProvider.GetRequiredService<AutoTrade.Trading.Handlers.Broker.IBrokerTokenProtector>(); }
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

    public void Set(DateTime utcNow)
    {
      _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);
    }
  }
}
