using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.MarketData;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
    public class CtraderMarketDataSourceTests
    {
        [Fact]
        public async Task Capture_MapsProviderScalesAndLeavesUnsupportedVolatilityAbsent()
        {
            CtraderSnapshotReadResult snapshot = new CtraderSnapshotReadResult
            {
                IsSuccess = true,
                CapturedAtUtc = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc),
                Environment = TradingEnvironment.Demo,
                CtidTraderAccountId = 123,
                Trader = new ProtoOATrader
                {
                    CtidTraderAccountId = 123,
                    Balance = 100000,
                    DepositAssetId = 1,
                    MoneyDigits = 2
                },
                MoneyDigits = 2
            };
            snapshot.Assets.Add(new ProtoOAAsset { AssetId = 1, Name = "USD" });
            snapshot.Symbols.Add(new ProtoOALightSymbol { SymbolId = 9, SymbolName = "EURUSD", Enabled = true });
            snapshot.SymbolDetails.Add(new ProtoOASymbol
            {
                SymbolId = 9,
                Digits = 5,
                PipPosition = 4,
                TradingMode = ProtoOATradingMode.Enabled,
                MinVolume = 1000,
                StepVolume = 1000,
                LotSize = 100000
            });
            snapshot.Spots.Add(new ProtoOASpotEvent
            {
                CtidTraderAccountId = 123,
                SymbolId = 9,
                Bid = 110000,
                Ask = 110020
            });

            CtraderMarketDataSource source = new CtraderMarketDataSource(new StubSnapshotReader(snapshot));
            MarketDataCapture capture = await source.CaptureAsync(
              new List<SymbolRequest> { new SymbolRequest { Symbol = "EURUSD", Market = MarketKind.Fx } },
              CancellationToken.None);

            Assert.True(capture.IsAvailable);
            Assert.Equal(1000, capture.Account.Balance);
            Assert.Equal("USD", capture.Account.Currency);
            Assert.InRange(capture.Symbols[0].SpreadPips.Value, 1.999, 2.001);
            Assert.NotNull(capture.Symbols[0].Price);
            Assert.InRange(capture.Symbols[0].Price.Value, 1.10009, 1.10011);
            Assert.Null(capture.Symbols[0].VolatilityPercent);
            Assert.True(capture.Symbols[0].IsTradable);
        }

        [Fact]
        public async Task Capture_WhenReaderIsUnavailable_ReturnsFailClosedCapture()
        {
            CtraderMarketDataSource source = new CtraderMarketDataSource(new StubSnapshotReader(new CtraderSnapshotReadResult
            {
                IsSuccess = false,
                ErrorCode = "BROKER_OFFLINE"
            }));

            MarketDataCapture capture = await source.CaptureAsync(Array.Empty<SymbolRequest>(), CancellationToken.None);

            Assert.False(capture.IsAvailable);
            Assert.Null(capture.Account);
            Assert.Empty(capture.Symbols);
        }

        private sealed class StubSnapshotReader(CtraderSnapshotReadResult snapshot) : ICtraderSnapshotReader
        {
            public Task<CtraderSnapshotReadResult> ReadAsync(IReadOnlyList<SymbolRequest> requestedSymbols, CancellationToken cancellationToken)
            {
                return Task.FromResult(snapshot);
            }
        }
    }
}
