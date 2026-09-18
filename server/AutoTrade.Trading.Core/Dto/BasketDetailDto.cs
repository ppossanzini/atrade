using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Registry detail: the editable draft plus which published version is currently active.
  /// </summary>
  public class BasketDetailDto
  {
    public Guid BasketId { get; set; }
    public string Name { get; set; }
    public BasketStatus Status { get; set; }
    public int LatestVersionNumber { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public int ActiveVersionNumber { get; set; }
    public List<BasketCompositionLegDto> DraftLegs { get; set; }
    public BasketPolicyDto DraftPolicy { get; set; }
  }
}
