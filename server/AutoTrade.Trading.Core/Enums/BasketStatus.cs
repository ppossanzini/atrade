namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Derived registry state: a basket is <see cref="Active"/> while it holds the active version,
  /// <see cref="Archived"/> once archived, and <see cref="Inactive"/> otherwise.
  /// </summary>
  public enum BasketStatus
  {
    Active = 0,
    Inactive = 1,
    Archived = 2
  }
}
