using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Basket;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using MapZilla;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;
using BasketVersionEntity = AutoTrade.Trading.Handlers.Model.BasketVersion;

namespace AutoTrade.Trading.Handlers.CQRS.BasketVersion
{
  /// <summary>
  /// Publication freezes the draft into an immutable version; activation moves the single
  /// active-version pointer, which is the only thing that decides what new analysis uses.
  /// </summary>
  public class BasketVersionCommandHandler(DB db, IHikyaku hikyaku, IMapper mapper, IJournalWriter journalWriter, TimeProvider timeProvider)
    : IRequestHandler<PublishBasketVersion, PublishBasketVersionResult>,
      IRequestHandler<ActivateBasketVersion, BasketOperationResult>,
      IRequestHandler<ValidateBasketVersionPublishable, bool>,
      IRequestHandler<ValidateBasketVersionActivatable, bool>
  {
    private const string BasketVersionEntityType = "BasketVersion";
    private const int ActiveVersionSlotId = 1;

    public async Task<PublishBasketVersionResult> Handle(PublishBasketVersion request, CancellationToken cancellationToken)
    {
      PublishBasketVersionResult result = new PublishBasketVersionResult
      {
        Outcome = BasketOperationOutcome.InvalidState
      };

      BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
      if (basket == null)
      {
        result.Outcome = BasketOperationOutcome.NotFound;

        return result;
      }

      bool isPublishable = await hikyaku.Send(new ValidateBasketVersionPublishable
      {
        BasketId = basket.Id
      }, cancellationToken);

      if (!isPublishable)
      {
        await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketOperationRejected, BasketVersionEntityType, basket.Id, "operation=publish", cancellationToken);

        return result;
      }

      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      int? lastNumber = await db.BasketVersions
        .Where(item => item.BasketId == basket.Id)
        .Select(item => (int?)item.Number)
        .MaxAsync(cancellationToken);

      int nextNumber = (lastNumber ?? 0) + 1;

      BasketVersionEntity version = new BasketVersionEntity
      {
        Id = Guid.CreateVersion7(),
        BasketId = basket.Id,
        Number = nextNumber,
        Note = request.Note,
        CreatedAtUtc = now,
        CreatedByOperatorId = request.OperatorId,
        PublishedAtUtc = now
      };

      db.BasketVersions.Add(version);

      // Ordinals follow the risk-priority order that execution will use: highest risk cap first,
      // then symbol, so the frozen snapshot is deterministic and reproducible.
      List<BasketDraftLeg> selectedLegs = await db.BasketDraftLegs
        .Where(item => item.BasketId == basket.Id && item.IsSelected)
        .OrderByDescending(item => item.RiskCap)
        .ThenBy(item => item.Symbol)
        .ToListAsync(cancellationToken);

      int ordinal = 0;
      foreach (BasketDraftLeg leg in selectedLegs)
      {
        db.BasketVersionLegs.Add(new BasketVersionLeg
        {
          Id = Guid.CreateVersion7(),
          VersionId = version.Id,
          Ordinal = ordinal,
          Symbol = leg.Symbol,
          Market = leg.Market,
          Direction = leg.Direction,
          TimeFrame = leg.TimeFrame,
          Weight = leg.Weight,
          RiskCap = leg.RiskCap,
          StopDistancePips = leg.StopDistancePips,
          MaxSpreadPips = leg.MaxSpreadPips,
          MaxVolatilityPercent = leg.MaxVolatilityPercent
        });

        ordinal++;
      }

      BasketDraftPolicy policy = await db.BasketDraftPolicies.FirstOrDefaultAsync(item => item.BasketId == basket.Id, cancellationToken);
      if (policy != null)
      {
        db.BasketVersionPolicies.Add(new BasketVersionPolicy
        {
          Id = Guid.CreateVersion7(),
          VersionId = version.Id,
          FailurePolicy = policy.FailurePolicy,
          MinimumCoverage = policy.MinimumCoverage,
          RiskPerBasket = policy.RiskPerBasket,
          DailyLossLimit = policy.DailyLossLimit
        });
      }

      basket.UpdatedAtUtc = now;

      await db.SaveChangesAsync(cancellationToken);
      await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketVersionPublished, BasketVersionEntityType, version.Id, "number=" + nextNumber, cancellationToken);

