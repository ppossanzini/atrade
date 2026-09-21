using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Evidence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// Covers the embedding seam around the runtime: what configuration means, what the adapter refuses before
  /// ONNX Runtime is even asked to load anything, and what happens with no source at all. The checkpoint is a
  /// deployment artefact, so the tests that need a real one are the live ones; everything here must pass on a
  /// machine that has never seen a model.
  /// </summary>
  public class EmbeddingSourceTests : IDisposable
  {
    private readonly List<string> createdFiles = new List<string>();

    public void Dispose()
    {
      foreach (string file in createdFiles)
      {
        try
        {
          if (File.Exists(file))
          {
            File.Delete(file);
          }
        }
        catch (IOException)
        {
          // A leftover temporary file is not worth failing a test run over.
        }
      }
    }

    /// <summary>A file that exists but is not a model. Enough to get past the existence checks.</summary>
    private string CreateExistingFile()
    {
      string path = Path.Combine(Path.GetTempPath(), "autotrade-embedding-" + Guid.NewGuid().ToString("N") + ".bin");

      File.WriteAllBytes(path, new byte[] { 0 });
      createdFiles.Add(path);

      return path;
    }

    private static EmbeddingOptions CreateOptions(string modelPath, string tokenizerPath, string profile)
    {
      return new EmbeddingOptions
      {
        Provider = EmbeddingProviderKind.JigenOnnx,
        ModelPath = modelPath,
        TokenizerPath = tokenizerPath,
        ModelName = "nomic-embed-text-v1.5",
        TextVersion = "v1",
        Profile = profile,
        ExecutionProvider = "cpu"
      };
    }

    [Fact]
    public void Options_WithNothingConfigured_AreNotUsable()
    {
      EmbeddingOptions options = EmbeddingOptionsFactory.FromConfiguration(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>())
        .Build());

      Assert.Equal(EmbeddingProviderKind.None, options.Provider);
      Assert.False(options.IsConfigured);

      // No provider means no engine, so a collection built from it cannot even be named: nothing can be written
      // into a space whose identity is unknown. The refusal lives in the name, which is the only thing the store
      // ever sees, so there is one place to get it right instead of one per call site.
      Assert.Equal(EmbeddingEngineKind.Unset, options.Engine);
      Assert.Throws<InvalidOperationException>(() => options.CreateCollection("episodes").EffectiveName);
    }

    [Fact]
    public void Options_ReadTheProviderAndTheCheckpoint()
    {
      EmbeddingOptions options = EmbeddingOptionsFactory.FromConfiguration(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
          { "Trading:Embedding:Provider", "JigenOnnx" },
          { "Trading:Embedding:ModelPath", "/models/model.onnx" },
          { "Trading:Embedding:TokenizerPath", "/models/tokenizer.json" },
          { "Trading:Embedding:ModelName", "nomic-embed-text-v1.5" },
          { "Trading:Embedding:TextVersion", "v1" },
          { "Trading:Embedding:Profile", "Nomic" },
          { "Trading:Embedding:OutputDimension", "256" },
          { "Trading:Embedding:IntraOpNumThreads", "2" }
        })
        .Build());

      Assert.True(options.IsConfigured);
      Assert.Equal(EmbeddingEngineKind.JigenOnnx, options.Engine);
      Assert.Equal(256, options.OutputDimension);
      Assert.Equal(2, options.IntraOpNumThreads);
    }

    [Theory]
    [InlineData("ModelPath")]
    [InlineData("TokenizerPath")]
    [InlineData("ModelName")]
    [InlineData("TextVersion")]
    public void Options_SelectingAProviderWithoutEverySetting_AreNotUsable(string missing)
    {
      Dictionary<string, string> settings = new Dictionary<string, string>
      {
        { "Trading:Embedding:Provider", "JigenOnnx" },
        { "Trading:Embedding:ModelPath", "/models/model.onnx" },
        { "Trading:Embedding:TokenizerPath", "/models/tokenizer.json" },
        { "Trading:Embedding:ModelName", "nomic-embed-text-v1.5" },
        { "Trading:Embedding:TextVersion", "v1" }
      };

      settings.Remove("Trading:Embedding:" + missing);

      EmbeddingOptions options = EmbeddingOptionsFactory.FromConfiguration(new ConfigurationBuilder()
        .AddInMemoryCollection(settings)
        .Build());

      // Selecting a provider and leaving it incomplete is a configuration mistake, and the startup guard has to
      // catch it rather than let a half-configured memory come up.
      Assert.False(options.IsConfigured);
      Assert.Throws<InvalidOperationException>(() => EmbeddingModule.EnsureProviderIsUsable(options));
    }

    [Fact]
    public void Options_CreateACollectionCarryingTheWholeProducerIdentity()
    {
      EmbeddingOptions options = CreateOptions("/models/model.onnx", "/models/tokenizer.json", "Nomic");

      EvidenceCollection collection = options.CreateCollection("episodes");

      Assert.Equal("episodes@JigenOnnx@nomic-embed-text-v1.5@v1", collection.EffectiveName);
    }

    [Fact]
    public void EnsureProviderIsUsable_WithNoProvider_Succeeds()
    {
      // Running without a semantic memory is a supported state, not a mistake.
      EmbeddingModule.EnsureProviderIsUsable(new EmbeddingOptions { Provider = EmbeddingProviderKind.None });
    }

    [Fact]
    public void Source_WithoutAModelPath_RefusesToBuild()
    {
      InvalidOperationException error = Assert.Throws<InvalidOperationException>(
        () => new JigenOnnxTextEmbeddingSource(CreateOptions(string.Empty, CreateExistingFile(), "Nomic"), NullLogger<JigenOnnxTextEmbeddingSource>.Instance));

      Assert.Contains("ModelPath", error.Message);
    }

    [Fact]
    public void Source_WithAMissingCheckpoint_RefusesToBuild()
    {
      string missing = Path.Combine(Path.GetTempPath(), "autotrade-absent-" + Guid.NewGuid().ToString("N") + ".onnx");

      // Refused here rather than inside a retrieval: a checkpoint that is not there has to fail where it can be
      // seen, not silently turn into an empty memory.
      InvalidOperationException error = Assert.Throws<InvalidOperationException>(
        () => new JigenOnnxTextEmbeddingSource(CreateOptions(missing, CreateExistingFile(), "Nomic"), NullLogger<JigenOnnxTextEmbeddingSource>.Instance));

      Assert.Contains(missing, error.Message);
      Assert.Contains("ModelPath", error.Message);
    }

    [Fact]
    public void Source_WithAMissingTokenizer_RefusesToBuild()
    {
      string missing = Path.Combine(Path.GetTempPath(), "autotrade-absent-" + Guid.NewGuid().ToString("N") + ".json");

      InvalidOperationException error = Assert.Throws<InvalidOperationException>(
        () => new JigenOnnxTextEmbeddingSource(CreateOptions(CreateExistingFile(), missing, "Nomic"), NullLogger<JigenOnnxTextEmbeddingSource>.Instance));

      Assert.Contains("TokenizerPath", error.Message);
    }

    [Fact]
    public void Source_WithAnUnknownProfile_RefusesToBuild()
    {
      // An unknown profile must not fall back to a default: the profile decides how the text is prepared, and
      // guessing it would produce vectors that are quietly wrong rather than absent.
      InvalidOperationException error = Assert.Throws<InvalidOperationException>(
        () => new JigenOnnxTextEmbeddingSource(CreateOptions(CreateExistingFile(), CreateExistingFile(), "SomethingElse"), NullLogger<JigenOnnxTextEmbeddingSource>.Instance));

      Assert.Contains("SomethingElse", error.Message);
      Assert.Contains("Nomic", error.Message);
    }

    [Fact]
    public async Task Source_WhenUnavailable_RefusesToEmbedInsteadOfReturningZeros()
    {
      UnavailableTextEmbeddingSource source = new UnavailableTextEmbeddingSource();

      Assert.False(source.IsAvailable);
      Assert.Equal(EmbeddingEngineKind.Unset, source.Engine);
      Assert.Null(source.ModelName);

      // A zero vector would rank against everything, so an absent source stops the work instead of contributing.
      await Assert.ThrowsAsync<InvalidOperationException>(() => source.EmbedDocumentAsync("an episode", CancellationToken.None));
      await Assert.ThrowsAsync<InvalidOperationException>(() => source.EmbedQueryAsync("a query", CancellationToken.None));
    }
  }
}
