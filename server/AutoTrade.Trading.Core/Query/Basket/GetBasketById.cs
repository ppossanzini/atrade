using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Basket
{
  /// <summary>Read condition: one basket by identity, with its draft and active version.</summary>
  public class GetBasketById : IRequest<BasketDetailDto>
  {
    public Guid BasketId { get; set; }
  }
}
