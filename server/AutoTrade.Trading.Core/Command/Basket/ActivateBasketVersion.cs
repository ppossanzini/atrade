using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Points the single active-version slot at one published version of this basket.
  /// </summary>
  public class ActivateBasketVersion : IRequest<BasketOperationResult>
  {
    public Guid BasketId { get; set; }
    public Guid VersionId { get; set; }
    public Guid OperatorId { get; set; }
  }
}
