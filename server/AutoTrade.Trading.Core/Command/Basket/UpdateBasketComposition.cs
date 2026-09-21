using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>Update scope: the draft composition legs.</summary>
    public class UpdateBasketComposition : IRequest<BasketOperationResult>
    {
        public Guid BasketId { get; set; }
        public List<BasketCompositionLegDto> Legs { get; set; }
        public Guid OperatorId { get; set; }
    }
}
