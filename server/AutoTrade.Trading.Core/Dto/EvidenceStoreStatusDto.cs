namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Semantic memory as the application sees it. <see cref="IsAvailable"/> is the fact a caller needs: an
    /// unavailable store degrades retrieval, it never grants authority, and it must be visible as such instead
    /// of being inferred from an empty result.
    /// </summary>
    public class EvidenceStoreStatusDto
    {
        /// <summary>Provider in force, for example None or Jigen.</summary>
        public string Provider { get; set; }

        public bool IsAvailable { get; set; }
    }
}
