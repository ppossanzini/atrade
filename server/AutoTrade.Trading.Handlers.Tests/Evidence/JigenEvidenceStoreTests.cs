using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Evidence;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// The semantic memory is measured against a real store in a temporary directory, because the two things
  /// worth pinning are behaviours of the engine: it does not create its directory, and one database belongs to
  /// one store. Everything else here guards the contract that keeps an absent store distinguishable from an
  /// empty result.
  /// </summary>
  public class JigenEvidenceStoreTests : IDisposable
  {
    private const string Collection = "episodes";

    private readonly List<string> createdDirectories = new List<string>();

    private string CreateTemporaryPath()
    {
      string path = Path.Combine(Path.GetTempPath(), "autotrade-jigen-" + Guid.NewGuid().ToString("N"));

      createdDirectories.Add(path);

      return path;
    }

    private EvidenceOptions CreateOptions(string path)
    {
      return new EvidenceOptions
      {
        Provider = EvidenceProviderKind.Jigen,
        DataBasePath = path,
        DataBaseName = "autotrade"
      };
    }

    private static float[] Vector(float x, float y, float z)
    {
      return new[] { x, y, z };
    }

    private static EvidenceRecord CreateRecord(string content, float[] embedding, string sourceRef)
    {
      return new EvidenceRecord
      {
        EvidenceId = Guid.CreateVersion7(),
        Collection = Collection,
        Content = content,
        Embedding = embedding,
        EmbeddingModel = "test-model",
        Query = "what happened after a partial fill",
        SourceRef = sourceRef,
        RecordedAtUtc = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
      };
    }

    [Fact]
    public void Constructor_WithoutAPath_RefusesToOpen()
    {
      EvidenceOptions options = new EvidenceOptions { Provider = EvidenceProviderKind.Jigen, DataBaseName = "autotrade" };

      Assert.Throws<InvalidOperationException>(() => new JigenEvidenceStore(options));
    }

    [Fact]
    public void Constructor_CreatesTheDirectoryTheEngineDoesNotCreate()
    {
      string path = CreateTemporaryPath();

      using (new JigenEvidenceStore(CreateOptions(path)))
      {
        // The engine reports a missing directory as "already open in another instance", which names the wrong
        // cause, so the adapter owns creating it.
      }

      Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task Search_ReturnsTheNearestEvidenceWithItsMetadata()
    {
      string path = CreateTemporaryPath();

      using var store = new JigenEvidenceStore(CreateOptions(path));

      EvidenceRecord nearest = CreateRecord("partial fill on the second leg", Vector(1.0f, 0.0f, 0.0f), "execution:1");

      await store.UpsertAsync(
        new List<EvidenceRecord>
        {
          nearest,
          CreateRecord("rejected order", Vector(0.0f, 1.0f, 0.0f), "execution:2")
        },
        CancellationToken.None);

      EvidenceSearchResult result = await store.SearchAsync(
        new EvidenceQuery { Collection = Collection, Embedding = Vector(1.0f, 0.0f, 0.0f), Top = 2, EmbeddingModel = "test-model" },
        CancellationToken.None);

      Assert.True(result.IsAvailable);
      Assert.Equal(2, result.Matches.Count);

      EvidenceMatch top = result.Matches[0];

      Assert.Equal(nearest.EvidenceId, top.EvidenceId);
      Assert.Equal("partial fill on the second leg", top.Content);
      Assert.Equal("execution:1", top.SourceRef);
      Assert.Equal("test-model", top.EmbeddingModel);
      Assert.Equal("what happened after a partial fill", top.Query);
      Assert.Equal(nearest.RecordedAtUtc, top.RecordedAtUtc);
      Assert.True(top.Score > result.Matches[1].Score);
    }

    [Fact]
    public async Task Search_SurvivesAReopenOfTheStore()
    {
      string path = CreateTemporaryPath();

      Guid evidenceId;

      using (var store = new JigenEvidenceStore(CreateOptions(path)))
      {
        EvidenceRecord record = CreateRecord("kept evidence", Vector(1.0f, 0.0f, 0.0f), "episode:9");
        evidenceId = record.EvidenceId;

        await store.UpsertAsync(new List<EvidenceRecord> { record }, CancellationToken.None);
      }

      using (var reopened = new JigenEvidenceStore(CreateOptions(path)))
      {
        EvidenceSearchResult result = await reopened.SearchAsync(
          new EvidenceQuery { Collection = Collection, Embedding = Vector(1.0f, 0.0f, 0.0f), Top = 1, EmbeddingModel = "test-model" },
          CancellationToken.None);

        Assert.Equal(evidenceId, Assert.Single(result.Matches).EvidenceId);
      }
    }

    [Fact]
    public async Task Search_OnAnEmptyCollection_IsAvailableAndFindsNothing()
    {
      string path = CreateTemporaryPath();

      using var store = new JigenEvidenceStore(CreateOptions(path));

      EvidenceSearchResult result = await store.SearchAsync(
        new EvidenceQuery { Collection = "nothing-here", Embedding = Vector(1.0f, 0.0f, 0.0f), Top = 3, EmbeddingModel = "test-model" },
        CancellationToken.None);

      // An empty result and an absent store must not look the same to a caller.
      Assert.True(result.IsAvailable);
      Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Upsert_WithoutAnEmbedding_IsRefused()
    {
      string path = CreateTemporaryPath();

      using var store = new JigenEvidenceStore(CreateOptions(path));

      EvidenceRecord record = CreateRecord("no vector", null, "episode:1");

      await Assert.ThrowsAsync<InvalidOperationException>(
        () => store.UpsertAsync(new List<EvidenceRecord> { record }, CancellationToken.None));
    }

    [Fact]
    public async Task Search_WithoutACollectionOrEmbedding_IsRefused()
    {
      string path = CreateTemporaryPath();

      using var store = new JigenEvidenceStore(CreateOptions(path));

      await Assert.ThrowsAsync<InvalidOperationException>(
        () => store.SearchAsync(new EvidenceQuery { Collection = "", Embedding = Vector(1.0f, 0.0f, 0.0f), Top = 1 }, CancellationToken.None));

      await Assert.ThrowsAsync<InvalidOperationException>(
        () => store.SearchAsync(new EvidenceQuery { Collection = Collection, Embedding = null, Top = 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task UnavailableStore_ReportsItselfUnavailableAndKeepsNothing()
    {
      var store = new UnavailableEvidenceStore();

      Assert.False(store.IsAvailable);

      await store.UpsertAsync(new List<EvidenceRecord> { CreateRecord("dropped", Vector(1.0f, 0.0f, 0.0f), "episode:1") }, CancellationToken.None);

      EvidenceSearchResult result = await store.SearchAsync(
        new EvidenceQuery { Collection = Collection, Embedding = Vector(1.0f, 0.0f, 0.0f), Top = 3 },
        CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Empty(result.Matches);
    }

    [Fact]
    public void Options_ReadTheProviderAndTheLocation()
    {
      EvidenceOptions options = EvidenceOptionsFactory.FromConfiguration(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
          { "Trading:Jigen:Provider", "Jigen" },
          { "Trading:Jigen:DataBasePath", "jigendb" },
          { "Trading:Jigen:DataBaseName", "autotrade" }
        })
        .Build());

      Assert.Equal(EvidenceProviderKind.Jigen, options.Provider);
      Assert.True(options.IsConfigured);
    }

    [Fact]
    public void Options_WithoutASection_LeaveTheStoreOff()
    {
      EvidenceOptions options = EvidenceOptionsFactory.FromConfiguration(new ConfigurationBuilder().Build());

      Assert.Equal(EvidenceProviderKind.None, options.Provider);
      Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("Jigen", "jigendb", "")]
    [InlineData("Jigen", "", "autotrade")]
    public void Options_WithAnIncompleteLocation_AreNotConfigured(string provider, string path, string name)
    {
      EvidenceOptions options = new EvidenceOptions
      {
        Provider = Enum.Parse<EvidenceProviderKind>(provider),
        DataBasePath = path,
        DataBaseName = name
      };

      Assert.False(options.IsConfigured);
    }

    public void Dispose()
    {
      foreach (string path in createdDirectories.Where(Directory.Exists))
      {
        try
        {
          Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
          // A leftover temporary directory must never fail a test run.
        }
      }
    }
  }
}
