using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>Update scope: basket identity only.</summary>
    public class UpdateBasketIdentity : IRequest<BasketOperationResult>
    {
        public Guid BasketId { get; set; }
        public string Name { get; set; }
        public Guid OperatorId { get; set; }
    }
}
