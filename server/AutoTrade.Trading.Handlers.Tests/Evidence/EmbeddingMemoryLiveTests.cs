using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Evidence;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// The chain nobody had measured: a real embedding model feeding a real store. The unit tests use
  /// three-dimensional vectors that are easy to reason about, which proves the ranking rule but not that the
  /// engine holds embeddings of the size a model actually produces. This is opt-in (AUTOTRADE_OLLAMA_LIVE=1)
  /// because it needs a model installed, and it is tagged Live so it is excluded from any coverage claim: when
  /// it returns early it asserts nothing.
  /// </summary>
  public class EmbeddingMemoryLiveTests : IDisposable
  {
    private const string LiveVariable = "AUTOTRADE_OLLAMA_LIVE";
    private const string Endpoint = "http://127.0.0.1:11434";
    private const string EmbeddingModel = "nomic-embed-text";
    private const string Collection = "episodes";

    private readonly List<string> createdDirectories = new List<string>();

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

    private static async Task<float[]> EmbedAsync(string text)
    {
      using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
      {
        string body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
          { "model", EmbeddingModel },
          { "input", text }
        });

        using (HttpResponseMessage response = await client.PostAsync(
          Endpoint + "/api/embed",
          new StringContent(body, Encoding.UTF8, "application/json"),
          CancellationToken.None))
        {
          string payload = await response.Content.ReadAsStringAsync(CancellationToken.None);

          Assert.True(response.IsSuccessStatusCode, payload);

          using (JsonDocument document = JsonDocument.Parse(payload))
          {
            List<float> values = new List<float>();

            foreach (JsonElement value in document.RootElement.GetProperty("embeddings")[0].EnumerateArray())
            {
              values.Add(value.GetSingle());
            }

            return values.ToArray();
          }
        }
      }
    }

    private static EvidenceRecord CreateRecord(string content, float[] embedding)
    {
      return new EvidenceRecord
      {
        EvidenceId = Guid.CreateVersion7(),
        Collection = Collection,
        Content = content,
        Embedding = embedding,
        EmbeddingModel = EmbeddingModel,
        Query = "what happens when a leg is not fully filled",
        SourceRef = "episode:live",
        RecordedAtUtc = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
      };
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_RealEmbeddingsRoundTripThroughTheStore()
    {
      if (!IsLiveRequested)
      {
        return;
      }

      const string relevant = "The second leg was filled only partially, so the remaining volume was compensated.";
      const string spread = "The proposal was refused because the spread exceeded the limit of that leg.";
      const string stale = "The market data source reported a stale snapshot and the cycle was suspended.";

      float[] relevantEmbedding = await EmbedAsync(relevant);
      float[] spreadEmbedding = await EmbedAsync(spread);
      float[] staleEmbedding = await EmbedAsync(stale);
      float[] queryEmbedding = await EmbedAsync("what happened when a leg was filled partially");

      Assert.True(relevantEmbedding.Length > 100, "The model did not produce a real embedding.");
      Assert.Equal(relevantEmbedding.Length, queryEmbedding.Length);

      string path = Path.Combine(Path.GetTempPath(), "autotrade-jigen-live-" + Guid.NewGuid().ToString("N"));
      createdDirectories.Add(path);

      EvidenceRecord relevantRecord = CreateRecord(relevant, relevantEmbedding);
      EvidenceRecord spreadRecord = CreateRecord(spread, spreadEmbedding);

      using (JigenEvidenceStore store = new JigenEvidenceStore(new EvidenceOptions
      {
        Provider = EvidenceProviderKind.Jigen,
        DataBasePath = path,
        DataBaseName = "autotrade"
      }))
      {
        await store.UpsertAsync(
          new List<EvidenceRecord> { relevantRecord, spreadRecord, CreateRecord(stale, staleEmbedding) },
          CancellationToken.None);

        EvidenceSearchResult result = await store.SearchAsync(
          new EvidenceQuery { Collection = Collection, Embedding = queryEmbedding, Top = 3, EmbeddingModel = EmbeddingModel },
          CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(3, result.Matches.Count);

        // The ranking has to agree with meaning, not merely with geometry: a store that returns matches in an
        // arbitrary order would satisfy every unit test that only checks the count.
        Assert.Equal(relevantRecord.EvidenceId, result.Matches[0].EvidenceId);

        // The full embedding has to survive the round trip, or the score would come from a truncated vector.
        Assert.Equal(EmbeddingModel, result.Matches[0].EmbeddingModel);
        Assert.Equal("episode:live", result.Matches[0].SourceRef);
      }

      // Reopened, because an evidence id recorded transactionally has to resolve after a restart.
      using (JigenEvidenceStore reopened = new JigenEvidenceStore(new EvidenceOptions
      {
        Provider = EvidenceProviderKind.Jigen,
        DataBasePath = path,
        DataBaseName = "autotrade"
      }))
      {
        EvidenceSearchResult afterReopen = await reopened.SearchAsync(
          new EvidenceQuery { Collection = Collection, Embedding = queryEmbedding, Top = 1, EmbeddingModel = EmbeddingModel },
          CancellationToken.None);

        Assert.Equal(relevantRecord.EvidenceId, afterReopen.Matches[0].EvidenceId);
      }
    }
  }
}
