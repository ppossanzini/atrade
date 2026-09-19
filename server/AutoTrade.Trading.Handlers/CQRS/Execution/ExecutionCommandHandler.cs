using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Execution;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Risk;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using ExecutionEntity = AutoTrade.Trading.Handlers.Model.Execution;

namespace AutoTrade.Trading.Handlers.CQRS.Execution
{
  /// <summary>
  /// The Execution Engine. It persists before it sends, sends one leg at a time in the frozen order, applies
  /// broker events through a dedup, and never treats silence as a rejection: an order whose outcome is unknown
  /// moves to reconciliation and is not sent again.
  ///
  /// It owns no risk decision: the volume comes from the sizing, which reads the legs of the version and the
  /// instrument facts of the provider, and a leg that cannot be sized stops the whole start before anything
  /// is sent or written.
  /// </summary>
  public class ExecutionCommandHandler(
    DB db,
    IHikyaku hikyaku,
    IMarketDataSource marketDataSource,
    IExecutionGateway executionGateway,
    ExecutionOptions executionOptions,
    IJournalWriter journalWriter,
    TimeProvider timeProvider)
    : IRequestHandler<StartExecution, ExecutionStartResultDto>,
      IRequestHandler<ConfirmCompensation, ExecutionStartResultDto>,
      IRequestHandler<ValidateExecutionStartable, bool>,
      IRequestHandler<ValidateCompensationConfirmable, bool>
  {
    private const int KillSwitchSingletonId = 1;
    private const string ExecutionEntityType = "Execution";

    public async Task<ExecutionStartResultDto> Handle(StartExecution request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      Proposal proposal = await db.Proposals
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.ProposalId, cancellationToken);

      if (proposal == null)
      {
        return Refused(ExecutionOutcome.NotFound, "proposal_not_found");
      }

      if (proposal.Status != ProposalStatus.Approved)
      {
        await JournalRefusalAsync(request.OperatorId, null, "proposal_not_approved:" + proposal.Status, cancellationToken);

        return Refused(ExecutionOutcome.NotAuthorized, "proposal_not_approved");
      }

      if (await db.Executions.AnyAsync(item => item.ProposalId == proposal.Id, cancellationToken))
      {
        return Refused(ExecutionOutcome.AlreadyExecuted, "proposal_already_executed");
      }

      if (executionOptions.Provider == ExecutionProviderKind.None)
      {
        return Refused(ExecutionOutcome.NotConfigured, "execution_provider_not_configured");
      }

      if (await db.KillSwitchStates.AsNoTracking().AnyAsync(item => item.Id == KillSwitchSingletonId && item.IsEngaged, cancellationToken))
      {
        await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.ExecutionBlocked, ExecutionEntityType, proposal.Id, "reason=kill_switch_engaged", cancellationToken);

        return Refused(ExecutionOutcome.Blocked, "kill_switch_engaged");
      }

      List<BasketVersionLeg> versionLegs = await db.BasketVersionLegs
        .AsNoTracking()
        .Where(item => item.VersionId == proposal.BasketVersionId)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      if (versionLegs.Count == 0)
      {
        return Refused(ExecutionOutcome.NotConfigured, "version_has_no_legs");
      }

