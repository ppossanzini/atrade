using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Analysis
{
    /// <summary>
    /// Vocabulary the model is allowed to answer with. An answer outside this set is not a weaker opinion, it is
    /// an answer nobody can act on, so it is discarded instead of being mapped onto the nearest known value.
    /// </summary>
    public enum AnalysisBias
    {
        Neutral = 0,
        Bullish = 1,
        Bearish = 2
    }

    /// <summary>
    /// Input handed to the model. It is versioned because an opinion is only comparable to another opinion
    /// produced by the same instruction, and it carries the measured facts as text so the model reads what the
    /// engine computed instead of recomputing anything.
    /// </summary>
    public class AnalysisRequest
    {
        /// <summary>Instruction version. Changing the wording of the prompt is changing this value.</summary>
        public string PromptVersion { get; set; }

        /// <summary>Symbol the answer must be about.</summary>
        public string Symbol { get; set; }

        /// <summary>Symbols the model may name. An answer about anything else is discarded.</summary>
        public List<string> AllowedSymbols { get; set; }

        /// <summary>Measured facts, rendered as text. The model may comment on them, never replace them.</summary>
        public string Context { get; set; }
    }

    /// <summary>
    /// A validated opinion. It exists only when the model answered, the answer parsed, and every field fell
    /// inside the allowlist and the configured bounds.
    /// </summary>
    public class AnalysisOpinion
    {
        public string Symbol { get; set; }

        public AnalysisBias Bias { get; set; }

        /// <summary>0 to 1, as required by the schema.</summary>
        public double Confidence { get; set; }

        public string Rationale { get; set; }
    }

    /// <summary>
    /// Outcome of one analysis call. Availability travels with the result for the same reason it does in the
    /// evidence tier: "the model is not there" and "the model said nothing usable" are different facts, and
    /// collapsing them would let a stopped model read as a neutral opinion.
    /// </summary>
    public class AnalysisResult
    {
        /// <summary>False when no model is configured or it could not be reached.</summary>
        public bool IsAvailable { get; set; }

        /// <summary>Set only when the answer passed validation. Null is the fail-closed outcome.</summary>
        public AnalysisOpinion Opinion { get; set; }

        /// <summary>Why there is no opinion: either the model was unreachable, or it answered and was discarded.</summary>
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Local analysis model. It produces an opinion about a symbol and nothing else: it never sets a size, a
    /// limit or a verdict, never authorises an order, and a stopped or unusable model fails closed. Its output is
    /// advisory input to analysis, exactly like a retrieval result.
    /// </summary>
    public interface IOllamaAnalysisClient
    {
        /// <summary>False when no model is configured, or the configured one was not found at startup.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Startup check: the configured model must be present on the local engine. Selecting a model that is not
        /// there aborts startup, because a degraded analysis that is silent is worse than a host that refuses.
        /// </summary>
        Task EnsureModelIsPresentAsync(CancellationToken cancellationToken);

        Task<AnalysisResult> AnalyseAsync(AnalysisRequest request, CancellationToken cancellationToken);
    }
}
