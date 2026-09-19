using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Risk;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Risk;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;
using BasketVersionEntity = AutoTrade.Trading.Handlers.Model.BasketVersion;

namespace AutoTrade.Trading.Handlers.CQRS.Risk
{
  /// <summary>
  /// Builds the risk input from stored state and one market capture, then hands it to the engine.
  /// Read-only: evaluating risk never changes anything, and missing data is reported as a blocking gate
  /// rather than assumed. The capture comes from the configured source, so this handler is identical
  /// whichever source is in force.
  /// </summary>
  public class RiskQueryHandler(DB db, RiskThresholds thresholds, RiskEngine engine, IMarketDataSource marketDataSource) : IRequestHandler<GetBasketRiskDecision, RiskDecisionDto>,
                                                                                   IRequestHandler<GetRiskLimits, RiskLimitsDto>
  {
    private const int ActiveVersionSlotId = 1;
    private const int KillSwitchSingletonId = 1;

    public async Task<RiskDecisionDto> Handle(GetBasketRiskDecision request, CancellationToken cancellationToken)
    {
      BasketEntity basket = await db.Baskets
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);

      if (basket == null)
      {
        return null;
      }

      ActiveBasketVersion activeVersion = await db.ActiveBasketVersions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == ActiveVersionSlotId, cancellationToken);

      bool holdsActiveVersion = activeVersion != null && activeVersion.BasketId == basket.Id;

      RiskCandidate candidate = new RiskCandidate
      {
        HasActiveVersion = holdsActiveVersion,
        KillSwitchEngaged = await db.KillSwitchStates
          .AsNoTracking()
          .AnyAsync(item => item.Id == KillSwitchSingletonId && item.IsEngaged, cancellationToken),
        Legs = new List<RiskCandidateLeg>()
      };

      if (holdsActiveVersion)
      {
        BasketVersionEntity version = await db.BasketVersions
          .AsNoTracking()
          .FirstAsync(item => item.Id == activeVersion.VersionId, cancellationToken);

        BasketVersionPolicy policy = await db.BasketVersionPolicies
          .AsNoTracking()
          .FirstOrDefaultAsync(item => item.VersionId == version.Id, cancellationToken);

        List<BasketVersionLeg> versionLegs = await db.BasketVersionLegs
          .AsNoTracking()
          .Where(item => item.VersionId == version.Id)
          .OrderBy(item => item.Ordinal)
          .ToListAsync(cancellationToken);

        if (policy != null)
        {
          candidate.FailurePolicy = policy.FailurePolicy;
          candidate.MinimumCoverage = policy.MinimumCoverage;
          candidate.RiskPerBasketLimit = policy.RiskPerBasket;
          candidate.DailyLossLimit = policy.DailyLossLimit;
        }

        candidate.VersionNumber = version.Number;

        foreach (BasketVersionLeg leg in versionLegs)
        {
          candidate.Legs.Add(new RiskCandidateLeg
          {
            Symbol = leg.Symbol,
            Market = leg.Market,
            Weight = leg.Weight,
            RiskCap = leg.RiskCap,
            MaxSpreadPips = leg.MaxSpreadPips,
            MaxVolatilityPercent = leg.MaxVolatilityPercent
          });
        }
      }

      MarketDataCapture capture = await marketDataSource.CaptureAsync(CreateSymbolRequests(candidate), cancellationToken);
      RiskEvaluationInput input = RiskInputFactory.Create(candidate, capture);

      RiskDecisionDto decision = engine.Evaluate(input);
      decision.BasketId = basket.Id;

      if (holdsActiveVersion)
      {
        decision.BasketVersionId = activeVersion.VersionId;
      }

      return decision;
    }

    private static List<SymbolRequest> CreateSymbolRequests(RiskCandidate candidate)
    {
      List<SymbolRequest> requests = new List<SymbolRequest>();

      if (candidate.Legs == null)
      {
        return requests;
      }

      foreach (RiskCandidateLeg leg in candidate.Legs)
      {
        requests.Add(new SymbolRequest
        {
          Symbol = leg.Symbol,
          Market = leg.Market
        });
      }

      return requests;
    }

    public Task<RiskLimitsDto> Handle(GetRiskLimits request, CancellationToken cancellationToken)
    {
      return Task.FromResult(new RiskLimitsDto
      {
        SnapshotMaxAgeSeconds = thresholds.SnapshotMaxAgeSeconds,
        IsConfigured = thresholds.IsConfigured
      });
    }
  }
}
