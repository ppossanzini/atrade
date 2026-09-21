using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Analysis;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Analysis
{
    /// <summary>
    /// Talks to the real local engine. It is opt-in on purpose: installing a model is a deployment decision, so
    /// the default suite must not require one. Set AUTOTRADE_OLLAMA_LIVE=1 to run it. Without that variable the
    /// body returns immediately and asserts nothing, which is why this class is tagged Live and must be excluded
    /// from any coverage claim: what it proves is only proved when it actually runs.
    /// </summary>
    public class OllamaAnalysisClientLiveTests
    {
        private const string LiveVariable = "AUTOTRADE_OLLAMA_LIVE";
        private const string Model = "qwen2.5:3b";

        private static bool IsLiveRequested
        {
            get { return Environment.GetEnvironmentVariable(LiveVariable) == "1"; }
        }

        [Fact]
        [Trait("Category", "Live")]
        public async Task Live_TheRealModelAnswersInsideTheSchema()
        {
            if (!IsLiveRequested)
            {
                return;
            }

            AnalysisOptions options = new AnalysisOptions
            {
                Provider = AnalysisProviderKind.Ollama,
                Endpoint = "http://127.0.0.1:11434",
                Model = Model,
                TimeoutSeconds = 120,
                MaxResponseBytes = 65536,
                MaxRationaleLength = 600,
                Temperature = 0
            };

            using (HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(options.EffectiveTimeoutSeconds) })
            {
                OllamaAnalysisClient client = new OllamaAnalysisClient(options, httpClient);

                await client.EnsureModelIsPresentAsync(CancellationToken.None);
                Assert.True(client.IsAvailable);

                AnalysisRequest request = new AnalysisRequest
                {
                    PromptVersion = "analysis-v1",
                    Symbol = "EURUSD",
                    AllowedSymbols = new List<string> { "EURUSD", "XAUUSD" },
                    Context = "The measured spread is 0.8 pips and volatility is 0.21 percent. The risk gate allowed the leg."
                };

                AnalysisResult result = await client.AnalyseAsync(request, CancellationToken.None);

                Assert.True(result.IsAvailable, result.FailureReason);
                Assert.NotNull(result.Opinion);
                Assert.Equal("EURUSD", result.Opinion.Symbol, ignoreCase: true);
                Assert.True(result.Opinion.Rationale.Length > 0);
                Assert.InRange(result.Opinion.Confidence, 0d, 1d);
            }
        }

        [Fact]
        [Trait("Category", "Live")]
        public async Task Live_TheRealModelCannotPushASymbolOutsideTheAllowlist()
        {
            if (!IsLiveRequested)
            {
                return;
            }

            AnalysisOptions options = new AnalysisOptions
            {
                Provider = AnalysisProviderKind.Ollama,
                Endpoint = "http://127.0.0.1:11434",
                Model = Model,
                TimeoutSeconds = 120,
                MaxResponseBytes = 65536,
                MaxRationaleLength = 600,
                Temperature = 0
            };

            using (HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(options.EffectiveTimeoutSeconds) })
            {
                OllamaAnalysisClient client = new OllamaAnalysisClient(options, httpClient);

                await client.EnsureModelIsPresentAsync(CancellationToken.None);

                // The prompt invites the widest possible answer and the allowlist still has to hold: an answer that
                // names anything else must come back as no opinion rather than as a usable one.
                AnalysisRequest request = new AnalysisRequest
                {
                    PromptVersion = "analysis-v1",
                    Symbol = "EURUSD",
                    AllowedSymbols = new List<string> { "EURUSD" },
                    Context = "Answer about the most interesting instrument you can think of."
                };

                AnalysisResult result = await client.AnalyseAsync(request, CancellationToken.None);

                if (result.Opinion != null)
                {
                    Assert.Equal("EURUSD", result.Opinion.Symbol, ignoreCase: true);
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
                }
            }
        }
    }
}
