using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Creates a registry entry with an empty draft composition. Legs are added through
  /// <see cref="UpdateBasketComposition"/> so nothing is invented on the operator's behalf.
  /// </summary>
  public class CreateBasket : IRequest<CreateBasketResult>
  {
    public string Name { get; set; }
    public Guid OperatorId { get; set; }
  }
}
