using System;
using System.Collections.Generic;
using AutoTrade.Trading.Handlers.Risk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Risk
{
  /// <summary>
  /// The snapshot validity window is read from configuration by hand, so the reading itself is pinned: a
  /// missing, empty or nonsensical value must stay unconfigured and therefore block, never fall back to a
  /// default. The leg limits are not read here any more, because they belong to the leg.
  /// </summary>
  public class RiskThresholdsFactoryTests
  {
    private static RiskThresholds FromSettings(Dictionary<string, string> settings)
    {
      IConfiguration configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(settings)
        .Build();

      return RiskThresholdsFactory.FromConfiguration(configuration);
    }

    [Fact]
    public void FromConfiguration_NullConfiguration_IsRejected()
    {
      Assert.Throws<ArgumentNullException>(() => RiskThresholdsFactory.FromConfiguration(null));
    }

    [Fact]
    public void FromConfiguration_ReadsTheSnapshotWindow()
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:SnapshotMaxAgeSeconds", "90" }
      });

      Assert.Equal(90, thresholds.SnapshotMaxAgeSeconds);
      Assert.True(thresholds.IsConfigured);
    }

    [Fact]
    public void FromConfiguration_WithoutAnyRiskSection_LeavesTheWindowUnconfigured()
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>());

      Assert.Null(thresholds.SnapshotMaxAgeSeconds);
      Assert.False(thresholds.IsConfigured);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void FromConfiguration_WithANonPositiveOrInvalidWindow_LeavesItUnconfigured(string value)
    {
      RiskThresholds thresholds = FromSettings(new Dictionary<string, string>
      {
        { "Trading:Risk:SnapshotMaxAgeSeconds", value }
      });

      Assert.Null(thresholds.SnapshotMaxAgeSeconds);
      Assert.False(thresholds.IsConfigured);
    }
  }
}
