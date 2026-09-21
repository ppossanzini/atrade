using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Market;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Market
{
    /// <summary>
    /// The routing matrix is the safety rule of the Market Manager, so it is pinned by table: every
    /// combination of mode and gate has exactly one expected routing, and no combination turns a non-allow
    /// into an automatic permission.
    /// </summary>
    public class AnalysisRulesTests
    {
        [Theory]
        [InlineData(MarketManagerMode.Manual, RiskGateVerdict.Allow, ProposalStatus.NeedsReview)]
        [InlineData(MarketManagerMode.Manual, RiskGateVerdict.Review, ProposalStatus.NeedsReview)]
        [InlineData(MarketManagerMode.Manual, RiskGateVerdict.Block, ProposalStatus.Blocked)]
        [InlineData(MarketManagerMode.Supervised, RiskGateVerdict.Allow, ProposalStatus.AutoApproved)]
        [InlineData(MarketManagerMode.Supervised, RiskGateVerdict.Review, ProposalStatus.NeedsReview)]
        [InlineData(MarketManagerMode.Supervised, RiskGateVerdict.Block, ProposalStatus.Blocked)]
        [InlineData(MarketManagerMode.Automatic, RiskGateVerdict.Allow, ProposalStatus.AutoApproved)]
        [InlineData(MarketManagerMode.Automatic, RiskGateVerdict.Review, ProposalStatus.NeedsReview)]
        [InlineData(MarketManagerMode.Automatic, RiskGateVerdict.Block, ProposalStatus.Blocked)]
        public void Route_AppliesTheApprovedMatrix(MarketManagerMode mode, RiskGateVerdict gate, ProposalStatus expected)
        {
            Assert.Equal(expected, AnalysisRules.Route(mode, gate));
        }

        [Theory]
        [InlineData(MarketManagerMode.Manual, ProposalStatus.NeedsReview, true)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.NeedsReview, true)]
        [InlineData(MarketManagerMode.Automatic, ProposalStatus.NeedsReview, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.AutoApproved, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.Blocked, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.Approved, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.Rejected, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.Suspended, false)]
        [InlineData(MarketManagerMode.Supervised, ProposalStatus.Expired, false)]
        public void IsDecidable_OnlyAdmitsAReviewableProposalInAModeThatAsksTheOperator(MarketManagerMode mode, ProposalStatus status, bool expected)
        {
            DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal(expected, AnalysisRules.IsDecidable(status, mode, now.AddMinutes(5), now));
        }

        [Fact]
        public void IsDecidable_RefusesAnExpiredProposalEvenInTheRightMode()
        {
            DateTime now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.False(AnalysisRules.IsDecidable(ProposalStatus.NeedsReview, MarketManagerMode.Supervised, now, now));
        }

        [Fact]
        public void IsTerminal_TreatsBlockedAsFinal()
        {
            Assert.True(AnalysisRules.IsTerminal(ProposalStatus.Blocked));
            Assert.True(AnalysisRules.IsTerminal(ProposalStatus.Expired));
            Assert.False(AnalysisRules.IsTerminal(ProposalStatus.NeedsReview));
            Assert.False(AnalysisRules.IsTerminal(ProposalStatus.AutoApproved));
        }

        [Fact]
        public void ComputeConfidence_WithoutACapture_IsZero()
        {
            Assert.Equal(0, AnalysisRules.ComputeConfidence(null, CompleteLegs()));
        }

        [Fact]
        public void ComputeConfidence_WithCompleteLegs_IsFull()
        {
            MarketDataCapture capture = AvailableCapture();

            Assert.Equal(100, AnalysisRules.ComputeConfidence(capture, CompleteLegs()));
        }

        [Fact]
        public void ComputeConfidence_PenalisesAMissingMeasurement()
        {
            List<RiskEvaluationLeg> legs = CompleteLegs();
            legs[1].SpreadPips = null;

            Assert.Equal(90, AnalysisRules.ComputeConfidence(AvailableCapture(), legs));
        }

        [Fact]
        public void ComputeConfidence_PenalisesAnUntradableLegMore()
        {
            List<RiskEvaluationLeg> legs = CompleteLegs();
            legs[1].IsExecutable = false;

            Assert.Equal(75, AnalysisRules.ComputeConfidence(AvailableCapture(), legs));
        }

        [Fact]
        public void ComputeCoverage_SumsOnlyTheExecutableWeight()
        {
            List<RiskEvaluationLeg> legs = CompleteLegs();
            legs[1].IsExecutable = false;

            Assert.Equal(60, AnalysisRules.ComputeCoverage(legs));
        }

        [Fact]
        public void BuildRationale_IsStableAndMarksWhatWasMissing()
        {
            List<RiskEvaluationLeg> legs = CompleteLegs();
            legs[1].SpreadPips = null;
            legs[1].IsExecutable = false;

            string rationale = AnalysisRules.BuildRationale(ProposalAction.Entry, 4, legs);

            Assert.Equal("entry|v4|coverage=60|legs=EURUSD:0.8/0.2/1,XAUUSD:n/0.5/0", rationale);
        }

        private static List<RiskEvaluationLeg> CompleteLegs()
        {
            return new List<RiskEvaluationLeg>
      {
        new RiskEvaluationLeg { Symbol = "EURUSD", Market = MarketKind.Fx, Weight = 60, SpreadPips = 0.8, VolatilityPercent = 0.2, IsExecutable = true },
        new RiskEvaluationLeg { Symbol = "XAUUSD", Market = MarketKind.Metal, Weight = 40, SpreadPips = 28, VolatilityPercent = 0.5, IsExecutable = true }
      };
        }

        private static MarketDataCapture AvailableCapture()
        {
            return new MarketDataCapture
            {
                IsAvailable = true,
                CapturedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}
