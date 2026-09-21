using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    public class MarketManagerStatusDto
    {
        public MarketManagerMode Mode { get; set; }
        public bool IsAnalysisRunning { get; set; }

        /// <summary>Instant of the last completed cycle, null when the analysis never ran.</summary>
        public DateTime? LastCycleAtUtc { get; set; }
    }
}
