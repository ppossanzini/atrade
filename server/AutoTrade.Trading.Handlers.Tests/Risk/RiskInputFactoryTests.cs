using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Risk
{
  /// <summary>
  /// The factory is the only place where stored state meets a market capture, so it is where absence must
  /// survive: an unavailable capture and an unquoted symbol both have to reach the engine as missing data,
  /// never as a zero that would judge the basket on invented values.
  /// </summary>
  public class RiskInputFactoryTests
  {
    private static RiskCandidate Candidate()
    {
      return new RiskCandidate
      {
        HasActiveVersion = true,
        VersionNumber = 3,
        KillSwitchEngaged = false,
        FailurePolicy = FailurePolicy.MinimumCoverage,
        MinimumCoverage = 75,
        RiskPerBasketLimit = 0.8,
        DailyLossLimit = 2.5,
        Legs = new List<RiskCandidateLeg>
        {
          new RiskCandidateLeg { Symbol = "EURUSD", Market = MarketKind.Fx, Weight = 60, RiskCap = 0.5 },
          new RiskCandidateLeg { Symbol = "XAUUSD", Market = MarketKind.Metal, Weight = 40, RiskCap = 0.3 }
        }
      };
    }

    private static MarketDataCapture Capture(AccountCapture account, params SymbolCapture[] symbols)
    {
      return new MarketDataCapture
      {
        IsAvailable = true,
        CapturedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Account = account,
        Symbols = new List<SymbolCapture>(symbols)
      };
    }

    private static AccountCapture HealthyAccount()
    {
      return new AccountCapture
      {
        Equity = 10000,
        Balance = 10000,
        RealizedPnlToday = 0,
        UnrealizedPnl = 0,
        Environment = TradingEnvironment.Demo
      };
    }

    [Fact]
    public void Create_WithoutACapture_LeavesEverythingUnjudged()
    {
      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), MarketDataCapture.Unavailable());

      Assert.Null(input.MarketDataCapturedAtUtc);
      Assert.Null(input.BasketRiskPercent);
      Assert.Null(input.DailyLossPercent);
      Assert.Equal(2, input.Legs.Count);
      Assert.All(input.Legs, leg => Assert.False(leg.IsExecutable));
      Assert.All(input.Legs, leg => Assert.Null(leg.SpreadPips));
      Assert.All(input.Legs, leg => Assert.Null(leg.VolatilityPercent));
    }

    [Fact]
    public void Create_WithACapture_JudgesEveryLegAndTheAccount()
    {
      MarketDataCapture capture = Capture(
        HealthyAccount(),
        new SymbolCapture { Symbol = "EURUSD", Price = 1.085, SpreadPips = 0.6, VolatilityPercent = 0.2, IsTradable = true },
        new SymbolCapture { Symbol = "XAUUSD", Price = 2650, SpreadPips = 24, VolatilityPercent = 0.5, IsTradable = true });

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), capture);

      Assert.Equal(capture.CapturedAtUtc, input.MarketDataCapturedAtUtc);
      Assert.Equal(0.8, input.BasketRiskPercent);
      Assert.Equal(0, input.DailyLossPercent);
      Assert.All(input.Legs, leg => Assert.True(leg.IsExecutable));
      Assert.Equal(0.6, input.Legs[0].SpreadPips);
      Assert.Equal(0.5, input.Legs[1].VolatilityPercent);
    }

    [Fact]
    public void Create_MatchesTheSymbolIgnoringCase()
    {
      MarketDataCapture capture = Capture(
        HealthyAccount(),
        new SymbolCapture { Symbol = "eurusd", SpreadPips = 0.6, VolatilityPercent = 0.2, IsTradable = true });

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), capture);

      Assert.True(input.Legs[0].IsExecutable);
      Assert.Equal(0.6, input.Legs[0].SpreadPips);
      Assert.False(input.Legs[1].IsExecutable);
    }

    [Fact]
    public void Create_WithoutAQuoteForOneLeg_LeavesOnlyThatLegUnjudged()
    {
      MarketDataCapture capture = Capture(
        HealthyAccount(),
        new SymbolCapture { Symbol = "EURUSD", SpreadPips = 0.6, VolatilityPercent = 0.2, IsTradable = true });

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), capture);

      Assert.True(input.Legs[0].IsExecutable);
      Assert.False(input.Legs[1].IsExecutable);
      Assert.Null(input.Legs[1].SpreadPips);
      Assert.Equal(0.8, input.BasketRiskPercent);
    }

    [Fact]
    public void Create_WithAnUntradableSymbol_KeepsItsMeasurementsButNotItsExecutability()
    {
      MarketDataCapture capture = Capture(
        HealthyAccount(),
        new SymbolCapture { Symbol = "EURUSD", SpreadPips = 0.6, VolatilityPercent = 0.2, IsTradable = false });

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), capture);

      Assert.False(input.Legs[0].IsExecutable);
      Assert.Equal(0.6, input.Legs[0].SpreadPips);
    }

    [Fact]
    public void Create_WithoutLegs_CannotReportABasketRisk()
    {
      RiskCandidate candidate = Candidate();
      candidate.Legs = new List<RiskCandidateLeg>();

      RiskEvaluationInput input = RiskInputFactory.Create(candidate, Capture(HealthyAccount()));

      Assert.Null(input.BasketRiskPercent);
    }

    [Fact]
    public void Create_WithANegativeDay_TurnsTheResultIntoALossPercentage()
    {
      AccountCapture account = HealthyAccount();
      account.RealizedPnlToday = -150;
      account.UnrealizedPnl = -50;

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), Capture(account));

      Assert.Equal(2, input.DailyLossPercent);
    }

    [Fact]
    public void Create_WithAProfitableDay_ReportsNoLossInsteadOfACredit()
    {
      AccountCapture account = HealthyAccount();
      account.RealizedPnlToday = 250;

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), Capture(account));

      Assert.Equal(0, input.DailyLossPercent);
    }

    [Fact]
    public void Create_WithoutAUsableEquity_CannotComputeTheDailyLoss()
    {
      AccountCapture account = HealthyAccount();
      account.Equity = 0;
      account.Balance = 0;
      account.RealizedPnlToday = -100;

      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), Capture(account));

      Assert.Null(input.DailyLossPercent);
    }

    [Fact]
    public void Create_WithoutAccountFacts_CannotComputeTheDailyLoss()
    {
      RiskEvaluationInput input = RiskInputFactory.Create(Candidate(), Capture(null));

      Assert.Null(input.DailyLossPercent);
      Assert.NotNull(input.MarketDataCapturedAtUtc);
    }

    [Fact]
    public void Create_KeepsTheStateGatesOfTheStoredCandidate()
    {
      RiskCandidate candidate = Candidate();
      candidate.KillSwitchEngaged = true;

      RiskEvaluationInput input = RiskInputFactory.Create(candidate, MarketDataCapture.Unavailable());

      Assert.True(input.KillSwitchEngaged);
      Assert.True(input.HasActiveVersion);
      Assert.Equal(3, input.VersionNumber);
      Assert.Equal(75, input.MinimumCoverage);
      Assert.Equal(0.8, input.RiskPerBasketLimit);
      Assert.Equal(2.5, input.DailyLossLimit);
    }

    [Fact]
    public void Create_WithoutACandidate_IsRejected()
    {
      Assert.Throws<ArgumentNullException>(() => RiskInputFactory.Create(null, MarketDataCapture.Unavailable()));
    }
  }
}
