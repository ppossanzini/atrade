using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>
    /// Validation type: policy value ranges. Minimum coverage must be a percentage between 50 and
    /// 100, and the risk and daily loss limits must be positive and bounded.
    /// </summary>
    public class ValidateBasketPolicyValues : IRequest<bool>
    {
        public BasketPolicyDto Policy { get; set; }
    }
}
