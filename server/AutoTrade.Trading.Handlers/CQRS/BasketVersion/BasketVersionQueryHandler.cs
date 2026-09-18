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
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;
using BasketVersionEntity = AutoTrade.Trading.Handlers.Model.BasketVersion;

namespace AutoTrade.Trading.Handlers.CQRS.BasketVersion
{
  public class BasketVersionQueryHandler(DB db) : IRequestHandler<GetBasketVersionsByBasket, List<BasketVersionDto>>
  {
    public async Task<List<BasketVersionDto>> Handle(GetBasketVersionsByBasket request, CancellationToken cancellationToken)
    {
      BasketEntity basket = await db.Baskets
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);

      if (basket == null)
      {
        return new List<BasketVersionDto>();
      }

      Guid? activeVersionId = await db.ActiveBasketVersions
        .AsNoTracking()
        .Where(item => item.BasketId == basket.Id)
        .Select(item => (Guid?)item.VersionId)
        .FirstOrDefaultAsync(cancellationToken);

      List<BasketVersionEntity> versions = await db.BasketVersions
        .AsNoTracking()
        .Where(item => item.BasketId == basket.Id)
        .OrderByDescending(item => item.Number)
        .ToListAsync(cancellationToken);

      return versions.Select(version => new BasketVersionDto
      {
        VersionId = version.Id,
        Number = version.Number,
        Status = DeriveVersionStatus(basket, version, activeVersionId),
        Note = version.Note,
        CreatedAtUtc = version.CreatedAtUtc,
        PublishedAtUtc = version.PublishedAtUtc
      }).ToList();
    }

    /// <summary>
    /// Archived wins over everything; otherwise the version referenced by the active pointer is
    /// active and any other version of a basket that has an active pointer is superseded.
    /// </summary>
    private static BasketVersionStatus DeriveVersionStatus(BasketEntity basket, BasketVersionEntity version, Guid? activeVersionId)
    {
      if (basket.ArchivedAtUtc.HasValue)
      {
        return BasketVersionStatus.Archived;
      }

      if (activeVersionId.HasValue && activeVersionId.Value == version.Id)
      {
        return BasketVersionStatus.Active;
      }

      return activeVersionId.HasValue ? BasketVersionStatus.Superseded : BasketVersionStatus.Published;
    }
  }
}
