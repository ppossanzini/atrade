using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// One row of the operator queue. It carries what routing decided and what the gate saw, so the queue
    /// is readable without opening every proposal.
    /// </summary>
    public class ProposalSummaryDto
    {
        public Guid ProposalId { get; set; }

        public Guid BasketId { get; set; }

        public string BasketName { get; set; }

        public int VersionNumber { get; set; }

        public ProposalAction Action { get; set; }

        /// <summary>Entry rule the strategy was following when the proposal was produced.</summary>
        public EntryMode EntryMode { get; set; }

        public ProposalStatus Status { get; set; }

        public RiskGateVerdict Gate { get; set; }

        /// <summary>How far the gate was from the thresholds it applied, as an informational percentage.</summary>
        public int Confidence { get; set; }

        public double ExpectedRiskPercent { get; set; }

        public DateTime ProposedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>
        /// Whether the operator can decide it now. It depends on the current mode, so it is computed per read
        /// instead of being stored: a proposal does not become decidable because it was stored that way.
        /// </summary>
        public bool IsDecidable { get; set; }
    }

    public class ProposalLegDto
    {
        public string Symbol { get; set; }

        public LegDirection Direction { get; set; }

        public int Weight { get; set; }

        public double RiskCap { get; set; }

        /// <summary>Stop distance of the leg, as frozen in the version the proposal was built from.</summary>
        public double StopDistancePips { get; set; }

        /// <summary>Spread limit of the leg, frozen with the version. Zero means not decided.</summary>
        public double MaxSpreadPips { get; set; }

        /// <summary>Volatility limit of the leg, frozen with the version. Zero means not decided.</summary>
        public double MaxVolatilityPercent { get; set; }
    }

    /// <summary>
    /// Full proposal, including the gate evaluations it was routed on and the snapshot identity of its input.
    /// </summary>
    public class ProposalDetailDto : ProposalSummaryDto
    {
        public Guid? SnapshotId { get; set; }

        public DateTime? SnapshotCapturedAtUtc { get; set; }

        public int CycleSequence { get; set; }

        /// <summary>Language neutral summary of the input the gate judged, filled by the proposal source.</summary>
        public string Rationale { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public Guid? DecidedByOperatorId { get; set; }

        public string DecisionReason { get; set; }

        public List<ProposalLegDto> Legs { get; set; }

        public List<RiskGateResultDto> Gates { get; set; }
    }

    public class ProposalDecisionResultDto
    {
        public Guid ProposalId { get; set; }

        public ProposalDecisionOutcome Outcome { get; set; }

        public ProposalStatus Status { get; set; }

        public DateTime DecidedAtUtc { get; set; }
    }

    /// <summary>Outcome of one analysis cycle, so the caller knows whether anything was proposed.</summary>
    public class AnalysisCycleResultDto
    {
        public bool Proposed { get; set; }

        public int CycleSequence { get; set; }

        public ProposalStatus? Status { get; set; }

        public RiskGateVerdict? Gate { get; set; }

        /// <summary>Why no proposal was created, when that is the case.</summary>
        public string Reason { get; set; }
    }
}
