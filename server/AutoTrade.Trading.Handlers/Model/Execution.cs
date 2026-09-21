using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// One execution of one proposal. Rows are written before anything is sent, and the status only moves
    /// forward: an execution that needs reconciliation stays visible as such instead of looking finished.
    /// </summary>
    [Table("Execution")]
    public class Execution
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// One proposal, one execution: enforced by a unique index. Null for a compensation, which is a new
        /// execution of an existing one and has no proposal of its own.
        /// </summary>
        public Guid? ProposalId { get; set; }

        public Guid BasketId { get; set; }

        public Guid BasketVersionId { get; set; }

        public int VersionNumber { get; set; }

        public Guid? SnapshotId { get; set; }

        public ExecutionStatus Status { get; set; }

        public FailurePolicy FailurePolicy { get; set; }

        public int MinimumCoverage { get; set; }

        /// <summary>Filled volume as a percentage of the planned volume, recomputed from the legs.</summary>
        public int Coverage { get; set; }

        /// <summary>Set when this execution compensates another one. A compensation is a new execution.</summary>
        public Guid? CompensationOfExecutionId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }

    /// <summary>
    /// One leg of an execution. The client order id is the idempotency key: it is generated and persisted
    /// before the send, so a repeated send cannot create a second order.
    /// </summary>
    [Table("ExecutionLeg")]
    public class ExecutionLeg
    {
        [Key]
        public Guid Id { get; set; }

        public Guid ExecutionId { get; set; }

        public int Ordinal { get; set; }

        [Required]
        [StringLength(32)]
        public string Symbol { get; set; }

        public MarketKind Market { get; set; }

        public LegDirection Direction { get; set; }

        /// <summary>Volume in broker units, already validated against the minimum and the step.</summary>
        public int VolumeUnits { get; set; }

        [Required]
        [StringLength(64)]
        public string ClientOrderId { get; set; }

        public ExecutionLegStatus Status { get; set; }

        [StringLength(64)]
        public string BrokerOrderId { get; set; }

        public int FilledVolumeUnits { get; set; }

        public double? AveragePrice { get; set; }

        [StringLength(64)]
        public string ErrorCode { get; set; }

        public DateTime? LastEventAtUtc { get; set; }
    }
}
