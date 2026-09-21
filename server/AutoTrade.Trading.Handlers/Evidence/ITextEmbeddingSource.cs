using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Turns text into a vector, in process. It is a separate seam from the store because embedding and ranking
  /// are different jobs that happen to share a format: the store compares numbers it is given, and it must
  /// never be the thing that decides how text became numbers.
  /// </summary>
  /// <remarks>
  /// The two roles are deliberately separate methods. A document and a query are not embedded the same way:
  /// the model is instructed differently for each, and swapping them degrades retrieval quietly instead of
  /// failing, which is the worst way for a retrieval to be wrong.
  /// </remarks>
  public interface ITextEmbeddingSource
  {
    /// <summary>False when no source is configured, or the configured one could not be loaded.</summary>
    bool IsAvailable { get; }

    /// <summary>What executes the model. It is part of a collection's identity, not a detail.</summary>
    EmbeddingEngineKind Engine { get; }

    /// <summary>Model producing the vectors. Semantic memory is not comparable across models.</summary>
    string ModelName { get; }

    /// <summary>Embeds an episode for storage. Fails rather than degrading: without a vector there is no record.</summary>
    Task<float[]> EmbedDocumentAsync(string text, CancellationToken cancellationToken);

    /// <summary>Embeds a retrieval query. Must be the same model as the documents, embedded as a query.</summary>
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken);
  }
}
