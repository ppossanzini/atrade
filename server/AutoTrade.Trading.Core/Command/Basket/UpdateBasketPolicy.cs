using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>Update scope: the draft policy.</summary>
    public class UpdateBasketPolicy : IRequest<BasketOperationResult>
    {
        public Guid BasketId { get; set; }
        public BasketPolicyDto Policy { get; set; }
        public Guid OperatorId { get; set; }
    }
}
