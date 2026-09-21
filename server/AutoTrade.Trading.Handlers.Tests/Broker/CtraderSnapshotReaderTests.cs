using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
    public class CtraderSnapshotReaderTests
    {
        [Fact]
        public async Task Read_WhenAuthorizationWasRotatedWhileWaitingOnRefreshGate_UsesReloadedToken()
        {
            string databaseName = Guid.CreateVersion7().ToString();
            DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
              .UseInMemoryDatabase(databaseName)
              .Options;
            DateTime now = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
            Guid authorizationId = Guid.CreateVersion7();

            using (DB writer = new DB(options))
            {
                writer.BrokerAuthorizations.Add(new BrokerAuthorization
                {
                    Id = authorizationId,
                    Environment = TradingEnvironment.Demo,
                    CtidTraderAccountId = 42,
                    AccessTokenCipher = "old-access",
                    RefreshTokenCipher = "old-refresh",
                    AccessTokenExpiresAtUtc = now.AddMinutes(-1),
                    AuthorizedAtUtc = now.AddDays(-1),
                    AuthorizedByOperatorId = Guid.CreateVersion7(),
                    UpdatedAtUtc = now.AddDays(-1)
                });
                await writer.SaveChangesAsync();
            }

            using DB readerDb = new DB(options);
            RotatingTokenProtector protector = new RotatingTokenProtector(options, now);
            BrokerOptions brokerOptions = new BrokerOptions { Environment = TradingEnvironment.Demo };
            FakeBrokerTokenClient tokenClient = new FakeBrokerTokenClient
            {
                RefreshResult = FakeBrokerTokenClient.Failure("refresh should not be called after reload")
            };
            CtraderSnapshotReader reader = new CtraderSnapshotReader(
              readerDb,
              brokerOptions,
              protector,
              tokenClient,
              new StubProtocolFactory(),
              new FixedTimeProvider(now),
              NullLogger<CtraderSnapshotReader>.Instance);

            CtraderSnapshotReadResult result = await reader.ReadAsync(Array.Empty<SymbolRequest>(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(tokenClient.RefreshedTokens);
            Assert.Equal("new-access", protector.LastPlaintext);
        }

        private sealed class RotatingTokenProtector(DbContextOptions<DB> options, DateTime now) : IBrokerTokenProtector
        {
            private bool rotated;

            public string LastPlaintext { get; private set; }

            public string Protect(string plaintext)
            {
                return plaintext;
            }

            public string Unprotect(string ciphertext)
            {
                return ciphertext;
            }

            public bool TryUnprotect(string ciphertext, out string plaintext)
            {
                if (!rotated && ciphertext == "old-access")
                {
                    using DB writer = new DB(options);
                    BrokerAuthorization authorization = writer.BrokerAuthorizations.Single();
                    authorization.AccessTokenCipher = "new-access";
                    authorization.RefreshTokenCipher = "new-refresh";
                    authorization.AccessTokenExpiresAtUtc = now.AddHours(1);
                    authorization.UpdatedAtUtc = now;
                    writer.SaveChanges();
                    rotated = true;
                }

                plaintext = ciphertext switch
                {
                    "new-access" => "new-access",
                    "old-access" => "old-access",
                    "new-refresh" => "new-refresh",
                    "old-refresh" => "old-refresh",
                    _ => null
                };
                LastPlaintext = plaintext;

                return plaintext != null;
            }
        }

        private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
        {
            private readonly DateTimeOffset value = new DateTimeOffset(utcNow, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow()
            {
                return value;
            }
        }

        private sealed class StubProtocolFactory : ICtraderProtocolClientFactory
        {
            public ICtraderProtocolClient Create()
            {
                return new StubProtocolClient();
            }
        }

        private sealed class StubProtocolClient : ICtraderProtocolClient
        {
            public Task<CtraderHandshakeResult> AuthenticateApplicationAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderHandshakeResult { IsAuthenticated = true });
            }

            public Task<CtraderAccountListResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderAccountListResult { IsSuccess = true });
            }

            public Task<CtraderAccountAuthResult> AuthenticateAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderAccountAuthResult { IsAuthenticated = true });
            }

            public Task<CtraderTraderResult> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderTraderResult { IsSuccess = true, Trader = new ProtoOATrader { CtidTraderAccountId = ctidTraderAccountId } });
            }

            public Task<CtraderAssetsResult> GetAssetsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderAssetsResult { IsSuccess = true });
            }

            public Task<CtraderSymbolsResult> GetSymbolsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderSymbolsResult { IsSuccess = true });
            }

            public Task<CtraderSymbolDetailsResult> GetSymbolDetailsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderSymbolDetailsResult { IsSuccess = true });
            }

            public Task<CtraderReconcileResult> ReconcileAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderReconcileResult { IsSuccess = true });
            }

            public Task<CtraderUnrealizedPnlResult> GetUnrealizedPnlAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderUnrealizedPnlResult { IsSuccess = true });
            }

            public Task<CtraderDealsResult> GetDealsAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderDealsResult { IsSuccess = true });
            }

            public Task<CtraderSpotResult> SubscribeSpotsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderSpotResult { IsSuccess = true });
            }

            public Task<CtraderTrendbarsResult> GetTrendbarsAsync(long ctidTraderAccountId, long symbolId, DateTime fromUtc, DateTime toUtc, ProtoOATrendbarPeriod period, uint count, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderTrendbarsResult { IsSuccess = true });
            }

            public Task<CtraderOrderPlacementResult> PlaceMarketOrderAsync(long ctidTraderAccountId, long symbolId, ProtoOATradeSide tradeSide, long volume, string clientOrderId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderOrderPlacementResult());
            }

            public Task<CtraderOrderDetailsResult> GetOrderDetailsAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderOrderDetailsResult { IsSuccess = true });
            }

            public Task<CtraderOrderListResult> GetOrdersAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderOrderListResult { IsSuccess = true });
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
