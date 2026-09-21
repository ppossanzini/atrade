using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>Who the episode is about, and what was being attempted.</summary>
  public class EpisodeProposalFacts
  {
    public Guid ProposalId { get; set; }

    public int VersionNumber { get; set; }

    public string EntryMode { get; set; }

    public string Action { get; set; }

    public double Confidence { get; set; }
  }

  /// <summary>
  /// Builds an episode from facts the system already evaluated, without reading anything back.
  /// </summary>
  /// <remarks>
  /// The situation is reconstructed from the gate evaluations themselves rather than from the raw market
  /// capture. That is deliberate: the gate is what actually judged those numbers, so an episode quoting the
  /// gates cannot disagree with the verdict it is describing.
  /// </remarks>
  public static class OperationalEpisodeBuilder
  {
    public static OperationalEpisode GateBlocked(EpisodeProposalFacts facts, IReadOnlyList<RiskGateResultDto> gates, DateTime occurredAtUtc)
    {
      if (facts == null)
      {
        throw new ArgumentNullException(nameof(facts));
      }

      if (gates == null)
      {
        throw new ArgumentNullException(nameof(gates));
      }

      OperationalEpisodeOutcome outcome = null;

      foreach (RiskGateResultDto gate in gates)
      {
        if (gate.Verdict != RiskGateVerdict.Block)
        {
          continue;
        }

        // The first blocker in evaluation order is reported as the deciding rule. The others stay in the
        // transactional record: repeating them here would make the memory claim a cause it did not establish.
        outcome = new OperationalEpisodeOutcome
        {
          Code = gate.Code.ToString(),
          Subject = gate.Subject,
          ObservedValue = gate.ObservedValue,
          ThresholdValue = gate.ThresholdValue,
          Unit = gate.Unit,
          Detail = gate.Detail
        };

        break;
      }

      if (outcome == null)
      {
        throw new InvalidOperationException("A blocked proposal has to carry at least one blocking gate: without it there is nothing to remember.");
      }

      OperationalEpisodeContext context = new OperationalEpisodeContext
      {
        VersionNumber = facts.VersionNumber,
        EntryMode = facts.EntryMode,
        Action = facts.Action,
        Confidence = facts.Confidence,
        Legs = ReadLegs(gates)
      };

      foreach (RiskGateResultDto gate in gates)
      {
        switch (gate.Code)
        {
          case RiskGateCode.CoverageBelowMinimum:
            context.CoveragePercent = gate.ObservedValue ?? 0d;
            context.MinimumCoveragePercent = gate.ThresholdValue ?? 0d;
            break;
          case RiskGateCode.SnapshotStale:
            context.SnapshotAgeSeconds = gate.ObservedValue ?? 0d;
            break;
          case RiskGateCode.DailyLossExceeded:
            context.DailyLossPercent = gate.ObservedValue ?? 0d;
            context.DailyLossLimitPercent = gate.ThresholdValue ?? 0d;
            break;
        }
      }

      return OperationalEpisodeRenderer.Create(
        OperationalEpisodeKind.GateBlocked,
        "proposal:" + facts.ProposalId,
        occurredAtUtc,
        context,
        outcome);
    }

    /// <summary>
    /// Rebuilds one leg per symbol, keeping evaluation order so the same run always produces the same text.
    /// </summary>
    private static List<OperationalEpisodeLeg> ReadLegs(IReadOnlyList<RiskGateResultDto> gates)
    {
      List<OperationalEpisodeLeg> legs = new List<OperationalEpisodeLeg>();

      foreach (RiskGateResultDto gate in gates)
      {
        if (string.IsNullOrWhiteSpace(gate.Subject) || gate.Subject == "basket")
        {
          continue;
        }

        if (gate.Code != RiskGateCode.LegSpreadExceeded && gate.Code != RiskGateCode.LegVolatilityExceeded)
        {
          continue;
        }

        OperationalEpisodeLeg leg = Find(legs, gate.Subject);

        if (gate.Code == RiskGateCode.LegSpreadExceeded)
        {
          leg.SpreadPips = gate.ObservedValue ?? 0d;
          leg.SpreadLimitPips = gate.ThresholdValue ?? 0d;
        }
        else
        {
          leg.VolatilityPercent = gate.ObservedValue ?? 0d;
          leg.VolatilityLimitPercent = gate.ThresholdValue ?? 0d;
        }
      }

      return legs;
    }

    private static OperationalEpisodeLeg Find(List<OperationalEpisodeLeg> legs, string symbol)
    {
      foreach (OperationalEpisodeLeg leg in legs)
      {
        if (string.Equals(leg.Symbol, symbol, StringComparison.Ordinal))
        {
          return leg;
        }
      }

      OperationalEpisodeLeg created = new OperationalEpisodeLeg { Symbol = symbol };
      legs.Add(created);

      return created;
    }
  }
}
