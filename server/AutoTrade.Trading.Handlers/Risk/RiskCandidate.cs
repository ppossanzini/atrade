using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Risk
{
  /// <summary>
  /// One leg of the version under evaluation, as stored. The declared risk cap travels with the leg
  /// because the basket risk is the sum of what the composition declared, never a value guessed from
  /// prices.
  /// </summary>
  public class RiskCandidateLeg
  {
    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public int Weight { get; set; }

    public double RiskCap { get; set; }
  }

  /// <summary>
  /// Stored state a risk evaluation starts from. It carries no market values at all: those arrive in the
  /// capture, so the same stored state can be judged against different market instants.
  /// </summary>
  public class RiskCandidate
  {
    public bool HasActiveVersion { get; set; }

    public int VersionNumber { get; set; }

    public bool KillSwitchEngaged { get; set; }

    public FailurePolicy FailurePolicy { get; set; }

    public int MinimumCoverage { get; set; }

    public double RiskPerBasketLimit { get; set; }

    public double DailyLossLimit { get; set; }

    public List<RiskCandidateLeg> Legs { get; set; }
  }
}
