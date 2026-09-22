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
using Microsoft.EntityFrameworkCore;
using BasketEntity = AutoTrade.Trading.Handlers.Model.Basket;

namespace AutoTrade.Trading.Handlers.CQRS.Basket
{
    /// <summary>
    /// Basket registry and draft lifecycle. Each update scope has its own command and handler, and
    /// this class is the single writer for every property it owns.
    /// </summary>
    public class BasketCommandHandler(DB db, IHikyaku hikyaku, IJournalWriter journalWriter, TimeProvider timeProvider)
      : IRequestHandler<CreateBasket, CreateBasketResult>,
        IRequestHandler<CloneBasket, CreateBasketResult>,
        IRequestHandler<UpdateBasketIdentity, BasketOperationResult>,
        IRequestHandler<UpdateBasketComposition, BasketOperationResult>,
        IRequestHandler<UpdateBasketPolicy, BasketOperationResult>,
        IRequestHandler<ArchiveBasket, BasketOperationResult>,
        IRequestHandler<ValidateBasketNamePresence, bool>,
        IRequestHandler<ValidateBasketNameUniqueness, bool>,
        IRequestHandler<ValidateBasketCompositionWeights, bool>,
        IRequestHandler<ValidateBasketCompositionSymbols, bool>,
        IRequestHandler<ValidateBasketPolicyValues, bool>,
        IRequestHandler<ValidateBasketArchivable, bool>
    {
        private const string BasketEntityType = "Basket";
        private const int MaxNameLength = 128;
        private const int MaxSymbolLength = 32;
        private const int TotalWeightTarget = 100;
        private const double MinRiskCap = 0.05;
        private const double MaxRiskCap = 5.0;
        private const double MaxStopDistancePips = 100000.0;
        private const double MaxLegSpreadPips = 100000.0;
        private const double MaxLegVolatilityPercent = 100.0;

        /// <summary>
        /// A leg may exist without a stop distance while the operator is still composing the basket: the refusal
        /// belongs to the sizing, which produces no volume and no order, not to the draft. What is rejected here is
        /// only a value that cannot mean anything.
        /// </summary>
        private static bool IsStopDistanceUsable(double stopDistancePips)
        {
            return stopDistancePips >= 0 && stopDistancePips <= MaxStopDistancePips;
        }

        /// <summary>
        /// A leg may also exist without its limits decided: the refusal belongs to the risk gate, which blocks
        /// that leg, not to the draft. Only a value that cannot mean anything is rejected here.
        /// </summary>
        private static bool IsLegLimitUsable(double limit, double maximum)
        {
            return limit >= 0 && limit <= maximum;
        }
        private const int MinCoveragePercent = 50;
        private const int MaxCoveragePercent = 100;
        private const double MinRiskPerBasket = 0.1;
        private const double MaxRiskPerBasket = 10.0;
        private const double MinDailyLossLimit = 0.1;
        private const double MaxDailyLossLimit = 20.0;

        // Defaults validated in the prototype and confirmed by the operator.
        private const EntryMode DefaultEntryMode = EntryMode.RegimeMomentum;
        private const FailurePolicy DefaultFailurePolicy = FailurePolicy.MinimumCoverage;
        private const int DefaultMinimumCoverage = 75;
        private const double DefaultRiskPerBasket = 0.8;
        private const double DefaultDailyLossLimit = 2.5;

        public async Task<CreateBasketResult> Handle(CreateBasket request, CancellationToken cancellationToken)
        {
            CreateBasketResult result = new CreateBasketResult
            {
                Outcome = BasketOperationOutcome.InvalidInput
            };

            if (!await IsNameAcceptableAsync(request.Name, Guid.Empty, cancellationToken))
            {
                result.Outcome = await IsNameTakenAsync(request.Name, Guid.Empty, cancellationToken)
                  ? BasketOperationOutcome.Conflict
                  : BasketOperationOutcome.InvalidInput;

                return result;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            string name = request.Name.Trim();

            BasketEntity basket = new BasketEntity
            {
                Id = Guid.CreateVersion7(),
                Name = name,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ArchivedAtUtc = null
            };

            db.Baskets.Add(basket);
            db.BasketDraftPolicies.Add(new BasketDraftPolicy
            {
                Id = Guid.CreateVersion7(),
                BasketId = basket.Id,
                EntryMode = DefaultEntryMode,
                FailurePolicy = DefaultFailurePolicy,
                MinimumCoverage = DefaultMinimumCoverage,
                RiskPerBasket = DefaultRiskPerBasket,
                DailyLossLimit = DefaultDailyLossLimit
            });

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketCreated, BasketEntityType, basket.Id, "name=" + name, cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;
            result.BasketId = basket.Id;

            return result;
        }

        public async Task<CreateBasketResult> Handle(CloneBasket request, CancellationToken cancellationToken)
        {
            CreateBasketResult result = new CreateBasketResult
            {
                Outcome = BasketOperationOutcome.InvalidInput
            };

            BasketEntity source = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.SourceBasketId, cancellationToken);
            if (source == null)
            {
                result.Outcome = BasketOperationOutcome.NotFound;

                return result;
            }

            if (!await IsNameAcceptableAsync(request.Name, Guid.Empty, cancellationToken))
            {
                result.Outcome = await IsNameTakenAsync(request.Name, Guid.Empty, cancellationToken)
                  ? BasketOperationOutcome.Conflict
                  : BasketOperationOutcome.InvalidInput;

                return result;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            string name = request.Name.Trim();

            BasketEntity clone = new BasketEntity
            {
                Id = Guid.CreateVersion7(),
                Name = name,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ArchivedAtUtc = null
            };

            db.Baskets.Add(clone);

            List<BasketDraftLeg> sourceLegs = await db.BasketDraftLegs
              .Where(item => item.BasketId == source.Id)
              .OrderBy(item => item.Symbol)
              .ToListAsync(cancellationToken);

            foreach (BasketDraftLeg sourceLeg in sourceLegs)
            {
                db.BasketDraftLegs.Add(new BasketDraftLeg
                {
                    Id = Guid.CreateVersion7(),
                    BasketId = clone.Id,
                    Symbol = sourceLeg.Symbol,
                    Market = sourceLeg.Market,
                    Direction = sourceLeg.Direction,
                    TimeFrame = sourceLeg.TimeFrame,
                    Weight = sourceLeg.Weight,
                    RiskCap = sourceLeg.RiskCap,
                    StopDistancePips = sourceLeg.StopDistancePips,
                    MaxSpreadPips = sourceLeg.MaxSpreadPips,
                    MaxVolatilityPercent = sourceLeg.MaxVolatilityPercent,
                    IsSelected = sourceLeg.IsSelected
                });
            }

            BasketDraftPolicy sourcePolicy = await db.BasketDraftPolicies.FirstOrDefaultAsync(item => item.BasketId == source.Id, cancellationToken);

            db.BasketDraftPolicies.Add(new BasketDraftPolicy
            {
                Id = Guid.CreateVersion7(),
                BasketId = clone.Id,
                EntryMode = sourcePolicy != null ? sourcePolicy.EntryMode : DefaultEntryMode,
                FailurePolicy = sourcePolicy != null ? sourcePolicy.FailurePolicy : DefaultFailurePolicy,
                MinimumCoverage = sourcePolicy != null ? sourcePolicy.MinimumCoverage : DefaultMinimumCoverage,
                RiskPerBasket = sourcePolicy != null ? sourcePolicy.RiskPerBasket : DefaultRiskPerBasket,
                DailyLossLimit = sourcePolicy != null ? sourcePolicy.DailyLossLimit : DefaultDailyLossLimit
            });

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketCloned, BasketEntityType, clone.Id, "sourceBasketId=" + source.Id, cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;
            result.BasketId = clone.Id;

            return result;
        }

        public async Task<BasketOperationResult> Handle(UpdateBasketIdentity request, CancellationToken cancellationToken)
        {
            BasketOperationResult result = new BasketOperationResult
            {
                Outcome = BasketOperationOutcome.InvalidInput
            };

            BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
            if (basket == null)
            {
                result.Outcome = BasketOperationOutcome.NotFound;

                return result;
            }

            if (basket.ArchivedAtUtc.HasValue)
            {
                result.Outcome = BasketOperationOutcome.InvalidState;

                return result;
            }

            // Identity is the only scope written here.
            if (!await hikyaku.Send(new ValidateBasketNamePresence { Name = request.Name }, cancellationToken))
            {
                return result;
            }

            if (!await hikyaku.Send(new ValidateBasketNameUniqueness { Name = request.Name, ExcludedBasketId = basket.Id }, cancellationToken))
            {
                result.Outcome = BasketOperationOutcome.Conflict;

                return result;
            }

            string name = request.Name.Trim();
            basket.Name = name;
            basket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketRenamed, BasketEntityType, basket.Id, "name=" + name, cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;

            return result;
        }

        public async Task<BasketOperationResult> Handle(UpdateBasketComposition request, CancellationToken cancellationToken)
        {
            BasketOperationResult result = new BasketOperationResult
            {
                Outcome = BasketOperationOutcome.InvalidInput
            };

            List<BasketCompositionLegDto> legs = request.Legs ?? new List<BasketCompositionLegDto>();

            if (!await hikyaku.Send(new ValidateBasketCompositionWeights { Legs = legs }, cancellationToken))
            {
                return result;
            }

            if (!await hikyaku.Send(new ValidateBasketCompositionSymbols { Legs = legs }, cancellationToken))
            {
                return result;
            }

            BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
            if (basket == null)
            {
                result.Outcome = BasketOperationOutcome.NotFound;

                return result;
            }

            if (basket.ArchivedAtUtc.HasValue)
            {
                result.Outcome = BasketOperationOutcome.InvalidState;

                return result;
            }

            List<BasketDraftLeg> existingLegs = await db.BasketDraftLegs.Where(item => item.BasketId == basket.Id).ToListAsync(cancellationToken);
            db.BasketDraftLegs.RemoveRange(existingLegs);

            foreach (BasketCompositionLegDto leg in legs)
            {
                db.BasketDraftLegs.Add(new BasketDraftLeg
                {
                    Id = Guid.CreateVersion7(),
                    BasketId = basket.Id,
                    Symbol = leg.Symbol.Trim().ToUpperInvariant(),
                    Direction = leg.Direction,
                    TimeFrame = leg.TimeFrame,
                    Weight = leg.Weight,
                    RiskCap = leg.RiskCap,
                    StopDistancePips = leg.StopDistancePips,
                    MaxSpreadPips = leg.MaxSpreadPips,
                    MaxVolatilityPercent = leg.MaxVolatilityPercent,
                    IsSelected = leg.IsSelected
                });
            }

            basket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketCompositionUpdated, BasketEntityType, basket.Id, "legs=" + legs.Count, cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;

            return result;
        }

        public async Task<BasketOperationResult> Handle(UpdateBasketPolicy request, CancellationToken cancellationToken)
        {
            BasketOperationResult result = new BasketOperationResult
            {
                Outcome = BasketOperationOutcome.InvalidInput
            };

            if (request.Policy == null)
            {
                return result;
            }

            if (!await hikyaku.Send(new ValidateBasketPolicyValues { Policy = request.Policy }, cancellationToken))
            {
                return result;
            }

            BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
            if (basket == null)
            {
                result.Outcome = BasketOperationOutcome.NotFound;

                return result;
            }

            if (basket.ArchivedAtUtc.HasValue)
            {
                result.Outcome = BasketOperationOutcome.InvalidState;

                return result;
            }

            BasketDraftPolicy policy = await db.BasketDraftPolicies.FirstOrDefaultAsync(item => item.BasketId == basket.Id, cancellationToken);
            if (policy == null)
            {
                policy = new BasketDraftPolicy
                {
                    Id = Guid.CreateVersion7(),
                    BasketId = basket.Id
                };

                db.BasketDraftPolicies.Add(policy);
            }

            policy.EntryMode = request.Policy.EntryMode;
            policy.FailurePolicy = request.Policy.FailurePolicy;
            policy.MinimumCoverage = request.Policy.MinimumCoverage;
            policy.RiskPerBasket = request.Policy.RiskPerBasket;
            policy.DailyLossLimit = request.Policy.DailyLossLimit;
            policy.CombinationMode = request.Policy.CombinationMode;
            policy.MinimumAgreement = request.Policy.MinimumAgreement;
            policy.MinimumStrategyConfidence = request.Policy.MinimumStrategyConfidence;
            policy.ConflictPolicy = request.Policy.ConflictPolicy;

            List<StrategyComponentDto> components = request.Policy.Components ?? new List<StrategyComponentDto>();
            List<BasketDraftStrategyComponent> storedComponents = await db.BasketDraftStrategyComponents.Where(item => item.BasketId == basket.Id).ToListAsync(cancellationToken);
            db.BasketDraftStrategyComponents.RemoveRange(storedComponents);
            int componentOrdinal = 0;
            foreach (StrategyComponentDto component in components)
            {
                db.BasketDraftStrategyComponents.Add(new BasketDraftStrategyComponent
                {
                    Id = Guid.CreateVersion7(),
                    BasketId = basket.Id,
                    Ordinal = componentOrdinal++, Type = component.Type, Enabled = component.Enabled,
                    Weight = component.Weight, TimeFrame = component.TimeFrame, ParametersJson = component.ParametersJson
                });
            }

            basket.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketPolicyUpdated, BasketEntityType, basket.Id, "failurePolicy=" + request.Policy.FailurePolicy, cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;

            return result;
        }

        public async Task<BasketOperationResult> Handle(ArchiveBasket request, CancellationToken cancellationToken)
        {
            BasketOperationResult result = new BasketOperationResult
            {
                Outcome = BasketOperationOutcome.InvalidState
            };

            BasketEntity basket = await db.Baskets.FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
            if (basket == null)
            {
                result.Outcome = BasketOperationOutcome.NotFound;

                return result;
            }

            bool isArchivable = await hikyaku.Send(new ValidateBasketArchivable { BasketId = basket.Id }, cancellationToken);
            if (!isArchivable)
            {
                await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketOperationRejected, BasketEntityType, basket.Id, "operation=archive", cancellationToken);

                return result;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            basket.ArchivedAtUtc = now;
            basket.UpdatedAtUtc = now;

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.BasketArchived, BasketEntityType, basket.Id, "archived=true", cancellationToken);

            result.Outcome = BasketOperationOutcome.Applied;

            return result;
        }

        public Task<bool> Handle(ValidateBasketNamePresence request, CancellationToken cancellationToken)
        {
            bool hasName = !string.IsNullOrWhiteSpace(request.Name);
            bool fitsLength = hasName && request.Name.Trim().Length <= MaxNameLength;

            return Task.FromResult(hasName && fitsLength);
        }

        public async Task<bool> Handle(ValidateBasketNameUniqueness request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return false;
            }

            return !await IsNameTakenAsync(request.Name, request.ExcludedBasketId, cancellationToken);
        }

