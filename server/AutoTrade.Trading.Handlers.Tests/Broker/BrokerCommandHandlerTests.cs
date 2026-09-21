using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Broker;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Broker;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Tests.Broker;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
    /// <summary>
    /// Covers the OAuth authorization flow: what the start command issues, what the callback accepts and
    /// refuses, and what ends up stored. Provider transport behaviour lives in the token client tests.
    /// </summary>
    public class BrokerCommandHandlerTests
    {
        private const string AccessToken = "access-token-value";
        private const string RefreshToken = "refresh-token-value";

        private static TradingTestContext CreateContext()
        {
            return new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Broker:ClientId", "client-id" },
        { "Trading:Broker:ClientSecret", "client-secret" },
        { "Trading:Broker:TokenKey", Convert.ToBase64String(new byte[32]) },
        { "Trading:Broker:RedirectUri", "http://127.0.0.1:5271/api/broker/callback" },
        { "Trading:Broker:ReturnUri", "http://127.0.0.1:5180/broker" },
        { "Trading:Broker:Environment", "Demo" },
        { "Trading:Broker:AllowManualTokenImport", "true" }
      });
        }

        private static TradingTestContext CreateContextWithoutTokenImport()
        {
            return new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Broker:ClientId", "client-id" },
        { "Trading:Broker:ClientSecret", "client-secret" },
        { "Trading:Broker:TokenKey", Convert.ToBase64String(new byte[32]) },
        { "Trading:Broker:RedirectUri", "http://127.0.0.1:5271/api/broker/callback" },
        { "Trading:Broker:Environment", "Demo" }
      });
        }

        private static TradingTestContext CreateUnconfiguredContext()
        {
            return new TradingTestContext(new Dictionary<string, string>());
        }

        private static async Task<BrokerAuthorizationStartDto> StartAsync(TradingTestContext context)
        {
            BrokerAuthorizationStartDto start = await context.Hikyaku.Send(
              new StartBrokerAuthorization { OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.Applied, start.Outcome);

            return start;
        }

        [Fact]
        public async Task StartAuthorization_ReturnsTheDocumentedConsentUrlAndAJournaledAttempt()
        {
            using TradingTestContext context = CreateContext();

            BrokerAuthorizationStartDto start = await StartAsync(context);

            Assert.StartsWith("https://id.ctrader.com/my/settings/openapi/grantingaccess/?", start.AuthorizationUrl, StringComparison.Ordinal);
            Assert.Contains("client_id=client-id", start.AuthorizationUrl, StringComparison.Ordinal);
            Assert.Contains("scope=accounts", start.AuthorizationUrl, StringComparison.Ordinal);
            Assert.Contains("product=web", start.AuthorizationUrl, StringComparison.Ordinal);
            Assert.Contains(
              "redirect_uri=" + Uri.EscapeDataString("http://127.0.0.1:5271/api/broker/callback"),
              start.AuthorizationUrl,
              StringComparison.Ordinal);

            // The client secret must never travel in the consent URL.
            Assert.DoesNotContain("client-secret", start.AuthorizationUrl, StringComparison.Ordinal);

            Assert.False(string.IsNullOrWhiteSpace(start.CorrelationId));
            Assert.True(start.ExpiresAtUtc > context.Clock.GetUtcNow().UtcDateTime);

            var attempt = Assert.Single(context.Db.BrokerAuthorizationAttempts);
            Assert.Equal(TradingEnvironment.Demo, attempt.Environment);
            Assert.Null(attempt.ConsumedAtUtc);

            // Only the hash is stored, so the row never reveals a usable correlator.
            Assert.DoesNotContain(start.CorrelationId, attempt.CorrelationHash, StringComparison.Ordinal);

            var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationStarted);
            Assert.DoesNotContain(start.CorrelationId, journalEvent.Payload, StringComparison.Ordinal);
            Assert.Empty(context.Db.BrokerAuthorizations);
        }

        [Fact]
        public async Task StartAuthorization_WhenTheBrokerIsNotConfigured_ReportsNotConfiguredAndWritesNothing()
        {
            using TradingTestContext context = CreateUnconfiguredContext();

            BrokerAuthorizationStartDto start = await context.Hikyaku.Send(
              new StartBrokerAuthorization { OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.NotConfigured, start.Outcome);
            Assert.Null(start.AuthorizationUrl);
            Assert.Empty(context.Db.BrokerAuthorizationAttempts);
            Assert.Empty(context.Db.JournalEvents);
        }

        [Fact]
        public async Task StartAuthorization_InvalidatesAnyPreviouslyPendingAttempt()
        {
            using TradingTestContext context = CreateContext();
            BrokerAuthorizationStartDto first = await StartAsync(context);
            BrokerAuthorizationStartDto second = await StartAsync(context);

            Assert.NotEqual(first.CorrelationId, second.CorrelationId);

            var attempts = context.Db.BrokerAuthorizationAttempts.ToList();
            Assert.Equal(2, attempts.Count);
            Assert.Single(attempts, item => item.ConsumedAtUtc == null);

            // The superseded correlator can no longer complete a flow.
            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "code", CorrelationId = first.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, completed.Outcome);
        }

        [Fact]
        public async Task CompleteAuthorization_StoresEncryptedTokensAndPublishesNoSecret()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            BrokerAuthorizationStartDto start = await StartAsync(context);

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.Applied, completed.Outcome);
            Assert.Equal(new[] { "auth-code" }, context.TokenClient.ExchangedCodes.ToArray());

            var authorization = Assert.Single(context.Db.BrokerAuthorizations);
            Assert.Equal(TradingEnvironment.Demo, authorization.Environment);

            // Nothing readable is persisted, and neither token appears in any stored column.
            Assert.StartsWith("v1.", authorization.AccessTokenCipher, StringComparison.Ordinal);
            Assert.StartsWith("v1.", authorization.RefreshTokenCipher, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, authorization.AccessTokenCipher, StringComparison.Ordinal);
            Assert.DoesNotContain(RefreshToken, authorization.RefreshTokenCipher, StringComparison.Ordinal);

            Assert.Equal(context.Clock.GetUtcNow().UtcDateTime.AddSeconds(2628000), authorization.AccessTokenExpiresAtUtc);
            Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, authorization.AuthorizedAtUtc);
            Assert.NotEqual(Guid.Empty, authorization.AuthorizedByOperatorId);
            Assert.Null(authorization.LastError);

            // The attempt is consumed, so the same callback cannot be replayed.
            var attempt = Assert.Single(context.Db.BrokerAuthorizationAttempts);
            Assert.NotNull(attempt.ConsumedAtUtc);

            Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationCompleted);
        }

        [Fact]
        public async Task CompleteAuthorization_WithTheSameCodeTwice_IsRejectedTheSecondTime()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            BrokerAuthorizationStartDto start = await StartAsync(context);

            BrokerAuthorizationResultDto first = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            BrokerAuthorizationResultDto second = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.Applied, first.Outcome);
            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, second.Outcome);

            // The provider was asked exactly once, so the code was not replayed.
            Assert.Single(context.TokenClient.ExchangedCodes);
            Assert.Single(context.Db.BrokerAuthorizations);
        }

        [Fact]
        public async Task CompleteAuthorization_WithoutStartingAFlow_IsRejected()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code" },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, completed.Outcome);
            Assert.Empty(context.TokenClient.ExchangedCodes);
            Assert.Empty(context.Db.BrokerAuthorizations);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationRejected);
        }

        [Fact]
        public async Task CompleteAuthorization_WhenTheCorrelatorExpired_IsRejected()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            BrokerAuthorizationStartDto start = await StartAsync(context);

            context.Clock.Advance(TimeSpan.FromMinutes(11));

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, completed.Outcome);
            Assert.Empty(context.TokenClient.ExchangedCodes);
            Assert.Empty(context.Db.BrokerAuthorizations);
        }

        [Fact]
        public async Task CompleteAuthorization_WithAMismatchedCorrelator_IsRejected()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            await StartAsync(context);

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = "Zm9yZ2VkLWNvcnJlbGF0b3I" },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, completed.Outcome);
            Assert.Empty(context.TokenClient.ExchangedCodes);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CompleteAuthorization_WithoutACode_ReturnsInvalidRequestWithoutTouchingTheProvider(string code)
        {
            using TradingTestContext context = CreateContext();
            BrokerAuthorizationStartDto start = await StartAsync(context);

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = code, CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidRequest, completed.Outcome);
            Assert.Empty(context.TokenClient.ExchangedCodes);
            Assert.Single(context.Db.BrokerAuthorizationAttempts, item => item.ConsumedAtUtc == null);
        }

        [Fact]
        public async Task CompleteAuthorization_WhenTheProviderRejects_ReportsTheReasonAndStoresNothing()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Failure("invalid_client");

            BrokerAuthorizationStartDto start = await StartAsync(context);

            BrokerAuthorizationResultDto completed = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.ProviderRejected, completed.Outcome);
            Assert.Equal("invalid_client", completed.Detail);
            Assert.Empty(context.Db.BrokerAuthorizations);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationRejected);
        }

        [Fact]
        public async Task CompleteAuthorization_WhenTheProviderRejects_ConsumesTheAttemptSoItCannotBeRetried()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Failure("invalid_grant");

            BrokerAuthorizationStartDto start = await StartAsync(context);

            await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            // The correlator is single use even on failure: retrying the same callback must not be possible,
            // the operator restarts the flow instead.
            var attempt = Assert.Single(context.Db.BrokerAuthorizationAttempts);
            Assert.NotNull(attempt.ConsumedAtUtc);

            BrokerAuthorizationResultDto retried = await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidCorrelation, retried.Outcome);
            Assert.Single(context.TokenClient.ExchangedCodes);
        }

        [Fact]
        public async Task RevokeAuthorization_RemovesTheStoredGrantAndJournalsIt()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 2628000);

            BrokerAuthorizationStartDto start = await StartAsync(context);
            await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            Guid operatorId = Guid.CreateVersion7();

            BrokerAuthorizationResultDto revoked = await context.Hikyaku.Send(
              new RevokeBrokerAuthorization { OperatorId = operatorId },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.Applied, revoked.Outcome);
            Assert.Empty(context.Db.BrokerAuthorizations);

            var journalEvent = Assert.Single(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationRevoked);
            Assert.Equal(operatorId, journalEvent.ActorId);
        }

        [Fact]
        public async Task RevokeAuthorization_WithoutAStoredGrant_ReportsNotFound()
        {
            using TradingTestContext context = CreateContext();

            BrokerAuthorizationResultDto revoked = await context.Hikyaku.Send(
              new RevokeBrokerAuthorization { OperatorId = Guid.CreateVersion7() },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.NotFound, revoked.Outcome);
        }

        [Fact]
        public async Task GetBrokerConnectionStatus_ReportsConfiguredButNotAuthorized()
        {
            using TradingTestContext context = CreateContext();

            BrokerConnectionStatusDto status = await context.Hikyaku.Send(new GetBrokerConnectionStatus(), CancellationToken.None);

            Assert.True(status.IsClientConfigured);
            Assert.False(status.IsAuthorized);
            Assert.Null(status.AuthorizedAtUtc);
            Assert.Null(status.AccessTokenExpiresAtUtc);
            Assert.False(status.IsAccessTokenExpired);
            Assert.Equal(TradingEnvironment.Demo, status.Environment);
        }

        [Fact]
        public async Task GetBrokerConnectionStatus_AfterAuthorization_ReportsTheExpiry()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success(AccessToken, RefreshToken, 1800);

            BrokerAuthorizationStartDto start = await StartAsync(context);
            await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            BrokerConnectionStatusDto fresh = await context.Hikyaku.Send(new GetBrokerConnectionStatus(), CancellationToken.None);

            Assert.True(fresh.IsAuthorized);
            Assert.False(fresh.IsAccessTokenExpired);
            Assert.Null(fresh.CtidTraderAccountId);

            context.Clock.Advance(TimeSpan.FromMinutes(31));

            BrokerConnectionStatusDto expired = await context.Hikyaku.Send(new GetBrokerConnectionStatus(), CancellationToken.None);

            Assert.True(expired.IsAuthorized);
            Assert.True(expired.IsAccessTokenExpired);
        }

        [Fact]
        public async Task GetBrokerConnectionStatus_WhenTheBrokerIsNotConfigured_SaysSoWithoutFailing()
        {
            using TradingTestContext context = CreateUnconfiguredContext();

            BrokerConnectionStatusDto status = await context.Hikyaku.Send(new GetBrokerConnectionStatus(), CancellationToken.None);

            Assert.False(status.IsClientConfigured);
            Assert.False(status.IsAuthorized);
        }

        [Fact]
        public async Task Validations_AreReadOnly()
        {
            using TradingTestContext context = CreateContext();

            int journalCountBefore = context.Db.JournalEvents.Count();

            bool configured = await context.Hikyaku.Send(new ValidateBrokerClientConfiguration(), CancellationToken.None);
            bool callbackOk = await context.Hikyaku.Send(new ValidateBrokerAuthorizationCallback { Code = "code" }, CancellationToken.None);

            Assert.True(configured);
            Assert.True(callbackOk);
            Assert.Equal(journalCountBefore, context.Db.JournalEvents.Count());
            Assert.Empty(context.Db.BrokerAuthorizations);
            Assert.Empty(context.Db.BrokerAuthorizationAttempts);
        }

        [Fact]
        public async Task ValidateBrokerClientConfiguration_WhenNotConfigured_ReturnsFalse()
        {
            using TradingTestContext context = CreateUnconfiguredContext();

            bool configured = await context.Hikyaku.Send(new ValidateBrokerClientConfiguration(), CancellationToken.None);

            Assert.False(configured);
        }

        [Fact]
        public async Task ImportTokens_NormalizesThroughRefreshAndStoresTheRotatedPair()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.RefreshResult = FakeBrokerTokenClient.Success("rotated-access", "rotated-refresh", 2628000);

            BrokerAuthorizationResultDto imported = await context.Hikyaku.Send(
              new ImportBrokerTokens
              {
                  AccessToken = "playground-access",
                  RefreshToken = "playground-refresh",
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.Applied, imported.Outcome);
            Assert.Equal(new[] { "playground-refresh" }, context.TokenClient.RefreshedTokens.ToArray());

            var authorization = Assert.Single(context.Db.BrokerAuthorizations);

            // What is stored is the rotated pair, not the pasted one, and it is readable only through the key.
            Assert.Equal("rotated-access", context.TokenProtector.Unprotect(authorization.AccessTokenCipher));
            Assert.Equal("rotated-refresh", context.TokenProtector.Unprotect(authorization.RefreshTokenCipher));
            Assert.DoesNotContain("playground-access", authorization.AccessTokenCipher, StringComparison.Ordinal);
            Assert.Equal(context.Clock.GetUtcNow().UtcDateTime.AddSeconds(2628000), authorization.AccessTokenExpiresAtUtc);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationImported);
        }

        [Fact]
        public async Task ImportTokens_WhenTheAffordanceIsDisabled_ReportsNotConfiguredWithoutCallingTheProvider()
        {
            using TradingTestContext context = CreateContextWithoutTokenImport();

            BrokerAuthorizationResultDto imported = await context.Hikyaku.Send(
              new ImportBrokerTokens
              {
                  AccessToken = "playground-access",
                  RefreshToken = "playground-refresh",
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.NotConfigured, imported.Outcome);
            Assert.Empty(context.TokenClient.RefreshedTokens);
            Assert.Empty(context.Db.BrokerAuthorizations);
        }

        [Fact]
        public async Task ImportTokens_WhenTheProviderRejectsThePair_StoresNothing()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.RefreshResult = FakeBrokerTokenClient.Failure("invalid_grant");

            BrokerAuthorizationResultDto imported = await context.Hikyaku.Send(
              new ImportBrokerTokens
              {
                  AccessToken = "playground-access",
                  RefreshToken = "playground-refresh",
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.ProviderRejected, imported.Outcome);
            Assert.Equal("invalid_grant", imported.Detail);
            Assert.Empty(context.Db.BrokerAuthorizations);
            Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.BrokerAuthorizationRejected);
        }

        [Theory]
        [InlineData(null, "refresh")]
        [InlineData("access", null)]
        [InlineData("", "")]
        [InlineData("   ", "refresh")]
        public async Task ImportTokens_WithMissingTokens_ReturnsInvalidRequestWithoutCallingTheProvider(string accessToken, string refreshToken)
        {
            using TradingTestContext context = CreateContext();

            BrokerAuthorizationResultDto imported = await context.Hikyaku.Send(
              new ImportBrokerTokens
              {
                  AccessToken = accessToken,
                  RefreshToken = refreshToken,
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            Assert.Equal(BrokerAuthorizationOutcome.InvalidRequest, imported.Outcome);
            Assert.Empty(context.TokenClient.RefreshedTokens);
            Assert.Empty(context.Db.BrokerAuthorizations);
        }

        [Fact]
        public async Task ImportTokens_ReplacesAPreviousGrantInsteadOfCreatingASecondRow()
        {
            using TradingTestContext context = CreateContext();
            context.TokenClient.ExchangeResult = FakeBrokerTokenClient.Success("first-access", "first-refresh", 2628000);
            context.TokenClient.RefreshResult = FakeBrokerTokenClient.Success("second-access", "second-refresh", 2628000);

            BrokerAuthorizationStartDto start = await StartAsync(context);
            await context.Hikyaku.Send(
              new CompleteBrokerAuthorization { Code = "auth-code", CorrelationId = start.CorrelationId },
              CancellationToken.None);

            await context.Hikyaku.Send(
              new ImportBrokerTokens
              {
                  AccessToken = "playground-access",
                  RefreshToken = "playground-refresh",
                  OperatorId = Guid.CreateVersion7()
              },
              CancellationToken.None);

            // One grant per environment: the import replaces the consent result.
            var authorization = Assert.Single(context.Db.BrokerAuthorizations);
            Assert.Equal("second-access", context.TokenProtector.Unprotect(authorization.AccessTokenCipher));
        }

        [Fact]
        public async Task ValidateBrokerTokenImport_IsReadOnlyAndRejectsBlanks()
        {
            using TradingTestContext context = CreateContext();

            bool complete = await context.Hikyaku.Send(
              new ValidateBrokerTokenImport { AccessToken = "access", RefreshToken = "refresh" },
              CancellationToken.None);

            bool blank = await context.Hikyaku.Send(
              new ValidateBrokerTokenImport { AccessToken = "  ", RefreshToken = "refresh" },
              CancellationToken.None);

            Assert.True(complete);
            Assert.False(blank);
            Assert.Empty(context.Db.BrokerAuthorizations);
            Assert.Empty(context.Db.BrokerAuthorizationAttempts);
        }
    }
}
