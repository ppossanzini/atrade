using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    [Table("JournalEvent")]
    public class JournalEvent
    {
        [Key]
        public Guid Id { get; set; }

        public long Sequence { get; set; }

        public Guid CorrelationId { get; set; }

        public JournalEventKind Kind { get; set; }

        public JournalActorType ActorType { get; set; }

        public Guid? ActorId { get; set; }

        [StringLength(64)]
        public string EntityType { get; set; }

        public Guid? EntityId { get; set; }

        [StringLength(2048)]
        public string Payload { get; set; }

        public DateTime OccurredAtUtc { get; set; }
    }
}
