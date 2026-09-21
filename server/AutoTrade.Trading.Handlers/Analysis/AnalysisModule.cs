using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTrade.Trading.Handlers.Analysis
{
    /// <summary>
    /// Analysis tier registration, called by the handlers module so the composition root keeps a single
    /// registration entrypoint per tier.
    /// </summary>
    public static class AnalysisModule
    {
        public static IServiceCollection AddTradingAnalysis(this IServiceCollection services, IConfiguration configuration)
        {
            AnalysisOptions options = AnalysisOptionsFactory.FromConfiguration(configuration);
            services.AddSingleton(options);

            if (options.Provider == AnalysisProviderKind.Ollama)
            {
                // One client for the process, with the configured bound as its timeout so a call cannot outlive the
                // analysis cycle that asked for it.
                services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(options.EffectiveTimeoutSeconds) });
                services.AddSingleton<IOllamaAnalysisClient, OllamaAnalysisClient>();
            }
            else
            {
                services.AddSingleton<IOllamaAnalysisClient, UnavailableAnalysisClient>();
            }

            return services;
        }

        /// <summary>
        /// Fail-closed cross-check of the selected provider. Selecting an engine without saying which model to use
        /// is a configuration mistake, and coming up without the analysis that was asked for would be worse than
        /// refusing to start: leaving the provider at None is the supported way to run without it.
        /// </summary>
        public static void EnsureProviderIsUsable(AnalysisOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Provider == AnalysisProviderKind.Ollama && !options.IsConfigured)
            {
                throw new InvalidOperationException("Trading:Ollama:Provider is Ollama but Trading:Ollama:Model or Trading:Ollama:Endpoint is missing. Set both, or leave the provider at None.");
            }
        }
    }

    /// <summary>
    /// The client in force when no engine is selected. It stays explicitly unavailable instead of producing a
    /// canned or empty opinion, so a caller can always tell "there is no analysis model" from "the model had
    /// nothing to say", and no rationale is ever invented to fill the gap.
    /// </summary>
    public class UnavailableAnalysisClient : IOllamaAnalysisClient
    {
        public bool IsAvailable
        {
            get { return false; }
        }

        public Task EnsureModelIsPresentAsync(CancellationToken cancellationToken)
        {
            // Nothing was selected, so there is nothing to check. Selecting a model is what introduces the check.
            return Task.CompletedTask;
        }

        public Task<AnalysisResult> AnalyseAsync(AnalysisRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AnalysisResult
            {
                IsAvailable = false,
                Opinion = null,
                FailureReason = "No analysis model is configured."
            });
        }
    }
}
