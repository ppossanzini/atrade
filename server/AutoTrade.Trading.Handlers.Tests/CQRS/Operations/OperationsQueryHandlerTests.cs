using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Operations;
using AutoTrade.Trading.Handlers.CQRS.Operations;
using AutoTrade.Trading.Handlers.Model;
using MapZilla;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.Operations
{
  public class OperationsQueryHandlerTests
  {
    private static TradingTestContext CreateContext()
    {
      return new TradingTestContext(new Dictionary<string, string>());
    }

    [Fact]
    public async Task GetOperationalStatus_ReturnsComposedStatusFromPersistedState()
    {
      using TradingTestContext context = CreateContext();

      context.Db.KillSwitchStates.Add(new KillSwitchState
      {
        Id = 1,
        IsEngaged = false,
        ChangedAtUtc = null,
        ChangedByOperatorId = null,
        Reason = null
      });

      context.Db.TradingAccounts.Add(new TradingAccount
      {
        Id = Guid.CreateVersion7(),
        BrokerAccountId = 7654321,
        Environment = TradingEnvironment.Demo,
        IsTradingEnabled = false,
        ConnectionState = BrokerConnectionState.Disconnected,
        LastBrokerSyncUtc = null,
        LastReconciledUtc = null,
        CreatedAtUtc = context.Clock.GetUtcNow().UtcDateTime
      });

      context.Db.MarketManagerStates.Add(new MarketManagerState
      {
        Id = 1,
        Mode = MarketManagerMode.Supervised,
        IsAnalysisRunning = false,
        UpdatedAtUtc = context.Clock.GetUtcNow().UtcDateTime
      });

      context.Db.SaveChanges();

      OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

      Assert.NotNull(status);
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, status.ServerTimeUtc);
      Assert.NotNull(status.KillSwitch);
      Assert.False(status.KillSwitch.IsEngaged);
      Assert.NotNull(status.Account);
      Assert.Equal(7654321, status.Account.BrokerAccountId);
      Assert.Equal(TradingEnvironment.Demo, status.Account.Environment);
      Assert.NotNull(status.MarketManager);
      Assert.Equal(MarketManagerMode.Supervised, status.MarketManager.Mode);
    }

    [Fact]
    public async Task GetOperationalStatus_WithoutKillSwitchRow_FailsClosedByReportingEngaged()
    {
      using TradingTestContext context = CreateContext();

      OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

      Assert.NotNull(status.KillSwitch);
      Assert.True(status.KillSwitch.IsEngaged);
      Assert.Null(status.Account);
      Assert.Null(status.MarketManager);
    }

    [Fact]
    public async Task GetOperationalStatus_IsReadOnly()
    {
      using TradingTestContext context = CreateContext();

      await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);
      await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

      Assert.Equal(0, context.Db.JournalEvents.Count());
      Assert.Equal(0, context.Db.KillSwitchStates.Count());
      Assert.Equal(0, context.Db.TradingAccounts.Count());
    }
  }
}
