namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Instrument class carried by a leg. The broker catalogue is the source of truth for which
  /// classes are actually available; this classification keeps the basket readable without it.
  /// </summary>
  public enum MarketKind
  {
    Fx = 0,
    Metal = 1,
    Index = 2
  }
}
