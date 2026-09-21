using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Execution;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Execution
{
  public class ExecutionReconciliationServiceTests
  {
    [Fact]
    public async Task UnknownBrokerOutcome_KeepsAccountAndExecutionFailClosed()
    {
      using DB db = CreateDatabase();
      (TradingAccount account, Model.Execution execution, ExecutionLeg leg) = SeedReconciliation(db);
      StubExecutionGateway gateway = new StubExecutionGateway(new OrderQueryResult
      {
        Outcome = OrderQueryOutcome.Unknown,
        ErrorCode = "BROKER_ORDER_NOT_FOUND"
      });
      ExecutionReconciliationService service = CreateService(db, gateway);

      var result = await service.ReconcileAsync(CancellationToken.None);

      Assert.False(result.IsClean);
      Assert.Equal(1, result.UnresolvedExecutionCount);
      Assert.Equal(BrokerConnectionState.ReconciliationRequired, account.ConnectionState);
      Assert.Null(account.LastReconciledUtc);
      Assert.Equal(ExecutionStatus.ReconciliationRequired, execution.Status);
      Assert.Equal(ExecutionLegStatus.ReconciliationRequired, leg.Status);
      Assert.Equal("BROKER_ORDER_NOT_FOUND", leg.ErrorCode);
    }

    [Fact]
    public async Task FilledOutcome_ResolvesExecutionAndPersistsWatermarkInOnePass()
    {
      using DB db = CreateDatabase();
      (TradingAccount account, Model.Execution execution, ExecutionLeg leg) = SeedReconciliation(db);
      StubExecutionGateway gateway = new StubExecutionGateway(new OrderQueryResult
      {
        Outcome = OrderQueryOutcome.Filled,
        BrokerOrderId = "CTRADER-42",
        FilledVolumeUnits = 10,
        AveragePrice = 1.25,
        Events = new List<OrderEventPayload>
        {
          new OrderEventPayload
          {
            BrokerEventId = "CTRADER:42:filled:7",
            Kind = ExecutionEventKind.OrderFilled,
            Symbol = leg.Symbol,
            FilledVolumeUnits = 10,
            AveragePrice = 1.25,
            Payload = "orderId=42"
          },
          new OrderEventPayload
          {
            BrokerEventId = "CTRADER:42:filled:7",
            Kind = ExecutionEventKind.OrderFilled,
            Symbol = leg.Symbol,
            FilledVolumeUnits = 10,
            AveragePrice = 1.25,
            Payload = "orderId=42"
          }
        }
      });
      ExecutionReconciliationService service = CreateService(db, gateway);

      var result = await service.ReconcileAsync(CancellationToken.None);

      Assert.True(result.IsClean);
      Assert.Equal(1, result.ResolvedExecutionCount);
      Assert.Equal(1, result.AppliedEventCount);
      Assert.Equal(1, result.DuplicateEventCount);
      Assert.Equal(BrokerConnectionState.Connected, account.ConnectionState);
      Assert.NotNull(account.LastReconciledUtc);
      Assert.Equal(ExecutionStatus.CompletedNominal, execution.Status);
      Assert.Equal(ExecutionLegStatus.Filled, leg.Status);
      Assert.Equal(10, leg.FilledVolumeUnits);
      Assert.Equal("CTRADER-42", leg.BrokerOrderId);
      Assert.Single(db.BrokerEvents);
    }

    [Fact]
    public async Task RejectedOutcomeWithNoFillDoesNotCreateExposure()
    {
      using DB db = CreateDatabase();
      (TradingAccount account, Model.Execution execution, ExecutionLeg leg) = SeedReconciliation(db);
      StubExecutionGateway gateway = new StubExecutionGateway(new OrderQueryResult
      {
        Outcome = OrderQueryOutcome.Rejected,
        BrokerOrderId = "CTRADER-43",
        ErrorCode = "BROKER_REJECTED",
        Events = new List<OrderEventPayload>
        {
          new OrderEventPayload
          {
            BrokerEventId = "CTRADER:43:rejected:none",
            Kind = ExecutionEventKind.OrderRejected,
            Symbol = leg.Symbol,
            Payload = "orderId=43"
          }
        }
      });
      ExecutionReconciliationService service = CreateService(db, gateway);

      await service.ReconcileAsync(CancellationToken.None);

      Assert.Equal(ExecutionStatus.CompensationRequired, execution.Status);
      Assert.Equal(ExecutionLegStatus.Rejected, leg.Status);
      Assert.Equal(0, leg.FilledVolumeUnits);
      Assert.Equal(BrokerConnectionState.Connected, account.ConnectionState);
    }

    private static ExecutionReconciliationService CreateService(DB db, StubExecutionGateway gateway)
    {
      return new ExecutionReconciliationService(
        db,
        gateway,
        new BrokerOptions { Environment = TradingEnvironment.Demo },
        new FixedTimeProvider(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
        NullLogger<ExecutionReconciliationService>.Instance);
    }

    private static (TradingAccount Account, Model.Execution Execution, ExecutionLeg Leg) SeedReconciliation(DB db)
    {
      DateTime createdAt = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);
      TradingAccount account = new TradingAccount
      {
        Id = Guid.CreateVersion7(),
        BrokerAccountId = 123,
        Environment = TradingEnvironment.Demo,
        IsTradingEnabled = true,
        ConnectionState = BrokerConnectionState.Connected,
        CreatedAtUtc = createdAt
      };
      Model.Execution execution = new Model.Execution
      {
        Id = Guid.CreateVersion7(),
        BasketId = Guid.CreateVersion7(),
        BasketVersionId = Guid.CreateVersion7(),
        VersionNumber = 1,
        Status = ExecutionStatus.ReconciliationRequired,
        FailurePolicy = FailurePolicy.AllOrNothing,
        MinimumCoverage = 100,
        CreatedAtUtc = createdAt
      };
      ExecutionLeg leg = new ExecutionLeg
      {
        Id = Guid.CreateVersion7(),
        ExecutionId = execution.Id,
        Ordinal = 0,
        Symbol = "EURUSD",
        Market = MarketKind.Fx,
        Direction = LegDirection.Long,
        VolumeUnits = 10,
        ClientOrderId = "AT-reconciliation-1",
        Status = ExecutionLegStatus.TimedOut
      };

      db.TradingAccounts.Add(account);
      db.Executions.Add(execution);
      db.ExecutionLegs.Add(leg);
      db.SaveChanges();

      return (account, execution, leg);
    }

    private static DB CreateDatabase()
    {
      DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

      return new DB(options);
    }

    private sealed class StubExecutionGateway(OrderQueryResult answer) : IExecutionGateway
    {
      public Task<OrderDispatchResult> SendAsync(OrderRequest request, CancellationToken cancellationToken)
      {
        throw new InvalidOperationException("Reconciliation must never send an order.");
      }

      public Task<OrderQueryResult> QueryAsync(string clientOrderId, string symbol, CancellationToken cancellationToken)
      {
        return Task.FromResult(answer);
      }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
      public override DateTimeOffset GetUtcNow()
      {
        return new DateTimeOffset(utcNow, TimeSpan.Zero);
      }
    }
  }
}
