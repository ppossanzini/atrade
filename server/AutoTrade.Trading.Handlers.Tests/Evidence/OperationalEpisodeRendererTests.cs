using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AutoTrade.Trading.Handlers.Evidence;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
    /// <summary>
    /// The rendering is the part of an episode that cannot be changed later without orphaning stored vectors, so
    /// it is pinned literally: same facts in, same text out, whatever the machine's culture is.
    /// </summary>
    public class OperationalEpisodeRendererTests
    {
        private static OperationalEpisodeContext GateBlockedContext()
        {
            return new OperationalEpisodeContext
            {
                VersionNumber = 3,
                EntryMode = "RegimeMomentum",
                Action = "Entry",
                Confidence = 100d,
                CoveragePercent = 100d,
                MinimumCoveragePercent = 100d,
                SnapshotAgeSeconds = 0.002d,
                DailyLossPercent = 0d,
                DailyLossLimitPercent = 2.5d,
                Legs = new List<OperationalEpisodeLeg>
        {
          new OperationalEpisodeLeg { Symbol = "EURUSD", SpreadPips = 0.802d, SpreadLimitPips = 1.5d, VolatilityPercent = 0.22d, VolatilityLimitPercent = 0.35d },
          new OperationalEpisodeLeg { Symbol = "XAUUSD", SpreadPips = 29.202d, SpreadLimitPips = 40d, VolatilityPercent = 0.574d, VolatilityLimitPercent = 0.8d }
        }
            };
        }

        [Fact]
        public void Render_GateBlocked_StatesTheSituationAndTheDecidingRule()
        {
            OperationalEpisodeContext context = GateBlockedContext();

            OperationalEpisodeOutcome outcome = new OperationalEpisodeOutcome
            {
                Code = "RiskPerBasketExceeded",
                Subject = "basket",
                ObservedValue = 2.3d,
                ThresholdValue = 0.8d,
                Unit = "percent",
                Detail = "The basket risk exceeds the risk per basket limit."
            };

            string text = OperationalEpisodeRenderer.Render(OperationalEpisodeKind.GateBlocked, context, outcome);

            Assert.Equal(
              "The gate blocked a proposal. Basket version 3, entry mode RegimeMomentum, action Entry, confidence 100. "
              + "Situation: coverage 100 of a required 100 percent, market snapshot age 0.002 seconds, daily loss 0 of 2.5 percent. "
              + "Legs: EURUSD spread 0.802 of 1.5 pips and volatility 0.22 of 0.35 percent; "
              + "XAUUSD spread 29.202 of 40 pips and volatility 0.574 of 0.8 percent. "
              + "Outcome: RiskPerBasketExceeded on basket, observed 2.3 against 0.8 percent. "
              + "The basket risk exceeds the risk per basket limit.",
              text);
        }

        [Fact]
        public void Render_IsInvariantUnderACommaDecimalCulture()
        {
            // The trap this guards against: the same facts rendering differently on a machine with an Italian locale,
            // which would change the vector and make stored episodes incomparable with new ones, silently.
            CultureInfo previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("it-IT");

                string underItalianCulture = OperationalEpisodeRenderer.Render(
                  OperationalEpisodeKind.GateBlocked,
                  GateBlockedContext(),
                  new OperationalEpisodeOutcome { Code = "RiskPerBasketExceeded", Subject = "basket", ObservedValue = 2.3d, ThresholdValue = 0.8d, Unit = "percent" });

                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                string underInvariantCulture = OperationalEpisodeRenderer.Render(
                  OperationalEpisodeKind.GateBlocked,
                  GateBlockedContext(),
                  new OperationalEpisodeOutcome { Code = "RiskPerBasketExceeded", Subject = "basket", ObservedValue = 2.3d, ThresholdValue = 0.8d, Unit = "percent" });

                Assert.Equal(underInvariantCulture, underItalianCulture);
                Assert.Contains("2.3", underItalianCulture, StringComparison.Ordinal);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Render_SameFacts_ProduceTheSameText()
        {
            OperationalEpisodeOutcome outcome = new OperationalEpisodeOutcome { Code = "KillSwitchEngaged", Reason = "operator" };

            string first = OperationalEpisodeRenderer.Render(OperationalEpisodeKind.KillSwitchChanged, GateBlockedContext(), outcome);
            string second = OperationalEpisodeRenderer.Render(OperationalEpisodeKind.KillSwitchChanged, GateBlockedContext(), outcome);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Render_ExecutionClosedNominal_ReportsTheOutcomeWithoutInventingAGate()
        {
            string text = OperationalEpisodeRenderer.Render(
              OperationalEpisodeKind.ExecutionCompletedNominal,
              new OperationalEpisodeContext
              {
                  VersionNumber = 3,
                  EntryMode = "RegimeMomentum",
                  Action = "Entry",
                  Confidence = 100d,
                  CoveragePercent = 100d,
                  MinimumCoveragePercent = 100d,
                  SnapshotAgeSeconds = 0.5d
              },
              new OperationalEpisodeOutcome { Code = "ExecutionCompletedNominal" });

            Assert.StartsWith("An execution was filled completely.", text, StringComparison.Ordinal);
            Assert.Contains("Outcome: ExecutionCompletedNominal.", text, StringComparison.Ordinal);

            // No leg detail was collected for this episode, and saying so is better than remaining silent: an absent
            // section must not read as "there were no legs".
            Assert.Contains("No leg detail was recorded.", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_WithoutAContext_StillSaysWhatHappened()
        {
            // A refusal can happen before any basket context exists, so the outcome is what carries the episode.
            string text = OperationalEpisodeRenderer.Render(
              OperationalEpisodeKind.ExecutionRefused,
              null,
              new OperationalEpisodeOutcome { Reason = "proposal_not_approved" });

            Assert.Equal("An execution was refused before it started. Reason: proposal_not_approved.", text);
        }

        [Fact]
        public void Create_StampsTheVersionOfTheWording()
        {
            OperationalEpisode episode = OperationalEpisodeRenderer.Create(
              OperationalEpisodeKind.GateBlocked,
              "proposal:01A0B94A",
              new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc),
              GateBlockedContext(),
              new OperationalEpisodeOutcome { Code = "RiskPerBasketExceeded" });

            // The version travels with the episode because a stored vector is only comparable with another one whose
            // text was rendered the same way.
            Assert.Equal(OperationalEpisodeRenderer.TextVersion, episode.TextVersion);
            Assert.Equal("v1", episode.TextVersion);
            Assert.Equal("proposal:01A0B94A", episode.SourceRef);
            Assert.NotEqual(Guid.Empty, episode.EpisodeId);
            Assert.Contains("RiskPerBasketExceeded", episode.Text, StringComparison.Ordinal);
        }
    }
}
