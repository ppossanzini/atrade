namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Behaviour when an execution cannot place every leg as intended.
  /// </summary>
  public enum FailurePolicy
  {
    MinimumCoverage = 0,
    AllOrNothing = 1,
    RequireConfirmation = 2
  }
}
