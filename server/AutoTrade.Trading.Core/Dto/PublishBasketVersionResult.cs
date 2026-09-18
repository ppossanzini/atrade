using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class PublishBasketVersionResult
  {
    public BasketOperationOutcome Outcome { get; set; }
    public Guid VersionId { get; set; }
    public int Number { get; set; }
  }
}
