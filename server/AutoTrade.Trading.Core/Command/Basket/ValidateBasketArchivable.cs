using System;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Validation type: state-transition legality of archival. An archived basket cannot be archived
  /// again, and the basket currently holding the active version cannot be archived.
  /// </summary>
  public class ValidateBasketArchivable : IRequest<bool>
  {
    public Guid BasketId { get; set; }
  }
}
