using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Broker;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;
using BrokerEntity = AutoTrade.Trading.Handlers.Model.BrokerAuthorization;

namespace AutoTrade.Trading.Handlers.CQRS.Broker
{
    /// <summary>
    /// Owns the stored provider grant. It is the single writer of the authorization row and of the
    /// correlator state, and it never writes a token in clear text.
    /// </summary>
    public class BrokerCommandHandler(DB db, IHikyaku hikyaku, IBrokerTokenProtector tokenProtector, IBrokerTokenClient tokenClient, IBrokerAuthorizationCorrelator correlator, BrokerOptions options, IJournalWriter journalWriter, TimeProvider timeProvider)
      : IRequestHandler<StartBrokerAuthorization, BrokerAuthorizationStartDto>,
        IRequestHandler<CompleteBrokerAuthorization, BrokerAuthorizationResultDto>,
        IRequestHandler<ImportBrokerTokens, BrokerAuthorizationResultDto>,
        IRequestHandler<RevokeBrokerAuthorization, BrokerAuthorizationResultDto>,
        IRequestHandler<ValidateBrokerClientConfiguration, bool>,
        IRequestHandler<ValidateBrokerAuthorizationCallback, bool>,
        IRequestHandler<ValidateBrokerTokenImport, bool>
    {
        private const string BrokerEntityType = "Broker";
        private const int MaxCodeLength = 512;
        private const int MaxTokenLength = 512;
        private const int MaxCorrelationLength = 128;

        public async Task<BrokerAuthorizationStartDto> Handle(StartBrokerAuthorization request, CancellationToken cancellationToken)
        {
            BrokerAuthorizationStartDto result = new BrokerAuthorizationStartDto
            {
                Outcome = BrokerAuthorizationOutcome.NotConfigured
            };

            if (!await hikyaku.Send(new ValidateBrokerClientConfiguration(), cancellationToken))
            {
                return result;
            }

            IssuedBrokerCorrelator issued = await correlator.IssueAsync(request.OperatorId, options.Environment, cancellationToken);

            result.Outcome = BrokerAuthorizationOutcome.Applied;
            result.AuthorizationUrl = BuildAuthorizationUrl();
            result.CorrelationId = issued.CorrelationId;
            result.ExpiresAtUtc = issued.ExpiresAtUtc;

            // The correlator itself is never journaled: the payload records only the environment.
            await journalWriter.AppendOperatorEventAsync(
              request.OperatorId,
              JournalEventKind.BrokerAuthorizationStarted,
              BrokerEntityType,
              null,
              "environment=" + options.Environment,
              cancellationToken);

            return result;
        }

        public async Task<BrokerAuthorizationResultDto> Handle(CompleteBrokerAuthorization request, CancellationToken cancellationToken)
        {
            BrokerAuthorizationResultDto result = new BrokerAuthorizationResultDto
            {
                Outcome = BrokerAuthorizationOutcome.InvalidRequest
            };

            if (!await hikyaku.Send(new ValidateBrokerAuthorizationCallback
            {
                Code = request.Code,
                CorrelationId = request.CorrelationId
            }, cancellationToken))
            {
                return result;
            }

            BrokerAuthorizationAttempt attempt = await correlator.ConsumeAsync(request.CorrelationId, options.Environment, cancellationToken);
            if (attempt == null)
            {
                result.Outcome = BrokerAuthorizationOutcome.InvalidCorrelation;

                await journalWriter.AppendOperatorEventAsync(
                  Guid.Empty,
                  JournalEventKind.BrokerAuthorizationRejected,
                  BrokerEntityType,
                  null,
                  "reason=no-pending-correlator",
                  cancellationToken);

                return result;
            }

            BrokerTokenExchangeResult exchange = await tokenClient.ExchangeAuthorizationCodeAsync(request.Code, cancellationToken);
            if (!exchange.IsSuccess)
            {
                result.Outcome = BrokerAuthorizationOutcome.ProviderRejected;
                result.Detail = exchange.ErrorDetail;

                await journalWriter.AppendOperatorEventAsync(
                  attempt.CreatedByOperatorId,
                  JournalEventKind.BrokerAuthorizationRejected,
                  BrokerEntityType,
                  null,
                  "reason=token-exchange-failed",
                  cancellationToken);

                return result;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            BrokerEntity authorization = await StoreGrantAsync(exchange.Tokens, attempt.CreatedByOperatorId, now, cancellationToken);

            await journalWriter.AppendOperatorEventAsync(
              attempt.CreatedByOperatorId,
              JournalEventKind.BrokerAuthorizationCompleted,
              BrokerEntityType,
              authorization.Id,
              "environment=" + options.Environment,
              cancellationToken);

            result.Outcome = BrokerAuthorizationOutcome.Applied;
            result.CtidTraderAccountId = authorization.CtidTraderAccountId;

            return result;
        }

        public async Task<BrokerAuthorizationResultDto> Handle(ImportBrokerTokens request, CancellationToken cancellationToken)
        {
            BrokerAuthorizationResultDto result = new BrokerAuthorizationResultDto
            {
                Outcome = BrokerAuthorizationOutcome.NotConfigured
            };

            // Manual import is a development affordance; when it is not enabled the endpoint behaves as if the
            // broker were not configured at all.
            if (!options.IsClientConfigured || !options.AllowManualTokenImport)
            {
                return result;
            }

            result.Outcome = BrokerAuthorizationOutcome.InvalidRequest;

            if (!await hikyaku.Send(new ValidateBrokerTokenImport
            {
                AccessToken = request.AccessToken,
                RefreshToken = request.RefreshToken
            }, cancellationToken))
            {
                return result;
            }

            // The imported pair is normalized through the provider refresh endpoint. That both proves the
            // credential is real and replaces the unknown expiry with the one the provider reports.
            BrokerTokenExchangeResult refresh = await tokenClient.RefreshAsync(request.RefreshToken, cancellationToken);
            if (!refresh.IsSuccess)
            {
                result.Outcome = BrokerAuthorizationOutcome.ProviderRejected;
                result.Detail = refresh.ErrorDetail;

                await journalWriter.AppendOperatorEventAsync(
                  request.OperatorId,
                  JournalEventKind.BrokerAuthorizationRejected,
                  BrokerEntityType,
                  null,
                  "reason=token-import-rejected",
                  cancellationToken);

                return result;
            }

            DateTime importedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            BrokerEntity imported = await StoreGrantAsync(refresh.Tokens, request.OperatorId, importedAtUtc, cancellationToken);

            await journalWriter.AppendOperatorEventAsync(
              request.OperatorId,
              JournalEventKind.BrokerAuthorizationImported,
              BrokerEntityType,
              imported.Id,
              "environment=" + options.Environment,
              cancellationToken);

            result.Outcome = BrokerAuthorizationOutcome.Applied;
            result.CtidTraderAccountId = imported.CtidTraderAccountId;

            return result;
        }

        public async Task<BrokerAuthorizationResultDto> Handle(RevokeBrokerAuthorization request, CancellationToken cancellationToken)
        {
            BrokerAuthorizationResultDto result = new BrokerAuthorizationResultDto
            {
                Outcome = BrokerAuthorizationOutcome.Applied
            };

            BrokerEntity authorization = await db.BrokerAuthorizations
              .FirstOrDefaultAsync(item => item.Environment == options.Environment, cancellationToken);

            if (authorization == null)
            {
                // Nothing stored means the desired state already holds; the caller is told so explicitly.
                result.Outcome = BrokerAuthorizationOutcome.NotFound;

                return result;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            // The provider keeps its own grant: deleting the local copy is a forget, not a server side revoke.
            db.BrokerAuthorizations.Remove(authorization);
            await db.SaveChangesAsync(cancellationToken);

            await journalWriter.AppendOperatorEventAsync(
              request.OperatorId,
              JournalEventKind.BrokerAuthorizationRevoked,
              BrokerEntityType,
              authorization.Id,
              "environment=" + options.Environment,
              cancellationToken);

            return result;
        }

        public Task<bool> Handle(ValidateBrokerClientConfiguration request, CancellationToken cancellationToken)
        {
            return Task.FromResult(options.IsClientConfigured);
        }

        /// <summary>
        /// Format rules only: whether a pending correlator actually exists is handler state, not a rule.
        /// </summary>
        public Task<bool> Handle(ValidateBrokerAuthorizationCallback request, CancellationToken cancellationToken)
        {
            bool codeUsable = !string.IsNullOrWhiteSpace(request.Code) && request.Code.Length <= MaxCodeLength;

            if (!codeUsable)
            {
                return Task.FromResult(false);
            }

            if (string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(request.CorrelationId.Length <= MaxCorrelationLength && IsBase64Url(request.CorrelationId));
        }

        public Task<bool> Handle(ValidateBrokerTokenImport request, CancellationToken cancellationToken)
        {
            // Presence and size only. The access token is validated and then replaced by the refreshed one, so
            // requiring it is a completeness check of the operator's input, not a credential check.
            bool accessUsable = !string.IsNullOrWhiteSpace(request.AccessToken) && request.AccessToken.Length <= MaxTokenLength;
            bool refreshUsable = !string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Length <= MaxTokenLength;

            return Task.FromResult(accessUsable && refreshUsable);
        }

        /// <summary>
        /// Single writer of the stored grant: every path that obtains tokens goes through here, so the
        /// encryption and the upsert rules cannot drift between them.
        /// </summary>
        private async Task<BrokerEntity> StoreGrantAsync(BrokerTokenResponse tokens, Guid operatorId, DateTime now, CancellationToken cancellationToken)
        {
            BrokerEntity authorization = await db.BrokerAuthorizations
              .FirstOrDefaultAsync(item => item.Environment == options.Environment, cancellationToken);

            if (authorization == null)
            {
                authorization = new BrokerEntity
                {
                    Id = Guid.CreateVersion7(),
                    Environment = options.Environment
                };

                db.BrokerAuthorizations.Add(authorization);
            }

            authorization.AccessTokenCipher = tokenProtector.Protect(tokens.AccessToken);
            authorization.RefreshTokenCipher = tokenProtector.Protect(tokens.RefreshToken);
            authorization.AccessTokenExpiresAtUtc = now.AddSeconds(tokens.ExpiresIn);
            authorization.AuthorizedAtUtc = now;
            authorization.AuthorizedByOperatorId = operatorId;
            authorization.UpdatedAtUtc = now;
            authorization.LastError = null;

            await db.SaveChangesAsync(cancellationToken);

            return authorization;
        }

        private string BuildAuthorizationUrl()
        {
            List<string> parameters = new List<string>
      {
        "client_id=" + Uri.EscapeDataString(options.ClientId),
        "redirect_uri=" + Uri.EscapeDataString(options.RedirectUri),
        "scope=" + Uri.EscapeDataString(options.Scope),
        "product=web"
      };

            return options.AuthorizationEndpoint + "?" + string.Join("&", parameters);
        }

        private static bool IsBase64Url(string value)
        {
            return value.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_');
        }
    }
}
