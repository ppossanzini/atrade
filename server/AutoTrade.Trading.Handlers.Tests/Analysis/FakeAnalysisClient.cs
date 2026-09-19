using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Analysis;

namespace AutoTrade.Trading.Handlers.Tests.Analysis
{
  /// <summary>
  /// Stand-in for the local model. Tests exercise the seam and the status reporting without a model running;
  /// the transport itself is covered by <see cref="OllamaAnalysisClientTests"/>.
  /// </summary>
  internal sealed class FakeAnalysisClient : IOllamaAnalysisClient
  {
    public bool IsAvailable { get; set; }

    /// <summary>Answer returned by the next call. Null means the model produced nothing usable.</summary>
    public AnalysisOpinion Opinion { get; set; }

    public string FailureReason { get; set; }

    public List<AnalysisRequest> Requests { get; } = new List<AnalysisRequest>();

    public bool WasCheckedForPresence { get; private set; }

    public Task EnsureModelIsPresentAsync(CancellationToken cancellationToken)
    {
      WasCheckedForPresence = true;

      return Task.CompletedTask;
    }

    public Task<AnalysisResult> AnalyseAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
      Requests.Add(request);

      return Task.FromResult(new AnalysisResult
      {
        IsAvailable = IsAvailable,
        Opinion = Opinion,
        FailureReason = FailureReason
      });
    }
  }
}
