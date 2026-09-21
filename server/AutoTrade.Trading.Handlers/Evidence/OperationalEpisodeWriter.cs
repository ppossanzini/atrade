using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Remembers an episode. It never reports failure upwards.
  /// </summary>
  /// <remarks>
  /// This is the opposite of the fail-closed rule that governs the rest of the system, and it is not an
  /// inconsistency: there the semantic memory <em>authorises</em>, here it <em>remembers</em>. An execution
  /// must still close when the memory is unavailable, so a failing memory degrades the record and leaves a
  /// trace in the log, instead of turning a completed operation into a failed one.
  /// </remarks>
  public interface IOperationalEpisodeWriter
  {
    Task RecordAsync(OperationalEpisode episode, CancellationToken cancellationToken);
  }

  public class JigenOperationalEpisodeWriter : IOperationalEpisodeWriter
  {
    /// <summary>
    /// Area of memory holding operational episodes. It is not "episodes" on purpose: that name belongs to the
    /// closed trades of the Win/Loss History, which are a different concept.
    /// </summary>
    public const string CollectionName = "operational-episodes";

    private readonly IJigenEvidenceStore store;
    private readonly ITextEmbeddingSource embedding;
    private readonly EmbeddingOptions embeddingOptions;
    private readonly ILogger logger;

    public JigenOperationalEpisodeWriter(
      IJigenEvidenceStore store,
      ITextEmbeddingSource embedding,
      EmbeddingOptions embeddingOptions,
      ILogger<JigenOperationalEpisodeWriter> logger)
    {
      this.store = store ?? throw new ArgumentNullException(nameof(store));
      this.embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
      this.embeddingOptions = embeddingOptions ?? throw new ArgumentNullException(nameof(embeddingOptions));
      this.logger = logger;
    }

    public async Task RecordAsync(OperationalEpisode episode, CancellationToken cancellationToken)
    {
      if (episode == null)
      {
        throw new ArgumentNullException(nameof(episode));
      }

      if (!store.IsAvailable || !embedding.IsAvailable)
      {
        // Reported, not raised: the caller has already committed the operation this episode describes.
        logger?.LogWarning(
          "Episode {EpisodeId} ({Kind}) was not remembered: store available={StoreAvailable}, embedding available={EmbeddingAvailable}.",
          episode.EpisodeId,
          episode.Kind,
          store.IsAvailable,
          embedding.IsAvailable);

        return;
      }

      try
      {
        float[] vector = await embedding.EmbedDocumentAsync(episode.Text, cancellationToken);

        EvidenceRecord record = new EvidenceRecord
        {
          EvidenceId = episode.EpisodeId,
          Collection = embeddingOptions.CreateCollection(CollectionName),
          Content = episode.Text,
          Embedding = vector,

          // The query that produced this evidence is the episode itself: an episode is stored because it
          // happened, not because something searched for it.
          Query = null,
          SourceRef = episode.SourceRef,
          RecordedAtUtc = episode.OccurredAtUtc
        };

        await store.UpsertAsync(new List<EvidenceRecord> { record }, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        // Shutting down is not a memory failure, and swallowing it would hide a real cancellation.
        throw;
      }
      catch (Exception error)
      {
        logger?.LogWarning(error, "Episode {EpisodeId} ({Kind}) was not remembered.", episode.EpisodeId, episode.Kind);
      }
    }
  }
}
