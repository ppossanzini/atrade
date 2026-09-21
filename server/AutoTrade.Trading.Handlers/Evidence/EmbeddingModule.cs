using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Evidence
{
    /// <summary>
    /// Embedding tier registration, called by the handlers module so the composition root keeps a single
    /// registration entrypoint per tier.
    /// </summary>
    public static class EmbeddingModule
    {
        public static IServiceCollection AddTradingEmbedding(this IServiceCollection services, IConfiguration configuration)
        {
            EmbeddingOptions options = EmbeddingOptionsFactory.FromConfiguration(configuration);
            services.AddSingleton(options);

            if (options.Provider == EmbeddingProviderKind.JigenOnnx)
            {
                // Singleton: the ONNX session loads the checkpoint into memory, so it is built once for the process and
                // its lifetime belongs to the host.
                services.AddSingleton<ITextEmbeddingSource>(provider => new JigenOnnxTextEmbeddingSource(
                  options,
                  provider.GetService<ILogger<JigenOnnxTextEmbeddingSource>>()));
            }
            else
            {
                services.AddSingleton<ITextEmbeddingSource, UnavailableTextEmbeddingSource>();
            }

            return services;
        }

        /// <summary>
        /// Fail-closed cross-check of the selected provider. Asking for a model without saying which checkpoint to
        /// use is a configuration mistake, and coming up with a memory that cannot embed would be worse than
        /// refusing to start: leaving the provider at None is the supported way to run without it.
        /// </summary>
        public static void EnsureProviderIsUsable(EmbeddingOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Provider == EmbeddingProviderKind.JigenOnnx && !options.IsConfigured)
            {
                throw new InvalidOperationException("Trading:Embedding:Provider is JigenOnnx but Trading:Embedding:ModelPath, TokenizerPath, ModelName or TextVersion is missing. Set all of them, or leave the provider at None.");
            }
        }
    }

    /// <summary>
    /// The source in force when no provider is selected. It stays explicitly unavailable instead of returning a
    /// zero vector, because a vector of zeros would rank against everything: an absent source has to stop a
    /// retrieval, not quietly become a match.
    /// </summary>
    public class UnavailableTextEmbeddingSource : ITextEmbeddingSource
    {
        public bool IsAvailable
        {
            get { return false; }
        }

        public EmbeddingEngineKind Engine
        {
            get { return EmbeddingEngineKind.Unset; }
        }

        public string ModelName
        {
            get { return null; }
        }

        public Task<float[]> EmbedDocumentAsync(string text, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No embedding source is configured, so nothing can be embedded.");
        }

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No embedding source is configured, so nothing can be retrieved.");
        }
    }
}
