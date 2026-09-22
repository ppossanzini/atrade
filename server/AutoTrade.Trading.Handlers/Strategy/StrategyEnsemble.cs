using System;
using System.Collections.Generic;
using System.Linq;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Strategy
{
    public sealed class StrategyComponentAssessment
    {
        public StrategyComponentType Type { get; init; }
        public int Weight { get; init; }
        public double Score { get; init; }
        public int Direction { get; init; }
        public string Rationale { get; init; }
    }

    public sealed class StrategyEnsembleInput
    {
        public IReadOnlyList<StrategyComponentAssessment> Components { get; init; }
        public int DataQuality { get; init; }
        public int MinimumAgreement { get; init; }
        public int MinimumConfidence { get; init; }
        public StrategyCombinationMode CombinationMode { get; init; }
        public StrategyConflictPolicy ConflictPolicy { get; init; }
    }

    public sealed class StrategyPolicy
    {
        public StrategyCombinationMode CombinationMode { get; init; }
        public int MinimumAgreement { get; init; }
        public int MinimumConfidence { get; init; }
        public StrategyConflictPolicy ConflictPolicy { get; init; }
        public IReadOnlyList<StrategyComponentPolicy> Components { get; init; }
    }

    public sealed class StrategyComponentPolicy
    {
        public StrategyComponentType Type { get; init; }
        public bool Enabled { get; init; }
        public int Weight { get; init; }
        public TimeFrame TimeFrame { get; init; }
    }

    public sealed class StrategyEnsembleResult
    {
        public int Direction { get; init; }
        public int Confidence { get; init; }
        public int Agreement { get; init; }
        public bool IsNoTrade { get; init; }
        public IReadOnlyList<StrategyComponentAssessment> Components { get; init; }
        public string Reason { get; init; }
    }

    /// <summary>
    /// Combines already measured strategy opinions. It never reads the clock, market or broker and never
    /// authorizes an order. An LLM can later explain or rank this result, but cannot bypass these bounds.
    /// </summary>
    public sealed class StrategyEnsemble
    {
        public StrategyEnsembleResult Evaluate(StrategyEnsembleInput input)
        {
            if (input == null || input.Components == null || input.Components.Count == 0)
            {
                return NoTrade("No strategy components are configured.");
            }

            List<StrategyComponentAssessment> active = input.Components
              .Where(item => item != null && item.Direction != 0)
              .ToList();

            if (active.Count == 0)
            {
                return NoTrade("No component produced an actionable direction.");
            }

            double positive = active.Where(item => item.Direction > 0).Sum(item => Math.Abs(item.Score) * EffectiveWeight(item));
            double negative = active.Where(item => item.Direction < 0).Sum(item => Math.Abs(item.Score) * EffectiveWeight(item));
            double total = positive + negative;
            int direction = positive == negative ? 0 : positive > negative ? 1 : -1;
            int agreement = total <= 0 ? 0 : (int)Math.Round(Math.Max(positive, negative) / total * 100, MidpointRounding.AwayFromZero);
            double weightTotal = active.Sum(EffectiveWeight);
            int componentConfidence = weightTotal <= 0
              ? 0
              : (int)Math.Round(active.Sum(item => Math.Min(1d, Math.Abs(item.Score)) * EffectiveWeight(item)) / weightTotal * 100, MidpointRounding.AwayFromZero);
            int confidence = Math.Max(0, Math.Min(100, (input.DataQuality + componentConfidence + agreement) / 3));

            bool consensusFailure = input.CombinationMode == StrategyCombinationMode.Consensus && active.Any(item => item.Direction != direction);
            bool regimeFailure = input.CombinationMode == StrategyCombinationMode.RegimeGated && active.All(item => item.Type != StrategyComponentType.TrendFollowing || item.Direction != direction);

            if (direction == 0 || agreement < input.MinimumAgreement || confidence < input.MinimumConfidence || consensusFailure || regimeFailure)
            {
                return new StrategyEnsembleResult
                {
                    Direction = 0,
                    Confidence = confidence,
                    Agreement = agreement,
                    IsNoTrade = true,
                    Components = active,
                    Reason = "The configured agreement or confidence threshold was not reached."
                };
            }

            return new StrategyEnsembleResult
            {
                Direction = direction,
                Confidence = confidence,
                Agreement = agreement,
                IsNoTrade = false,
                Components = active,
                Reason = "The weighted component ensemble reached its configured thresholds."
            };
        }

        private static double EffectiveWeight(StrategyComponentAssessment component)
        {
            return component.Weight > 0 ? component.Weight : 1;
        }

        private static StrategyEnsembleResult NoTrade(string reason)
        {
            return new StrategyEnsembleResult
            {
                Direction = 0,
                Confidence = 0,
                Agreement = 0,
                IsNoTrade = true,
                Components = Array.Empty<StrategyComponentAssessment>(),
                Reason = reason
            };
        }
    }
}
