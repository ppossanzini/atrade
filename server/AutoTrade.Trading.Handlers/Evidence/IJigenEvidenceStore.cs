using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// What executed the embedding model. The same model run by two engines is not guaranteed to produce the
  /// same vectors: quantisation, preprocessing and the task prefix differ, and a text embedded as a document
  /// is not the same vector as one embedded as a query. So the engine is part of the identity of a space.
  /// </summary>
  /// <remarks>
  /// There is deliberately no externally hosted engine here. Embedding runs in process with the same runtime
  /// that ranks the vectors, so a retrieval cannot depend on a service being up, and the numbers compared are
  /// the numbers produced by the code that owns them.
  /// </remarks>
  public enum EmbeddingEngineKind
  {
    /// <summary>Not selected. Starting at one means a forgotten assignment cannot pass for a real engine.</summary>
    Unset = 0,

    /// <summary>Jigen's own in-process ONNX runtime.</summary>
    JigenOnnx = 1
  }

  /// <summary>
  /// Which part of the semantic memory a record belongs to. The engine, the embedding model and the text
  /// version are part of the identity, not attributes of a record: vectors produced by different models are
  /// not comparable, and neither are the same model run by two engines, nor texts rendered by a different
  /// instruction. Making them part of the name means two incompatible spaces can never merge, nothing has to
  /// remember to keep them apart at run time, and changing engine or model creates a new collection instead of
  /// silently mixing numbers that were produced differently.
  /// </summary>
  public class EvidenceCollection
  {
    /// <summary>Logical area of memory, for example "episodes".</summary>
    public string Name { get; set; }

    /// <summary>Engine that produced, and will produce, every vector in this collection.</summary>
    public EmbeddingEngineKind Engine { get; set; }

    /// <summary>Model that produced, and will produce, every vector in this collection.</summary>
    public string EmbeddingModel { get; set; }

    /// <summary>
    /// How an episode was rendered into text before being embedded. Rewording that rendering changes the
    /// vectors, so it is versioned exactly like a prompt.
    /// </summary>
    public string TextVersion { get; set; }

    /// <summary>
    /// The name the store sees. The separator is not a colon because model names already contain one.
    /// </summary>
    public string EffectiveName
    {
      get
      {
        if (string.IsNullOrWhiteSpace(Name)
          || Engine == EmbeddingEngineKind.Unset
          || !Enum.IsDefined(typeof(EmbeddingEngineKind), Engine)
          || string.IsNullOrWhiteSpace(EmbeddingModel)
          || string.IsNullOrWhiteSpace(TextVersion))
        {
          throw new InvalidOperationException("A collection needs a name, an embedding engine, an embedding model and a text version: "
            + "without all of them, evidence produced in incompatible ways could end up sharing one space.");
        }

        return Name + "@" + Engine + "@" + EmbeddingModel + "@" + TextVersion;
      }
    }
  }

  /// <summary>
  /// One piece of semantic memory. It carries the vector because the store ranks by similarity, and the
  /// metadata needed to make the retrieval reproducible later: which query asked for it, and which
  /// transactional record it belongs to. The embedding model is not repeated here because the collection
  /// names it, and one fact in two places is one fact too many.
  /// </summary>
  public class EvidenceRecord
  {
    public Guid EvidenceId { get; set; }

    public EvidenceCollection Collection { get; set; }

    /// <summary>Text handed to the model, and returned with a match so a retrieval can be read without a lookup.</summary>
    public string Content { get; set; }

    public float[] Embedding { get; set; }

    /// <summary>Query this evidence was retrieved for, when it came from a retrieval.</summary>
    public string Query { get; set; }

    /// <summary>Transactional reference (proposal, execution, episode) this evidence belongs to.</summary>
    public string SourceRef { get; set; }

    public DateTime RecordedAtUtc { get; set; }
  }

  /// <summary>What to look for. Top is explicit because a retrieval without a bound is not a decision.</summary>
  public class EvidenceQuery
  {
    /// <summary>
    /// Collection to search. Its embedding model has to be the one that produced <see cref="Embedding"/>, and
    /// that is the only thing that keeps the comparison meaningful: nothing is checked at run time, because
    /// the collection name already guarantees it.
    /// </summary>
    public EvidenceCollection Collection { get; set; }

    public float[] Embedding { get; set; }

    public int Top { get; set; }
  }

  public class EvidenceMatch
  {
    public Guid EvidenceId { get; set; }

    /// <summary>Cosine similarity, as reported by the store.</summary>
    public double Score { get; set; }

    public string Content { get; set; }

    /// <summary>Collection the match came from, so its model and text version travel with the result.</summary>
    public EvidenceCollection Collection { get; set; }

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
