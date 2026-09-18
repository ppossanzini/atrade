using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Outcome of a risk evaluation. The aggregate verdict is derived from the gates, never set directly,
  /// so it can always be explained by the list that accompanies it.
  /// </summary>
  public class RiskDecisionDto
  {
    public RiskGateVerdict Verdict { get; set; }

    public DateTime EvaluatedAtUtc { get; set; }

    public Guid? BasketId { get; set; }

    public Guid? BasketVersionId { get; set; }

    public int VersionNumber { get; set; }

    public DateTime? SnapshotCapturedAtUtc { get; set; }

    public List<RiskGateResultDto> Gates { get; set; }
  }
}
