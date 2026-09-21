using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    public class CreateBasketResult
    {
        public BasketOperationOutcome Outcome { get; set; }
        public Guid BasketId { get; set; }
    }
}
