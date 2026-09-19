using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// One row of the execution queue. Coverage is measured, not assumed: it is the share of the planned
  /// volume that was actually filled.
  /// </summary>
  public class ExecutionSummaryDto
  {
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// The proposal this execution came from. Null for a compensation, which closes exposure nobody proposed
    /// as a new order and therefore belongs to no proposal.
    /// </summary>
    public Guid? ProposalId { get; set; }

    public Guid BasketId { get; set; }

    public string BasketName { get; set; }

    public int VersionNumber { get; set; }

    public ExecutionStatus Status { get; set; }

    public FailurePolicy FailurePolicy { get; set; }

    public int MinimumCoverage { get; set; }

    /// <summary>Filled volume as a percentage of the planned volume.</summary>
    public int Coverage { get; set; }

    public int LegCount { get; set; }

    public int FilledLegCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>True when the residual exposure needs an operator decision.</summary>
    public bool NeedsCompensation { get; set; }
  }

  public class ExecutionLegDto
  {
    public Guid LegId { get; set; }

    public int Ordinal { get; set; }

    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public LegDirection Direction { get; set; }

    public int VolumeUnits { get; set; }

    public int FilledVolumeUnits { get; set; }

    public string ClientOrderId { get; set; }

    public string BrokerOrderId { get; set; }

    public ExecutionLegStatus Status { get; set; }

    public double? AveragePrice { get; set; }

    public string ErrorCode { get; set; }

    public DateTime? LastEventAtUtc { get; set; }
  }

  /// <summary>What the broker reported, once, deduplicated by its own identity.</summary>
  public class ExecutionEventDto
  {
    public string BrokerEventId { get; set; }

    public ExecutionEventKind Kind { get; set; }

    public string Symbol { get; set; }

    public string Payload { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
  }

  public class ExecutionDetailDto : ExecutionSummaryDto
  {
    public Guid? SnapshotId { get; set; }

    public Guid? CompensationOfExecutionId { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public List<ExecutionLegDto> Legs { get; set; }

    public List<ExecutionEventDto> Events { get; set; }
  }

  public class ExecutionStartResultDto
  {
    public Guid ExecutionId { get; set; }

    public ExecutionOutcome Outcome { get; set; }

    public ExecutionStatus Status { get; set; }

    /// <summary>Why the start was refused, when that is the case.</summary>
    public string Reason { get; set; }
  }
}
