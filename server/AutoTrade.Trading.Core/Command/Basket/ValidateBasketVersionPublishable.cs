using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Validation type: state-transition legality of publishing. The basket must exist, not be
  /// archived, and its draft must pass the composition and policy validations.
  /// </summary>
  public class ValidateBasketVersionPublishable : IRequest<bool>
  {
    public Guid BasketId { get; set; }
  }
}
