using System;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// How text is turned into vectors. None is the default and means no source at all: nothing is embedded and
  /// the source reports itself unavailable, so the capability is never acquired by accident.
  /// </summary>
  public enum EmbeddingProviderKind
  {
    None = 0,

    /// <summary>Jigen's in-process ONNX runtime. The checkpoint is a file on disk; nothing is fetched at run time.</summary>
    JigenOnnx = 1
  }

  public class EmbeddingOptions
  {
    /// <summary>Task prefixes the model expects. They are what makes a document vector differ from a query one.</summary>
    public const string DocumentTask = "search_document";
    public const string QueryTask = "search_query";

    public EmbeddingProviderKind Provider { get; set; }

    /// <summary>Path to the ONNX checkpoint. Supplied by the deployment, never by the package.</summary>
    public string ModelPath { get; set; }

    /// <summary>Path to the tokenizer: either a tokenizer ONNX model or a tokenizer JSON file.</summary>
    public string TokenizerPath { get; set; }

    /// <summary>
    /// Model identity, for example "nomic-embed-text-v1.5". It is part of a collection's name, so it should be
    /// the checkpoint's own name and not an internal label that can drift from it.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// How an episode is rendered into the text that gets embedded. Rewording the rendering changes every
    /// vector, so it is part of a collection's name exactly like a prompt version.
    /// </summary>
    public string TextVersion { get; set; }

    /// <summary>Model family the checkpoint belongs to: Nomic, Qwen3, SigLip2 or Custom.</summary>
    public string Profile { get; set; }

    /// <summary>Inference provider: cpu by default, or cuda, dml, openvino, coreml, rocm.</summary>
    public string ExecutionProvider { get; set; }

    /// <summary>Optional reduced output size. Zero keeps the checkpoint's native dimension.</summary>
    public int OutputDimension { get; set; }

    /// <summary>Token budget per sequence. Zero lets the adapter keep the model default.</summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// Threads for a single inference. Zero lets ONNX Runtime use every core, which oversubscribes the CPU once
    /// more than one analysis cycle runs at a time.
    /// </summary>
    public int IntraOpNumThreads { get; set; }

    /// <summary>True when a provider is selected and everything it needs is named.</summary>
    public bool IsConfigured
    {
      get
      {
        return Provider == EmbeddingProviderKind.JigenOnnx
          && !string.IsNullOrWhiteSpace(ModelPath)
          && !string.IsNullOrWhiteSpace(TokenizerPath)
          && !string.IsNullOrWhiteSpace(ModelName)
          && !string.IsNullOrWhiteSpace(TextVersion);
      }
    }

    /// <summary>
    /// The engine that will produce the vectors. Unset when no provider is selected, so a collection built from
    /// an unconfigured source cannot be named and therefore cannot be written to.
    /// </summary>
    public EmbeddingEngineKind Engine
    {
      get { return Provider == EmbeddingProviderKind.JigenOnnx ? EmbeddingEngineKind.JigenOnnx : EmbeddingEngineKind.Unset; }
    }

    /// <summary>
    /// Builds the collection a caller should read or write. This is the only place that assembles the identity,
    /// so the engine, the model and the text version cannot be forgotten at a call site and end up mixing
    /// vectors that were produced differently.
    /// </summary>
    public EvidenceCollection CreateCollection(string name)
    {
      return new EvidenceCollection
      {
        Name = name,
        Engine = Engine,
        EmbeddingModel = ModelName,
        TextVersion = TextVersion
      };
    }
  }

  public static class EmbeddingOptionsFactory
  {
    private const string SectionKey = "Trading:Embedding";

    public static EmbeddingOptions FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      IConfigurationSection section = configuration.GetSection(SectionKey);

      return new EmbeddingOptions
      {
        Provider = ParseProvider(section["Provider"]),
        ModelPath = section["ModelPath"],
        TokenizerPath = section["TokenizerPath"],
        ModelName = section["ModelName"],
        TextVersion = section["TextVersion"],
        Profile = section["Profile"],
        ExecutionProvider = section["ExecutionProvider"],
        OutputDimension = ParseInt(section["OutputDimension"]),
        MaxTokens = ParseInt(section["MaxTokens"]),
        IntraOpNumThreads = ParseInt(section["IntraOpNumThreads"])
      };
    }

    private static EmbeddingProviderKind ParseProvider(string value)
    {
      EmbeddingProviderKind provider;

      return Enum.TryParse(value, ignoreCase: true, result: out provider) ? provider : EmbeddingProviderKind.None;
    }

    private static int ParseInt(string value)
    {
      int parsed;

      return int.TryParse(value, out parsed) ? parsed : 0;
    }
  }
}
