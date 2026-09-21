namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Analysis model as the application sees it. <see cref="IsAvailable"/> is the fact a caller needs: an
    /// unavailable model produces no opinion, it never grants authority, and it must be visible as such instead
    /// of being inferred from a missing rationale.
    /// </summary>
    public class AnalysisModelStatusDto
    {
        /// <summary>Provider in force, for example None or Ollama.</summary>
        public string Provider { get; set; }

        /// <summary>Model the provider was told to use. Reported even when it is not available, so the operator sees what was asked for.</summary>
        public string Model { get; set; }

        public bool IsAvailable { get; set; }
    }
}
