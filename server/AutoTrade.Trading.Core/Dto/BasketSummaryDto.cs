using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    public class BasketSummaryDto
    {
        public Guid BasketId { get; set; }
        public string Name { get; set; }
        public BasketStatus Status { get; set; }
        public int ActiveVersionNumber { get; set; }
        public int LatestVersionNumber { get; set; }
        public int SelectedLegCount { get; set; }
        public int TotalWeight { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
