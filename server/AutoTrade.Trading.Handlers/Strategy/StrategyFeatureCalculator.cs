using System;
using System.Collections.Generic;
using System.Linq;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;

namespace AutoTrade.Trading.Handlers.Strategy
{
    public static class StrategyFeatureCalculator
    {
        public static StrategyComponentAssessment Evaluate(StrategyComponentType type, SymbolCapture capture)
        {
            if (capture == null || capture.Bars == null || capture.Bars.Count < 20)
            {
                return new StrategyComponentAssessment { Type = type, Weight = 1, Score = 0, Direction = 0, Rationale = "Insufficient closed bars." };
            }

            List<MarketBar> bars = capture.Bars.Where(IsValid).OrderBy(item => item.ClosedAtUtc).ToList();
            if (bars.Count < 20)
            {
                return new StrategyComponentAssessment { Type = type, Weight = 1, Score = 0, Direction = 0, Rationale = "Closed bars are incomplete or invalid." };
            }

            double last = bars[^1].Close;
            double first = bars[0].Close;
            double change = first == 0 ? 0 : (last - first) / first;
            int direction = change > 0 ? 1 : change < 0 ? -1 : 0;
            double strength = Math.Min(1, Math.Abs(change) * 100);

            if (type == StrategyComponentType.MeanReversion)
            {
                direction = -direction;
                strength = Math.Min(1, Math.Abs(change) * 120);
            }

            if (type == StrategyComponentType.VolatilityFilter)
            {
                double range = bars.Average(item => item.High - item.Low);
                strength = capture.Price.GetValueOrDefault() > 0 ? Math.Min(1, range / capture.Price.Value * 1000) : 0;
                direction = strength > 0.2 ? 1 : 0;
            }

            return new StrategyComponentAssessment
            {
                Type = type,
                Weight = 1,
                Score = strength,
                Direction = direction,
                Rationale = "Calculated from " + bars.Count + " closed M15 bars."
            };
        }

        private static bool IsValid(MarketBar bar)
        {
            return bar != null && bar.Close > 0 && bar.Open > 0 && bar.High >= Math.Max(bar.Open, bar.Close) && bar.Low <= Math.Min(bar.Open, bar.Close);
        }
    }
}
