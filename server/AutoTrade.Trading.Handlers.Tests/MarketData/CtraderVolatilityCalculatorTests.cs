using System;
using System.Collections.Generic;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.MarketData;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.MarketData
{
  public class CtraderVolatilityCalculatorTests
  {
    private static readonly DateTime CapturedAtUtc = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_IsDeterministicAndReturnsAnnualizedPercentage()
    {
      List<ProtoOATrendbar> bars = CreateBars(i => 100000 + (i * 7) + (i % 3));

      double? first = CtraderVolatilityCalculator.Calculate(bars, CapturedAtUtc);
      double? second = CtraderVolatilityCalculator.Calculate(bars, CapturedAtUtc);

      Assert.NotNull(first);
      Assert.True(first.Value > 0);
      Assert.Equal(first, second);
    }

    [Fact]
    public void Calculate_WithInsufficientBars_FailsClosed()
    {
      List<ProtoOATrendbar> bars = CreateBars(i => 100000, CtraderVolatilityContract.BarCount - 1);

      Assert.Null(CtraderVolatilityCalculator.Calculate(bars, CapturedAtUtc));
    }

    [Fact]
    public void Calculate_WithMalformedOrStaleBar_FailsClosed()
    {
      List<ProtoOATrendbar> malformed = CreateBars(i => 100000);
      malformed[10].ClearDeltaClose();

      Assert.Null(CtraderVolatilityCalculator.Calculate(malformed, CapturedAtUtc));

      List<ProtoOATrendbar> stale = CreateBars(i => 100000);
      stale[stale.Count - 1].UtcTimestampInMinutes = checked((uint)(CapturedAtUtc.AddHours(-2) - DateTime.UnixEpoch).TotalMinutes);

      Assert.Null(CtraderVolatilityCalculator.Calculate(stale, CapturedAtUtc));
    }

    [Fact]
    public void Calculate_WithDuplicateTimestamp_FailsClosed()
    {
      List<ProtoOATrendbar> bars = CreateBars(i => 100000);
      bars[95].UtcTimestampInMinutes = bars[94].UtcTimestampInMinutes;

      Assert.Null(CtraderVolatilityCalculator.Calculate(bars, CapturedAtUtc));
    }

    private static List<ProtoOATrendbar> CreateBars(Func<int, int> close, int count = CtraderVolatilityContract.BarCount)
    {
      List<ProtoOATrendbar> bars = new List<ProtoOATrendbar>();
      for (int index = 0; index < count; index++)
      {
        DateTime openedAtUtc = CapturedAtUtc.Add(CtraderVolatilityContract.BarDuration.Multiply(-(count - index)));
        int closeValue = close(index);
        bars.Add(new ProtoOATrendbar
        {
          Volume = 1,
          Period = CtraderVolatilityContract.Period,
          Low = 100000,
          DeltaClose = checked((uint)(closeValue - 100000)),
          DeltaOpen = 0,
          DeltaHigh = checked((uint)(closeValue - 100000)),
          UtcTimestampInMinutes = checked((uint)(openedAtUtc - DateTime.UnixEpoch).TotalMinutes)
        });
      }

      return bars;
    }
  }
}
