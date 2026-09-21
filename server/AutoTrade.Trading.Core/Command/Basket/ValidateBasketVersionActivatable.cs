using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>
    /// Validation type: state-transition legality of activation. The version must belong to the
    /// basket, be published, and the basket must not be archived.
    /// </summary>
    public class ValidateBasketVersionActivatable : IRequest<bool>
    {
        public Guid BasketId { get; set; }
        public Guid VersionId { get; set; }
    }
}
