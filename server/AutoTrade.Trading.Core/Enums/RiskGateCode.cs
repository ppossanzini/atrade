namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Stable gate codes. They are persisted in journal payloads and shown to the operator, so the numeric
    /// values are part of the audit contract and must never be reused for a different rule.
    /// </summary>
    public enum RiskGateCode
    {
        ActiveVersionMissing = 1,
        KillSwitchEngaged = 2,
        SnapshotMissing = 3,
        SnapshotStale = 4,
        CoverageBelowMinimum = 5,
        RiskPerBasketExceeded = 6,
        DailyLossExceeded = 7,
        LegSpreadExceeded = 8,
        LegVolatilityExceeded = 9,
        LegDataMissing = 10,

        /// <summary>Basket level input unavailable, for example the current risk or the daily loss.</summary>
        BasketDataMissing = 11,

        /// <summary>
        /// A configured threshold is missing. Kept distinct from missing market data, because the remedy is
        /// configuring the limit, not waiting for the feed.
        /// </summary>
        ThresholdNotConfigured = 12
    }
}
