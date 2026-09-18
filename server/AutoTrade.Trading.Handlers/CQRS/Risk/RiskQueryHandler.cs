using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Risk;
using AutoTrade.Trading.Handlers.Risk;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;
using BasketVersionEntity = AutoTrade.Trading.Handlers.Model.BasketVersion;

namespace AutoTrade.Trading.Handlers.CQRS.Risk
{
  /// <summary>
  /// Builds the risk input from stored state and hands it to the engine. Read-only: evaluating risk never
  /// changes anything, and the missing market data is reported as a blocking gate rather than assumed.
  /// </summary>
  public class RiskQueryHandler(DB db, RiskThresholds thresholds, RiskEngine engine) : IRequestHandler<GetBasketRiskDecision, RiskDecisionDto>,
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

      RiskEvaluationInput input = new RiskEvaluationInput
      {
        HasActiveVersion = holdsActiveVersion,
        KillSwitchEngaged = await db.KillSwitchStates
          .AsNoTracking()
          .AnyAsync(item => item.Id == KillSwitchSingletonId && item.IsEngaged, cancellationToken),

        // Market data arrives with the analysis pipeline; until then no snapshot exists and the engine
        // blocks. Nothing here fabricates a healthy market.
        MarketDataCapturedAtUtc = null,
        Legs = new List<RiskEvaluationLeg>()
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
          input.FailurePolicy = policy.FailurePolicy;
          input.MinimumCoverage = policy.MinimumCoverage;
          input.RiskPerBasketLimit = policy.RiskPerBasket;
          input.DailyLossLimit = policy.DailyLossLimit;
        }

        input.VersionNumber = version.Number;

        foreach (BasketVersionLeg leg in versionLegs)
        {
          input.Legs.Add(new RiskEvaluationLeg
          {
            Symbol = leg.Symbol,
            Weight = leg.Weight,
            IsExecutable = false
          });
        }

        RiskDecisionDto decision = engine.Evaluate(input);
        decision.BasketId = basket.Id;
        decision.BasketVersionId = version.Id;

        return decision;
      }

      RiskDecisionDto inactive = engine.Evaluate(input);
      inactive.BasketId = basket.Id;

      return inactive;
    }

    public Task<RiskLimitsDto> Handle(GetRiskLimits request, CancellationToken cancellationToken)
    {
      return Task.FromResult(new RiskLimitsDto
      {
        SnapshotMaxAgeSeconds = thresholds.SnapshotMaxAgeSeconds,
        LegSpreadMaxPips = thresholds.LegSpreadMaxPips,
        LegVolatilityMaxPercent = thresholds.LegVolatilityMaxPercent,
        IsFullyConfigured = thresholds.IsFullyConfigured
      });
    }
  }
}