      result.Outcome = BasketOperationOutcome.Applied;
      result.VersionId = version.Id;
      result.Number = nextNumber;

      return result;
    }

    public async Task<BasketOperationResult> Handle(ActivateBasketVersion request, CancellationToken cancellationToken)
    {
      BasketOperationResult result = new BasketOperationResult
      {
        Outcome = BasketOperationOutcome.InvalidState
      };

      bool isActivatable = await hikyaku.Send(new ValidateBasketVersionActivatable
      {
        BasketId = request.BasketId,
        VersionId = request.VersionId
      }, cancellationToken);

      if (!isActivatable)
      {
        await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketOperationRejected, BasketVersionEntityType, request.VersionId, "operation=activate", cancellationToken);

        return result;
      }

      BasketVersionEntity version = await db.BasketVersions.FirstOrDefaultAsync(item => item.Id == request.VersionId, cancellationToken);
      if (version == null)
      {
        result.Outcome = BasketOperationOutcome.NotFound;

        return result;
      }

      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      ActiveBasketVersion activeVersion = await db.ActiveBasketVersions.FirstOrDefaultAsync(item => item.Id == ActiveVersionSlotId, cancellationToken);
      if (activeVersion == null)
      {
        activeVersion = new ActiveBasketVersion
        {
          Id = ActiveVersionSlotId
        };

        db.ActiveBasketVersions.Add(activeVersion);
      }

      activeVersion.BasketId = version.BasketId;
      activeVersion.VersionId = version.Id;
      activeVersion.ActivatedAtUtc = now;
      activeVersion.ActivatedByOperatorId = request.OperatorId;

      BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == version.BasketId, cancellationToken);
      if (basket != null)
      {
        basket.UpdatedAtUtc = now;
      }

      await db.SaveChangesAsync(cancellationToken);
      await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketVersionActivated, BasketVersionEntityType, version.Id, "number=" + version.Number, cancellationToken);

      result.Outcome = BasketOperationOutcome.Applied;

      return result;
    }

    public async Task<bool> Handle(ValidateBasketVersionPublishable request, CancellationToken cancellationToken)
    {
      BasketEntity basket = await db.Baskets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);

      if (basket == null || basket.ArchivedAtUtc.HasValue)
      {
        return false;
      }

      List<BasketDraftLeg> draftLegs = await db.BasketDraftLegs.AsNoTracking().Where(item => item.BasketId == basket.Id).ToListAsync(cancellationToken);
      if (draftLegs.Count == 0)
      {
        return false;
      }

      List<BasketCompositionLegDto> legDtos = draftLegs.Select(item => mapper.Map<BasketCompositionLegDto>(item)).ToList();

      if (!await hikyaku.Send(new ValidateBasketCompositionWeights { Legs = legDtos }, cancellationToken))
      {
        return false;
      }

      if (!await hikyaku.Send(new ValidateBasketCompositionSymbols { Legs = legDtos }, cancellationToken))
      {
        return false;
      }

      BasketDraftPolicy policy = await db.BasketDraftPolicies.AsNoTracking().FirstOrDefaultAsync(item => item.BasketId == basket.Id, cancellationToken);
      if (policy == null)
      {
        return false;
      }

      BasketPolicyDto policyDto = mapper.Map<BasketPolicyDto>(policy);

      return await hikyaku.Send(new ValidateBasketPolicyValues { Policy = policyDto }, cancellationToken);
    }

    public async Task<bool> Handle(ValidateBasketVersionActivatable request, CancellationToken cancellationToken)
    {
      BasketEntity basket = await db.Baskets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);

      if (basket == null || basket.ArchivedAtUtc.HasValue)
      {
        return false;
      }

      return await db.BasketVersions
        .AsNoTracking()
        .AnyAsync(item => item.Id == request.VersionId && item.BasketId == basket.Id, cancellationToken);
    }
  }
}
