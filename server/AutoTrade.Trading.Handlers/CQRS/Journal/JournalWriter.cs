using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Journal
{
  /// <summary>
  /// Append-only writer for the functional decision journal.
  /// The journal never updates or deletes events: every entry gets a new unique sequence number.
  /// </summary>
  public interface IJournalWriter
  {
    Task AppendOperatorEventAsync(Guid operatorId, JournalEventKind kind, string entityType, Guid? entityId, string payload, CancellationToken cancellationToken);

    Task AppendSystemEventAsync(JournalEventKind kind, string entityType, Guid? entityId, string payload, CancellationToken cancellationToken);
  }

  public class JournalWriter(DB db, TimeProvider timeProvider) : IJournalWriter
  {
    public Task AppendOperatorEventAsync(Guid operatorId, JournalEventKind kind, string entityType, Guid? entityId, string payload, CancellationToken cancellationToken)
    {
      return AppendAsync(JournalActorType.Operator, operatorId, kind, entityType, entityId, payload, cancellationToken);
    }

    public Task AppendSystemEventAsync(JournalEventKind kind, string entityType, Guid? entityId, string payload, CancellationToken cancellationToken)
    {
      return AppendAsync(JournalActorType.System, null, kind, entityType, entityId, payload, cancellationToken);
    }

    private async Task AppendAsync(JournalActorType actorType, Guid? actorId, JournalEventKind kind, string entityType, Guid? entityId, string payload, CancellationToken cancellationToken)
    {
      long? lastSequence = await db.JournalEvents.Select(item => (long?)item.Sequence).MaxAsync(cancellationToken);

      JournalEvent journalEvent = new JournalEvent
      {
        Id = Guid.CreateVersion7(),
        Sequence = (lastSequence ?? 0L) + 1L,
        CorrelationId = Guid.CreateVersion7(),
        Kind = kind,
        ActorType = actorType,
        ActorId = actorId,
        EntityType = entityType,
        EntityId = entityId,
        Payload = payload,
        OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime
      };

      db.JournalEvents.Add(journalEvent);
      await db.SaveChangesAsync(cancellationToken);
    }
  }
}
