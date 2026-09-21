using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
    /// <summary>Validation type: presence and format of the basket name.</summary>
    public class ValidateBasketNamePresence : IRequest<bool>
    {
        public string Name { get; set; }
    }
}
