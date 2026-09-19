using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Execution
{
  /// <summary>The execution queue, most recent first.</summary>
  public class GetExecutionQueue : IRequest<List<ExecutionSummaryDto>>
  {
  }

  /// <summary>Full execution with its legs and the broker events that were applied to it.</summary>
  public class GetExecutionDetail : IRequest<ExecutionDetailDto>
  {
    public Guid ExecutionId { get; set; }
  }
}
