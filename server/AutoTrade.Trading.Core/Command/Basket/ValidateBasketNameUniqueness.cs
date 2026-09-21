using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>
    /// Validation type: uniqueness of the basket name among non-archived baskets.
    /// <see cref="ExcludedBasketId"/> allows renaming a basket without colliding with itself.
    /// </summary>
    public class ValidateBasketNameUniqueness : IRequest<bool>
    {
        public string Name { get; set; }
        public Guid ExcludedBasketId { get; set; }
    }
}
