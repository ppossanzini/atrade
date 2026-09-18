using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Basket
{
  /// <summary>
  /// Non-destructive archival. Versions, and every historical reference to them, are preserved.
  /// </summary>
  public class ArchiveBasket : IRequest<BasketOperationResult>
  {
    public Guid BasketId { get; set; }
    public Guid OperatorId { get; set; }
  }
}
