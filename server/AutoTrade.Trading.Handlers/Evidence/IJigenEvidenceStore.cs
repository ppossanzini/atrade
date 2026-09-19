using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// One piece of semantic memory. It carries the vector because the store ranks by similarity, and the
  /// metadata needed to make the retrieval reproducible later: which model produced the embedding, which
  /// query asked for it, and which transactional record it belongs to.
  /// </summary>
  public class EvidenceRecord
  {
    public Guid EvidenceId { get; set; }

    public string Collection { get; set; }

    /// <summary>Text handed to the model, and returned with a match so a retrieval can be read without a lookup.</summary>
    public string Content { get; set; }

    public float[] Embedding { get; set; }

    /// <summary>Model that produced the embedding. Semantic memory is not comparable across models.</summary>
    public string EmbeddingModel { get; set; }

    /// <summary>Query this evidence was retrieved for, when it came from a retrieval.</summary>
    public string Query { get; set; }

    /// <summary>Transactional reference (proposal, execution, episode) this evidence belongs to.</summary>
    public string SourceRef { get; set; }

    public DateTime RecordedAtUtc { get; set; }
  }

  /// <summary>What to look for. Top is explicit because a retrieval without a bound is not a decision.</summary>
  public class EvidenceQuery
  {
    public string Collection { get; set; }

    public float[] Embedding { get; set; }

    public int Top { get; set; }

    /// <summary>Model that produced the query embedding, checked against the stored evidence.</summary>
    public string EmbeddingModel { get; set; }
  }

  public class EvidenceMatch
  {
    public Guid EvidenceId { get; set; }

    /// <summary>Cosine similarity, as reported by the store.</summary>
    public double Score { get; set; }

    public string Content { get; set; }

    public string EmbeddingModel { get; set; }

    public string Query { get; set; }

    public string SourceRef { get; set; }

    public DateTime RecordedAtUtc { get; set; }
  }

  /// <summary>
  /// Outcome of a retrieval. Availability travels with the result on purpose: an empty list of matches and
  /// an unavailable store are different facts, and collapsing them would turn a missing semantic memory into
  /// a silent "nothing was found".
  /// </summary>
  public class EvidenceSearchResult
  {
    public bool IsAvailable { get; set; }

    public List<EvidenceMatch> Matches { get; set; }
  }

  /// <summary>
  /// Semantic memory. It holds evidence and nothing else: it never stores transactional state, never decides
  /// a verdict and never reaches the order gateway. A missing store degrades retrieval, never authority.
  /// </summary>
  public interface IJigenEvidenceStore
  {
    /// <summary>False when no store is configured or it could not be opened.</summary>
    bool IsAvailable { get; }

    Task UpsertAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken cancellationToken);

    Task<EvidenceSearchResult> SearchAsync(EvidenceQuery query, CancellationToken cancellationToken);
  }
}
