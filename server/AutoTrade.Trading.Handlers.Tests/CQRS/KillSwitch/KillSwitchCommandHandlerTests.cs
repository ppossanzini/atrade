using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Operations;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Model;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.KillSwitch
{
  public class KillSwitchCommandHandlerTests
  {
    private static TradingTestContext CreateContext()
    {
      return new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Reconciliation:FreshnessMinutes", "15" }
      });
    }

    private static TradingAccount CreateAccount(TradingTestContext context, BrokerConnectionState connectionState, DateTime? lastReconciledUtc)
    {
      TradingAccount account = new TradingAccount
      {
        Id = Guid.CreateVersion7(),
        BrokerAccountId = 1234567,
        Environment = TradingEnvironment.Demo,
        IsTradingEnabled = false,
        ConnectionState = connectionState,
        LastBrokerSyncUtc = null,
        LastReconciledUtc = lastReconciledUtc,
        CreatedAtUtc = context.Clock.GetUtcNow().UtcDateTime
      };

      context.Db.TradingAccounts.Add(account);
      context.Db.SaveChanges();

      return account;
    }

    [Fact]
    public async Task EngageKillSwitch_EngagesAndWritesJournal()
    {
      using TradingTestContext context = CreateContext();
      Guid operatorId = Guid.CreateVersion7();

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new EngageKillSwitch { OperatorId = operatorId, Reason = "manual halt" },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Applied, result.Outcome);
      Assert.True(result.IsEngaged);

      KillSwitchState state = await context.Db.KillSwitchStates.FindAsync(1);
      Assert.True(state.IsEngaged);
      Assert.Equal(operatorId, state.ChangedByOperatorId);
      Assert.Equal("manual halt", state.Reason);

      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.KillSwitchEngaged);
    }

    [Fact]
    public async Task ReleaseKillSwitch_WithoutTradingAccount_ReleasesAndWritesJournal()
    {
      using TradingTestContext context = CreateContext();

      await context.Hikyaku.Send(
        new EngageKillSwitch { OperatorId = Guid.CreateVersion7(), Reason = "manual halt" },
        CancellationToken.None);

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new ReleaseKillSwitch { OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Applied, result.Outcome);
      Assert.False(result.IsEngaged);

      KillSwitchState state = await context.Db.KillSwitchStates.FindAsync(1);
      Assert.False(state.IsEngaged);
      Assert.Null(state.Reason);

      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.KillSwitchReleased);
    }

    [Fact]
    public async Task ReleaseKillSwitch_WhenReconciliationIsRequired_IsBlockedAndStaysEngaged()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.ReconciliationRequired, null);

      await context.Hikyaku.Send(
        new EngageKillSwitch { OperatorId = Guid.CreateVersion7(), Reason = "manual halt" },
        CancellationToken.None);

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new ReleaseKillSwitch { OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Blocked, result.Outcome);
      Assert.True(result.IsEngaged);

      KillSwitchState state = await context.Db.KillSwitchStates.FindAsync(1);
      Assert.True(state.IsEngaged);

      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.KillSwitchReleaseBlocked);
      Assert.DoesNotContain(context.Db.JournalEvents, item => item.Kind == JournalEventKind.KillSwitchReleased);
    }

    [Fact]
    public async Task ReleaseKillSwitch_WhenDegraded_IsBlocked()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Degraded, null);

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new ReleaseKillSwitch { OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public async Task ReleaseKillSwitch_WhenConnectedAndRecentlyReconciled_IsApplied()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Connected, context.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new ReleaseKillSwitch { OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Applied, result.Outcome);
    }

    [Fact]
    public async Task ReleaseKillSwitch_WhenConnectedAndReconciliationIsStale_IsBlocked()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Connected, context.Clock.GetUtcNow().UtcDateTime.AddMinutes(-30));

      KillSwitchChangeResult result = await context.Hikyaku.Send(
        new ReleaseKillSwitch { OperatorId = Guid.CreateVersion7() },
        CancellationToken.None);

      Assert.Equal(KillSwitchChangeOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public async Task ValidateKillSwitchReleaseEligibility_WithoutAccount_IsEligible()
    {
      using TradingTestContext context = CreateContext();

      bool isEligible = await context.Hikyaku.Send(new ValidateKillSwitchReleaseEligibility(), CancellationToken.None);

      Assert.True(isEligible);
    }

    [Fact]
    public async Task ValidateKillSwitchReleaseEligibility_WhenDisconnected_IsEligible()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Disconnected, null);

      bool isEligible = await context.Hikyaku.Send(new ValidateKillSwitchReleaseEligibility(), CancellationToken.None);

      Assert.True(isEligible);
    }

    [Fact]
    public async Task ValidateKillSwitchReleaseEligibility_WhenConnectedWithoutReconciliation_IsNotEligible()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Connected, null);

      bool isEligible = await context.Hikyaku.Send(new ValidateKillSwitchReleaseEligibility(), CancellationToken.None);

      Assert.False(isEligible);
    }

    [Fact]
    public async Task ValidateKillSwitchReleaseEligibility_StaysReadOnly()
    {
      using TradingTestContext context = CreateContext();
      CreateAccount(context, BrokerConnectionState.Connected, context.Clock.GetUtcNow().UtcDateTime);

      await context.Hikyaku.Send(new ValidateKillSwitchReleaseEligibility(), CancellationToken.None);

      Assert.Equal(0, context.Db.JournalEvents.Count());
      Assert.Equal(0, context.Db.KillSwitchStates.Count());
    }
  }
}
