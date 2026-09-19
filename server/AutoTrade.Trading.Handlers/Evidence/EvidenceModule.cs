using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Evidence tier registration, called by the handlers module so the composition root keeps a single
  /// registration entrypoint per tier.
  /// </summary>
  public static class EvidenceModule
  {
    public static IServiceCollection AddTradingEvidence(this IServiceCollection services, IConfiguration configuration)
    {
      EvidenceOptions options = EvidenceOptionsFactory.FromConfiguration(configuration);
      services.AddSingleton(options);

      if (options.Provider == EvidenceProviderKind.Jigen)
      {
        // Singleton: one store per path, and the host owns its lifetime.
        services.AddSingleton<IJigenEvidenceStore>(provider => new JigenEvidenceStore(options));
      }
      else
      {
        services.AddSingleton<IJigenEvidenceStore, UnavailableEvidenceStore>();
      }

      return services;
    }

    /// <summary>
    /// Fail-closed cross-check of the selected provider. Selecting a store without saying where it lives is a
    /// configuration mistake, and coming up without the semantic memory that was asked for would be worse than
    /// refusing to start: leaving the provider at None is the supported way to run without it.
    /// </summary>
    public static void EnsureProviderIsUsable(EvidenceOptions options)
    {
      if (options == null)
      {
        throw new ArgumentNullException(nameof(options));
      }

      if (options.Provider == EvidenceProviderKind.Jigen && !options.IsConfigured)
      {
        throw new InvalidOperationException("Trading:Jigen:Provider is Jigen but Trading:Jigen:DataBasePath or Trading:Jigen:DataBaseName is missing. Set both, or leave the provider at None.");
      }
    }
  }

  /// <summary>
  /// The store in force when no provider is selected. It stays explicitly unavailable instead of returning an
  /// empty result, so a caller can always tell "semantic memory is off" from "nothing matched", and nothing
  /// falls back to a semantic answer nobody configured.
  /// </summary>
  public class UnavailableEvidenceStore : IJigenEvidenceStore
  {
    public bool IsAvailable
    {
      get { return false; }
    }

    public Task UpsertAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken cancellationToken)
    {
      // Evidence is auxiliary: writing it nowhere is exactly what "no store configured" means.
      return Task.CompletedTask;
    }

    public Task<EvidenceSearchResult> SearchAsync(EvidenceQuery query, CancellationToken cancellationToken)
    {
      return Task.FromResult(new EvidenceSearchResult
      {
        IsAvailable = false,
        Matches = new List<EvidenceMatch>()
      });
    }
  }
}
