using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Freezes the current draft into an immutable published version.
  /// </summary>
  public class PublishBasketVersion : IRequest<PublishBasketVersionResult>
  {
    public Guid BasketId { get; set; }
    public string Note { get; set; }
    public Guid OperatorId { get; set; }
  }
}
