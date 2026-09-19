using System;
using System.Collections.Generic;
using System.Linq;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Risk;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Risk
{
  /// <summary>
  /// Boundary table for the risk engine. Every threshold is probed at, just below and just above its
  /// limit, and every case that cannot be judged must block: there is no permissive path through a
  /// missing input.
  /// </summary>
  public class RiskEngineTests
  {
    private static readonly DateTime BaseTime = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

    private static FakeTimeProvider CreateClock()
    {
      return new FakeTimeProvider(BaseTime);
    }

    private static RiskThresholds CreateConfiguredThresholds()
    {
      return new RiskThresholds
      {
        SnapshotMaxAgeSeconds = 60
      };
    }

    /// <summary>A fully healthy input: every gate should allow unless a case deliberately breaks one.</summary>
    private static RiskEvaluationInput CreateHealthyInput()
    {
      return new RiskEvaluationInput
      {
        HasActiveVersion = true,
        KillSwitchEngaged = false,
        VersionNumber = 1,
        MarketDataCapturedAtUtc = BaseTime,
        FailurePolicy = FailurePolicy.MinimumCoverage,
        MinimumCoverage = 75,
        RiskPerBasketLimit = 0.8,
        DailyLossLimit = 2.5,
        BasketRiskPercent = 0.4,
        DailyLossPercent = 0.5,
        Legs = new List<RiskEvaluationLeg>
        {
          new RiskEvaluationLeg { Symbol = "EURUSD", Market = MarketKind.Fx, Weight = 60, SpreadPips = 0.8, VolatilityPercent = 7.0, SpreadLimit = 2.0, VolatilityLimit = 15.0, IsExecutable = true },
          new RiskEvaluationLeg { Symbol = "XAUUSD", Market = MarketKind.Metal, Weight = 40, SpreadPips = 1.5, VolatilityPercent = 12.0, SpreadLimit = 4.0, VolatilityLimit = 25.0, IsExecutable = true }
        }
      };
    }

    private static RiskEngine CreateEngine(FakeTimeProvider clock, RiskThresholds thresholds)
    {
      return new RiskEngine(thresholds, clock);
    }

    [Fact]
    public void HealthyInput_Allows()
    {
      FakeTimeProvider clock = CreateClock();
      RiskDecisionDtoAssertions.AssertVerdict(CreateEngine(clock, CreateConfiguredThresholds()).Evaluate(CreateHealthyInput()), RiskGateVerdict.Allow);
    }

    [Fact]
    public void Evaluation_AssignsCodeObservedThresholdAndTimestampToEveryGate()
    {
      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(CreateHealthyInput());

      Assert.NotEmpty(decision.Gates);
      Assert.Equal(BaseTime, decision.EvaluatedAtUtc);

      foreach (var gate in decision.Gates)
      {
        Assert.False(string.IsNullOrWhiteSpace(gate.Subject));
        Assert.False(string.IsNullOrWhiteSpace(gate.Detail));
        Assert.Equal(BaseTime, gate.EvaluatedAtUtc);
      }

      // The numeric gates expose both the observed value and the limit they were compared with.
      var riskGate = Assert.Single(decision.Gates, item => item.Code == RiskGateCode.RiskPerBasketExceeded);
      Assert.Equal(0.4, riskGate.ObservedValue);
      Assert.Equal(0.8, riskGate.ThresholdValue);

      var coverageGate = Assert.Single(decision.Gates, item => item.Code == RiskGateCode.CoverageBelowMinimum);
      Assert.Equal(100, coverageGate.ObservedValue);
      Assert.Equal(100, coverageGate.ThresholdValue);
    }

    [Fact]
    public void Evaluation_IsDeterministic()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      RiskEngine first = CreateEngine(CreateClock(), CreateConfiguredThresholds());
      RiskEngine second = CreateEngine(CreateClock(), CreateConfiguredThresholds());

      RiskDecisionDto left = first.Evaluate(input);
      RiskDecisionDto right = second.Evaluate(input);

      Assert.Equal(left.Verdict, right.Verdict);
      Assert.Equal(
        left.Gates.Select(item => item.Code + ":" + item.Verdict + ":" + item.ObservedValue).ToArray(),
        right.Gates.Select(item => item.Code + ":" + item.Verdict + ":" + item.ObservedValue).ToArray());
    }

    [Fact]
    public void Evaluation_WithoutInput_IsRejected()
    {
      RiskEngine engine = CreateEngine(CreateClock(), CreateConfiguredThresholds());

      Assert.Throws<ArgumentNullException>(() => engine.Evaluate(null));
    }

    [Fact]
    public void ActiveVersionMissing_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.HasActiveVersion = false;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.ActiveVersionMissing, RiskGateVerdict.Block, "basket");
    }

    [Fact]
    public void KillSwitchEngaged_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.KillSwitchEngaged = true;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.KillSwitchEngaged, RiskGateVerdict.Block, "basket");
    }

    [Fact]
    public void SnapshotMissing_BlocksAndSkipsTheGatesItWouldMakeMeaningless()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.MarketDataCapturedAtUtc = null;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      Assert.Equal(3, decision.Gates.Count);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.SnapshotMissing, RiskGateVerdict.Block, "basket");
      Assert.DoesNotContain(decision.Gates, item => item.Code == RiskGateCode.CoverageBelowMinimum);
    }

    [Theory]
    [InlineData(59, RiskGateVerdict.Allow)]
    [InlineData(60, RiskGateVerdict.Allow)]
    [InlineData(61, RiskGateVerdict.Block)]
    public void SnapshotAge_IsJudgedAtTheBoundary(int ageSeconds, RiskGateVerdict expected)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.MarketDataCapturedAtUtc = BaseTime.AddSeconds(-ageSeconds);

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.SnapshotStale, expected, "basket");
    }

    [Fact]
    public void SnapshotAge_WithoutAConfiguredWindow_Blocks()
    {
      RiskThresholds thresholds = CreateConfiguredThresholds();
      thresholds.SnapshotMaxAgeSeconds = null;

      RiskDecisionDto decision = CreateEngine(CreateClock(), thresholds).Evaluate(CreateHealthyInput());

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.ThresholdNotConfigured, RiskGateVerdict.Block, "SnapshotMaxAgeSeconds");
    }

    [Theory]
    [InlineData(FailurePolicy.AllOrNothing, 75, 100, RiskGateVerdict.Allow)]
    [InlineData(FailurePolicy.AllOrNothing, 75, 99, RiskGateVerdict.Block)]
    [InlineData(FailurePolicy.MinimumCoverage, 75, 75, RiskGateVerdict.Allow)]
    [InlineData(FailurePolicy.MinimumCoverage, 75, 74, RiskGateVerdict.Block)]
    [InlineData(FailurePolicy.RequireConfirmation, 75, 75, RiskGateVerdict.Review)]
    [InlineData(FailurePolicy.RequireConfirmation, 75, 99, RiskGateVerdict.Review)]
    [InlineData(FailurePolicy.RequireConfirmation, 75, 74, RiskGateVerdict.Block)]
    [InlineData(FailurePolicy.RequireConfirmation, 75, 100, RiskGateVerdict.Allow)]
    public void Coverage_IsJudgedAgainstThePolicyAtTheBoundary(FailurePolicy policy, int minimumCoverage, int coverage, RiskGateVerdict expected)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.FailurePolicy = policy;
      input.MinimumCoverage = minimumCoverage;
      input.Legs = new List<RiskEvaluationLeg>
      {
        new RiskEvaluationLeg { Symbol = "EURUSD", Weight = coverage, SpreadPips = 0.8, VolatilityPercent = 7.0, IsExecutable = true },
        new RiskEvaluationLeg { Symbol = "XAUUSD", Weight = 100 - coverage, SpreadPips = 1.5, VolatilityPercent = 12.0, IsExecutable = false }
      };

      // The executable share of the selected weight is what coverage measures.
      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.CoverageBelowMinimum, expected, "basket");
    }

    [Fact]
    public void Coverage_WithoutLegs_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs = new List<RiskEvaluationLeg>();

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Block, "basket");
    }

    [Theory]
    [InlineData(0.79)]
    [InlineData(0.8)]
    public void BasketRisk_AtOrBelowTheLimit_Allows(double observed)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.BasketRiskPercent = observed;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.RiskPerBasketExceeded, RiskGateVerdict.Allow, "basket");
    }

    [Fact]
    public void BasketRisk_AboveTheLimit_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.BasketRiskPercent = 0.81;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.RiskPerBasketExceeded, RiskGateVerdict.Block, "basket");
    }

    [Fact]
    public void BasketRisk_WhenItCannotBeComputed_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.BasketRiskPercent = null;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.BasketDataMissing, RiskGateVerdict.Block, "BasketRiskPercent");
    }

    [Theory]
    [InlineData(2.49, RiskGateVerdict.Allow)]
    [InlineData(2.5, RiskGateVerdict.Allow)]
    [InlineData(2.51, RiskGateVerdict.Block)]
    public void DailyLoss_IsJudgedAtTheBoundary(double observed, RiskGateVerdict expected)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.DailyLossPercent = observed;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.DailyLossExceeded, expected, "basket");
    }

    [Fact]
    public void DailyLoss_WhenItCannotBeComputed_Blocks()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.DailyLossPercent = null;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.BasketDataMissing, RiskGateVerdict.Block, "DailyLossPercent");
    }

    [Theory]
    [InlineData(1.99, RiskGateVerdict.Allow)]
    [InlineData(2.0, RiskGateVerdict.Allow)]
    [InlineData(2.01, RiskGateVerdict.Block)]
    public void LegSpread_IsJudgedAtTheBoundary(double spreadPips, RiskGateVerdict expected)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs[0].SpreadPips = spreadPips;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegSpreadExceeded, expected, "EURUSD");
    }

    [Fact]
    public void LegSpread_WithTheSameValue_IsJudgedDifferentlyByMarket()
    {
      // The two limits are deliberately apart: the same spread is acceptable on a metal and unacceptable
      // on a major FX pair, which is the whole point of configuring them per market.
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs[0].SpreadPips = 3.5;
      input.Legs[1].SpreadPips = 3.5;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Block, "EURUSD");
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Allow, "XAUUSD");
    }

    [Fact]
    public void LegVolatility_WithTheSameValue_IsJudgedDifferentlyByMarket()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs[0].VolatilityPercent = 18.0;
      input.Legs[1].VolatilityPercent = 18.0;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegVolatilityExceeded, RiskGateVerdict.Block, "EURUSD");
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegVolatilityExceeded, RiskGateVerdict.Allow, "XAUUSD");
    }

    [Fact]
    public void LegLimits_WhenALegHasNoLimit_BlocksOnlyThatLeg()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs[1].SpreadLimit = null;
      input.Legs[1].VolatilityLimit = null;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      // The leg without a decision borrows nothing from another leg or from its market: it blocks on its
      // own missing limit.
      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      Assert.Equal(2, decision.Gates.Count(item => item.Code == RiskGateCode.ThresholdNotConfigured && item.Subject == "XAUUSD"));
      Assert.DoesNotContain(decision.Gates, item => item.Subject == "EURUSD" && item.Code == RiskGateCode.ThresholdNotConfigured);
      Assert.All(
        decision.Gates.Where(item => item.Subject == "XAUUSD" && item.Code == RiskGateCode.ThresholdNotConfigured),
        item => Assert.Equal(MarketKind.Metal, item.Market));
    }

    [Fact]
    public void LegLimits_WithTheSameMarketJudgedByDifferentLimits_UsesTheLimitOfEachLeg()
    {
      RiskEvaluationInput input = CreateHealthyInput();

      // One FX leg tolerates the observed spread, the other does not: the limit travelled with the leg and
      // is not inherited from the market the two legs share.
      input.Legs = new List<RiskEvaluationLeg>
      {
        new RiskEvaluationLeg { Symbol = "EURUSD", Market = MarketKind.Fx, Weight = 50, SpreadPips = 1.0, VolatilityPercent = 7.0, SpreadLimit = 1.5, VolatilityLimit = 15.0, IsExecutable = true },
        new RiskEvaluationLeg { Symbol = "GBPUSD", Market = MarketKind.Fx, Weight = 50, SpreadPips = 1.0, VolatilityPercent = 7.0, SpreadLimit = 0.5, VolatilityLimit = 15.0, IsExecutable = true }
      };

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Allow, "EURUSD");
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegSpreadExceeded, RiskGateVerdict.Block, "GBPUSD");
    }

    [Fact]
    public void LegGates_CarryTheMarketTheyWereJudgedAgainst()
    {
      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(CreateHealthyInput());

      Assert.All(
        decision.Gates.Where(item => item.Subject == "EURUSD"),
        item => Assert.Equal(MarketKind.Fx, item.Market));
      Assert.All(
        decision.Gates.Where(item => item.Subject == "XAUUSD"),
        item => Assert.Equal(MarketKind.Metal, item.Market));

      // Basket level gates are market agnostic and must not pretend otherwise.
      Assert.All(
        decision.Gates.Where(item => item.Subject == "basket"),
        item => Assert.Null(item.Market));
    }

    [Theory]
    [InlineData(14.99, RiskGateVerdict.Allow)]
    [InlineData(15.0, RiskGateVerdict.Allow)]
    [InlineData(15.01, RiskGateVerdict.Block)]
    public void LegVolatility_IsJudgedAtTheBoundary(double volatilityPercent, RiskGateVerdict expected)
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.Legs[0].VolatilityPercent = volatilityPercent;

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.LegVolatilityExceeded, expected, "EURUSD");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LegMarketData_WhenMissing_BlocksRegardlessOfTheOtherLeg(bool breakSpread)
    {
      RiskEvaluationInput input = CreateHealthyInput();

      if (breakSpread)
      {
        input.Legs[0].SpreadPips = null;
      }
      else
      {
        input.Legs[1].VolatilityPercent = null;
      }

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      Assert.Contains(decision.Gates, item => item.Code == RiskGateCode.LegDataMissing && item.Verdict == RiskGateVerdict.Block);
    }

    [Fact]
    public void LegLimits_WithoutConfiguration_Block()
    {
      RiskEvaluationInput input = CreateHealthyInput();

      foreach (RiskEvaluationLeg leg in input.Legs)
      {
        leg.SpreadLimit = null;
        leg.VolatilityLimit = null;
      }

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      Assert.Equal(2, decision.Gates.Count(item => item.Code == RiskGateCode.ThresholdNotConfigured && item.Subject == "EURUSD"));
      Assert.Equal(2, decision.Gates.Count(item => item.Code == RiskGateCode.ThresholdNotConfigured && item.Subject == "XAUUSD"));
    }

    [Fact]
    public void Aggregate_BlockingWinsOverReview()
    {
      RiskEvaluationInput input = CreateHealthyInput();
      input.FailurePolicy = FailurePolicy.RequireConfirmation;
      input.BasketRiskPercent = 5.0;

      // Coverage is partial but above the minimum, so on its own it would only require review; the risk
      // gate blocks, and block wins.
      input.Legs = new List<RiskEvaluationLeg>
      {
        new RiskEvaluationLeg { Symbol = "EURUSD", Weight = 80, SpreadPips = 0.8, VolatilityPercent = 7.0, SpreadLimit = 2.0, VolatilityLimit = 15.0, IsExecutable = true },
        new RiskEvaluationLeg { Symbol = "XAUUSD", Weight = 20, SpreadPips = 1.5, VolatilityPercent = 12.0, SpreadLimit = 4.0, VolatilityLimit = 25.0, IsExecutable = false }
      };

      RiskDecisionDto decision = CreateEngine(CreateClock(), CreateConfiguredThresholds()).Evaluate(input);

      RiskDecisionDtoAssertions.AssertVerdict(decision, RiskGateVerdict.Block);
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.CoverageBelowMinimum, RiskGateVerdict.Review, "basket");
      RiskDecisionDtoAssertions.AssertGate(decision, RiskGateCode.RiskPerBasketExceeded, RiskGateVerdict.Block, "basket");
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(null, false)]
    public void ThresholdConfiguration_IsReportedHonestly(int? age, bool expected)
    {
      RiskThresholds thresholds = CreateConfiguredThresholds();
      thresholds.SnapshotMaxAgeSeconds = age;

      Assert.Equal(expected, thresholds.IsConfigured);
    }
  }

  /// <summary>Focused assertions so the table cases stay one line each.</summary>
  internal static class RiskDecisionDtoAssertions
  {
    public static void AssertVerdict(RiskDecisionDto decision, RiskGateVerdict expected)
    {
      Assert.Equal(expected, decision.Verdict);
    }

    public static void AssertGate(RiskDecisionDto decision, RiskGateCode code, RiskGateVerdict expected, string subject)
    {
      RiskGateResultDto gate = Assert.Single(
        decision.Gates,
        item => item.Code == code && item.Subject == subject);

      Assert.Equal(expected, gate.Verdict);
    }
  }
}
