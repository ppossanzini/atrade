using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Basket;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using MapZilla;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;

namespace AutoTrade.Trading.Handlers.CQRS.Basket
{
    /// <summary>
    /// Read-only basket projections. Registry status and version status are derived from the
    /// archived flag and the single active-version pointer, so no duplicate state is stored.
    /// </summary>
    public class BasketQueryHandler(DB db, IMapper mapper) : IRequestHandler<GetBaskets, List<BasketSummaryDto>>,
                                                             IRequestHandler<GetBasketById, BasketDetailDto>,
                                                             IRequestHandler<GetActiveBasket, BasketDetailDto>
    {
        public async Task<List<BasketSummaryDto>> Handle(GetBaskets request, CancellationToken cancellationToken)
        {
            ActiveSlot activeSlot = await ReadActiveSlotAsync(cancellationToken);

            List<BasketEntity> baskets = await db.Baskets
              .AsNoTracking()
              .OrderBy(item => item.Name)
              .ToListAsync(cancellationToken);

            var latestVersions = await db.BasketVersions
              .AsNoTracking()
              .GroupBy(item => item.BasketId)
              .Select(group => new
              {
                  BasketId = group.Key,
                  LatestNumber = group.Max(item => item.Number)
              })
              .ToListAsync(cancellationToken);

            var legTotals = await db.BasketDraftLegs
              .AsNoTracking()
              .Where(item => item.IsSelected)
              .GroupBy(item => item.BasketId)
              .Select(group => new
              {
                  BasketId = group.Key,
                  SelectedLegCount = group.Count(),
                  TotalWeight = group.Sum(item => item.Weight)
              })
              .ToListAsync(cancellationToken);

            List<BasketSummaryDto> summaries = new List<BasketSummaryDto>();

            foreach (BasketEntity basket in baskets)
            {
                var latest = latestVersions.FirstOrDefault(item => item.BasketId == basket.Id);
                var legs = legTotals.FirstOrDefault(item => item.BasketId == basket.Id);

                summaries.Add(new BasketSummaryDto
                {
                    BasketId = basket.Id,
                    Name = basket.Name,
                    Status = DeriveBasketStatus(basket, activeSlot),
                    ActiveVersionNumber = activeSlot != null && activeSlot.BasketId == basket.Id ? activeSlot.Number : 0,
                    LatestVersionNumber = latest != null ? latest.LatestNumber : 0,
                    SelectedLegCount = legs != null ? legs.SelectedLegCount : 0,
                    TotalWeight = legs != null ? legs.TotalWeight : 0,
                    UpdatedAtUtc = basket.UpdatedAtUtc
                });
            }

            return summaries;
        }

        public async Task<BasketDetailDto> Handle(GetBasketById request, CancellationToken cancellationToken)
        {
            BasketEntity basket = await db.Baskets
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);

            if (basket == null)
            {
                return null;
            }

            return await BuildDetailAsync(basket, cancellationToken);
        }

        public async Task<BasketDetailDto> Handle(GetActiveBasket request, CancellationToken cancellationToken)
        {
            ActiveSlot activeSlot = await ReadActiveSlotAsync(cancellationToken);
            if (activeSlot == null)
            {
                return null;
            }

            BasketEntity basket = await db.Baskets
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.Id == activeSlot.BasketId, cancellationToken);

            if (basket == null)
            {
                return null;
            }

            return await BuildDetailAsync(basket, cancellationToken);
        }

        private async Task<BasketDetailDto> BuildDetailAsync(BasketEntity basket, CancellationToken cancellationToken)
        {
            List<BasketDraftLeg> draftLegs = await db.BasketDraftLegs
              .AsNoTracking()
              .Where(item => item.BasketId == basket.Id)
              .OrderBy(item => item.Symbol)
              .ToListAsync(cancellationToken);

            BasketDraftPolicy draftPolicy = await db.BasketDraftPolicies
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.BasketId == basket.Id, cancellationToken);

            int? latestNumber = await db.BasketVersions
              .AsNoTracking()
              .Where(item => item.BasketId == basket.Id)
              .Select(item => (int?)item.Number)
              .MaxAsync(cancellationToken);

            ActiveSlot activeSlot = await ReadActiveSlotAsync(cancellationToken);

            bool holdsActiveVersion = activeSlot != null && activeSlot.BasketId == basket.Id;

            BasketVersionPolicy activePolicy = holdsActiveVersion
              ? await db.BasketVersionPolicies.AsNoTracking().FirstOrDefaultAsync(item => item.VersionId == activeSlot.VersionId, cancellationToken)
              : null;

            BasketDetailDto detail = new BasketDetailDto
            {
                BasketId = basket.Id,
                Name = basket.Name,
                Status = DeriveBasketStatus(basket, activeSlot),
                LatestVersionNumber = latestNumber ?? 0,
                ActiveVersionId = holdsActiveVersion ? activeSlot.VersionId : (Guid?)null,
                ActiveVersionNumber = holdsActiveVersion ? activeSlot.Number : 0,
                DraftLegs = draftLegs.Select(item => mapper.Map<BasketCompositionLegDto>(item)).ToList(),
                DraftPolicy = draftPolicy != null ? mapper.Map<BasketPolicyDto>(draftPolicy) : null,
                ActivePolicy = activePolicy != null ? mapper.Map<BasketPolicyDto>(activePolicy) : null
            };

            return detail;
        }

        private async Task<ActiveSlot> ReadActiveSlotAsync(CancellationToken cancellationToken)
        {
            // The slot table is a singleton, so the read is deterministic; the explicit ordering keeps the
            // ordering undefined-warning out of the logs.
            return await (from slot in db.ActiveBasketVersions.AsNoTracking()
                          join version in db.BasketVersions.AsNoTracking() on slot.VersionId equals version.Id
                          orderby slot.Id
                          select new ActiveSlot
                          {
                              BasketId = slot.BasketId,
                              VersionId = slot.VersionId,
                              Number = version.Number
                          }).FirstOrDefaultAsync(cancellationToken);
        }

        private static BasketStatus DeriveBasketStatus(BasketEntity basket, ActiveSlot activeSlot)
        {
            if (basket.ArchivedAtUtc.HasValue)
            {
                return BasketStatus.Archived;
            }

            return activeSlot != null && activeSlot.BasketId == basket.Id ? BasketStatus.Active : BasketStatus.Inactive;
        }

        private class ActiveSlot
        {
            public Guid BasketId { get; set; }

            public Guid VersionId { get; set; }

            public int Number { get; set; }
        }
    }
}
