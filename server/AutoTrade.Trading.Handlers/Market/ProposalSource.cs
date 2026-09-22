using System;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;
using AutoTrade.Trading.Handlers.Strategy;
using AutoTrade.Trading.Handlers.Analysis;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public StrategyEnsembleResult Strategy { get; set; }

        public int? LlmConfidence { get; set; }

        public string LlmRationale { get; set; }

        public string SelectedScenario { get; set; }
    }

    /// <summary>
    /// Where candidate proposals come from. This slice ships the deterministic source; an evidence driven
    /// source plugs in here without touching routing, persistence or the screen.
    /// </summary>
    public interface IProposalSource
    {
        Task<ProposalCandidate> CreateAsync(MarketDataCapture capture, RiskEvaluationInput input, StrategyPolicy policy, CancellationToken cancellationToken);
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
        private readonly StrategyEnsemble ensemble = new StrategyEnsemble();

        private readonly IOllamaAnalysisClient analysisClient;

        public DeterministicProposalSource(IOllamaAnalysisClient analysisClient)
        {
            this.analysisClient = analysisClient;
        }

        public async Task<ProposalCandidate> CreateAsync(MarketDataCapture capture, RiskEvaluationInput input, StrategyPolicy policy, CancellationToken cancellationToken)
        {
            ProposalAction action = ProposalAction.Entry;
            List<StrategyComponentAssessment> assessments = new List<StrategyComponentAssessment>();
            IReadOnlyList<StrategyComponentPolicy> configured = policy?.Components?.Where(item => item.Enabled).ToList()
              ?? new List<StrategyComponentPolicy>
              {
                  new StrategyComponentPolicy { Type = StrategyComponentType.TrendFollowing, Enabled = true, Weight = 20 },
                  new StrategyComponentPolicy { Type = StrategyComponentType.Momentum, Enabled = true, Weight = 20 },
                  new StrategyComponentPolicy { Type = StrategyComponentType.Breakout, Enabled = true, Weight = 20 },
                  new StrategyComponentPolicy { Type = StrategyComponentType.MeanReversion, Enabled = true, Weight = 20 },
                  new StrategyComponentPolicy { Type = StrategyComponentType.VolatilityFilter, Enabled = true, Weight = 20 }
              };

            foreach (SymbolCapture symbol in capture?.Symbols ?? new List<SymbolCapture>())
            {
                foreach (StrategyComponentPolicy component in configured)
                {
                    StrategyComponentAssessment assessment = StrategyFeatureCalculator.Evaluate(component.Type, symbol);
                    assessments.Add(new StrategyComponentAssessment
                    {
                        Type = assessment.Type, Weight = component.Weight, Score = assessment.Score,
                        Direction = assessment.Direction, Rationale = assessment.Rationale
                    });
                }
            }

            StrategyEnsembleResult strategy = ensemble.Evaluate(new StrategyEnsembleInput
            {
                Components = assessments,
                DataQuality = AnalysisRules.ComputeConfidence(capture, input != null ? input.Legs : null),
                MinimumAgreement = policy?.MinimumAgreement > 0 ? policy.MinimumAgreement : 55,
                MinimumConfidence = policy?.MinimumConfidence > 0 ? policy.MinimumConfidence : 50,
                CombinationMode = policy?.CombinationMode ?? StrategyCombinationMode.WeightedEnsemble,
                ConflictPolicy = policy?.ConflictPolicy ?? StrategyConflictPolicy.NoTrade
            });

            List<AnalysisOpinion> opinions = new List<AnalysisOpinion>();
            foreach (SymbolCapture symbol in capture?.Symbols ?? new List<SymbolCapture>())
            {
                string context = "strategy=" + strategy.Reason + ";agreement=" + strategy.Agreement + ";components="
                  + string.Join(",", strategy.Components.Select(item => item.Type + ":" + item.Direction + ":" + item.Score.ToString("F3")));
                AnalysisResult analysis = await analysisClient.AnalyseAsync(new AnalysisRequest
                {
                    PromptVersion = "strategy-ensemble-v1",
                    Symbol = symbol.Symbol,
                    AllowedSymbols = new List<string> { symbol.Symbol },
                    Context = context
                }, cancellationToken);
                if (analysis.IsAvailable && analysis.Opinion != null)
                {
                    opinions.Add(analysis.Opinion);
                }
            }

            int? llmConfidence = opinions.Count == 0 ? null : (int)Math.Round(opinions.Average(item => item.Confidence) * 100, MidpointRounding.AwayFromZero);
            string llmRationale = opinions.Count == 0 ? "LLM assessment unavailable." : string.Join(" | ", opinions.Select(item => item.Symbol + ": " + item.Rationale));
            string scenario = strategy.IsNoTrade ? "NoTrade" : strategy.Direction > 0 ? "CompositeLong" : "CompositeShort";

            return new ProposalCandidate
            {
                Action = action,
                Confidence = strategy.Confidence,
                Strategy = strategy,
                LlmConfidence = llmConfidence,
                LlmRationale = llmRationale,
                SelectedScenario = scenario,
                Rationale = AnalysisRules.BuildRationale(action, input != null ? input.VersionNumber : 0, input != null ? input.Legs : null)
                  + "|strategy=" + strategy.Reason
                  + "|agreement=" + strategy.Agreement
            };
        }
    }
}
