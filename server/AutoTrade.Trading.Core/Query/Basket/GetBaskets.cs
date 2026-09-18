using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Basket
{
  /// <summary>Read condition: the full registry, archived entries included.</summary>
  public class GetBaskets : IRequest<List<BasketSummaryDto>>
  {
  }
}
