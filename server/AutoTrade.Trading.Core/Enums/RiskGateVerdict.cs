namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>
    /// Verdict vocabulary, aligned with the approved prototype wording: approved, review, blocked.
    /// Block always wins over Review, and Review wins over Allow.
    /// </summary>
    public enum RiskGateVerdict
    {
        Allow = 0,
        Review = 1,
        Block = 2
    }
}
