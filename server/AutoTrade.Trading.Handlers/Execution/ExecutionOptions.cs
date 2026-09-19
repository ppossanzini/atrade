using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>
  /// Which gateway is in force. None is the default and means the execution does not start at all, because
  /// without a provider no order can be sent and pretending otherwise would be worse than refusing.
  /// </summary>
  public enum ExecutionProviderKind
  {
    None = 0,
    Simulated = 1,
    Ctrader = 2
  }

  public class ExecutionOptions
  {
    public ExecutionProviderKind Provider { get; set; }

    public SimulatedExecutionOptions Simulated { get; set; }
  }

  /// <summary>
  /// Profile of the simulated gateway. One entry per symbol, so a scenario can be forced deliberately:
  /// a full fill, a partial one, a rejection, or silence that has to become a timeout.
  /// </summary>
  public class SimulatedExecutionOptions
  {
    public string DefaultBehaviour { get; set; }

    public string DefaultAfterQueryBehaviour { get; set; }

    public Dictionary<string, SimulatedSymbolBehaviour> Symbols { get; set; }
  }

  public class SimulatedSymbolBehaviour
  {
    /// <summary>Filled, PartialFill, Rejected or NoResponse.</summary>
    public string Behaviour { get; set; }

    /// <summary>Share of the requested volume filled by a partial fill, between 0 and 1.</summary>
    public double FillRatio { get; set; }

    public string ErrorCode { get; set; }

    /// <summary>When true the gateway reports the same broker event twice, to exercise dedup.</summary>
    public bool DuplicateEvent { get; set; }

    /// <summary>What a reconciliation query answers: Filled, PartiallyFilled, Rejected or Unknown.</summary>
    public string AfterQueryBehaviour { get; set; }
  }

  public static class ExecutionOptionsFactory
  {
    private const string SectionKey = "Trading:Execution";

    public static ExecutionOptions FromConfiguration(IConfiguration configuration)
    {
      if (configuration == null)
      {
        throw new ArgumentNullException(nameof(configuration));
      }

      IConfigurationSection section = configuration.GetSection(SectionKey);

      ExecutionOptions options = new ExecutionOptions
      {
        Provider = ParseProvider(section["Provider"]),
        Simulated = new SimulatedExecutionOptions
        {
          DefaultBehaviour = section["Simulated:DefaultBehaviour"],
          DefaultAfterQueryBehaviour = section["Simulated:DefaultAfterQueryBehaviour"],
          Symbols = new Dictionary<string, SimulatedSymbolBehaviour>(StringComparer.OrdinalIgnoreCase)
        }
      };

      foreach (IConfigurationSection symbol in section.GetSection("Simulated:Symbols").GetChildren())
      {
        options.Simulated.Symbols[symbol.Key] = new SimulatedSymbolBehaviour
        {
          Behaviour = symbol["Behaviour"],
          FillRatio = ReadDouble(symbol, "FillRatio"),
          ErrorCode = symbol["ErrorCode"],
          DuplicateEvent = ReadBool(symbol, "DuplicateEvent"),
          AfterQueryBehaviour = symbol["AfterQueryBehaviour"]
        };
      }

      return options;
    }

    private static ExecutionProviderKind ParseProvider(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        return ExecutionProviderKind.None;
      }

      if (Enum.TryParse(value.Trim(), true, out ExecutionProviderKind provider))
      {
        return provider;
      }

      throw new InvalidOperationException($"Configuration value '{SectionKey}:Provider' is not a known execution provider: '{value}'. Use None, Simulated or Ctrader.");
    }

    private static double ReadDouble(IConfigurationSection section, string key)
    {
      string value = section[key];

      if (string.IsNullOrWhiteSpace(value))
      {
        return 0;
      }

      if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
      {
        return parsed;
      }

      throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not a number: '{value}'.");
    }

    private static bool ReadBool(IConfigurationSection section, string key)
    {
      string value = section[key];

      if (string.IsNullOrWhiteSpace(value))
      {
        return false;
      }

      if (bool.TryParse(value, out bool parsed))
      {
        return parsed;
      }

      throw new InvalidOperationException($"Configuration value '{section.Path}:{key}' is not a boolean: '{value}'.");
    }
  }
}