        public Task<bool> Handle(ValidateBasketCompositionWeights request, CancellationToken cancellationToken)
        {
            List<BasketCompositionLegDto> legs = request.Legs ?? new List<BasketCompositionLegDto>();

            if (legs.Count == 0)
            {
                return Task.FromResult(false);
            }

            bool everyWeightInRange = legs.All(leg => leg.Weight >= 0 && leg.Weight <= TotalWeightTarget);
            if (!everyWeightInRange)
            {
                return Task.FromResult(false);
            }

            List<BasketCompositionLegDto> selectedLegs = legs.Where(leg => leg.IsSelected).ToList();

            if (selectedLegs.Count == 0 || selectedLegs.Any(leg => leg.Weight <= 0))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(selectedLegs.Sum(leg => leg.Weight) == TotalWeightTarget);
        }

        public Task<bool> Handle(ValidateBasketCompositionSymbols request, CancellationToken cancellationToken)
        {
            List<BasketCompositionLegDto> legs = request.Legs ?? new List<BasketCompositionLegDto>();

            bool everySymbolUsable = legs.All(leg =>
              !string.IsNullOrWhiteSpace(leg.Symbol)
              && leg.Symbol.Trim().Length <= MaxSymbolLength
              && leg.RiskCap >= MinRiskCap
              && leg.RiskCap <= MaxRiskCap
              && IsStopDistanceUsable(leg.StopDistancePips)
              && IsLegLimitUsable(leg.MaxSpreadPips, MaxLegSpreadPips)
              && IsLegLimitUsable(leg.MaxVolatilityPercent, MaxLegVolatilityPercent));

            if (!everySymbolUsable)
            {
                return Task.FromResult(false);
            }

            List<string> symbols = legs.Select(leg => leg.Symbol.Trim().ToUpperInvariant()).ToList();

            return Task.FromResult(symbols.Distinct().Count() == symbols.Count);
        }

