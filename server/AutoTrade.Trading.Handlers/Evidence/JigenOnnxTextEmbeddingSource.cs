using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jigen.SemanticTools;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Embedding with Jigen's in-process ONNX runtime. Nothing leaves the process and no engine has to be
  /// running: the checkpoint is a file, and the same library that produces the vector is the one that will
  /// rank it.
  /// </summary>
  /// <remarks>
  /// The adapter owns the two things the runtime does not handle: it checks the checkpoint and the tokenizer
  /// before handing them to ONNX Runtime, because a missing file otherwise surfaces as an opaque inference
  /// error after the session has already started to initialise; and it owns mapping the configured profile and
  /// execution provider, so an unknown value fails at construction instead of falling back to something the
  /// operator did not ask for.
  /// </remarks>
  public class JigenOnnxTextEmbeddingSource : ITextEmbeddingSource, IDisposable
  {
    private readonly IEmbeddingGenerator generator;
    private readonly IDisposable disposer;
    private readonly string modelName;

    public JigenOnnxTextEmbeddingSource(EmbeddingOptions options, ILogger logger)
    {
      if (options == null)
      {
        throw new ArgumentNullException(nameof(options));
      }

      CheckFileExists(options.ModelPath, "Trading:Embedding:ModelPath");
      CheckFileExists(options.TokenizerPath, "Trading:Embedding:TokenizerPath");

      EmbeddingModelProfile profile;

      if (!Enum.TryParse(options.Profile, ignoreCase: true, result: out profile) || !Enum.IsDefined(typeof(EmbeddingModelProfile), profile))
      {
        throw new InvalidOperationException("Trading:Embedding:Profile is '" + options.Profile + "', which is not a profile this build knows. Use Nomic, Qwen3, SigLip2 or Custom.");
      }

      EmbeddingGeneratorOptions generatorOptions = new EmbeddingGeneratorOptions
      {
        Profile = profile,
        ExecutionProvider = string.IsNullOrWhiteSpace(options.ExecutionProvider) ? "cpu" : options.ExecutionProvider,
        OutputDimension = options.OutputDimension,
        IntraOpNumThreads = options.IntraOpNumThreads
      };

      if (options.MaxTokens > 0)
      {
        generatorOptions.MaxTokens = options.MaxTokens;
      }

      // The interface deliberately has no Dispose, but the session holds native handles, so the concrete
      // generator is kept as the disposer as well as the generator.
      OnnxEmbeddingGenerator created = new OnnxEmbeddingGenerator(options.TokenizerPath, options.ModelPath, logger, generatorOptions);

      generator = created;
      disposer = created;
      modelName = options.ModelName;
    }

    public bool IsAvailable
    {
      get { return true; }
    }

    public EmbeddingEngineKind Engine
    {
      get { return EmbeddingEngineKind.JigenOnnx; }
    }

    public string ModelName
    {
      get { return modelName; }
    }

    /// <summary>
    /// The task prefix is not decoration: the checkpoint is trained to embed a document and a query into the
    /// same space only when it is told which one it is looking at.
    /// </summary>
    public Task<float[]> EmbedDocumentAsync(string text, CancellationToken cancellationToken)
    {
      return EmbedAsync(EmbeddingOptions.DocumentTask, text, cancellationToken);
    }

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken)
    {
      return EmbedAsync(EmbeddingOptions.QueryTask, text, cancellationToken);
    }

    public void Dispose()
    {
      disposer.Dispose();
    }

    private Task<float[]> EmbedAsync(string task, string text, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        // The runtime answers with an empty or degenerate vector rather than refusing, and a vector of nothing
        // would silently become a match for nothing.
        throw new InvalidOperationException("Cannot embed empty text: the embedding would be meaningless and would rank against everything.");
      }

      return generator.GenerateEmbeddingAsync(task, text, cancellationToken);
    }

    private static void CheckFileExists(string path, string settingName)
    {
      if (string.IsNullOrWhiteSpace(path))
      {
        throw new InvalidOperationException(settingName + " is empty. The model checkpoint is a deployment artefact: it has to be provided.");
      }

      if (!File.Exists(path))
      {
        throw new InvalidOperationException(settingName + " points at '" + path + "', which does not exist. The semantic memory cannot embed anything without the checkpoint.");
      }
    }
  }
}
