using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Operations;
using AutoTrade.Trading.Handlers.Analysis;
using AutoTrade.Trading.Handlers.Evidence;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using MapZilla;
using MapZilla.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Operations
{
    public class OperationsQueryHandler(DB db, IMapper mapper, TimeProvider timeProvider, EvidenceOptions evidenceOptions, IJigenEvidenceStore evidenceStore, EmbeddingOptions embeddingOptions, ITextEmbeddingSource embeddingSource, AnalysisOptions analysisOptions, IOllamaAnalysisClient analysisClient) : IRequestHandler<GetOperationalStatus, OperationalStatusDto>
    {
        private const int KillSwitchStateId = 1;
        private const int MarketManagerStateId = 1;

        public async Task<OperationalStatusDto> Handle(GetOperationalStatus request, CancellationToken cancellationToken)
        {
            KillSwitchStatusDto killSwitch = await db.KillSwitchStates
              .Where(item => item.Id == KillSwitchStateId)
              .ProjectTo<KillSwitchStatusDto>(mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            TradingAccountStatusDto account = await db.TradingAccounts
              .OrderBy(item => item.CreatedAtUtc)
              .ProjectTo<TradingAccountStatusDto>(mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            MarketManagerStatusDto marketManager = await db.MarketManagerStates
              .Where(item => item.Id == MarketManagerStateId)
              .ProjectTo<MarketManagerStatusDto>(mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            return new OperationalStatusDto
            {
                ServerTimeUtc = timeProvider.GetUtcNow().UtcDateTime,
                KillSwitch = killSwitch ?? CreateFailClosedKillSwitch(),
                Account = account,
                MarketManager = marketManager,
                EvidenceStore = new EvidenceStoreStatusDto
                {
                    Provider = evidenceOptions.Provider.ToString(),
                    IsAvailable = evidenceStore.IsAvailable
                },
                AnalysisModel = new AnalysisModelStatusDto
                {
                    Provider = analysisOptions.Provider.ToString(),
                    Model = analysisOptions.Model,
                    IsAvailable = analysisClient.IsAvailable
                },
                Embedding = new EmbeddingSourceStatusDto
                {
                    Engine = embeddingOptions.Engine.ToString(),
                    // Reported only when a provider is selected: with none, a leftover model name in the settings would
                    // read as a configured memory that is merely unavailable, which is a different fact.
                    Model = embeddingOptions.Engine == EmbeddingEngineKind.Unset ? null : embeddingOptions.ModelName,
                    TextVersion = embeddingOptions.Engine == EmbeddingEngineKind.Unset ? null : embeddingOptions.TextVersion,
                    IsAvailable = embeddingSource.IsAvailable
                }
            };
        }

        /// <summary>
        /// A missing kill-switch row means the operational state could not be read, so the reported
        /// state stays engaged. The system never reports "safe to trade" from missing data.
        /// </summary>
        private static KillSwitchStatusDto CreateFailClosedKillSwitch()
        {
            return new KillSwitchStatusDto
            {
                IsEngaged = true,
                ChangedAtUtc = null,
                ChangedByOperatorId = null,
                Reason = null
            };
        }
    }
}
