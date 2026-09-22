using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// One proposal of the active version. It points at the snapshot it was judged on, so the input of the
    /// decision is reproducible and the proposal is not a claim about a market nobody recorded.
    /// </summary>
    [Table("Proposal")]
    public class Proposal
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BasketId { get; set; }

        public Guid BasketVersionId { get; set; }

        public int VersionNumber { get; set; }

        public Guid? SnapshotId { get; set; }

        public ProposalAction Action { get; set; }

        /// <summary>Entry rule declared by the version the proposal was built from.</summary>
        public EntryMode EntryMode { get; set; }

        /// <summary>Aggregate verdict of the gate at generation.</summary>
        public RiskGateVerdict Gate { get; set; }

        public ProposalStatus Status { get; set; }

        /// <summary>Informational confidence of the source, 0 when the source cannot express one.</summary>
        public int Confidence { get; set; }

        public int StrategyAgreement { get; set; }

        public int? LlmConfidence { get; set; }

        public double ExpectedRiskPercent { get; set; }

        public DateTime ProposedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public Guid? DecidedByOperatorId { get; set; }

        [StringLength(512)]
        public string DecisionReason { get; set; }

        /// <summary>Language neutral summary of what the source judged. Audit material, not UI copy.</summary>
        [StringLength(512)]
        public string Rationale { get; set; }

        [StringLength(1024)]
        public string LlmRationale { get; set; }

        [StringLength(128)]
        public string SelectedScenario { get; set; }

        public int CycleSequence { get; set; }
    }

    /// <summary>Frozen leg of a proposal, taken from the version that was active when it was generated.</summary>
    [Table("ProposalLeg")]
    public class ProposalLeg
    {
        [Key]
        public Guid Id { get; set; }

        public Guid ProposalId { get; set; }

        public int Ordinal { get; set; }

        [Required]
        [StringLength(32)]
        public string Symbol { get; set; }

        public MarketKind Market { get; set; }

        public LegDirection Direction { get; set; }

        public int Weight { get; set; }

        public double RiskCap { get; set; }

        /// <summary>Stop distance of the leg, copied from the version the proposal was built from.</summary>
        public double StopDistancePips { get; set; }

        /// <summary>Spread limit of the leg, copied from the version the proposal was built from.</summary>
        public double MaxSpreadPips { get; set; }

        /// <summary>Volatility limit of the leg, copied from the version the proposal was built from.</summary>
        public double MaxVolatilityPercent { get; set; }
    }
}
