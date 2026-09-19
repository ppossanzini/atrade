using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Operations;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using ExecutionEntity = AutoTrade.Trading.Handlers.Model.Execution;

namespace AutoTrade.Trading.Handlers.CQRS.Operations
{
  /// <summary>
  /// State of the live promotion gate. The requirements are the ones recorded in the roadmap, and each one
  /// is answered in the only honest way available: measured when the application owns the evidence, and
  /// declared not verifiable when it does not, because an approval or a drill cannot be inferred from data.
  ///
  /// Nothing here can open the gate. Live stays out of scope until a person closes every requirement, and a
  /// requirement that is not verifiable is never reported as satisfied just because nothing contradicts it.
  /// </summary>
  public class PromotionQueryHandler(DB db) : IRequestHandler<GetPromotionStatus, PromotionStatusDto>
  {
    private const string DemoPeriodCriteria = "demo_period_criteria";
    private const string ReconciliationClean = "reconciliation_clean";
    private const string RecoveryDrill = "recovery_drill";
    private const string LiveParametersApproved = "live_parameters_approved";
    private const string SecurityReview = "security_review";
    private const string RollbackProcedure = "rollback_procedure";
    private const string AdrAndApproval = "adr_and_approval";

    public async Task<PromotionStatusDto> Handle(GetPromotionStatus request, CancellationToken cancellationToken)
    {
      List<PromotionRequirementDto> requirements = new List<PromotionRequirementDto>
      {
        NotVerifiable(DemoPeriodCriteria),
        await ReadReconciliationAsync(cancellationToken),
        NotVerifiable(RecoveryDrill),
        await ReadLiveParametersAsync(cancellationToken),
        NotVerifiable(SecurityReview),
        await ReadRollbackProcedureAsync(cancellationToken),
        NotVerifiable(AdrAndApproval)
      };

      TradingAccount account = await db.TradingAccounts
        .AsNoTracking()
        .OrderBy(item => item.CreatedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

      return new PromotionStatusDto
      {
        IsLiveEligible = requirements.All(item => item.State == PromotionRequirementState.Satisfied),
        CurrentEnvironment = account != null ? account.Environment.ToString() : null,
        Requirements = requirements
      };
    }

    /// <summary>
    /// Zero unresolved divergences is measurable, but an empty history proves nothing: the requirement asks
    /// for a period of running, so it only holds once executions have actually completed.
    /// </summary>
    private async Task<PromotionRequirementDto> ReadReconciliationAsync(CancellationToken cancellationToken)
    {
      int unresolved = await db.Executions
        .AsNoTracking()
        .CountAsync(item => item.Status == ExecutionStatus.ReconciliationRequired, cancellationToken);

      int completed = await db.Executions
        .AsNoTracking()
        .CountAsync(item => item.Status == ExecutionStatus.CompletedNominal || item.Status == ExecutionStatus.CompletedPartial, cancellationToken);

      if (unresolved > 0)
      {
        return Requirement(ReconciliationClean, PromotionRequirementState.NotSatisfied, "unresolved=" + unresolved + ";completed=" + completed);
      }

      return completed > 0
        ? Requirement(ReconciliationClean, PromotionRequirementState.Satisfied, "unresolved=0;completed=" + completed)
        : Requirement(ReconciliationClean, PromotionRequirementState.NotSatisfied, "unresolved=0;completed=0");
    }

    /// <summary>
    /// The approval of live limits, symbols, volumes and operating mode is a decision, not a measurement.
    /// The only fact the application owns is which account exists, and it reports that as the evidence.
    /// </summary>
    private async Task<PromotionRequirementDto> ReadLiveParametersAsync(CancellationToken cancellationToken)
    {
      TradingAccount account = await db.TradingAccounts
        .AsNoTracking()
        .OrderBy(item => item.CreatedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

      return Requirement(
        LiveParametersApproved,
        PromotionRequirementState.NotVerifiable,
        account != null ? "environment=" + account.Environment : null);
    }

    /// <summary>
    /// The kill switch is exercised through the journal, and that count is real evidence that the procedure
    /// has been run at least once. Rolling back to demo is not something the application can demonstrate,
    /// so the requirement stays open with the count as its evidence.
    /// </summary>
    private async Task<PromotionRequirementDto> ReadRollbackProcedureAsync(CancellationToken cancellationToken)
    {
      int engagements = await db.JournalEvents
        .AsNoTracking()
        .CountAsync(item => item.Kind == JournalEventKind.KillSwitchEngaged, cancellationToken);

      return Requirement(RollbackProcedure, PromotionRequirementState.NotVerifiable, "killSwitchEngagements=" + engagements);
    }

    private static PromotionRequirementDto NotVerifiable(string key)
    {
      return Requirement(key, PromotionRequirementState.NotVerifiable, null);
    }

    private static PromotionRequirementDto Requirement(string key, PromotionRequirementState state, string evidence)
    {
      return new PromotionRequirementDto
      {
        Key = key,
        State = state,
        Evidence = evidence
      };
    }
  }
}
