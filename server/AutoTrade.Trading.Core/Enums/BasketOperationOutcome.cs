namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Outcome of a basket lifecycle command. Commands never throw for an expected rejection:
  /// the outcome lets the caller map it to a transport response and the journal to record it.
  /// </summary>
  public enum BasketOperationOutcome
  {
    Applied = 0,
    InvalidInput = 1,
    Conflict = 2,
    InvalidState = 3,
    NotFound = 4
  }
}
