using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Basket
{
  /// <summary>Read condition: the basket that currently holds the active version.</summary>
  public class GetActiveBasket : IRequest<BasketDetailDto>
  {
  }
}
