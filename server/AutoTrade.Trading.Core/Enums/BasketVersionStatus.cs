namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Derived version state. A persisted version row is always published, because the editable
    /// state lives in the draft tables; the active and superseded labels come from the single
    /// active-version pointer.
    /// </summary>
    public enum BasketVersionStatus
    {
        Published = 0,
        Active = 1,
        Superseded = 2,
        Archived = 3
    }
}
