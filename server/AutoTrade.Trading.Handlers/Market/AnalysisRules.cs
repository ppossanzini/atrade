using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Risk;
using BasketVersionPolicyEntity = AutoTrade.Trading.Handlers.Model.BasketVersionPolicy;

namespace AutoTrade.Trading.Handlers.Market
{
    /// <summary>
    /// The deterministic rules of the Market Manager: how a gate and a mode route a proposal, when a proposal
    /// is decidable, and how the informational confidence and the audit summary are computed.
    ///
    /// They are pure functions, so the whole routing matrix can be tested by table and the same rule cannot
    /// be interpreted in two places.
    /// </summary>
    public static class AnalysisRules
    {
        private const int MissingLegPenalty = 10;
        private const int UntradableLegPenalty = 25;

        /// <summary>
        /// Routing matrix. Automatic never turns a review into a permission: the mode decides who may forward,
        /// the gate decides whether forwarding is allowed at all, and a block is terminal in every mode.
        /// </summary>
        public static ProposalStatus Route(MarketManagerMode mode, RiskGateVerdict gate)
        {
            if (gate == RiskGateVerdict.Block)
            {
                return ProposalStatus.Blocked;
            }

            if (gate == RiskGateVerdict.Review)
            {
                return ProposalStatus.NeedsReview;
            }

            return mode == MarketManagerMode.Manual ? ProposalStatus.NeedsReview : ProposalStatus.AutoApproved;
        }

        /// <summary>
        /// Whether the operator may decide now. In Automatic the operator has delegated the decisions, so a
        /// proposal must be brought back to Manual or Supervised on purpose before it can be approved by hand.
        /// </summary>
        public static bool IsDecidable(ProposalStatus status, MarketManagerMode mode, DateTime expiresAtUtc, DateTime now)
        {
            if (status != ProposalStatus.NeedsReview || now >= expiresAtUtc)
            {
                return false;
            }

            return mode == MarketManagerMode.Manual || mode == MarketManagerMode.Supervised;
        }

        public static bool IsTerminal(ProposalStatus status)
        {
            return status == ProposalStatus.Approved
              || status == ProposalStatus.Rejected
              || status == ProposalStatus.Suspended
              || status == ProposalStatus.Expired
              || status == ProposalStatus.Blocked;
        }

        /// <summary>
        /// Informational confidence of the deterministic source: how complete and usable the observation of the
        /// basket is. It is deliberately independent of the thresholds, because it describes the input rather
        /// than the headroom against a limit. A source backed by evidence replaces it in a later slice.
        /// </summary>
        public static int ComputeConfidence(MarketDataCapture capture, List<RiskEvaluationLeg> legs)
        {
            if (capture == null || !capture.IsAvailable || legs == null || legs.Count == 0)
            {
                return 0;
            }

            int confidence = 100;

            foreach (RiskEvaluationLeg leg in legs)
            {
                if (leg.SpreadPips == null || leg.VolatilityPercent == null)
                {
                    confidence -= MissingLegPenalty;
                }
                else if (!leg.IsExecutable)
                {
                    confidence -= UntradableLegPenalty;
                }
            }

            return Math.Max(0, Math.Min(100, confidence));
        }

        /// <summary>
        /// Language neutral summary of the input a proposal was judged on. It is audit material, not UI copy:
        /// the screen renders codes and values, so the record stays readable whatever the interface language is.
        /// </summary>
        public static string BuildRationale(ProposalAction action, int versionNumber, List<RiskEvaluationLeg> legs)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(action.ToString().ToLowerInvariant());
            builder.Append("|v");
            builder.Append(versionNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append("|coverage=");
            builder.Append(ComputeCoverage(legs).ToString(CultureInfo.InvariantCulture));

            if (legs != null && legs.Count > 0)
            {
                builder.Append("|legs=");

                for (int index = 0; index < legs.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    RiskEvaluationLeg leg = legs[index];

                    builder.Append(leg.Symbol);
                    builder.Append(':');
                    builder.Append(Format(leg.SpreadPips));
                    builder.Append('/');
                    builder.Append(Format(leg.VolatilityPercent));
                    builder.Append('/');
                    builder.Append(leg.IsExecutable ? "1" : "0");
                }
            }

            return builder.ToString();
        }

        /// <summary>Share of the weight that can actually be executed, on the same definition the engine uses.</summary>
        public static int ComputeCoverage(List<RiskEvaluationLeg> legs)
        {
            if (legs == null)
            {
                return 0;
            }

            int coverage = 0;

            foreach (RiskEvaluationLeg leg in legs)
            {
                if (leg.IsExecutable)
                {
                    coverage += leg.Weight;
                }
            }

            return coverage;
        }

        /// <summary>Builds the engine input of the candidate from stored state.</summary>
        public static RiskCandidate BuildCandidate(bool hasActiveVersion, int versionNumber, BasketVersionPolicyEntity policy, List<BasketVersionLeg> legs, bool killSwitchEngaged)
        {
            RiskCandidate candidate = new RiskCandidate
            {
                HasActiveVersion = hasActiveVersion,
                VersionNumber = versionNumber,
                KillSwitchEngaged = killSwitchEngaged,
                Legs = new List<RiskCandidateLeg>()
            };

            if (policy != null)
            {
                candidate.FailurePolicy = policy.FailurePolicy;
                candidate.MinimumCoverage = policy.MinimumCoverage;
                candidate.RiskPerBasketLimit = policy.RiskPerBasket;
                candidate.DailyLossLimit = policy.DailyLossLimit;
            }

            if (legs != null)
            {
                foreach (BasketVersionLeg leg in legs)
                {
                    candidate.Legs.Add(new RiskCandidateLeg
                    {
                        Symbol = leg.Symbol,
                        Market = leg.Market,
                        Weight = leg.Weight,
                        RiskCap = leg.RiskCap,
                        MaxSpreadPips = leg.MaxSpreadPips,
                        MaxVolatilityPercent = leg.MaxVolatilityPercent
                    });
                }
            }

            return candidate;
        }

        public static List<SymbolRequest> CreateSymbolRequests(RiskCandidate candidate)
        {
            List<SymbolRequest> requests = new List<SymbolRequest>();

            if (candidate == null || candidate.Legs == null)
            {
                return requests;
            }

            foreach (RiskCandidateLeg leg in candidate.Legs)
            {
                requests.Add(new SymbolRequest
                {
                    Symbol = leg.Symbol,
                    Market = leg.Market
                });
            }

            return requests;
        }

        private static string Format(double? value)
        {
            return value.HasValue ? Math.Round(value.Value, 3).ToString(CultureInfo.InvariantCulture) : "n";
        }
    }
}
