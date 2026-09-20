using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using MessagePack;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Semantic memory backed by the in-process Jigen store.
  ///
  /// Two behaviours of the engine shape this adapter and are handled here rather than assumed away. The
  /// first: the store does not create its directory, and when the directory is missing it reports the
  /// database as already open in another instance, which names the wrong cause — so the directory is
  /// created here before opening. The second: a database is openable by one store per path, so this adapter
  /// is a singleton and owns the store for the lifetime of the host.
  ///
  /// The tier is deliberate in what it swallows: nothing. A store that cannot be opened aborts startup,
  /// because a provider selected on purpose must not come up silently missing; a store that is not selected
  /// is represented by <see cref="UnavailableEvidenceStore"/> and says so. A failed write or search
  /// propagates instead of being reported as an empty result.
  /// </summary>
  public class JigenEvidenceStore : IJigenEvidenceStore, IDisposable
  {
    private readonly Store store;

    public JigenEvidenceStore(EvidenceOptions options)
    {
      if (options == null)
      {
        throw new ArgumentNullException(nameof(options));
      }

      if (!options.IsConfigured)
      {
        throw new InvalidOperationException("The evidence store needs Trading:Jigen:DataBasePath and Trading:Jigen:DataBaseName to be set.");
      }

      Directory.CreateDirectory(options.DataBasePath);

      store = new Store(new StoreOptions
      {
        DataBasePath = options.DataBasePath,
        DataBaseName = options.DataBaseName,
        Wal = new WalOptions { Enabled = true }
      });
    }

    public bool IsAvailable
    {
      get { return true; }
    }

    public async Task UpsertAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken cancellationToken)
    {
      if (records == null || records.Count == 0)
      {
        return;
      }

      List<VectorEntry> entries = new List<VectorEntry>();

      foreach (EvidenceRecord record in records)
      {
        if (record.Embedding == null || record.Embedding.Length == 0)
        {
          throw new InvalidOperationException("Evidence cannot be stored without an embedding: " + record.EvidenceId);
        }

        entries.Add(new VectorEntry
        {
          Id = record.EvidenceId.ToByteArray(),
          CollectionName = record.Collection.EffectiveName,
          Content = MessagePackDocumentSerializer.Instance.Serialize(StoredEvidence.From(record)),
          Embedding = record.Embedding
        });
      }

      await store.AppendContentBulk(entries);

      // Durable before returning: an evidence id recorded transactionally must resolve after a restart.
      await store.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieved by similarity. The engine's search is synchronous and in-process, so it is wrapped rather
    /// than pretended to be asynchronous work.
    /// </summary>
    public Task<EvidenceSearchResult> SearchAsync(EvidenceQuery query, CancellationToken cancellationToken)
    {
      if (query == null || query.Collection == null || query.Embedding == null || query.Embedding.Length == 0)
      {
        throw new InvalidOperationException("A retrieval needs a collection and a query embedding.");
      }

      List<EvidenceMatch> matches = new List<EvidenceMatch>();

      // Searched by effective name, so the model and the text version are part of the space being searched and
      // nothing produced under a different model can be ranked against this query.
      foreach ((VectorEntry entry, float score) in store.Search(query.Collection.EffectiveName, query.Embedding, Math.Max(1, query.Top)))
      {
        StoredEvidence stored = MessagePackDocumentSerializer.Instance.Deserialize<StoredEvidence>(entry.Content);

        matches.Add(new EvidenceMatch
        {
          EvidenceId = stored.EvidenceId,
          Score = score,
          Content = stored.Content,
          Collection = query.Collection,
          Query = stored.Query,
          SourceRef = stored.SourceRef,
          RecordedAtUtc = stored.RecordedAtUtc
        });
      }

      return Task.FromResult(new EvidenceSearchResult
      {
        IsAvailable = true,
        Matches = matches
      });
    }

    public void Dispose()
    {
      store.Dispose();
    }

    /// <summary>
    /// What is written next to the vector. The embedding itself is stored by the engine, so it is not
    /// duplicated here; everything else is what makes a retrieval explainable afterwards.
    /// </summary>
    [MessagePackObject]
    public class StoredEvidence
    {
      [Key(0)]
      public Guid EvidenceId { get; set; }

      [Key(1)]
      public string Content { get; set; }

      [Key(2)]
      public string EmbeddingModel { get; set; }

      [Key(3)]
      public string Query { get; set; }

      [Key(4)]
      public string SourceRef { get; set; }

      [Key(5)]
      public DateTime RecordedAtUtc { get; set; }

      public static StoredEvidence From(EvidenceRecord record)
      {
        return new StoredEvidence
        {
          EvidenceId = record.EvidenceId,
          Content = record.Content,
          EmbeddingModel = record.Collection.EmbeddingModel,
          Query = record.Query,
          SourceRef = record.SourceRef,
          RecordedAtUtc = record.RecordedAtUtc
        };
      }
    }
  }
}
