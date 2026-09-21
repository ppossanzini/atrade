using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// Single active-version pointer. This row is the only place that decides which basket version
    /// drives new analysis, which is what keeps the "exactly one active basket" invariant true.
    /// </summary>
    [Table("ActiveBasketVersion")]
    public class ActiveBasketVersion
    {
        [Key]
        public int Id { get; set; }

        public Guid BasketId { get; set; }

        public Guid VersionId { get; set; }

        public DateTime ActivatedAtUtc { get; set; }

        public Guid ActivatedByOperatorId { get; set; }
    }
}
