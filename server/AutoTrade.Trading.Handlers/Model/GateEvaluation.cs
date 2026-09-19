using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// One gate of the evaluation a proposal was routed on. Append-only like the journal: a re-evaluation
  /// adds rows, it never rewrites what the proposal was actually judged by.
  /// </summary>
  [Table("GateEvaluation")]
  public class GateEvaluation
  {
    [Key]
    public Guid Id { get; set; }

    public Guid ProposalId { get; set; }

    public int Ordinal { get; set; }

    public RiskGateCode Code { get; set; }

    public RiskGateVerdict Verdict { get; set; }

    [StringLength(32)]
    public string Subject { get; set; }

    public MarketKind? Market { get; set; }

    public double? ObservedValue { get; set; }

    public double? ThresholdValue { get; set; }

    [StringLength(16)]
    public string Unit { get; set; }

    public DateTime EvaluatedAtUtc { get; set; }

    [StringLength(512)]
    public string Detail { get; set; }
  }
}
