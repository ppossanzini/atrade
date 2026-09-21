using System;
using System.Collections.Generic;
using System.Linq;
using AutoTrade.Trading.Handlers.Broker.Protocol;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// Fixed cTrader volatility contract used by the risk input. It uses the latest 96 closed M15 bars,
  /// close-to-close logarithmic returns, sample standard deviation and a 252-session annualisation.
  /// The result is a percentage, matching the risk engine's percentage thresholds.
  /// </summary>
  public static class CtraderVolatilityContract
  {
    public const ProtoOATrendbarPeriod Period = ProtoOATrendbarPeriod.M15;

    public const int BarCount = 96;

    public const int BarsPerSession = 96;

    public const int SessionsPerYear = 252;

    public static readonly TimeSpan BarDuration = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan MaximumBarAge = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(24);
  }

  public static class CtraderVolatilityCalculator
  {
    public static double? Calculate(IReadOnlyList<ProtoOATrendbar> trendbars, DateTime capturedAtUtc)
    {
      if (trendbars == null || trendbars.Count < CtraderVolatilityContract.BarCount)
      {
        return null;
      }

      if (capturedAtUtc.Kind == DateTimeKind.Unspecified)
      {
        capturedAtUtc = DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc);
      }
      else if (capturedAtUtc.Kind != DateTimeKind.Utc)
      {
        capturedAtUtc = capturedAtUtc.ToUniversalTime();
      }

      List<ClosedBar> bars = new List<ClosedBar>();

      foreach (ProtoOATrendbar trendbar in trendbars)
      {
        if (trendbar == null || trendbar.Volume <= 0 || (trendbar.HasPeriod && trendbar.Period != CtraderVolatilityContract.Period)
          || !trendbar.HasLow || !trendbar.HasDeltaClose || !trendbar.HasUtcTimestampInMinutes)
        {
          return null;
        }

        double close = (double)trendbar.Low + trendbar.DeltaClose;
        DateTime openedAtUtc;
        try
        {
          openedAtUtc = DateTime.UnixEpoch.AddMinutes(trendbar.UtcTimestampInMinutes);
        }
        catch (ArgumentOutOfRangeException)
        {
          return null;
        }
        DateTime closedAtUtc = openedAtUtc.Add(CtraderVolatilityContract.BarDuration);

        if (!double.IsFinite(close) || close <= 0 || openedAtUtc > capturedAtUtc || closedAtUtc > capturedAtUtc)
        {
          return null;
        }

        bars.Add(new ClosedBar(openedAtUtc, close));
      }

      List<ClosedBar> ordered = bars
        .OrderBy(item => item.OpenedAtUtc)
        .ToList();

      if (ordered.Count != ordered.Select(item => item.OpenedAtUtc).Distinct().Count())
      {
        return null;
      }

      ClosedBar latest = ordered[ordered.Count - 1];
      DateTime latestCloseUtc = latest.OpenedAtUtc.Add(CtraderVolatilityContract.BarDuration);
      TimeSpan latestAge = capturedAtUtc - latestCloseUtc;
      if (latestAge < TimeSpan.Zero || latestAge > CtraderVolatilityContract.MaximumBarAge)
      {
        return null;
      }

      List<ClosedBar> window = ordered
        .Skip(Math.Max(0, ordered.Count - CtraderVolatilityContract.BarCount))
        .ToList();

      if (window.Count != CtraderVolatilityContract.BarCount)
      {
        return null;
      }

      List<double> returns = new List<double>(window.Count - 1);
      for (int index = 1; index < window.Count; index++)
      {
        if (window[index - 1].Close <= 0 || window[index].Close <= 0)
        {
          return null;
        }

        double value = Math.Log(window[index].Close / window[index - 1].Close);
        if (!double.IsFinite(value))
        {
          return null;
        }

        returns.Add(value);
      }

      if (returns.Count < 2)
      {
        return null;
      }

      double mean = returns.Average();
      double sumSquared = returns.Sum(item => Math.Pow(item - mean, 2));
      double standardDeviation = Math.Sqrt(sumSquared / (returns.Count - 1));
      double annualizedPercent = standardDeviation
        * Math.Sqrt(CtraderVolatilityContract.SessionsPerYear * CtraderVolatilityContract.BarsPerSession)
        * 100;

      return double.IsFinite(annualizedPercent) ? annualizedPercent : (double?)null;
    }

    private readonly struct ClosedBar
    {
      public ClosedBar(DateTime openedAtUtc, double close)
      {
        OpenedAtUtc = openedAtUtc;
        Close = close;
      }

      public DateTime OpenedAtUtc { get; }

      public double Close { get; }
    }
  }
}
