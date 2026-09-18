using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Operator-owned leg definition. Analysis values (score, correlation, volatility, spread, leg
  /// status) are produced by the analysis pipeline and are deliberately not part of this contract.
  /// </summary>
  public class BasketCompositionLegDto
  {
    public string Symbol { get; set; }
    public MarketKind Market { get; set; }
    public LegDirection Direction { get; set; }
    public TimeFrame TimeFrame { get; set; }
    public int Weight { get; set; }
    public double RiskCap { get; set; }
    public bool IsSelected { get; set; }
  }
}
