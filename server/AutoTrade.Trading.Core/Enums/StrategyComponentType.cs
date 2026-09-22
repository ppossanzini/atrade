namespace AutoTrade.Trading.Core.Enums
{
    /// <summary>Supported building blocks of a composed basket strategy.</summary>
    public enum StrategyComponentType
    {
        TrendFollowing = 0,
        Momentum = 1,
        Breakout = 2,
        MeanReversion = 3,
        VolatilityFilter = 4,
        NoTrade = 5
    }
}
