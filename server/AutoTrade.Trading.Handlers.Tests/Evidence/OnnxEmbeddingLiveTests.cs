using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Evidence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// The whole chain with no stand-in at all: the production adapter, the real checkpoint, the real store. The
  /// unit tests prove the parts; only this proves that a real model's vectors survive the round trip and rank
  /// according to meaning.
  /// </summary>
  /// <remarks>
  /// Opt-in (AUTOTRADE_EMBEDDING_LIVE=1) because the checkpoint is a deployment artefact and the default suite
  /// must not require one. Tagged Live so it is excluded from any coverage claim: when it returns early it
  /// asserts nothing.
  /// </remarks>
  public class OnnxEmbeddingLiveTests : IDisposable
  {
    private const string LiveVariable = "AUTOTRADE_EMBEDDING_LIVE";
    private const string CheckpointDirectory = "/data/onnx/nomic-embed-text-v1.5";
    private const string ModelFile = "model_int8.onnx";
    private const string FullPrecisionModelFile = "model.onnx";
    private const string TokenizerFile = "tokenizer.onnx";

    private readonly ITestOutputHelper output;
    private readonly List<string> createdDirectories = new List<string>();

    public OnnxEmbeddingLiveTests(ITestOutputHelper output)
    {
      this.output = output;
    }

    private static bool IsLiveRequested
    {
      get { return Environment.GetEnvironmentVariable(LiveVariable) == "1"; }
    }

    public void Dispose()
    {
      foreach (string directory in createdDirectories)
      {
        try
        {
          if (Directory.Exists(directory))
          {
            Directory.Delete(directory, recursive: true);
          }
        }
        catch (IOException)
        {
          // A leftover temporary directory is not worth failing a test run over.
        }
      }
    }

    private static EmbeddingOptions CreateOptions()
    {
      return CreateOptions(ModelFile);
    }

    private static EmbeddingOptions CreateOptions(string modelFile)
    {
      return new EmbeddingOptions
      {
        Provider = EmbeddingProviderKind.JigenOnnx,
        ModelPath = Path.Combine(CheckpointDirectory, modelFile),
        TokenizerPath = Path.Combine(CheckpointDirectory, TokenizerFile),
        ModelName = "nomic-embed-text-v1.5",
        TextVersion = "v1",
        Profile = "Nomic",
        ExecutionProvider = "cpu"
      };
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_RealOnnxEmbeddingsRankByMeaningThroughTheStore()
    {
      if (!IsLiveRequested)
      {
        return;
      }

      EmbeddingOptions options = CreateOptions();

      using (JigenOnnxTextEmbeddingSource source = new JigenOnnxTextEmbeddingSource(options, NullLogger<JigenOnnxTextEmbeddingSource>.Instance))
      {
        const string relevant = "The second leg was filled only partially, so the remaining volume was compensated.";
        const string spread = "The proposal was refused because the spread exceeded the limit of that leg.";
        const string stale = "The market data source reported a stale snapshot and the cycle was suspended.";

        float[] relevantVector = await source.EmbedDocumentAsync(relevant, CancellationToken.None);
        float[] spreadVector = await source.EmbedDocumentAsync(spread, CancellationToken.None);
        float[] staleVector = await source.EmbedDocumentAsync(stale, CancellationToken.None);
        float[] queryVector = await source.EmbedQueryAsync("what happened when a leg was filled partially", CancellationToken.None);

        // The checkpoint's native size. A short vector here would mean the output was truncated somewhere.
        Assert.Equal(768, relevantVector.Length);
        Assert.Equal(relevantVector.Length, queryVector.Length);
        Assert.True(relevantVector.Any(value => value != 0f), "The model returned an all-zero embedding.");

        EvidenceCollection collection = options.CreateCollection("episodes");

        string path = Path.Combine(Path.GetTempPath(), "autotrade-onnx-live-" + Guid.NewGuid().ToString("N"));
        createdDirectories.Add(path);

        EvidenceRecord relevantRecord = CreateRecord(collection, relevant, relevantVector, "episode:partial-fill");

        using (JigenEvidenceStore store = new JigenEvidenceStore(new EvidenceOptions
        {
          Provider = EvidenceProviderKind.Jigen,
          DataBasePath = path,
          DataBaseName = "autotrade"
        }))
        {
          await store.UpsertAsync(
            new List<EvidenceRecord>
            {
              relevantRecord,
              CreateRecord(collection, spread, spreadVector, "episode:spread"),
              CreateRecord(collection, stale, staleVector, "episode:stale")
            },
            CancellationToken.None);

          EvidenceSearchResult result = await store.SearchAsync(
            new EvidenceQuery { Collection = collection, Embedding = queryVector, Top = 3 },
            CancellationToken.None);

          Assert.True(result.IsAvailable);
          Assert.Equal(3, result.Matches.Count);

          // The ranking has to agree with meaning. A store that returned matches in an arbitrary order would
          // satisfy every assertion that only checks the count.
          Assert.Equal(relevantRecord.EvidenceId, result.Matches[0].EvidenceId);
          Assert.Equal("episode:partial-fill", result.Matches[0].SourceRef);
          Assert.True(result.Matches[0].Score > result.Matches[1].Score);
        }

        // Reopened: an evidence id recorded transactionally has to resolve after a restart.
        using (JigenEvidenceStore reopened = new JigenEvidenceStore(new EvidenceOptions
        {
          Provider = EvidenceProviderKind.Jigen,
          DataBasePath = path,
          DataBaseName = "autotrade"
        }))
        {
          EvidenceSearchResult afterReopen = await reopened.SearchAsync(
            new EvidenceQuery { Collection = collection, Embedding = queryVector, Top = 1 },
            CancellationToken.None);

          Assert.Equal(relevantRecord.EvidenceId, Assert.Single(afterReopen.Matches).EvidenceId);
        }
      }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_TheSameTextIsNotTheSameVectorAsDocumentAndAsQuery()
    {
      if (!IsLiveRequested)
      {
        return;
      }

      EmbeddingOptions options = CreateOptions();

      using (JigenOnnxTextEmbeddingSource source = new JigenOnnxTextEmbeddingSource(options, NullLogger<JigenOnnxTextEmbeddingSource>.Instance))
      {
        const string text = "the leg was filled partially";

        float[] asDocument = await source.EmbedDocumentAsync(text, CancellationToken.None);
        float[] asQuery = await source.EmbedQueryAsync(text, CancellationToken.None);

        // The task prefix is part of the embedding, which is exactly why the two roles are separate methods
        // instead of one method with a flag: if these were identical, a query would be searched as a document
        // and retrieval would degrade without ever failing.
        Assert.Equal(asDocument.Length, asQuery.Length);
        Assert.True(asDocument.Where((value, index) => value != asQuery[index]).Any(), "Document and query embeddings are identical: the task prefix is not being applied.");
      }
    }

    /// <summary>
    /// Quantisation is a compromise between memory and quality, so the compromise is measured rather than
    /// assumed. Both variants have to agree on which episode is the best match; the scores are printed so the
    /// difference is visible instead of being summarised away.
    /// </summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_QuantisedAndFullPrecisionAgreeOnTheBestMatch()
    {
      if (!IsLiveRequested)
      {
        return;
      }

      const string relevant = "The second leg was filled only partially, so the remaining volume was compensated.";
      const string spread = "The proposal was refused because the spread exceeded the limit of that leg.";
      const string stale = "The market data source reported a stale snapshot and the cycle was suspended.";
      const string query = "what happened when a leg was filled partially";

      List<string> bestPerVariant = new List<string>();

      foreach (string modelFile in new[] { ModelFile, FullPrecisionModelFile })
      {
        EmbeddingOptions options = CreateOptions(modelFile);

        using (JigenOnnxTextEmbeddingSource source = new JigenOnnxTextEmbeddingSource(options, NullLogger<JigenOnnxTextEmbeddingSource>.Instance))
        {
          EvidenceCollection collection = options.CreateCollection("episodes");

          string path = Path.Combine(Path.GetTempPath(), "autotrade-onnx-compare-" + Guid.NewGuid().ToString("N"));
          createdDirectories.Add(path);

          List<EvidenceRecord> records = new List<EvidenceRecord>
          {
            CreateRecord(collection, relevant, await source.EmbedDocumentAsync(relevant, CancellationToken.None), "relevant"),
            CreateRecord(collection, spread, await source.EmbedDocumentAsync(spread, CancellationToken.None), "spread"),
            CreateRecord(collection, stale, await source.EmbedDocumentAsync(stale, CancellationToken.None), "stale")
          };

          using (JigenEvidenceStore store = new JigenEvidenceStore(new EvidenceOptions
          {
            Provider = EvidenceProviderKind.Jigen,
            DataBasePath = path,
            DataBaseName = "autotrade"
          }))
          {
            await store.UpsertAsync(records, CancellationToken.None);

            EvidenceSearchResult result = await store.SearchAsync(
              new EvidenceQuery { Collection = collection, Embedding = await source.EmbedQueryAsync(query, CancellationToken.None), Top = 3 },
              CancellationToken.None);

            bestPerVariant.Add(result.Matches[0].SourceRef);

            foreach (EvidenceMatch match in result.Matches)
            {
              output.WriteLine(modelFile + " -> " + match.SourceRef + " score " + match.Score.ToString("F6"));
            }
          }
        }
      }

      Assert.Equal(bestPerVariant[0], bestPerVariant[1]);
      Assert.Equal("relevant", bestPerVariant[0]);
    }

    /// <summary>
    /// The whole chain, from a blocked gate to a retrievable memory: the production builder, the production
    /// writer, the real embedding model and the real store. Nothing is stubbed.
    /// </summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_ABlockedGateBecomesARetrievableEpisode()
    {
      if (!IsLiveRequested)
      {
        return;
      }

      EmbeddingOptions options = CreateOptions();

      string path = Path.Combine(Path.GetTempPath(), "autotrade-onnx-episode-" + Guid.NewGuid().ToString("N"));
      createdDirectories.Add(path);

      using (JigenOnnxTextEmbeddingSource source = new JigenOnnxTextEmbeddingSource(options, NullLogger<JigenOnnxTextEmbeddingSource>.Instance))
      using (JigenEvidenceStore store = new JigenEvidenceStore(new EvidenceOptions
      {
        Provider = EvidenceProviderKind.Jigen,
        DataBasePath = path,
        DataBaseName = "autotrade"
      }))
      {
        JigenOperationalEpisodeWriter writer = new JigenOperationalEpisodeWriter(
          store,
          source,
          options,
          NullLogger<JigenOperationalEpisodeWriter>.Instance);

        OperationalEpisode blocked = OperationalEpisodeBuilder.GateBlocked(
          new EpisodeProposalFacts
          {
            ProposalId = Guid.CreateVersion7(),
            VersionNumber = 3,
            EntryMode = "RegimeMomentum",
            Action = "Entry",
            Confidence = 100d
          },
          new List<RiskGateResultDto>
          {
            new RiskGateResultDto { Code = RiskGateCode.CoverageBelowMinimum, Verdict = RiskGateVerdict.Allow, Subject = "basket", ObservedValue = 100d, ThresholdValue = 100d, Unit = "percent", Detail = "Every selected leg is executable." },
            new RiskGateResultDto { Code = RiskGateCode.RiskPerBasketExceeded, Verdict = RiskGateVerdict.Block, Subject = "basket", ObservedValue = 2.3d, ThresholdValue = 0.8d, Unit = "percent", Detail = "The basket risk exceeds the risk per basket limit." },
            new RiskGateResultDto { Code = RiskGateCode.LegSpreadExceeded, Verdict = RiskGateVerdict.Allow, Subject = "EURUSD", ObservedValue = 0.802d, ThresholdValue = 1.5d, Unit = "pips", Detail = "The spread is within the limit of this leg." }
          },
          new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc));

        await writer.RecordAsync(blocked, CancellationToken.None);

        // Retrieved by meaning, not by identifier: this is the question the memory exists to answer.
        float[] queryVector = await source.EmbedQueryAsync(
          "a proposal stopped because the basket risk was over the limit",
          CancellationToken.None);

        EvidenceSearchResult result = await store.SearchAsync(
          new EvidenceQuery { Collection = options.CreateCollection(JigenOperationalEpisodeWriter.CollectionName), Embedding = queryVector, Top = 3 },
          CancellationToken.None);

        Assert.True(result.IsAvailable);

        EvidenceMatch match = Assert.Single(result.Matches);

        Assert.Equal(blocked.EpisodeId, match.EvidenceId);
        Assert.Equal(blocked.SourceRef, match.SourceRef);
        Assert.Equal(blocked.Text, match.Content);
        Assert.True(match.Score > 0.4d, "The episode was stored but is barely recognisable for its own situation.");
      }
    }

    private static EvidenceRecord CreateRecord(EvidenceCollection collection, string content, float[] embedding, string sourceRef)
    {
      return new EvidenceRecord
      {
        EvidenceId = Guid.CreateVersion7(),
        Collection = collection,
        Content = content,
        Embedding = embedding,
        Query = "what happens when a leg is not fully filled",
        SourceRef = sourceRef,
        RecordedAtUtc = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc)
      };
    }
  }
}
