using System;
using System.Collections.Generic;
using AutoTrade.Trading.Handlers.MarketData;

namespace AutoTrade.Trading.Handlers.Risk
{
    /// <summary>
    /// Turns stored state plus one market capture into the engine input. It is the only place where the two
    /// are combined, and it is deterministic: the same state and the same capture always produce the same
    /// input, which is what makes a decision reproducible and explainable.
    ///
    /// Absence is preserved instead of being filled in. When the capture is unavailable, every market and
    /// account value stays null and every leg stays non executable, so the engine blocks; when a symbol is
    /// not quoted, only that leg loses its values and the rest of the basket is still judged.
    /// </summary>
    public static class RiskInputFactory
    {
        public static RiskEvaluationInput Create(RiskCandidate candidate, MarketDataCapture capture)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            RiskEvaluationInput input = new RiskEvaluationInput
            {
                HasActiveVersion = candidate.HasActiveVersion,
                VersionNumber = candidate.VersionNumber,
                KillSwitchEngaged = candidate.KillSwitchEngaged,
                FailurePolicy = candidate.FailurePolicy,
                MinimumCoverage = candidate.MinimumCoverage,
                RiskPerBasketLimit = candidate.RiskPerBasketLimit,
                DailyLossLimit = candidate.DailyLossLimit,
                MarketDataCapturedAtUtc = null,
                BasketRiskPercent = null,
                DailyLossPercent = null,
                Legs = new List<RiskEvaluationLeg>()
            };

            bool hasCapture = capture != null && capture.IsAvailable && capture.CapturedAtUtc.HasValue;

            if (!hasCapture)
            {
                input.Legs.AddRange(CreateUnjudgedLegs(candidate));

                return input;
            }

            input.MarketDataCapturedAtUtc = capture.CapturedAtUtc;

            if (candidate.Legs != null)
            {
                foreach (RiskCandidateLeg leg in candidate.Legs)
                {
                    SymbolCapture quote = FindSymbol(capture, leg.Symbol);

                    input.Legs.Add(new RiskEvaluationLeg
                    {
                        Symbol = leg.Symbol,
                        Market = leg.Market,
                        Weight = leg.Weight,

                        // A leg is executable only when the source says it is: an unquoted symbol is not an
                        // executable leg, it is a leg that reduces coverage.
                        IsExecutable = quote != null && quote.IsTradable,
                        SpreadPips = quote != null ? quote.SpreadPips : null,
                        VolatilityPercent = quote != null ? quote.VolatilityPercent : null,
                        SpreadLimit = Limit(leg.MaxSpreadPips),
                        VolatilityLimit = Limit(leg.MaxVolatilityPercent)
                    });
                }

                input.BasketRiskPercent = SumDeclaredRisk(candidate.Legs);
            }

            input.DailyLossPercent = ComputeDailyLoss(capture.Account);

            return input;
        }

        private static List<RiskEvaluationLeg> CreateUnjudgedLegs(RiskCandidate candidate)
        {
            List<RiskEvaluationLeg> legs = new List<RiskEvaluationLeg>();

            if (candidate.Legs == null)
            {
                return legs;
            }

            foreach (RiskCandidateLeg leg in candidate.Legs)
            {
                legs.Add(new RiskEvaluationLeg
                {
                    Symbol = leg.Symbol,
                    Market = leg.Market,
                    Weight = leg.Weight,
                    IsExecutable = false,
                    SpreadPips = null,
                    VolatilityPercent = null,
                    SpreadLimit = Limit(leg.MaxSpreadPips),
                    VolatilityLimit = Limit(leg.MaxVolatilityPercent)
                });
            }

            return legs;
        }

        /// <summary>
        /// Zero is how a leg says "not decided", because a limit of zero would tolerate nothing at all. It
        /// becomes null so the engine refuses the leg instead of judging it against nothing.
        /// </summary>
        private static double? Limit(double value)
        {
            return value > 0 ? value : null;
        }

        private static SymbolCapture FindSymbol(MarketDataCapture capture, string symbol)
        {
            if (capture.Symbols == null || string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }

            foreach (SymbolCapture candidate in capture.Symbols)
            {
                if (string.Equals(candidate.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Basket risk is what the active version declared: the sum of the risk caps of its legs, each of them
        /// a percentage of the account. It is a declared figure, not a position sizing result, and it is
        /// recomputed only when a market capture exists, because a risk figure without a market has no meaning.
        /// </summary>
        private static double? SumDeclaredRisk(List<RiskCandidateLeg> legs)
        {
            if (legs.Count == 0)
            {
                return null;
            }

            double total = 0;

            foreach (RiskCandidateLeg leg in legs)
            {
                total += leg.RiskCap;
            }

            return Math.Round(total, 4);
        }

        /// <summary>
        /// Daily loss as a percentage of equity: the part of today's result that is negative, realised plus
        /// unrealised. A positive day is a zero loss, never a credit. The day boundary belongs to the source
        /// reporting the account, and the absence of an account cannot be reported as a zero.
        /// </summary>
        private static double? ComputeDailyLoss(AccountCapture account)
        {
            if (account == null)
            {
                return null;
            }

            double reference = account.Equity > 0 ? account.Equity : account.Balance;

            if (reference <= 0)
            {
                return null;
            }

            double result = account.RealizedPnlToday + account.UnrealizedPnl;

            return result >= 0 ? 0 : Math.Round(-result / reference * 100, 4);
        }
    }
}
