using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Operations;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.CQRS.KillSwitch
{
  public class KillSwitchCommandHandler(DB db, IHikyaku hikyaku, IJournalWriter journalWriter, IConfiguration configuration, TimeProvider timeProvider)
    : IRequestHandler<EngageKillSwitch, KillSwitchChangeResult>,
      IRequestHandler<ReleaseKillSwitch, KillSwitchChangeResult>,
      IRequestHandler<ValidateKillSwitchReleaseEligibility, bool>
  {
    private const int KillSwitchStateId = 1;
    private const string KillSwitchEntityType = "KillSwitch";
    private const string EngagedPayload = "isEngaged=true";
    private const string ReleasedPayload = "isEngaged=false";
    private const string ReleaseBlockedPayload = "reason=broker_not_reconciled";

    public async Task<KillSwitchChangeResult> Handle(EngageKillSwitch request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      KillSwitchState state = await LoadOrCreateStateAsync(cancellationToken);
      state.IsEngaged = true;
      state.ChangedAtUtc = now;
      state.ChangedByOperatorId = request.OperatorId;
      state.Reason = request.Reason;

      await db.SaveChangesAsync(cancellationToken);
      await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.KillSwitchEngaged, KillSwitchEntityType, null, EngagedPayload, cancellationToken);

      return new KillSwitchChangeResult
      {
        Outcome = KillSwitchChangeOutcome.Applied,
        IsEngaged = true,
        ChangedAtUtc = now
      };
    }

    public async Task<KillSwitchChangeResult> Handle(ReleaseKillSwitch request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      bool isEligible = await hikyaku.Send(new ValidateKillSwitchReleaseEligibility(), cancellationToken);

      KillSwitchState state = await db.KillSwitchStates.FirstOrDefaultAsync(item => item.Id == KillSwitchStateId, cancellationToken);

      if (!isEligible)
      {
        await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.KillSwitchReleaseBlocked, KillSwitchEntityType, null, ReleaseBlockedPayload, cancellationToken);

        return new KillSwitchChangeResult
        {
          Outcome = KillSwitchChangeOutcome.Blocked,
          IsEngaged = true,
          ChangedAtUtc = state != null && state.ChangedAtUtc.HasValue ? state.ChangedAtUtc.Value : now
        };
      }

      if (state == null)
      {
        state = new KillSwitchState { Id = KillSwitchStateId };
        db.KillSwitchStates.Add(state);
      }

      state.IsEngaged = false;
      state.ChangedAtUtc = now;
      state.ChangedByOperatorId = request.OperatorId;
      state.Reason = null;

      await db.SaveChangesAsync(cancellationToken);
      await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.KillSwitchReleased, KillSwitchEntityType, null, ReleasedPayload, cancellationToken);

      return new KillSwitchChangeResult
      {
        Outcome = KillSwitchChangeOutcome.Applied,
        IsEngaged = false,
        ChangedAtUtc = now
      };
    }

    public async Task<bool> Handle(ValidateKillSwitchReleaseEligibility request, CancellationToken cancellationToken)
    {
      TradingAccount account = await db.TradingAccounts.OrderBy(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

      if (account == null)
      {
        return true;
      }

      if (account.ConnectionState == BrokerConnectionState.ReconciliationRequired || account.ConnectionState == BrokerConnectionState.Degraded)
      {
        return false;
      }

      if (account.ConnectionState == BrokerConnectionState.Disconnected)
      {
        return true;
      }

      if (!account.LastReconciledUtc.HasValue)
      {
        return false;
      }

      int freshnessMinutes = configuration.GetValue<int>("Trading:Reconciliation:FreshnessMinutes");
      return account.LastReconciledUtc.Value >= timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-freshnessMinutes);
    }

    private async Task<KillSwitchState> LoadOrCreateStateAsync(CancellationToken cancellationToken)
    {
      KillSwitchState state = await db.KillSwitchStates.FirstOrDefaultAsync(item => item.Id == KillSwitchStateId, cancellationToken);
      if (state != null)
      {
        return state;
      }

      state = new KillSwitchState { Id = KillSwitchStateId };
      db.KillSwitchStates.Add(state);

      return state;
    }
  }
}
