using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>
    /// Validation type: weight coherence of a composition. Selected legs must reach exactly 100%,
    /// each selected weight must be positive, and at least one leg must be selected.
    /// </summary>
    public class ValidateBasketCompositionWeights : IRequest<bool>
    {
        public List<BasketCompositionLegDto> Legs { get; set; }
    }
}