      BasketVersionPolicy policy = await db.BasketVersionPolicies
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.VersionId == proposal.BasketVersionId, cancellationToken);

      List<SymbolRequest> requests = versionLegs
        .Select(leg => new SymbolRequest { Symbol = leg.Symbol, Market = leg.Market })
        .ToList();

      MarketDataCapture capture = await marketDataSource.CaptureAsync(requests, cancellationToken);

      if (!capture.IsAvailable || !capture.CapturedAtUtc.HasValue || capture.Account == null)
      {
        return Refused(ExecutionOutcome.NotConfigured, "market_data_unavailable");
      }

      List<SymbolSpecification> specifications = await marketDataSource.DescribeAsync(requests, cancellationToken);
      List<ExecutionLeg> legs = new List<ExecutionLeg>();
      Guid executionId = Guid.CreateVersion7();
      int ordinal = 0;

      foreach (BasketVersionLeg versionLeg in versionLegs)
      {
        SymbolSpecification specification = specifications.FirstOrDefault(item => string.Equals(item.Symbol, versionLeg.Symbol, StringComparison.OrdinalIgnoreCase));

        SizingResult sizing = PositionSizingCalculator.Compute(new SizingInput
        {
          Equity = capture.Account.Equity,
          AccountCurrency = capture.Account.Currency,
          RiskCapPercent = versionLeg.RiskCap,
          StopDistancePips = versionLeg.StopDistancePips,
          Specification = specification
        });

        // Nothing is written and nothing is sent when a single leg cannot be sized: a partial send would leave
        // exposure nobody decided, and a written execution the operator cannot retry after fixing the input.
        if (!sizing.IsSized)
        {
          await JournalRefusalAsync(request.OperatorId, proposal.Id, versionLeg.Symbol + ":" + sizing.Reason, cancellationToken);

          return Refused(ExecutionOutcome.NotConfigured, versionLeg.Symbol + ":" + sizing.Reason);
        }

        legs.Add(new ExecutionLeg
        {
          Id = Guid.CreateVersion7(),
          ExecutionId = executionId,
          Ordinal = ordinal,
          Symbol = versionLeg.Symbol,
          Market = versionLeg.Market,
          Direction = versionLeg.Direction,
          VolumeUnits = sizing.VolumeUnits,
          ClientOrderId = BuildClientOrderId(executionId, ordinal),
          Status = ExecutionLegStatus.Pending
        });

        ordinal++;
      }

      ExecutionEntity execution = new ExecutionEntity
      {
        Id = executionId,
        ProposalId = proposal.Id,
        BasketId = proposal.BasketId,
        BasketVersionId = proposal.BasketVersionId,
        VersionNumber = proposal.VersionNumber,
        SnapshotId = proposal.SnapshotId,
        Status = ExecutionStatus.Pending,
        FailurePolicy = policy != null ? policy.FailurePolicy : FailurePolicy.RequireConfirmation,
        MinimumCoverage = policy != null ? policy.MinimumCoverage : 100,
        Coverage = 0,
        CreatedAtUtc = now
      };

      db.Executions.Add(execution);
      db.ExecutionLegs.AddRange(legs);

      // Persist first: from here on the operation exists, and a restart resumes it instead of losing it.
      await db.SaveChangesAsync(cancellationToken);
      await journalWriter.AppendOperatorEventAsync(
        request.OperatorId,
        JournalEventKind.ExecutionCreated,
        ExecutionEntityType,
        execution.Id,
        "proposal=" + proposal.Id + ";legs=" + legs.Count,
        cancellationToken);

      await RunSequenceAsync(execution, legs, request.OperatorId, cancellationToken);

      return new ExecutionStartResultDto
      {
        ExecutionId = execution.Id,
        Outcome = ExecutionOutcome.Applied,
        Status = execution.Status
      };
    }

    public async Task<ExecutionStartResultDto> Handle(ConfirmCompensation request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      ExecutionEntity original = await db.Executions.FirstOrDefaultAsync(item => item.Id == request.ExecutionId, cancellationToken);

      if (original == null)
      {
        return Refused(ExecutionOutcome.NotFound, "execution_not_found");
      }

      if (original.Status != ExecutionStatus.CompensationRequired)
      {
        return Refused(ExecutionOutcome.Conflict, "not_in_compensation_required");
      }

      if (string.IsNullOrWhiteSpace(request.Reason))
      {
        return Refused(ExecutionOutcome.Conflict, "reason_required");
      }

      List<ExecutionLeg> filledLegs = await db.ExecutionLegs
        .Where(item => item.ExecutionId == original.Id && item.FilledVolumeUnits > 0)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      original.Status = ExecutionStatus.Compensating;
      original.CompletedAtUtc = null;

      if (filledLegs.Count == 0)
      {
        // Nothing was really executed, so there is no exposure to close: the execution simply ends as partial
        // instead of pretending a compensation happened.
        original.Status = ExecutionStatus.CompletedPartial;
        original.CompletedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.ExecutionCompensationConfirmed, ExecutionEntityType, original.Id, "reason=" + request.Reason + ";result=nothing_to_compensate", cancellationToken);

        return new ExecutionStartResultDto { ExecutionId = original.Id, Outcome = ExecutionOutcome.Applied, Status = original.Status };
      }

      Guid compensationId = Guid.CreateVersion7();
      List<ExecutionLeg> compensationLegs = new List<ExecutionLeg>();
      int ordinal = 0;

      foreach (ExecutionLeg filledLeg in filledLegs)
      {
        compensationLegs.Add(new ExecutionLeg
        {
          Id = Guid.CreateVersion7(),
          ExecutionId = compensationId,
          Ordinal = ordinal,
          Symbol = filledLeg.Symbol,
          Market = filledLeg.Market,

          // A compensation is the opposite side of the same volume: never the same client order id.
          Direction = filledLeg.Direction == LegDirection.Long ? LegDirection.Short : LegDirection.Long,
          VolumeUnits = filledLeg.FilledVolumeUnits,
          ClientOrderId = BuildClientOrderId(compensationId, ordinal),
          Status = ExecutionLegStatus.Pending
        });

        ordinal++;
      }

      ExecutionEntity compensation = new ExecutionEntity
      {
        Id = compensationId,
        ProposalId = null,
        BasketId = original.BasketId,
        BasketVersionId = original.BasketVersionId,
        VersionNumber = original.VersionNumber,
        SnapshotId = original.SnapshotId,
        Status = ExecutionStatus.Pending,
        FailurePolicy = FailurePolicy.MinimumCoverage,
        MinimumCoverage = 100,
        Coverage = 0,
        CompensationOfExecutionId = original.Id,
        CreatedAtUtc = now
      };

      db.Executions.Add(compensation);
      db.ExecutionLegs.AddRange(compensationLegs);
      await db.SaveChangesAsync(cancellationToken);

      await journalWriter.AppendOperatorEventAsync(
        request.OperatorId,
        JournalEventKind.ExecutionCompensationConfirmed,
        ExecutionEntityType,
        original.Id,
        "reason=" + request.Reason + ";compensation=" + compensation.Id,
        cancellationToken);

      await RunSequenceAsync(compensation, compensationLegs, request.OperatorId, cancellationToken);

      // The original ends as partial: its exposure was closed by a new sequence, never by rewriting what it did.
      original.Status = ExecutionStatus.CompletedPartial;
      original.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
      await db.SaveChangesAsync(cancellationToken);

      return new ExecutionStartResultDto { ExecutionId = compensation.Id, Outcome = ExecutionOutcome.Applied, Status = compensation.Status };
    }

    public async Task<bool> Handle(ValidateExecutionStartable request, CancellationToken cancellationToken)
    {
      Proposal proposal = await db.Proposals
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.ProposalId, cancellationToken);

      if (proposal == null || proposal.Status != ProposalStatus.Approved)
      {
        return false;
      }

      return !await db.Executions.AnyAsync(item => item.ProposalId == proposal.Id, cancellationToken);
    }

    public async Task<bool> Handle(ValidateCompensationConfirmable request, CancellationToken cancellationToken)
    {
      ExecutionEntity execution = await db.Executions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.ExecutionId, cancellationToken);

      return execution != null && execution.Status == ExecutionStatus.CompensationRequired;
    }

    /// <summary>
    /// Sends the legs one at a time, in the order they were frozen with. The sequence stops at the first leg
    /// whose outcome is unknown, because sending the next one would compound an exposure nobody can measure.
    /// </summary>
    private async Task RunSequenceAsync(ExecutionEntity execution, List<ExecutionLeg> legs, Guid operatorId, CancellationToken cancellationToken)
    {
      execution.Status = ExecutionStatus.Dispatching;
      execution.StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
      await db.SaveChangesAsync(cancellationToken);

      foreach (ExecutionLeg leg in legs.OrderBy(item => item.Ordinal))
      {
        leg.Status = ExecutionLegStatus.Dispatched;
        leg.LastEventAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ExecutionLegDispatched, ExecutionEntityType, execution.Id, "leg=" + leg.Symbol + ";clientOrderId=" + leg.ClientOrderId, cancellationToken);

        OrderDispatchResult dispatch = await executionGateway.SendAsync(
          new OrderRequest
          {
            ClientOrderId = leg.ClientOrderId,
            Symbol = leg.Symbol,
            Market = leg.Market,
            Direction = leg.Direction,
            VolumeUnits = leg.VolumeUnits
          },
          cancellationToken);

        leg.BrokerOrderId = dispatch.BrokerOrderId;

        if (dispatch.Outcome == OrderDispatchOutcome.NoResponse)
        {
          leg.Status = ExecutionLegStatus.TimedOut;
          await db.SaveChangesAsync(cancellationToken);
          await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ExecutionLegTimedOut, ExecutionEntityType, execution.Id, "leg=" + leg.Symbol + ";clientOrderId=" + leg.ClientOrderId, cancellationToken);

          execution.Status = ExecutionStatus.ReconciliationRequired;
          await db.SaveChangesAsync(cancellationToken);
          await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ExecutionReconciliationRequired, ExecutionEntityType, execution.Id, "leg=" + leg.Symbol, cancellationToken);

          return;
        }

        if (dispatch.ErrorCode != null)
        {
          leg.ErrorCode = dispatch.ErrorCode;
        }

        await ApplyEventsAsync(execution, leg, dispatch.Events, operatorId, cancellationToken);

        if (leg.Status == ExecutionLegStatus.Rejected && execution.FailurePolicy == FailurePolicy.AllOrNothing)
        {
          // The version asked for everything or nothing: one rejected leg already broke that promise, so the
          // remaining legs are not sent and the exposure becomes an explicit decision.
          execution.Status = ExecutionStatus.CompensationRequired;
          execution.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
          await db.SaveChangesAsync(cancellationToken);
          await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ExecutionCompensationRequired, ExecutionEntityType, execution.Id, "reason=all_or_nothing_violated", cancellationToken);

          return;
        }
      }

      CompleteSequence(execution, legs);
      await db.SaveChangesAsync(cancellationToken);

      JournalEventKind kind = execution.Status == ExecutionStatus.CompletedNominal
        ? JournalEventKind.ExecutionCompletedNominal
        : execution.Status == ExecutionStatus.CompletedPartial
          ? JournalEventKind.ExecutionCompletedPartial
          : JournalEventKind.ExecutionCompensationRequired;

      await journalWriter.AppendOperatorEventAsync(operatorId, kind, ExecutionEntityType, execution.Id, "coverage=" + execution.Coverage, cancellationToken);
    }

    /// <summary>
    /// Records and applies the events of one dispatch. The broker event identity is the dedup key: an event
    /// already stored is counted and journalled but never applied, which is what keeps a repeated delivery
    /// from filling a leg twice.
    /// </summary>
    private async Task ApplyEventsAsync(ExecutionEntity execution, ExecutionLeg leg, List<OrderEventPayload> events, Guid operatorId, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      foreach (OrderEventPayload payload in events)
      {
        if (!string.IsNullOrWhiteSpace(payload.BrokerEventId)
          && await db.BrokerEvents.AnyAsync(item => item.BrokerEventId == payload.BrokerEventId, cancellationToken))
        {
          await journalWriter.AppendSystemEventAsync(JournalEventKind.BrokerEventDuplicateIgnored, ExecutionEntityType, execution.Id, "event=" + payload.BrokerEventId, cancellationToken);

          continue;
        }

        db.BrokerEvents.Add(new BrokerEvent
        {
          Id = Guid.CreateVersion7(),
          ExecutionId = execution.Id,
          LegId = leg.Id,
          BrokerEventId = payload.BrokerEventId,
          Kind = payload.Kind,
          Symbol = payload.Symbol,
          Payload = payload.Payload,
          ReceivedAtUtc = now
        });

        ApplyEvent(leg, payload, now);
        await db.SaveChangesAsync(cancellationToken);

        await journalWriter.AppendSystemEventAsync(EventJournalKind(payload.Kind), ExecutionEntityType, execution.Id, "leg=" + leg.Symbol + ";event=" + payload.BrokerEventId, cancellationToken);
      }
    }

    private static void ApplyEvent(ExecutionLeg leg, OrderEventPayload payload, DateTime now)
    {
      leg.LastEventAtUtc = now;

      if (payload.AveragePrice.HasValue)
      {
        leg.AveragePrice = payload.AveragePrice;
      }

      switch (payload.Kind)
      {
        case ExecutionEventKind.OrderAccepted:
          if (leg.Status == ExecutionLegStatus.Dispatched)
          {
            leg.Status = ExecutionLegStatus.Accepted;
          }

          break;

        case ExecutionEventKind.OrderPartiallyFilled:
          leg.FilledVolumeUnits += payload.FilledVolumeUnits;
          leg.Status = ExecutionLegStatus.PartiallyFilled;
          break;

        case ExecutionEventKind.OrderFilled:
          leg.FilledVolumeUnits += payload.FilledVolumeUnits;
          leg.Status = ExecutionLegStatus.Filled;
          break;

        case ExecutionEventKind.OrderRejected:
          leg.Status = ExecutionLegStatus.Rejected;
          break;

        case ExecutionEventKind.OrderCancelled:
          // A cancellation is not a rejection, but an order cancelled without any fill is a leg that did not
          // execute, and it must not be counted as filled.
          leg.Status = leg.FilledVolumeUnits > 0 ? ExecutionLegStatus.PartiallyFilled : ExecutionLegStatus.Rejected;
          break;
      }
    }

    /// <summary>Measures coverage from what was actually filled and closes the execution accordingly.</summary>
    private static void CompleteSequence(ExecutionEntity execution, List<ExecutionLeg> legs)
    {
      int planned = legs.Sum(item => item.VolumeUnits);
      int filled = legs.Sum(item => item.FilledVolumeUnits);

      execution.Coverage = planned > 0 ? (int)Math.Floor(filled * 100.0 / planned) : 0;
      execution.CompletedAtUtc = DateTime.UtcNow;

      if (execution.Coverage >= 100)
      {
        execution.Status = ExecutionStatus.CompletedNominal;

        return;
      }

      bool policySatisfied = execution.FailurePolicy == FailurePolicy.MinimumCoverage
        && execution.Coverage >= execution.MinimumCoverage;

      execution.Status = policySatisfied ? ExecutionStatus.CompletedPartial : ExecutionStatus.CompensationRequired;
    }

    private static JournalEventKind EventJournalKind(ExecutionEventKind kind)
    {
      switch (kind)
      {
        case ExecutionEventKind.OrderAccepted:
          return JournalEventKind.ExecutionLegAccepted;

        case ExecutionEventKind.OrderPartiallyFilled:
          return JournalEventKind.ExecutionLegPartiallyFilled;

        case ExecutionEventKind.OrderFilled:
          return JournalEventKind.ExecutionLegFilled;

        case ExecutionEventKind.OrderRejected:
          return JournalEventKind.ExecutionLegRejected;

        default:
          return JournalEventKind.BrokerEventDuplicateIgnored;
      }
    }

    private static string BuildClientOrderId(Guid executionId, int ordinal)
    {
      return "AT-" + executionId.ToString("N").Substring(0, 12) + "-" + ordinal;
    }

    private async Task JournalRefusalAsync(Guid operatorId, Guid? entityId, string reason, CancellationToken cancellationToken)
    {
      await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ExecutionStartRefused, ExecutionEntityType, entityId, "reason=" + reason, cancellationToken);
    }

    private static ExecutionStartResultDto Refused(ExecutionOutcome outcome, string reason)
    {
      return new ExecutionStartResultDto
      {
        Outcome = outcome,
        Status = ExecutionStatus.Blocked,
        Reason = reason
      };
    }
  }
}
