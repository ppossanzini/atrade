using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  public class CloneBasket : IRequest<CreateBasketResult>
  {
    public Guid SourceBasketId { get; set; }
    public string Name { get; set; }
    public Guid OperatorId { get; set; }
  }
}