        public Task<bool> Handle(ValidateBasketPolicyValues request, CancellationToken cancellationToken)
        {
            if (request.Policy == null)
            {
                return Task.FromResult(false);
            }

            bool coverageValid = request.Policy.MinimumCoverage >= MinCoveragePercent
              && request.Policy.MinimumCoverage <= MaxCoveragePercent;

            bool riskValid = request.Policy.RiskPerBasket >= MinRiskPerBasket
              && request.Policy.RiskPerBasket <= MaxRiskPerBasket;

            bool lossValid = request.Policy.DailyLossLimit >= MinDailyLossLimit
              && request.Policy.DailyLossLimit <= MaxDailyLossLimit;

            // The declared rule must be one the vocabulary knows: an unknown value would make the strategy
            // unreportable in a proposal, which is the only thing the field is for until an evidence source exists.
            bool entryModeValid = Enum.IsDefined(typeof(EntryMode), request.Policy.EntryMode);

            return Task.FromResult(coverageValid && riskValid && lossValid && entryModeValid);
        }

        public async Task<bool> Handle(ValidateBasketArchivable request, CancellationToken cancellationToken)
        {
            BasketEntity basket = await db.Baskets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.BasketId, cancellationToken);
            if (basket == null || basket.ArchivedAtUtc.HasValue)
            {
                return false;
            }

            bool holdsActiveVersion = await db.ActiveBasketVersions.AnyAsync(item => item.BasketId == basket.Id, cancellationToken);

            return !holdsActiveVersion;
        }

        private async Task<bool> IsNameAcceptableAsync(string name, Guid excludedBasketId, CancellationToken cancellationToken)
        {
            if (!await Handle(new ValidateBasketNamePresence { Name = name }, cancellationToken))
            {
                return false;
            }

            return !await IsNameTakenAsync(name, excludedBasketId, cancellationToken);
        }

        private async Task<bool> IsNameTakenAsync(string name, Guid excludedBasketId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string candidate = name.Trim().ToLowerInvariant();

            return await db.Baskets
              .AsNoTracking()
              .AnyAsync(
                item => item.ArchivedAtUtc == null
                        && item.Id != excludedBasketId
                        && item.Name.ToLower() == candidate,
                cancellationToken);
        }
    }
}
