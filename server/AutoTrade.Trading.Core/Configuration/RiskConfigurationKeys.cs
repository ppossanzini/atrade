namespace AutoTrade.Trading.Core.Configuration
{
    /// <summary>
    /// Configuration keys of the risk section. No threshold has a default in code: an unconfigured limit
    /// makes its gate block, so a missing decision can never become a permissive one.
    ///
    /// Only the snapshot validity window is here. It describes how fresh the data of the feed must be, which
    /// is a property of the feed and not a decision about a basket. The leg limits are not settings: they
    /// belong to the leg, because one value cannot describe both a major FX pair and an index, and because a
    /// limit that shapes a decision has to be versioned and journalled with it.
    /// </summary>
    public static class RiskConfigurationKeys
    {
        public const string Section = "Trading:Risk";

        public const string SnapshotMaxAgeSeconds = "Trading:Risk:SnapshotMaxAgeSeconds";
    }
}
