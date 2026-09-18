using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class BasketVersionDto
  {
    public Guid VersionId { get; set; }
    public int Number { get; set; }
    public BasketVersionStatus Status { get; set; }
    public string Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
  }
}
