using System;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Analysis
{
  /// <summary>
  /// Which engine runs the local model. None is the default and means no analysis model at all: every call
  /// reports itself unavailable, so no rationale and no fallback text is ever produced by accident.
  /// </summary>
  public enum AnalysisProviderKind
  {
    None = 0,
    Ollama = 1
  }

  public class AnalysisOptions
  {
    private const string DefaultEndpoint = "http://127.0.0.1:11434";
    private const int DefaultTimeoutSeconds = 30;
    private const int DefaultMaxResponseBytes = 65536;
    private const int DefaultMaxRationaleLength = 600;

    public AnalysisProviderKind Provider { get; set; }

    /// <summary>Local engine address. It is expected to be loopback: the model is a local capability.</summary>
    public string Endpoint { get; set; }

    /// <summary>Model the engine must already have. Nothing is downloaded at run time.</summary>
    public string Model { get; set; }

    /// <summary>Bound on a single call, so a stuck model cannot hold an analysis cycle open.</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>Upper bound on the answer body. A model that rambles is a model that is not answering the schema.</summary>
    public int MaxResponseBytes { get; set; }

    /// <summary>Upper bound on the rationale text kept in the opinion.</summary>
    public int MaxRationaleLength { get; set; }

    /// <summary>Sampling temperature. Zero keeps the same input producing the same opinion.</summary>
    public double Temperature { get; set; }

    /// <summary>True when a provider is selected and its settings are complete.</summary>
    public bool IsConfigured
    {
      get
      {
        return Provider == AnalysisProviderKind.Ollama
          && !string.IsNullOrWhiteSpace(Endpoint)
          && !string.IsNullOrWhiteSpace(Model);
      }
    }

    public string EffectiveEndpoint
    {
      get { return string.IsNullOrWhiteSpace(Endpoint) ? DefaultEndpoint : Endpoint.TrimEnd('/'); }
    }

    public int EffectiveTimeoutSeconds
    {
      get { return TimeoutSeconds > 0 ? TimeoutSeconds : DefaultTimeoutSeconds; }
    }

    public int EffectiveMaxResponseBytes
    {
      get { return MaxResponseBytes > 0 ? MaxResponseBytes : DefaultMaxResponseBytes; }
    }

    public int EffectiveMaxRationaleLength
    {
      get { return MaxRationaleLength > 0 ? MaxRationaleLength : DefaultMaxRationaleLength; }
    }
  }

  public static class AnalysisOptionsFactory
  {
    private const string SectionKey = "Trading:Ollama";

    public static AnalysisOptions FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      IConfigurationSection section = configuration.GetSection(SectionKey);

      return new AnalysisOptions
      {
        Provider = ParseProvider(section["Provider"]),
        Endpoint = section["Endpoint"],
        Model = section["Model"],
        TimeoutSeconds = ParseInt(section["TimeoutSeconds"]),
        MaxResponseBytes = ParseInt(section["MaxResponseBytes"]),
        MaxRationaleLength = ParseInt(section["MaxRationaleLength"]),
        Temperature = ParseDouble(section["Temperature"])
      };
    }

    private static AnalysisProviderKind ParseProvider(string value)
    {
      AnalysisProviderKind provider;

      return Enum.TryParse(value, ignoreCase: true, result: out provider) ? provider : AnalysisProviderKind.None;
    }

    private static int ParseInt(string value)
    {
      int parsed;

      return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private static double ParseDouble(string value)
    {
      double parsed;

      return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) ? parsed : 0d;
    }
  }
}
