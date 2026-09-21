using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>
    /// Validation type: referential coherence of a composition. Symbols must be present, well formed
    /// and unique, and risk caps must stay inside the allowed range.
    /// </summary>
    public class ValidateBasketCompositionSymbols : IRequest<bool>
    {
        public List<BasketCompositionLegDto> Legs { get; set; }
    }
}
