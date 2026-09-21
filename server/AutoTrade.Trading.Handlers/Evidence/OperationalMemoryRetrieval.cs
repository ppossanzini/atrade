using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Evidence
{
    /// <summary>One episode that was found to be similar to the situation at hand.</summary>
    public class RetrievedEpisode
    {
        public Guid EvidenceId { get; set; }

        public int Rank { get; set; }

        public double Score { get; set; }

        public string SourceRef { get; set; }

        public string EmbeddingModel { get; set; }

        public string Text { get; set; }
    }

    /// <summary>Outcome of a retrieval, with availability travelling with it for the same reason as everywhere else.</summary>
    public class MemoryRetrievalResult
    {
        public bool IsAvailable { get; set; }

        public string FailureReason { get; set; }

        /// <summary>Hash of the question asked, so a retrieval can be recognised later without storing its text again.</summary>
        public string QueryHash { get; set; }

        public List<RetrievedEpisode> Episodes { get; set; }
    }

    /// <summary>
    /// Looks for situations similar to the one being judged.
    /// </summary>
    /// <remarks>
    /// It answers a question, it does not decide anything: what comes back is evidence with a score, never a
    /// verdict, a size or a permission. It also never reports failure upwards, because memory is an auxiliary
    /// capability and a missing one must not stop an analysis cycle.
    /// </remarks>
    public interface IOperationalMemoryRetrieval
    {
        Task<MemoryRetrievalResult> FindSimilarAsync(string situation, int top, CancellationToken cancellationToken);
    }

    public class JigenOperationalMemoryRetrieval : IOperationalMemoryRetrieval
    {
        private const int MaxTop = 20;

        private readonly IJigenEvidenceStore store;
        private readonly ITextEmbeddingSource embedding;
        private readonly EmbeddingOptions embeddingOptions;
        private readonly ILogger logger;

        public JigenOperationalMemoryRetrieval(
          IJigenEvidenceStore store,
          ITextEmbeddingSource embedding,
          EmbeddingOptions embeddingOptions,
          ILogger<JigenOperationalMemoryRetrieval> logger)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
            this.embeddingOptions = embeddingOptions ?? throw new ArgumentNullException(nameof(embeddingOptions));
            this.logger = logger;
        }

        public async Task<MemoryRetrievalResult> FindSimilarAsync(string situation, int top, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(situation))
            {
                return Unavailable("Nothing to look for: the situation was empty.");
            }

            if (!store.IsAvailable || !embedding.IsAvailable)
            {
                return Unavailable("The semantic memory is not in force, so no similar situation could be retrieved.");
            }

            try
            {
                // Asked as a query, not as a document: the model is instructed differently for each, and asking with
                // the document prefix would rank this against the wrong side of the space.
                float[] vector = await embedding.EmbedQueryAsync(situation, cancellationToken);

                EvidenceSearchResult search = await store.SearchAsync(
                  new EvidenceQuery
                  {
                      Collection = embeddingOptions.CreateCollection(JigenOperationalEpisodeWriter.CollectionName),
                      Embedding = vector,
                      Top = Math.Clamp(top, 1, MaxTop)
                  },
                  cancellationToken);

                List<RetrievedEpisode> episodes = new List<RetrievedEpisode>();
                int rank = 0;

                foreach (EvidenceMatch match in search.Matches)
                {
                    episodes.Add(new RetrievedEpisode
                    {
                        EvidenceId = match.EvidenceId,
                        Rank = rank++,
                        Score = match.Score,
                        SourceRef = match.SourceRef,
                        EmbeddingModel = match.Collection != null ? match.Collection.EmbeddingModel : null,
                        Text = match.Content
                    });
                }

                return new MemoryRetrievalResult
                {
                    IsAvailable = search.IsAvailable,
                    QueryHash = Hash(situation),
                    Episodes = episodes
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                logger?.LogWarning(error, "A retrieval against the semantic memory failed.");

                return Unavailable("The retrieval failed: " + error.Message);
            }
        }

        /// <summary>
        /// Hashed so the transactional record can refer to the question without duplicating its text in two places
        /// where the two copies could drift.
        /// </summary>
        private static string Hash(string text)
        {
            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            StringBuilder hex = new StringBuilder(digest.Length * 2);

            foreach (byte value in digest)
            {
                hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static MemoryRetrievalResult Unavailable(string reason)
        {
            return new MemoryRetrievalResult
            {
                IsAvailable = false,
                FailureReason = reason,
                Episodes = new List<RetrievedEpisode>()
            };
        }
    }
}
