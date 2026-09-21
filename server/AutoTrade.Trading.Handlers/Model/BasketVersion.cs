using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// Immutable published snapshot. Once <see cref="PublishedAtUtc"/> is set the row is never
    /// updated: composition and policy are copied into the version tables.
    /// </summary>
    [Table("BasketVersion")]
    public class BasketVersion
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BasketId { get; set; }

        public int Number { get; set; }

        [StringLength(256)]
        public string Note { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public Guid CreatedByOperatorId { get; set; }

        public DateTime PublishedAtUtc { get; set; }
    }
}
