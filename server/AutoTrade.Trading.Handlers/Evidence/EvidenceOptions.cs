using System;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Which provider backs the semantic memory. None is the default and means no store at all: nothing is
  /// written and every retrieval reports itself unavailable, so semantic memory stays an explicit capability
  /// instead of something a deployment acquires by accident.
  /// </summary>
  public enum EvidenceProviderKind
  {
    None = 0,
    Jigen = 1
  }

  public class EvidenceOptions
  {
    public EvidenceProviderKind Provider { get; set; }

    /// <summary>Directory holding the store files. The adapter creates it when it does not exist.</summary>
    public string DataBasePath { get; set; }

    /// <summary>Database name inside the directory; it names the files, so one store is one database.</summary>
    public string DataBaseName { get; set; }

    /// <summary>True when a provider is selected and its settings are complete.</summary>
    public bool IsConfigured
    {
      get
      {
        return Provider == EvidenceProviderKind.Jigen
          && !string.IsNullOrWhiteSpace(DataBasePath)
          && !string.IsNullOrWhiteSpace(DataBaseName);
      }
    }
  }

  public static class EvidenceOptionsFactory
  {
    private const string SectionKey = "Trading:Jigen";

    public static EvidenceOptions FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      IConfigurationSection section = configuration.GetSection(SectionKey);

      return new EvidenceOptions
      {
        Provider = ParseProvider(section["Provider"]),
        DataBasePath = section["DataBasePath"],
        DataBaseName = section["DataBaseName"]
      };
    }

    private static EvidenceProviderKind ParseProvider(string value)
    {
      EvidenceProviderKind provider;

      return Enum.TryParse(value, ignoreCase: true, result: out provider) ? provider : EvidenceProviderKind.None;
    }
  }
}
