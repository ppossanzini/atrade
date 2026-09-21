using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;

namespace AutoTrade.Trading.Handlers.Market
{
    /// <summary>
    /// What a source proposes and what it can say about it. The action is part of the candidate because a
    /// source that only observes the market may still ask nothing at all.
    /// </summary>
    public class ProposalCandidate
    {
        public ProposalAction Action { get; set; }

        public int Confidence { get; set; }

        public string Rationale { get; set; }
    }

    /// <summary>
    /// Where candidate proposals come from. This slice ships the deterministic source; an evidence driven
    /// source plugs in here without touching routing, persistence or the screen.
    /// </summary>
    public interface IProposalSource
    {
        ProposalCandidate Create(MarketDataCapture capture, RiskEvaluationInput input);
    }

    /// <summary>
    /// The source in force now: it always asks for an entry on the active version and describes the
    /// observation instead of pretending to interpret it.
    ///
    /// Reduce and exit need positions to reason about and arrive with the execution and reconciliation work;
    /// until then this source never asks for them, which is why the vocabulary carries them while the source
    /// does not use them.
    /// </summary>
    public class DeterministicProposalSource : IProposalSource
    {
        public ProposalCandidate Create(MarketDataCapture capture, RiskEvaluationInput input)
        {
            ProposalAction action = ProposalAction.Entry;

            return new ProposalCandidate
            {
                Action = action,
                Confidence = AnalysisRules.ComputeConfidence(capture, input != null ? input.Legs : null),
                Rationale = AnalysisRules.BuildRationale(action, input != null ? input.VersionNumber : 0, input != null ? input.Legs : null)
            };
        }
    }
}
