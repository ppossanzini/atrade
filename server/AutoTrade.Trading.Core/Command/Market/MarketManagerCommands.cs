using System;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Market
{
  /// <summary>
  /// Changes the operating mode. The mode governs who decides, so the change is a decision of the
  /// operator and is journalled as such.
  /// </summary>
  public class SetMarketManagerMode : IRequest<MarketManagerMode>
  {
    public MarketManagerMode Mode { get; set; }

    public Guid OperatorId { get; set; }
  }

  /// <summary>Starts or stops the continuous analysis. Stopping never cancels proposals already issued.</summary>
  public class SetAnalysisState : IRequest<bool>
  {
    public bool IsRunning { get; set; }

    public Guid OperatorId { get; set; }
  }

  /// <summary>
  /// One iteration of the analysis cycle. It is a request rather than a method so the worker, a test and
  /// a future scheduler all run the same code path.
  /// </summary>
  public class RunAnalysisCycle : IRequest<AnalysisCycleResultDto>
  {
  }
}
