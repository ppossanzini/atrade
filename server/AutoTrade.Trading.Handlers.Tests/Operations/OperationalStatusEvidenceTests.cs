using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Operations;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Operations
{
    /// <summary>
    /// The operational status has to answer for the semantic memory as well. Without a store configured the
    /// answer is "jigen/none is not available", which is a reported state: an operator reading the status must
    /// never have to infer from an empty retrieval that semantic memory is off.
    /// </summary>
    public class OperationalStatusEvidenceTests
    {
        private static TradingTestContext CreateContext()
        {
            return new TradingTestContext(new Dictionary<string, string>());
        }

        [Fact]
        public async Task Status_WithoutAConfiguredStore_ReportsItUnavailable()
        {
            using TradingTestContext context = CreateContext();

            OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

            Assert.NotNull(status.EvidenceStore);
            Assert.Equal("None", status.EvidenceStore.Provider);
            Assert.False(status.EvidenceStore.IsAvailable);
        }

        [Fact]
        public async Task Status_WithoutAConfiguredModel_ReportsProviderNoneAndUnavailable()
        {
            using TradingTestContext context = CreateContext();

            OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

            Assert.NotNull(status.AnalysisModel);
            Assert.Equal("None", status.AnalysisModel.Provider);
            Assert.False(status.AnalysisModel.IsAvailable);
        }

        [Fact]
        public async Task Status_WithoutAnEmbeddingSource_ReportsItSeparatelyFromTheStore()
        {
            using TradingTestContext context = CreateContext();

            OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

            // The two are reported apart on purpose: here the store is off as well, but the point is that an operator
            // can tell which of the two is missing when only one of them is.
            Assert.NotNull(status.Embedding);
            Assert.Equal("Unset", status.Embedding.Engine);
            Assert.False(status.Embedding.IsAvailable);
        }

        [Fact]
        public async Task Status_WithAModelInForce_ReportsWhatIsInForce()
        {
            using TradingTestContext context = new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Ollama:Provider", "Ollama" },
        { "Trading:Ollama:Model", "qwen2.5:3b" }
      });

            context.AnalysisClient.IsAvailable = true;

            OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

            // The model is reported even when it is not available, so the operator sees what was asked for and can
            // tell a missing model from a model that is present and returning nothing usable.
            Assert.Equal("Ollama", status.AnalysisModel.Provider);
            Assert.Equal("qwen2.5:3b", status.AnalysisModel.Model);
            Assert.True(status.AnalysisModel.IsAvailable);
        }
    }
}
