using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>
  /// Resolves unknown broker outcomes after a timeout, restart or reconnect. The first write marks the account
  /// as requiring reconciliation, so an exception cannot leave the account apparently healthy. Once provider
  /// state has been correlated, one final SaveChanges call atomically persists the event rows, leg transitions,
  /// execution transitions and account watermark on the production SQLite database.
  ///
  /// Pending legs are never queried or sent here: they were persisted before dispatch and therefore have no
  /// broker outcome to infer. Every other non-terminal leg is queried by its deterministic client order id.
  /// </summary>
  public sealed class ExecutionReconciliationService(
    DB db,
    IExecutionGateway executionGateway,
    BrokerOptions brokerOptions,
    TimeProvider timeProvider,
    ILogger<ExecutionReconciliationService> logger) : IExecutionReconciliationService
  {
    private static readonly HashSet<ExecutionLegStatus> QueryableStatuses = new HashSet<ExecutionLegStatus>
    {
      ExecutionLegStatus.Dispatched,
      ExecutionLegStatus.Accepted,
      ExecutionLegStatus.PartiallyFilled,
      ExecutionLegStatus.TimedOut,
      ExecutionLegStatus.ReconciliationRequired
    };

    public async Task<ExecutionReconciliationResultDto> ReconcileAsync(CancellationToken cancellationToken)
    {
      TradingAccount account = await db.TradingAccounts
        .OrderBy(item => item.CreatedAtUtc)
        .FirstOrDefaultAsync(item => item.Environment == brokerOptions.Environment, cancellationToken);

      if (account == null)
      {
        return new ExecutionReconciliationResultDto
        {
          IsClean = true,
          ErrorCode = "BROKER_ACCOUNT_NOT_REGISTERED"
        };
      }

      DateTime now = timeProvider.GetUtcNow().UtcDateTime;
      account.ConnectionState = BrokerConnectionState.ReconciliationRequired;
      await db.SaveChangesAsync(cancellationToken);

      List<Model.Execution> executions = await db.Executions
        .Where(item => item.Status == ExecutionStatus.Dispatching
          || item.Status == ExecutionStatus.AwaitingBroker
          || item.Status == ExecutionStatus.ReconciliationRequired
          || item.Status == ExecutionStatus.Compensating)
        .OrderBy(item => item.CreatedAtUtc)
        .ToListAsync(cancellationToken);

      ExecutionReconciliationResultDto result = new ExecutionReconciliationResultDto
      {
        ExecutionCount = executions.Count
      };

      foreach (Model.Execution execution in executions)
      {
        List<ExecutionLeg> legs = await db.ExecutionLegs
          .Where(item => item.ExecutionId == execution.Id)
          .OrderBy(item => item.Ordinal)
          .ToListAsync(cancellationToken);

        bool executionResolved = true;

        foreach (ExecutionLeg leg in legs.Where(item => QueryableStatuses.Contains(item.Status)))
        {
          OrderQueryResult query = await executionGateway.QueryAsync(leg.ClientOrderId, leg.Symbol, cancellationToken);

          if (query == null || query.Outcome == OrderQueryOutcome.Unknown)
          {
            executionResolved = false;
            leg.Status = ExecutionLegStatus.ReconciliationRequired;
            leg.ErrorCode = query?.ErrorCode ?? "BROKER_RECONCILIATION_UNKNOWN";
            leg.LastEventAtUtc = now;
            continue;
          }

          ApplyQuery(leg, query, now, result);
        }

        if (executionResolved)
        {
          CompleteResolvedExecution(execution, legs, now);
          result.ResolvedExecutionCount++;
        }
        else
        {
          execution.Status = ExecutionStatus.ReconciliationRequired;
          result.UnresolvedExecutionCount++;
        }
      }

      account.LastBrokerSyncUtc = now;
      bool clean = result.UnresolvedExecutionCount == 0;
      if (clean)
      {
        account.LastReconciledUtc = now;
        account.ConnectionState = BrokerConnectionState.Connected;
        result.IsClean = true;
        result.ReconciledAtUtc = now;
      }
      else
      {
        account.ConnectionState = BrokerConnectionState.ReconciliationRequired;
      }

      // This is the single unit of work for broker events, execution legs, executions and the account
      // watermark. SQLite wraps SaveChanges in a transaction; no migration or second write path is needed.
      await db.SaveChangesAsync(cancellationToken);

      logger.LogInformation(
        "cTrader reconciliation pass completed. Executions={ExecutionCount}, Resolved={ResolvedCount}, Unresolved={UnresolvedCount}, AppliedEvents={AppliedEvents}, DuplicateEvents={DuplicateEvents}, Clean={IsClean}.",
        result.ExecutionCount,
        result.ResolvedExecutionCount,
        result.UnresolvedExecutionCount,
        result.AppliedEventCount,
        result.DuplicateEventCount,
        result.IsClean);

      return result;
    }

    private void ApplyQuery(ExecutionLeg leg, OrderQueryResult query, DateTime now, ExecutionReconciliationResultDto result)
    {
      if (!string.IsNullOrWhiteSpace(query.BrokerOrderId))
      {
        leg.BrokerOrderId = query.BrokerOrderId;
      }

      foreach (OrderEventPayload payload in query.Events ?? new List<OrderEventPayload>())
      {
        string brokerEventId = string.IsNullOrWhiteSpace(payload.BrokerEventId)
          ? "RECON:" + leg.ClientOrderId + ":" + query.Outcome + ":" + query.FilledVolumeUnits
          : payload.BrokerEventId;

        bool exists = db.BrokerEvents.Local.Any(item => item.BrokerEventId == brokerEventId)
          || db.BrokerEvents.Any(item => item.BrokerEventId == brokerEventId);

        if (exists)
        {
          result.DuplicateEventCount++;
          continue;
        }

        db.BrokerEvents.Add(new BrokerEvent
        {
          Id = Guid.CreateVersion7(),
          ExecutionId = leg.ExecutionId,
          LegId = leg.Id,
          BrokerEventId = brokerEventId,
          Kind = payload.Kind,
          Symbol = payload.Symbol ?? leg.Symbol,
          Payload = payload.Payload,
          ReceivedAtUtc = now
        });

        result.AppliedEventCount++;
      }

      leg.LastEventAtUtc = now;
      if (query.AveragePrice.HasValue)
      {
        leg.AveragePrice = query.AveragePrice;
      }

      int cumulativeFilled = Math.Max(leg.FilledVolumeUnits, query.FilledVolumeUnits);
      if (cumulativeFilled > 0)
      {
        leg.FilledVolumeUnits = cumulativeFilled;
      }

      switch (query.Outcome)
      {
        case OrderQueryOutcome.Accepted:
          leg.Status = ExecutionLegStatus.Accepted;
          break;

        case OrderQueryOutcome.PartiallyFilled:
          leg.Status = ExecutionLegStatus.PartiallyFilled;
          break;

        case OrderQueryOutcome.Filled:
          leg.Status = ExecutionLegStatus.Filled;
          break;

        case OrderQueryOutcome.Rejected:
          leg.Status = leg.FilledVolumeUnits > 0
            ? ExecutionLegStatus.PartiallyFilled
            : ExecutionLegStatus.Rejected;
          break;

        default:
          leg.Status = ExecutionLegStatus.ReconciliationRequired;
          break;
      }
    }

    private static void CompleteResolvedExecution(Model.Execution execution, List<ExecutionLeg> legs, DateTime now)
    {
      int planned = legs.Sum(item => item.VolumeUnits);
      int filled = legs.Sum(item => item.FilledVolumeUnits);
      execution.Coverage = planned > 0 ? (int)Math.Floor(filled * 100.0 / planned) : 0;
      execution.CompletedAtUtc = now;

      if (execution.Coverage >= 100)
      {
        execution.Status = ExecutionStatus.CompletedNominal;
        return;
      }

      execution.Status = execution.FailurePolicy == FailurePolicy.MinimumCoverage
        && execution.Coverage >= execution.MinimumCoverage
        ? ExecutionStatus.CompletedPartial
        : ExecutionStatus.CompensationRequired;
    }
  }
}
