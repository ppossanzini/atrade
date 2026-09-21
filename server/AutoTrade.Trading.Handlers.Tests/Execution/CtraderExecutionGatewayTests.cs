using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.Execution;
using AutoTrade.Trading.Handlers.Model;
using AutoTrade.Trading.Handlers.Tests.Broker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Execution
{
    public class CtraderExecutionGatewayTests
    {
        [Fact]
        public async Task SendAndQuery_UsesDeterministicClientOrderIdAndMapsBrokerState()
        {
            DateTime now = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
            DbContextOptions<DB> dbOptions = new DbContextOptionsBuilder<DB>()
              .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
              .Options;

            using DB db = new DB(dbOptions);
            db.BrokerAuthorizations.Add(new BrokerAuthorization
            {
                Id = Guid.CreateVersion7(),
                Environment = TradingEnvironment.Demo,
                CtidTraderAccountId = 42,
                AccessTokenCipher = "access",
                RefreshTokenCipher = "refresh",
                AccessTokenExpiresAtUtc = now.AddHours(1),
                AuthorizedAtUtc = now,
                UpdatedAtUtc = now,
                AuthorizedByOperatorId = Guid.CreateVersion7()
            });
            await db.SaveChangesAsync();

            FakeCtraderClient client = new FakeCtraderClient();
            CtraderExecutionGateway gateway = new CtraderExecutionGateway(
              db,
              new BrokerOptions { Environment = TradingEnvironment.Demo, Scope = "accounts trading", ClientId = "client", ClientSecret = "secret" },
              new ExecutionOptions { Provider = ExecutionProviderKind.Ctrader, Ctrader = new CtraderExecutionOptions() },
              new PlaintextTokenProtector(),
              new FakeBrokerTokenClient(),
              new FakeCtraderClientFactory(client),
              new FixedTimeProvider(now),
              NullLogger<CtraderExecutionGateway>.Instance);

            OrderRequest request = new OrderRequest
            {
                ClientOrderId = "exec-01-leg-00",
                Symbol = "EURUSD",
                Market = MarketKind.Fx,
                Direction = LegDirection.Long,
                VolumeUnits = 1000
            };

            OrderDispatchResult dispatch = await gateway.SendAsync(request, CancellationToken.None);
            OrderQueryResult query = await gateway.QueryAsync(request.ClientOrderId, request.Symbol, CancellationToken.None);

            Assert.Equal(OrderDispatchOutcome.Filled, dispatch.Outcome);
            Assert.Equal("9001", dispatch.BrokerOrderId);
            Assert.Equal(ExecutionEventKind.OrderFilled, Assert.Single(dispatch.Events).Kind);
            Assert.Equal(request.ClientOrderId, client.LastClientOrderId);
            Assert.Equal(OrderQueryOutcome.Filled, query.Outcome);
            Assert.Equal(1000, query.FilledVolumeUnits);
            Assert.Equal("9001", query.BrokerOrderId);
        }

        [Fact]
        public void EnsureProviderIsUsable_RequiresExplicitLivePromotionAndTradingScope()
        {
            ExecutionOptions options = new ExecutionOptions
            {
                Provider = ExecutionProviderKind.Ctrader,
                Ctrader = new CtraderExecutionOptions()
            };
            BrokerOptions live = new BrokerOptions { Environment = TradingEnvironment.Live, Scope = BrokerOptions.TradingScope };
            BrokerOptions demoWithoutTradingScope = new BrokerOptions { Environment = TradingEnvironment.Demo, Scope = BrokerOptions.AccountsScope };

            Assert.Throws<InvalidOperationException>(() => ExecutionModule.EnsureProviderIsUsable(options, live));
            Assert.Throws<InvalidOperationException>(() => ExecutionModule.EnsureProviderIsUsable(options, demoWithoutTradingScope));

            options.Ctrader.AllowLive = true;
            ExecutionModule.EnsureProviderIsUsable(options, live);
        }

        [Fact]
        public void EnsureReconciliationIsUsable_RequiresAnEnabledPositiveScheduleForCtrader()
        {
            ExecutionOptions options = new ExecutionOptions
            {
                Provider = ExecutionProviderKind.Ctrader,
                Ctrader = new CtraderExecutionOptions()
            };

            Assert.Throws<InvalidOperationException>(() => ExecutionModule.EnsureReconciliationIsUsable(options, null));
            Assert.Throws<InvalidOperationException>(() => ExecutionModule.EnsureReconciliationIsUsable(
              options,
              new ExecutionReconciliationOptions { IsEnabled = false, IntervalSeconds = 30 }));
            Assert.Throws<InvalidOperationException>(() => ExecutionModule.EnsureReconciliationIsUsable(
              options,
              new ExecutionReconciliationOptions { IsEnabled = true, IntervalSeconds = 0 }));

            ExecutionModule.EnsureReconciliationIsUsable(
              options,
              new ExecutionReconciliationOptions { IsEnabled = true, IntervalSeconds = 30 });
        }

        private sealed class PlaintextTokenProtector : IBrokerTokenProtector
        {
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
                plaintext = ciphertext;
                return !string.IsNullOrWhiteSpace(ciphertext);
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

        private sealed class FakeCtraderClientFactory(FakeCtraderClient client) : ICtraderProtocolClientFactory
        {
            public ICtraderProtocolClient Create()
            {
                return client;
            }
        }

        private sealed class FakeCtraderClient : ICtraderProtocolClient
        {
            public string LastClientOrderId { get; private set; }

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
                return Task.FromResult(new CtraderAccountAuthResult { IsAuthenticated = true, CtidTraderAccountId = ctidTraderAccountId });
            }

            public Task<CtraderTraderResult> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderTraderResult { IsSuccess = true, Trader = new ProtoOATrader() });
            }

            public Task<CtraderAssetsResult> GetAssetsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new CtraderAssetsResult { IsSuccess = true });
            }

            public Task<CtraderSymbolsResult> GetSymbolsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
            {
                CtraderSymbolsResult result = new CtraderSymbolsResult { IsSuccess = true };
                result.Symbols.Add(new ProtoOALightSymbol { SymbolId = 99, SymbolName = "EURUSD", Enabled = true });

                return Task.FromResult(result);
            }

            public Task<CtraderSymbolDetailsResult> GetSymbolDetailsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
            {
                CtraderSymbolDetailsResult result = new CtraderSymbolDetailsResult { IsSuccess = true };
                result.Symbols.Add(new ProtoOASymbol { SymbolId = 99, TradingMode = ProtoOATradingMode.Enabled });

                return Task.FromResult(result);
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
                LastClientOrderId = clientOrderId;
                ProtoOAOrder order = Order(clientOrderId);
                ProtoOAExecutionEvent execution = new ProtoOAExecutionEvent
                {
                    CtidTraderAccountId = ctidTraderAccountId,
                    ExecutionType = ProtoOAExecutionType.OrderFilled,
                    Order = order,
                    Deal = new ProtoOADeal { DealId = 7001, OrderId = order.OrderId, FilledVolume = volume, ExecutionPrice = 1.1 }
                };

                return Task.FromResult(new CtraderOrderPlacementResult { IsSuccess = true, ExecutionEvent = execution });
            }

            public Task<CtraderOrderDetailsResult> GetOrderDetailsAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken)
            {
                CtraderOrderDetailsResult result = new CtraderOrderDetailsResult { IsSuccess = true, Order = Order("exec-01-leg-00") };
                result.Deals.Add(new ProtoOADeal { DealId = 7001, OrderId = orderId, FilledVolume = 1000, ExecutionPrice = 1.1, ExecutionTimestamp = 1 });

                return Task.FromResult(result);
            }

            public Task<CtraderOrderListResult> GetOrdersAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            {
                CtraderOrderListResult result = new CtraderOrderListResult { IsSuccess = true };
                result.Orders.Add(Order("exec-01-leg-00"));

                return Task.FromResult(result);
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            private static ProtoOAOrder Order(string clientOrderId)
            {
                return new ProtoOAOrder
                {
                    OrderId = 9001,
                    TradeData = new ProtoOATradeData { SymbolId = 99, Volume = 1000, TradeSide = ProtoOATradeSide.Buy },
                    OrderType = ProtoOAOrderType.Market,
                    OrderStatus = ProtoOAOrderStatus.OrderStatusFilled,
                    ExecutedVolume = 1000,
                    ClientOrderId = clientOrderId,
                    ExecutionPrice = 1.1
                };
            }
        }
    }
}
