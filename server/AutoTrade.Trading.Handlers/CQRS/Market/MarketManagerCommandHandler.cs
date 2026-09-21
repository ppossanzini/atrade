using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Market;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Market;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Risk;
using Hikyaku;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Market
{
    /// <summary>
    /// Mode, analysis state and operator decisions. The three decision commands share one path on purpose: the
    /// rules of decidability, expiry and gate re-evaluation must be identical whatever the operator chose, and
    /// a difference between approve, reject and suspend would be a hole rather than a feature.
    /// </summary>
    public class MarketManagerCommandHandler(DB db, IHikyaku hikyaku, IMarketDataSource marketDataSource, RiskEngine engine, IJournalWriter journalWriter, MarketOptions marketOptions, TimeProvider timeProvider)
      : IRequestHandler<SetMarketManagerMode, MarketManagerMode>,
        IRequestHandler<SetAnalysisState, bool>,
        IRequestHandler<ApproveProposal, ProposalDecisionResultDto>,
        IRequestHandler<RejectProposal, ProposalDecisionResultDto>,
        IRequestHandler<SuspendProposal, ProposalDecisionResultDto>,
        IRequestHandler<ValidateMarketManagerMode, bool>,
        IRequestHandler<ValidateAnalysisStateChange, bool>
    {
        private const int MarketManagerStateId = 1;
        private const int ActiveVersionSlotId = 1;
        private const int KillSwitchSingletonId = 1;

        public async Task<MarketManagerMode> Handle(SetMarketManagerMode request, CancellationToken cancellationToken)
        {
            if (!await hikyaku.Send(new ValidateMarketManagerMode { Mode = request.Mode.ToString() }, cancellationToken))
            {
                throw new InvalidOperationException("The requested market manager mode is not valid.");
            }

            MarketManagerState state = await RequireStateAsync(cancellationToken);

            if (state.Mode == request.Mode)
            {
                return state.Mode;
            }

            MarketManagerMode previous = state.Mode;
            state.Mode = request.Mode;
            state.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);

            await journalWriter.AppendOperatorEventAsync(
              request.OperatorId,
              JournalEventKind.MarketManagerModeChanged,
              "MarketManagerState",
              null,
              "from=" + previous + ";to=" + request.Mode,
              cancellationToken);

            return state.Mode;
        }

        public async Task<bool> Handle(SetAnalysisState request, CancellationToken cancellationToken)
        {
            MarketManagerState state = await RequireStateAsync(cancellationToken);

            if (!await hikyaku.Send(new ValidateAnalysisStateChange
            {
                IsRunning = request.IsRunning,
                CurrentIsRunning = state.IsAnalysisRunning,
                OperatorId = request.OperatorId
            }, cancellationToken))
            {
                return false;
            }

            state.IsAnalysisRunning = request.IsRunning;
            state.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);

            await journalWriter.AppendOperatorEventAsync(
              request.OperatorId,
              request.IsRunning ? JournalEventKind.AnalysisStarted : JournalEventKind.AnalysisStopped,
              "MarketManagerState",
              null,
              "isRunning=" + request.IsRunning,
              cancellationToken);

            return true;
        }

        public Task<ProposalDecisionResultDto> Handle(ApproveProposal request, CancellationToken cancellationToken)
        {
            return DecideAsync(request.ProposalId, request.OperatorId, ProposalStatus.Approved, null, cancellationToken);
        }

        public Task<ProposalDecisionResultDto> Handle(RejectProposal request, CancellationToken cancellationToken)
        {
            return DecideAsync(request.ProposalId, request.OperatorId, ProposalStatus.Rejected, request.Reason, cancellationToken);
        }

        public Task<ProposalDecisionResultDto> Handle(SuspendProposal request, CancellationToken cancellationToken)
        {
            return DecideAsync(request.ProposalId, request.OperatorId, ProposalStatus.Suspended, request.Reason, cancellationToken);
        }

        public Task<bool> Handle(ValidateMarketManagerMode request, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(request.Mode) && Enum.TryParse(request.Mode, true, out MarketManagerMode _));
        }

        public async Task<bool> Handle(ValidateAnalysisStateChange request, CancellationToken cancellationToken)
        {
            if (request.OperatorId == Guid.Empty || request.IsRunning == request.CurrentIsRunning)
            {
                return false;
            }

            // Starting an analysis that could not be judged would only fill the queue with proposals nobody asked
            // for, so the operator is told which input is still missing instead of collecting blocked rows.
            if (request.IsRunning && !marketOptions.IsConfigured)
            {
                return false;
            }

            if (request.IsRunning)
            {
                ActiveBasketVersion activeVersion = await db.ActiveBasketVersions
                  .AsNoTracking()
                  .FirstOrDefaultAsync(item => item.Id == ActiveVersionSlotId, cancellationToken);

                return activeVersion != null;
            }

            return true;
        }

        /// <summary>
        /// Applies a decision. Approving re-evaluates the gate with a fresh capture: the operator approved what
        /// the proposal showed, and if the basket is riskier now the approval is refused instead of forwarded.
        /// Refusing and suspending stay available, because stopping is never the dangerous direction.
        /// </summary>
        private async Task<ProposalDecisionResultDto> DecideAsync(Guid proposalId, Guid operatorId, ProposalStatus target, string reason, CancellationToken cancellationToken)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            Proposal proposal = await db.Proposals.FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);

            if (proposal == null)
            {
                return Refused(proposalId, ProposalDecisionOutcome.NotFound, ProposalStatus.Blocked, now);
            }

            // A blocked proposal was never decided by anybody: it is simply not decidable, and saying "already
            // decided" would blame the operator for a gate's verdict.
            if (proposal.Status == ProposalStatus.Blocked)
            {
                return Refused(proposalId, ProposalDecisionOutcome.NotDecidable, proposal.Status, now);
            }

            if (AnalysisRules.IsTerminal(proposal.Status))
            {
                return Refused(proposalId, ProposalDecisionOutcome.AlreadyDecided, proposal.Status, now);
            }

            if (now >= proposal.ExpiresAtUtc)
            {
                proposal.Status = ProposalStatus.Expired;
                proposal.DecidedAtUtc = now;
                proposal.DecidedByOperatorId = operatorId;
                await db.SaveChangesAsync(cancellationToken);
                await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ProposalExpired, "Proposal", proposal.Id, "expired=" + proposal.ExpiresAtUtc.ToString("O"), cancellationToken);

                return Refused(proposalId, ProposalDecisionOutcome.Expired, ProposalStatus.Expired, now);
            }

            // The mode decides who may act. In Automatic the operator has delegated the decisions, so a proposal
            // must be brought back to Manual or Supervised on purpose: otherwise the mode would be a label rather
            // than a rule, and approvals would bypass the very delegation it expresses.
            MarketManagerState state = await db.MarketManagerStates
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.Id == MarketManagerStateId, cancellationToken);

            if (state == null || !AnalysisRules.IsDecidable(proposal.Status, state.Mode, proposal.ExpiresAtUtc, now))
            {
                await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ProposalDecisionRefused, "Proposal", proposal.Id, "reason=mode_requires_operator;mode=" + (state != null ? state.Mode.ToString() : "unknown"), cancellationToken);

                return Refused(proposalId, ProposalDecisionOutcome.NotDecidable, proposal.Status, now);
            }

            if (target == ProposalStatus.Approved && !await IsGateStillExplicitlyAllowedAsync(proposal, cancellationToken))
            {
                await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.ProposalDecisionRefused, "Proposal", proposal.Id, "reason=gate_regressed", cancellationToken);

                return Refused(proposalId, ProposalDecisionOutcome.GateRegressed, proposal.Status, now);
            }

            proposal.Status = target;
            proposal.DecidedAtUtc = now;
            proposal.DecidedByOperatorId = operatorId;
            proposal.DecisionReason = reason;

            await db.SaveChangesAsync(cancellationToken);

            JournalEventKind kind = target == ProposalStatus.Approved
              ? JournalEventKind.ProposalApproved
              : target == ProposalStatus.Rejected ? JournalEventKind.ProposalRejected : JournalEventKind.ProposalSuspended;

            string payload = "status=" + target + (string.IsNullOrWhiteSpace(reason) ? string.Empty : ";reason=" + reason);

            await journalWriter.AppendOperatorEventAsync(operatorId, kind, "Proposal", proposal.Id, payload, cancellationToken);

            return new ProposalDecisionResultDto
            {
                ProposalId = proposal.Id,
                Outcome = ProposalDecisionOutcome.Applied,
                Status = proposal.Status,
                DecidedAtUtc = now
            };
        }

        /// <summary>Re-evaluates the active version of the proposal's basket and requires an explicit allow.</summary>
        private async Task<bool> IsGateStillExplicitlyAllowedAsync(Proposal proposal, CancellationToken cancellationToken)
        {
            ActiveBasketVersion activeVersion = await db.ActiveBasketVersions
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.Id == ActiveVersionSlotId, cancellationToken);

            if (activeVersion == null || activeVersion.VersionId != proposal.BasketVersionId)
            {
                return false;
            }

            BasketVersionPolicy policy = await db.BasketVersionPolicies
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.VersionId == proposal.BasketVersionId, cancellationToken);

            List<BasketVersionLeg> legs = await db.BasketVersionLegs
              .AsNoTracking()
              .Where(item => item.VersionId == proposal.BasketVersionId)
              .OrderBy(item => item.Ordinal)
              .ToListAsync(cancellationToken);

            bool killSwitchEngaged = await db.KillSwitchStates
              .AsNoTracking()
              .AnyAsync(item => item.Id == KillSwitchSingletonId && item.IsEngaged, cancellationToken);

            RiskCandidate candidate = AnalysisRules.BuildCandidate(true, proposal.VersionNumber, policy, legs, killSwitchEngaged);
            MarketDataCapture capture = await marketDataSource.CaptureAsync(AnalysisRules.CreateSymbolRequests(candidate), cancellationToken);

            if (capture == null || !capture.IsAvailable || !capture.CapturedAtUtc.HasValue)
            {
                return false;
            }

            RiskDecisionDto decision = engine.Evaluate(RiskInputFactory.Create(candidate, capture));

            return decision.Verdict == RiskGateVerdict.Allow;
        }

        private async Task<MarketManagerState> RequireStateAsync(CancellationToken cancellationToken)
        {
            MarketManagerState state = await db.MarketManagerStates.FirstOrDefaultAsync(item => item.Id == MarketManagerStateId, cancellationToken);

            if (state == null)
            {
                throw new InvalidOperationException("The market manager state row is missing, so no mode can be changed.");
            }

            return state;
        }

        private static ProposalDecisionResultDto Refused(Guid proposalId, ProposalDecisionOutcome outcome, ProposalStatus status, DateTime now)
        {
            return new ProposalDecisionResultDto
            {
                ProposalId = proposalId,
                Outcome = outcome,
                Status = status,
                DecidedAtUtc = now
            };
        }
    }
}
