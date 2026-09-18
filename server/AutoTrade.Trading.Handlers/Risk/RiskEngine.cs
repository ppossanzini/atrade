using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// Deterministic risk engine.
  ///
  /// It is a pure function of its input plus the configured thresholds: no database, no clock reads other
  /// than the injected one, no LLM. Every rule emits a gate carrying code, observed value, threshold and
  /// timestamp, and any input it cannot judge produces a blocking gate rather than a permissive default.
  /// </summary>
  public class RiskEngine(RiskThresholds thresholds, TimeProvider timeProvider)
  {
    private const string BasketSubject = "basket";
    private const string CoverageUnit = "percent";
    private const string PercentUnit = "percent";
    private const string PipsUnit = "pips";

    public RiskDecisionDto Evaluate(RiskEvaluationInput input)
    {
      if (input == null)
      {
        throw new ArgumentNullException(nameof(input));
      }

      DateTime now = timeProvider.GetUtcNow().UtcDateTime;
      List<RiskGateResultDto> gates = new List<RiskGateResultDto>();

      gates.Add(EvaluateActiveVersion(input, now));
      gates.Add(EvaluateKillSwitch(input, now));

      // Without a snapshot nothing downstream can be judged: the state gates are still reported, and a
      // single explicit gate records why the rest is missing.
      if (!input.MarketDataCapturedAtUtc.HasValue)
      {
        gates.Add(CreateGate(RiskGateCode.SnapshotMissing, RiskGateVerdict.Block, BasketSubject, null, null, null, now, "No market snapshot is available for this version."));

        return BuildDecision(input, gates, now);
      }

      gates.Add(EvaluateSnapshotAge(input, now));

      gates.AddRange(EvaluateCoverage(input, now));
      gates.Add(EvaluateBasketRisk(input, now));
      gates.Add(EvaluateDailyLoss(input, now));
      gates.AddRange(EvaluateLegs(input, now));

      return BuildDecision(input, gates, now);
    }

    private static RiskGateResultDto EvaluateActiveVersion(RiskEvaluationInput input, DateTime now)
    {
      return input.HasActiveVersion
        ? CreateGate(RiskGateCode.ActiveVersionMissing, RiskGateVerdict.Allow, BasketSubject, input.VersionNumber, null, null, now, "An active version is in force.")
        : CreateGate(RiskGateCode.ActiveVersionMissing, RiskGateVerdict.Block, BasketSubject, null, null, null, now, "This basket has no active version.");
    }

    private static RiskGateResultDto EvaluateKillSwitch(RiskEvaluationInput input, DateTime now)
    {
      return input.KillSwitchEngaged
        ? CreateGate(RiskGateCode.KillSwitchEngaged, RiskGateVerdict.Block, BasketSubject, null, null, null, now, "The kill switch is engaged.")
        : CreateGate(RiskGateCode.KillSwitchEngaged, RiskGateVerdict.Allow, BasketSubject, null, null, null, now, "The kill switch is released.");
    }

    private RiskGateResultDto EvaluateSnapshotAge(RiskEvaluationInput input, DateTime now)
    {
      if (!thresholds.SnapshotMaxAgeSeconds.HasValue)
      {
        return CreateGate(
          RiskGateCode.ThresholdNotConfigured,
          RiskGateVerdict.Block,
          "SnapshotMaxAgeSeconds",
          null,
          null,
          "seconds",
          now,
          "The snapshot validity window is not configured, so freshness cannot be judged.");
      }

      double ageSeconds = (now - input.MarketDataCapturedAtUtc.Value).TotalSeconds;
      double limit = thresholds.SnapshotMaxAgeSeconds.Value;

      return ageSeconds > limit
        ? CreateGate(RiskGateCode.SnapshotStale, RiskGateVerdict.Block, BasketSubject, Math.Round(ageSeconds, 3), limit, "seconds", now, "The market snapshot is older than the configured validity window.")
        : CreateGate(RiskGateCode.SnapshotStale, RiskGateVerdict.Allow, BasketSubject, Math.Round(ageSeconds, 3), limit, "seconds", now, "The market snapshot is within the validity window.");
    }

    /// <summary>
    /// Coverage is the share of the selected weight that can actually be executed. The failure policy
    /// decides whether a partial coverage blocks or requires a confirmation; it never turns a shortfall
    /// into an implicit permission.
    /// </summary>
    private static List<RiskGateResultDto> EvaluateCoverage(RiskEvaluationInput input, DateTime now)
    {
      double coverage = 0;
      bool hasLegs = input.Legs != null && input.Legs.Count > 0;

      if (hasLegs)
      {
        foreach (RiskEvaluationLeg leg in input.Legs)
        {
          if (leg.IsExecutable)
          {
            coverage += leg.Weight;
          }
        }
      }

      if (!hasLegs)
      {
        return new List<RiskGateResultDto>
        {
          CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Block, BasketSubject, null, input.MinimumCoverage, CoverageUnit, now, "The active version has no legs to evaluate.")
        };
      }

      if (coverage >= 100)
      {
        return new List<RiskGateResultDto>
        {
          CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Allow, BasketSubject, coverage, 100, CoverageUnit, now, "Every selected leg is executable.")
        };
      }

      if (input.FailurePolicy == FailurePolicy.AllOrNothing)
      {
        return new List<RiskGateResultDto>
        {
          CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Block, BasketSubject, coverage, 100, CoverageUnit, now, "The version requires all legs; partial execution is not allowed.")
        };
      }

      if (coverage < input.MinimumCoverage)
      {
        return new List<RiskGateResultDto>
        {
          CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Block, BasketSubject, coverage, input.MinimumCoverage, CoverageUnit, now, "Coverage is below the minimum required by the policy.")
        };
      }

      if (input.FailurePolicy == FailurePolicy.RequireConfirmation)
      {
        return new List<RiskGateResultDto>
        {
          CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Review, BasketSubject, coverage, 100, CoverageUnit, now, "Coverage is partial and the policy requires an explicit confirmation.")
        };
      }

      return new List<RiskGateResultDto>
      {
        CreateGate(RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Allow, BasketSubject, coverage, input.MinimumCoverage, CoverageUnit, now, "Coverage is at or above the minimum required by the policy.")
      };
    }

    private static RiskGateResultDto EvaluateBasketRisk(RiskEvaluationInput input, DateTime now)
    {
      if (!input.BasketRiskPercent.HasValue)
      {
        return CreateGate(RiskGateCode.BasketDataMissing, RiskGateVerdict.Block, "BasketRiskPercent", null, input.RiskPerBasketLimit, PercentUnit, now, "The current basket risk cannot be computed.");
      }

      double observed = input.BasketRiskPercent.Value;

      return observed > input.RiskPerBasketLimit
        ? CreateGate(RiskGateCode.RiskPerBasketExceeded, RiskGateVerdict.Block, BasketSubject, observed, input.RiskPerBasketLimit, PercentUnit, now, "The basket risk exceeds the risk per basket limit.")
        : CreateGate(RiskGateCode.RiskPerBasketExceeded, RiskGateVerdict.Allow, BasketSubject, observed, input.RiskPerBasketLimit, PercentUnit, now, "The basket risk is within the limit.");
    }

    private static RiskGateResultDto EvaluateDailyLoss(RiskEvaluationInput input, DateTime now)
    {
      if (!input.DailyLossPercent.HasValue)
      {
        return CreateGate(RiskGateCode.BasketDataMissing, RiskGateVerdict.Block, "DailyLossPercent", null, input.DailyLossLimit, PercentUnit, now, "The daily loss cannot be computed.");
      }

      double observed = input.DailyLossPercent.Value;

      return observed > input.DailyLossLimit
        ? CreateGate(RiskGateCode.DailyLossExceeded, RiskGateVerdict.Block, BasketSubject, observed, input.DailyLossLimit, PercentUnit, now, "The daily loss exceeds the limit set by the policy.")
        : CreateGate(RiskGateCode.DailyLossExceeded, RiskGateVerdict.Allow, BasketSubject, observed, input.DailyLossLimit, PercentUnit, now, "The daily loss is within the limit.");
    }

    private List<RiskGateResultDto> EvaluateLegs(RiskEvaluationInput input, DateTime now)
    {
      List<RiskGateResultDto> gates = new List<RiskGateResultDto>();

      foreach (RiskEvaluationLeg leg in input.Legs)
      {
        gates.Add(EvaluateLegSpread(leg, now));
        gates.Add(EvaluateLegVolatility(leg, now));
      }

      return gates;
    }

    private RiskGateResultDto EvaluateLegSpread(RiskEvaluationLeg leg, DateTime now)
    {
      MarketLegLimits limits = thresholds.ForMarket(leg.Market);

      if (!leg.SpreadPips.HasValue)
      {
        return CreateGate(RiskGateCode.LegDataMissing, RiskGateVerdict.Block, leg.Symbol, leg.Market, null, limits.LegSpreadMaxPips, PipsUnit, now, "The spread is not available for this leg.");
      }

      if (!limits.LegSpreadMaxPips.HasValue)
      {
        return CreateGate(RiskGateCode.ThresholdNotConfigured, RiskGateVerdict.Block, leg.Symbol, leg.Market, leg.SpreadPips, null, PipsUnit, now, "The maximum spread for market " + leg.Market + " is not configured.");
      }

      double observed = leg.SpreadPips.Value;
      double limit = limits.LegSpreadMaxPips.Value;

      return observed > limit
        ? CreateGate(RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Block, leg.Symbol, leg.Market, observed, limit, PipsUnit, now, "The spread exceeds the limit configured for market " + leg.Market + ".")
        : CreateGate(RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Allow, leg.Symbol, leg.Market, observed, limit, PipsUnit, now, "The spread is within the limit configured for market " + leg.Market + ".");
    }

    private RiskGateResultDto EvaluateLegVolatility(RiskEvaluationLeg leg, DateTime now)
    {
      MarketLegLimits limits = thresholds.ForMarket(leg.Market);

      if (!leg.VolatilityPercent.HasValue)
      {
        return CreateGate(RiskGateCode.LegDataMissing, RiskGateVerdict.Block, leg.Symbol, leg.Market, null, limits.LegVolatilityMaxPercent, PercentUnit, now, "The volatility is not available for this leg.");
      }

      if (!limits.LegVolatilityMaxPercent.HasValue)
      {
        return CreateGate(RiskGateCode.ThresholdNotConfigured, RiskGateVerdict.Block, leg.Symbol, leg.Market, leg.VolatilityPercent, null, PercentUnit, now, "The maximum volatility for market " + leg.Market + " is not configured.");
      }

      double observed = leg.VolatilityPercent.Value;
      double limit = limits.LegVolatilityMaxPercent.Value;

      return observed > limit
        ? CreateGate(RiskGateCode.LegVolatilityExceeded, RiskGateVerdict.Block, leg.Symbol, leg.Market, observed, limit, PercentUnit, now, "The volatility exceeds the limit configured for market " + leg.Market + ".")
        : CreateGate(RiskGateCode.LegVolatilityExceeded, RiskGateVerdict.Allow, leg.Symbol, leg.Market, observed, limit, PercentUnit, now, "The volatility is within the limit configured for market " + leg.Market + ".");
    }

    /// <summary>Blocking wins over review, review wins over allow: the aggregate is derived, never chosen.</summary>
    private static RiskDecisionDto BuildDecision(RiskEvaluationInput input, List<RiskGateResultDto> gates, DateTime now)
    {
      RiskGateVerdict verdict = RiskGateVerdict.Allow;

      foreach (RiskGateResultDto gate in gates)
      {
        if (gate.Verdict == RiskGateVerdict.Block)
        {
          verdict = RiskGateVerdict.Block;

          break;
        }

        if (gate.Verdict == RiskGateVerdict.Review)
        {
          verdict = RiskGateVerdict.Review;
        }
      }

      return new RiskDecisionDto
      {
        Verdict = verdict,
        EvaluatedAtUtc = now,
        VersionNumber = input.VersionNumber,
        SnapshotCapturedAtUtc = input.MarketDataCapturedAtUtc,
        Gates = gates
      };
    }

    private static RiskGateResultDto CreateGate(RiskGateCode code, RiskGateVerdict verdict, string subject, double? observedValue, double? thresholdValue, string unit, DateTime now, string detail)
    {
      return CreateGate(code, verdict, subject, null, observedValue, thresholdValue, unit, now, detail);
    }

    /// <summary>
    /// Market is set only by the leg gates: the basket gates judge the basket as a whole, and reporting a
    /// market on them would suggest a limit that was never applied.
    /// </summary>
    private static RiskGateResultDto CreateGate(RiskGateCode code, RiskGateVerdict verdict, string subject, MarketKind? market, double? observedValue, double? thresholdValue, string unit, DateTime now, string detail)
    {
      return new RiskGateResultDto
      {
        Code = code,
        Verdict = verdict,
        Subject = subject,
        Market = market,
        ObservedValue = observedValue,
        ThresholdValue = thresholdValue,
        Unit = unit,
        EvaluatedAtUtc = now,
        Detail = detail
      };
    }
  }
}
