using System;
using System.Collections.Generic;

namespace AutoTrade.Trading.Handlers.Evidence
{
    /// <summary>
    /// What happened. It is a closed vocabulary: an episode is one of these, and a new kind means a new version
    /// of the rendering, because the text of an episode is part of the identity of the collection it lives in.
    /// </summary>
    public enum OperationalEpisodeKind
    {
        /// <summary>The gate refused to route a proposal forward.</summary>
        GateBlocked = 0,

        /// <summary>An execution was closed with nominal coverage: everything that was planned was filled.</summary>
        ExecutionCompletedNominal = 1,

        /// <summary>An execution was closed without nominal coverage, so all-or-nothing was not honoured.</summary>
        ExecutionCompletedPartial = 2,

        /// <summary>A compensating execution was created, because a leg had to be undone.</summary>
        ExecutionCompensated = 3,

        /// <summary>An execution was never started, and the reason is recorded.</summary>
        ExecutionRefused = 4,

        /// <summary>The kill switch changed state.</summary>
        KillSwitchChanged = 5
    }

    /// <summary>One leg of the basket as it was at the moment of the episode.</summary>
    public class OperationalEpisodeLeg
    {
        public string Symbol { get; set; }

        public double SpreadPips { get; set; }

        public double SpreadLimitPips { get; set; }

        public double VolatilityPercent { get; set; }

        public double VolatilityLimitPercent { get; set; }
    }

    /// <summary>
    /// The measurable situation an episode happened in.
    /// </summary>
    /// <remarks>
    /// This is not decoration. A retrieval compares situations, so an episode without its numbers could only be
    /// matched on wording; with them, "the basket risk was twice the limit while the spreads were calm" is
    /// comparable to a new situation, and the memory answers the question it exists to answer.
    /// </remarks>
    public class OperationalEpisodeContext
    {
        public int VersionNumber { get; set; }

        public string EntryMode { get; set; }

        public string Action { get; set; }

        public double Confidence { get; set; }

        public double CoveragePercent { get; set; }

        public double MinimumCoveragePercent { get; set; }

        public double SnapshotAgeSeconds { get; set; }

        public double DailyLossPercent { get; set; }

        public double DailyLossLimitPercent { get; set; }

        public List<OperationalEpisodeLeg> Legs { get; set; }
    }

    /// <summary>What came out of it, in the terms the system already uses.</summary>
    public class OperationalEpisodeOutcome
    {
        /// <summary>The rule that decided, for example RiskPerBasketExceeded, or the state that was reached.</summary>
        public string Code { get; set; }

        /// <summary>What the rule looked at: "basket", or a symbol.</summary>
        public string Subject { get; set; }

        public double? ObservedValue { get; set; }

        public double? ThresholdValue { get; set; }

        public string Unit { get; set; }

        /// <summary>The sentence the engine itself produced, kept verbatim so the memory cannot paraphrase a rule.</summary>
        public string Detail { get; set; }

        /// <summary>Free-text reason, used where the system records one instead of a code.</summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// One thing that happened, ready to be remembered.
    /// </summary>
    public class OperationalEpisode
    {
        public Guid EpisodeId { get; set; }

        public OperationalEpisodeKind Kind { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        /// <summary>Transactional record that proves the episode, for example "execution:&lt;id&gt;".</summary>
        public string SourceRef { get; set; }

        /// <summary>The rendered text. Its wording is versioned, because changing it changes every vector.</summary>
        public string Text { get; set; }

        public string TextVersion { get; set; }
    }
}
