using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// One piece of memory that was retrieved for a proposal, recorded so a retrieval can be explained later.
  /// </summary>
  /// <remarks>
  /// Append-only, like the gate evaluations: a retrieval is a fact about what the system was shown, and
  /// rewriting it would make the past unreadable. The vector is deliberately absent: it belongs to the semantic
  /// store, and duplicating it here would create a second copy of something that must have exactly one home.
  /// What is kept is what makes the retrieval reproducible — which episode, how close, produced by which model,
  /// and a hash of the question that found it.
  /// </remarks>
  [Table("ProposalEvidence")]
  public class ProposalEvidence
  {
    [Key]
    public Guid Id { get; set; }

    public Guid ProposalId { get; set; }

    /// <summary>Episode that was retrieved. It identifies a record in the semantic store, not a row here.</summary>
    public Guid EvidenceId { get; set; }

    /// <summary>Position in the result, so a meaningful ranking can be told from an arbitrary one.</summary>
    public int Rank { get; set; }

    public double Score { get; set; }

    /// <summary>Model that produced the query vector. Retrievals are not comparable across models.</summary>
    public string EmbeddingModel { get; set; }

    /// <summary>Hash of the question asked, so the same question can be recognised without storing its text twice.</summary>
    public string QueryHash { get; set; }

    /// <summary>Transactional reference of the retrieved episode, carried so the result can be read without a lookup.</summary>
    public string SourceRef { get; set; }

    public DateTime RetrievedAtUtc { get; set; }
  }
}
