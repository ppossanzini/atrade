using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Strategy;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Market
{
    public class StrategyEnsembleTests
    {
        [Fact]
        public void ConflictingComponentsBecomeNoTradeWhenAgreementIsTooLow()
        {
            StrategyEnsembleResult result = new StrategyEnsemble().Evaluate(new StrategyEnsembleInput
            {
                DataQuality = 90,
                MinimumAgreement = 70,
                MinimumConfidence = 50,
                CombinationMode = StrategyCombinationMode.WeightedEnsemble,
                ConflictPolicy = StrategyConflictPolicy.NoTrade,
                Components = new List<StrategyComponentAssessment>
                {
                    new StrategyComponentAssessment { Type = StrategyComponentType.TrendFollowing, Direction = 1, Score = 0.6, Weight = 50 },
                    new StrategyComponentAssessment { Type = StrategyComponentType.MeanReversion, Direction = -1, Score = 0.5, Weight = 50 }
                }
            });

            Assert.True(result.IsNoTrade);
            Assert.Equal(0, result.Direction);
            Assert.True(result.Confidence < 100);
        }

        [Fact]
        public void StrongWeightedAgreementProducesActionableDirection()
        {
            StrategyEnsembleResult result = new StrategyEnsemble().Evaluate(new StrategyEnsembleInput
            {
                DataQuality = 90,
                MinimumAgreement = 60,
                MinimumConfidence = 60,
                CombinationMode = StrategyCombinationMode.WeightedEnsemble,
                ConflictPolicy = StrategyConflictPolicy.NoTrade,
                Components = new List<StrategyComponentAssessment>
                {
                    new StrategyComponentAssessment { Type = StrategyComponentType.TrendFollowing, Direction = 1, Score = 0.9, Weight = 70 },
                    new StrategyComponentAssessment { Type = StrategyComponentType.Breakout, Direction = 1, Score = 0.8, Weight = 30 }
                }
            });

            Assert.False(result.IsNoTrade);
            Assert.Equal(1, result.Direction);
            Assert.True(result.Confidence >= 60);
        }
    }
}
