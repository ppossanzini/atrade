using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Basket
{
  /// <summary>Read condition: version history of one basket, newest first.</summary>
  public class GetBasketVersionsByBasket : IRequest<List<BasketVersionDto>>
  {
    public Guid BasketId { get; set; }
  }
}
