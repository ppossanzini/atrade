using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// One evaluated gate. AC-08 requires code, observed value, threshold and timestamp on every gate, so
  /// those fields are mandatory in this contract and the client never has to invent a missing one.
  /// </summary>
  public class RiskGateResultDto
  {
    public RiskGateCode Code { get; set; }

    public RiskGateVerdict Verdict { get; set; }

    /// <summary>What the gate is about: the basket or a specific leg symbol.</summary>
    public string Subject { get; set; }

    /// <summary>Market the limit belongs to. Null for gates that judge the basket as a whole.</summary>
    public MarketKind? Market { get; set; }

    public double? ObservedValue { get; set; }

    public double? ThresholdValue { get; set; }

    public string Unit { get; set; }

    public DateTime EvaluatedAtUtc { get; set; }

    public string Detail { get; set; }
  }
}
