using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Execution;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;
using ExecutionEntity = AutoTrade.Trading.Handlers.Model.Execution;

namespace AutoTrade.Trading.Handlers.CQRS.Execution
{
  /// <summary>
  /// Read side of the execution. Coverage and the compensation need are recomputed from the legs, so the screen
  /// cannot show a finished execution whose legs say otherwise.
  /// </summary>
  public class ExecutionQueryHandler(DB db)
    : IRequestHandler<GetExecutionQueue, List<ExecutionSummaryDto>>,
      IRequestHandler<GetExecutionDetail, ExecutionDetailDto>
  {
    private const int QueueLimit = 200;

    public async Task<List<ExecutionSummaryDto>> Handle(GetExecutionQueue request, CancellationToken cancellationToken)
    {
      List<ExecutionEntity> executions = await db.Executions
        .AsNoTracking()
        .OrderByDescending(item => item.CreatedAtUtc)
        .Take(QueueLimit)
        .ToListAsync(cancellationToken);

      Dictionary<Guid, string> basketNames = await ReadBasketNamesAsync(executions, cancellationToken);
      List<ExecutionLeg> legs = await db.ExecutionLegs
        .AsNoTracking()
        .Where(item => executions.Select(execution => execution.Id).Contains(item.ExecutionId))
        .ToListAsync(cancellationToken);

      List<ExecutionSummaryDto> queue = new List<ExecutionSummaryDto>();

      foreach (ExecutionEntity execution in executions)
      {
        List<ExecutionLeg> executionLegs = legs.Where(item => item.ExecutionId == execution.Id).ToList();

        queue.Add(Summarise(
          execution,
          executionLegs,
          basketNames.TryGetValue(execution.BasketId, out string name) ? name : null));
      }

      return queue;
    }

    public async Task<ExecutionDetailDto> Handle(GetExecutionDetail request, CancellationToken cancellationToken)
    {
      ExecutionEntity execution = await db.Executions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == request.ExecutionId, cancellationToken);

      if (execution == null)
      {
        return null;
      }

      string basketName = await db.Baskets
        .AsNoTracking()
        .Where(item => item.Id == execution.BasketId)
        .Select(item => item.Name)
        .FirstOrDefaultAsync(cancellationToken);

      List<ExecutionLeg> legs = await db.ExecutionLegs
        .AsNoTracking()
        .Where(item => item.ExecutionId == execution.Id)
        .OrderBy(item => item.Ordinal)
        .ToListAsync(cancellationToken);

      List<BrokerEvent> events = await db.BrokerEvents
        .AsNoTracking()
        .Where(item => item.ExecutionId == execution.Id)
        .OrderBy(item => item.ReceivedAtUtc)
        .ToListAsync(cancellationToken);

      ExecutionSummaryDto summary = Summarise(execution, legs, basketName);

      ExecutionDetailDto detail = new ExecutionDetailDto
      {
        ExecutionId = summary.ExecutionId,
        BasketId = summary.BasketId,
        BasketName = summary.BasketName,
        VersionNumber = summary.VersionNumber,
        Status = summary.Status,
        FailurePolicy = summary.FailurePolicy,
        MinimumCoverage = summary.MinimumCoverage,
        Coverage = summary.Coverage,
        LegCount = summary.LegCount,
        FilledLegCount = summary.FilledLegCount,
        CreatedAtUtc = summary.CreatedAtUtc,
        CompletedAtUtc = summary.CompletedAtUtc,
        NeedsCompensation = summary.NeedsCompensation,
        ProposalId = execution.ProposalId,
        SnapshotId = execution.SnapshotId,
        CompensationOfExecutionId = execution.CompensationOfExecutionId,
        StartedAtUtc = execution.StartedAtUtc,
        Legs = new List<ExecutionLegDto>(),
        Events = new List<ExecutionEventDto>()
      };

      Dictionary<Guid, string> symbolsByLegId = legs.ToDictionary(item => item.Id, item => item.Symbol);

      foreach (ExecutionLeg leg in legs)
      {
        detail.Legs.Add(new ExecutionLegDto
        {
          LegId = leg.Id,
          Ordinal = leg.Ordinal,
          Symbol = leg.Symbol,
          Market = leg.Market,
          Direction = leg.Direction,
          VolumeUnits = leg.VolumeUnits,
          FilledVolumeUnits = leg.FilledVolumeUnits,
          ClientOrderId = leg.ClientOrderId,
          BrokerOrderId = leg.BrokerOrderId,
          Status = leg.Status,
          AveragePrice = leg.AveragePrice,
          ErrorCode = leg.ErrorCode,
          LastEventAtUtc = leg.LastEventAtUtc
        });
      }

      foreach (BrokerEvent brokerEvent in events)
      {
        detail.Events.Add(new ExecutionEventDto
        {
          BrokerEventId = brokerEvent.BrokerEventId,
          Kind = brokerEvent.Kind,
          Symbol = brokerEvent.LegId.HasValue && symbolsByLegId.TryGetValue(brokerEvent.LegId.Value, out string symbol) ? symbol : brokerEvent.Symbol,
          Payload = brokerEvent.Payload,
          ReceivedAtUtc = brokerEvent.ReceivedAtUtc
        });
      }

      return detail;
    }

    /// <summary>
    /// Coverage is measured, never assumed: it is filled volume over planned volume, and a leg is counted as
    /// filled only when its own status says so.
    /// </summary>
    private static ExecutionSummaryDto Summarise(ExecutionEntity execution, List<ExecutionLeg> legs, string basketName)
    {
      int planned = legs.Sum(item => item.VolumeUnits);
      int filled = legs.Sum(item => item.FilledVolumeUnits);

      return new ExecutionSummaryDto
      {
        ExecutionId = execution.Id,
        BasketId = execution.BasketId,
        BasketName = basketName,
        VersionNumber = execution.VersionNumber,
        Status = execution.Status,
        FailurePolicy = execution.FailurePolicy,
        MinimumCoverage = execution.MinimumCoverage,
        Coverage = planned > 0 ? (int)Math.Floor(filled * 100.0 / planned) : 0,
        LegCount = legs.Count,
        FilledLegCount = legs.Count(item => item.Status == ExecutionLegStatus.Filled),
        CreatedAtUtc = execution.CreatedAtUtc,
        CompletedAtUtc = execution.CompletedAtUtc,
        NeedsCompensation = execution.Status == ExecutionStatus.CompensationRequired
      };
    }

    private async Task<Dictionary<Guid, string>> ReadBasketNamesAsync(List<ExecutionEntity> executions, CancellationToken cancellationToken)
    {
      List<Guid> basketIds = executions.Select(item => item.BasketId).Distinct().ToList();

      List<BasketEntity> baskets = await db.Baskets
        .AsNoTracking()
        .Where(item => basketIds.Contains(item.Id))
        .ToListAsync(cancellationToken);

      Dictionary<Guid, string> names = new Dictionary<Guid, string>();

      foreach (BasketEntity basket in baskets)
      {
        names[basket.Id] = basket.Name;
      }

      return names;
    }
  }
}
