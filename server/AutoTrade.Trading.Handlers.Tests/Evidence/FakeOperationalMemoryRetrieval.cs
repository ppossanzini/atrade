using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Evidence;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// Stands in for the semantic memory so a cycle test can decide what the past looks like, without a store,
  /// a model or a checkpoint.
  /// </summary>
  internal sealed class FakeOperationalMemoryRetrieval : IOperationalMemoryRetrieval
  {
    public bool IsAvailable { get; set; }

    public string QueryHash { get; set; } = "hash-of-the-question";

    public string FailureReason { get; set; }

    public List<RetrievedEpisode> Episodes { get; } = new List<RetrievedEpisode>();

    /// <summary>What the application actually asked, so a test can check the question and not just the answer.</summary>
    public string LastSituation { get; private set; }

    public int LastTop { get; private set; }

    public Task<MemoryRetrievalResult> FindSimilarAsync(string situation, int top, CancellationToken cancellationToken)
    {
      LastSituation = situation;
      LastTop = top;

      return Task.FromResult(new MemoryRetrievalResult
      {
        IsAvailable = IsAvailable,
        QueryHash = IsAvailable ? QueryHash : null,
        FailureReason = IsAvailable ? null : FailureReason ?? "The semantic memory is not in force.",
        Episodes = IsAvailable ? new List<RetrievedEpisode>(Episodes) : new List<RetrievedEpisode>()
      });
    }
  }
}
