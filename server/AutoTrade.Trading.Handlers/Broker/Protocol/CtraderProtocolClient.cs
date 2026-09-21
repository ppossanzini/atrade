using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Broker.Protocol
{
    /// <summary>Outcome of the application level authentication.</summary>
    public class CtraderHandshakeResult
    {
        public bool IsAuthenticated { get; set; }

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    /// <summary>Outcome of the account list request.</summary>
    public class CtraderAccountListResult
    {
        public bool IsSuccess { get; set; }

        public List<long> CtidTraderAccountIds { get; } = new List<long>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderAccountAuthResult
    {
        public bool IsAuthenticated { get; set; }

        public long CtidTraderAccountId { get; set; }

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderTraderResult
    {
        public bool IsSuccess { get; set; }

        public ProtoOATrader Trader { get; set; }

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderAssetsResult
    {
        public bool IsSuccess { get; set; }

        public List<ProtoOAAsset> Assets { get; } = new List<ProtoOAAsset>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderSymbolsResult
    {
        public bool IsSuccess { get; set; }

        public List<ProtoOALightSymbol> Symbols { get; } = new List<ProtoOALightSymbol>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderSymbolDetailsResult
    {
        public bool IsSuccess { get; set; }

        public List<ProtoOASymbol> Symbols { get; } = new List<ProtoOASymbol>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderReconcileResult
    {
        public bool IsSuccess { get; set; }

        public List<ProtoOAPosition> Positions { get; } = new List<ProtoOAPosition>();

        public List<ProtoOAOrder> Orders { get; } = new List<ProtoOAOrder>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderUnrealizedPnlResult
    {
        public bool IsSuccess { get; set; }

        public uint MoneyDigits { get; set; }

        public List<ProtoOAPositionUnrealizedPnL> Positions { get; } = new List<ProtoOAPositionUnrealizedPnL>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderDealsResult
    {
        public bool IsSuccess { get; set; }

        public bool HasMore { get; set; }

        public List<ProtoOADeal> Deals { get; } = new List<ProtoOADeal>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderSpotResult
    {
        public bool IsSuccess { get; set; }

        public List<ProtoOASpotEvent> Spots { get; } = new List<ProtoOASpotEvent>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderTrendbarsResult
    {
        public bool IsSuccess { get; set; }

        public bool HasMore { get; set; }

        public List<ProtoOATrendbar> Trendbars { get; } = new List<ProtoOATrendbar>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderOrderPlacementResult
    {
        public bool IsSuccess { get; set; }

        public ProtoOAExecutionEvent ExecutionEvent { get; set; }

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderOrderDetailsResult
    {
        public bool IsSuccess { get; set; }

        public ProtoOAOrder Order { get; set; }

        public List<ProtoOADeal> Deals { get; } = new List<ProtoOADeal>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public class CtraderOrderListResult
    {
        public bool IsSuccess { get; set; }

        public bool HasMore { get; set; }

        public List<ProtoOAOrder> Orders { get; } = new List<ProtoOAOrder>();

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public interface ICtraderProtocolClient : IAsyncDisposable
    {
        Task<CtraderHandshakeResult> AuthenticateApplicationAsync(CancellationToken cancellationToken);

        Task<CtraderAccountListResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken);

        Task<CtraderAccountAuthResult> AuthenticateAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken cancellationToken);

        Task<CtraderTraderResult> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken);

        Task<CtraderAssetsResult> GetAssetsAsync(long ctidTraderAccountId, CancellationToken cancellationToken);

        Task<CtraderSymbolsResult> GetSymbolsAsync(long ctidTraderAccountId, CancellationToken cancellationToken);

        Task<CtraderSymbolDetailsResult> GetSymbolDetailsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken);

        Task<CtraderReconcileResult> ReconcileAsync(long ctidTraderAccountId, CancellationToken cancellationToken);

        Task<CtraderUnrealizedPnlResult> GetUnrealizedPnlAsync(long ctidTraderAccountId, CancellationToken cancellationToken);

        Task<CtraderDealsResult> GetDealsAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

        Task<CtraderSpotResult> SubscribeSpotsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken);

        Task<CtraderTrendbarsResult> GetTrendbarsAsync(long ctidTraderAccountId, long symbolId, DateTime fromUtc, DateTime toUtc, ProtoOATrendbarPeriod period, uint count, CancellationToken cancellationToken);

        Task<CtraderOrderPlacementResult> PlaceMarketOrderAsync(long ctidTraderAccountId, long symbolId, ProtoOATradeSide tradeSide, long volume, string clientOrderId, CancellationToken cancellationToken);

        Task<CtraderOrderDetailsResult> GetOrderDetailsAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken);

        Task<CtraderOrderListResult> GetOrdersAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Creates one connection per use. Connections are long lived by nature, but nothing is shared between
    /// two callers, so a failed connection cannot corrupt an unrelated request.
    /// </summary>
    public interface ICtraderProtocolClientFactory
    {
        ICtraderProtocolClient Create();
    }

    public sealed class CtraderProtocolClientFactory(BrokerOptions options, ILoggerFactory loggerFactory) : ICtraderProtocolClientFactory
    {
        public ICtraderProtocolClient Create()
        {
            return new CtraderProtocolClient(options, loggerFactory.CreateLogger<CtraderProtocolClient>());
        }
    }

    /// <summary>
    /// WebSocket transport for the cTrader Open API Protobuf protocol.
    ///
    /// Framing follows the official documentation: over WebSocket one binary frame carries one
    /// <c>ProtoMessage</c>, so no length prefix is involved (that is required only by the TCP transport).
    /// Requests are correlated by the envelope <c>clientMsgId</c>, and the connection is kept alive with a
    /// heartbeat because the server drops connections idle for more than 10 seconds.
    /// </summary>
    public sealed class CtraderProtocolClient : ICtraderProtocolClient
    {
        private readonly BrokerOptions options;
        private readonly ILogger<CtraderProtocolClient> logger;
        private readonly CtraderConnectionManager connection;
        private readonly ConcurrentQueue<ProtoMessage> events = new ConcurrentQueue<ProtoMessage>();
        private readonly SemaphoreSlim eventSignal = new SemaphoreSlim(0);
        private string accessToken;
        private long? authenticatedAccountId;

        public CtraderProtocolClient(BrokerOptions options, ILogger<CtraderProtocolClient> logger)
        {
            this.options = options;
            this.logger = logger;
            connection = new CtraderConnectionManager(options, logger);
            connection.MessageReceived += OnMessageReceived;
        }

        public async Task<CtraderHandshakeResult> AuthenticateApplicationAsync(CancellationToken cancellationToken)
        {
            if (!await connection.EnsureConnectedAsync(CtraderRequestKind.Authentication, cancellationToken))
            {
                return new CtraderHandshakeResult
                {
                    Description = "The broker WebSocket could not be opened: " + connection.Health.LastError
                };
            }

            ProtoOAApplicationAuthReq request = new ProtoOAApplicationAuthReq
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.ApplicationAuthRequest, request, CtraderRequestKind.Authentication, cancellationToken);

            CtraderHandshakeResult result = new CtraderHandshakeResult();

            if (response == null)
            {
                result.Description = "The broker did not answer the application authentication request.";

                return result;
            }

            if (response.PayloadType == CtraderPayloadTypes.ErrorResponse)
            {
                ProtoErrorRes error = ProtoErrorRes.Parser.ParseFrom(response.Payload);
                result.ErrorCode = error.ErrorCode;
                result.Description = error.Description;

                return result;
            }

            if (response.PayloadType == CtraderPayloadTypes.OpenApiErrorResponse)
            {
                ProtoOAErrorRes error = ProtoOAErrorRes.Parser.ParseFrom(response.Payload);
                result.ErrorCode = error.ErrorCode;
                result.Description = error.Description;

                return result;
            }

            if (response.PayloadType != CtraderPayloadTypes.ApplicationAuthResponse)
            {
                result.Description = "Unexpected response to the application authentication request: " + response.PayloadType + ".";

                return result;
            }

            result.IsAuthenticated = true;
            connection.SetReauthentication(ReauthenticateCurrentSessionAsync);
            connection.MarkAuthenticated();

            return result;
        }

        public async Task<CtraderAccountListResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken)
        {
            ProtoOAGetAccountListByAccessTokenReq request = new ProtoOAGetAccountListByAccessTokenReq
            {
                AccessToken = accessToken
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.AccountsByAccessTokenRequest, request, CtraderRequestKind.ReadOnly, cancellationToken);

            CtraderAccountListResult result = new CtraderAccountListResult();

            if (response == null)
            {
                result.Description = "The broker did not answer the account list request.";

                return result;
            }

            if (response.PayloadType == CtraderPayloadTypes.ErrorResponse)
            {
                ProtoErrorRes error = ProtoErrorRes.Parser.ParseFrom(response.Payload);
                result.ErrorCode = error.ErrorCode;
                result.Description = error.Description;

                return result;
            }

            if (response.PayloadType == CtraderPayloadTypes.OpenApiErrorResponse)
            {
                ProtoOAErrorRes error = ProtoOAErrorRes.Parser.ParseFrom(response.Payload);
                result.ErrorCode = error.ErrorCode;
                result.Description = error.Description;

                return result;
            }

            if (response.PayloadType != CtraderPayloadTypes.AccountsByAccessTokenResponse)
            {
                result.Description = "Unexpected response to the account list request: " + response.PayloadType + ".";

                return result;
            }

            ProtoOAGetAccountListByAccessTokenRes accounts = ProtoOAGetAccountListByAccessTokenRes.Parser.ParseFrom(response.Payload);

            foreach (ProtoOACtidTraderAccount account in accounts.CtidTraderAccount)
            {
                // The account list exposes the identifier as uint64 while ProtoOAAccountAuthReq expects int64, so
                // an out of range value would be unusable anyway: fail loudly instead of storing a wrapped id.
                result.CtidTraderAccountIds.Add(checked((long)account.CtidTraderAccountId));
            }

            result.IsSuccess = true;
            return result;
        }

        public async Task<CtraderAccountAuthResult> AuthenticateAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken cancellationToken)
        {
            ProtoOAAccountAuthReq request = new ProtoOAAccountAuthReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                AccessToken = accessToken
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.AccountAuthRequest, request, CtraderRequestKind.Authentication, cancellationToken);
            CtraderAccountAuthResult result = new CtraderAccountAuthResult { CtidTraderAccountId = ctidTraderAccountId };

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.AccountAuthResponse)
            {
                result.Description = UnexpectedResponse("account authentication", response);

                return result;
            }

            ProtoOAAccountAuthRes authenticated = ProtoOAAccountAuthRes.Parser.ParseFrom(response.Payload);
            result.CtidTraderAccountId = authenticated.CtidTraderAccountId;
            result.IsAuthenticated = true;
            this.accessToken = accessToken;
            authenticatedAccountId = authenticated.CtidTraderAccountId;
            connection.MarkAuthenticated();

            return result;
        }

        public async Task<CtraderTraderResult> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
        {
            ProtoOATraderReq request = new ProtoOATraderReq { CtidTraderAccountId = ctidTraderAccountId };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.TraderRequest, request, cancellationToken);
            CtraderTraderResult result = new CtraderTraderResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.TraderResponse)
            {
                result.Description = UnexpectedResponse("trader snapshot", response);

                return result;
            }

            result.Trader = ProtoOATraderRes.Parser.ParseFrom(response.Payload).Trader;
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderAssetsResult> GetAssetsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
        {
            ProtoOAAssetListReq request = new ProtoOAAssetListReq { CtidTraderAccountId = ctidTraderAccountId };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.AssetListRequest, request, cancellationToken);
            CtraderAssetsResult result = new CtraderAssetsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.AssetListResponse)
            {
                result.Description = UnexpectedResponse("asset list", response);

                return result;
            }

            result.Assets.AddRange(ProtoOAAssetListRes.Parser.ParseFrom(response.Payload).Asset);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderSymbolsResult> GetSymbolsAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
        {
            ProtoOASymbolsListReq request = new ProtoOASymbolsListReq { CtidTraderAccountId = ctidTraderAccountId };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.SymbolsListRequest, request, cancellationToken);
            CtraderSymbolsResult result = new CtraderSymbolsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.SymbolsListResponse)
            {
                result.Description = UnexpectedResponse("symbol list", response);

                return result;
            }

            result.Symbols.AddRange(ProtoOASymbolsListRes.Parser.ParseFrom(response.Payload).Symbol);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderSymbolDetailsResult> GetSymbolDetailsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
        {
            ProtoOASymbolByIdReq request = new ProtoOASymbolByIdReq { CtidTraderAccountId = ctidTraderAccountId };

            if (symbolIds != null)
            {
                request.SymbolId.Add(symbolIds);
            }

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.SymbolByIdRequest, request, cancellationToken);
            CtraderSymbolDetailsResult result = new CtraderSymbolDetailsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.SymbolByIdResponse)
            {
                result.Description = UnexpectedResponse("symbol details", response);

                return result;
            }

            result.Symbols.AddRange(ProtoOASymbolByIdRes.Parser.ParseFrom(response.Payload).Symbol);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderReconcileResult> ReconcileAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
        {
            ProtoOAReconcileReq request = new ProtoOAReconcileReq { CtidTraderAccountId = ctidTraderAccountId };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.ReconcileRequest, request, cancellationToken);
            CtraderReconcileResult result = new CtraderReconcileResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.ReconcileResponse)
            {
                result.Description = UnexpectedResponse("account reconciliation", response);

                return result;
            }

            ProtoOAReconcileRes reconciliation = ProtoOAReconcileRes.Parser.ParseFrom(response.Payload);
            result.Positions.AddRange(reconciliation.Position);
            result.Orders.AddRange(reconciliation.Order);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderUnrealizedPnlResult> GetUnrealizedPnlAsync(long ctidTraderAccountId, CancellationToken cancellationToken)
        {
            ProtoOAGetPositionUnrealizedPnLReq request = new ProtoOAGetPositionUnrealizedPnLReq { CtidTraderAccountId = ctidTraderAccountId };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.PositionUnrealizedPnlRequest, request, cancellationToken);
            CtraderUnrealizedPnlResult result = new CtraderUnrealizedPnlResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.PositionUnrealizedPnlResponse)
            {
                result.Description = UnexpectedResponse("unrealized PnL", response);

                return result;
            }

            ProtoOAGetPositionUnrealizedPnLRes pnl = ProtoOAGetPositionUnrealizedPnLRes.Parser.ParseFrom(response.Payload);
            result.MoneyDigits = pnl.MoneyDigits;
            result.Positions.AddRange(pnl.PositionUnrealizedPnL);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderDealsResult> GetDealsAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            ProtoOADealListReq request = new ProtoOADealListReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                FromTimestamp = new DateTimeOffset(fromUtc.ToUniversalTime()).ToUnixTimeMilliseconds(),
                ToTimestamp = new DateTimeOffset(toUtc.ToUniversalTime()).ToUnixTimeMilliseconds(),
                MaxRows = 1000
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.DealListRequest, request, cancellationToken);
            CtraderDealsResult result = new CtraderDealsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.DealListResponse)
            {
                result.Description = UnexpectedResponse("deal list", response);

                return result;
            }

            ProtoOADealListRes deals = ProtoOADealListRes.Parser.ParseFrom(response.Payload);
            result.Deals.AddRange(deals.Deal);
            result.HasMore = deals.HasMore;
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderSpotResult> SubscribeSpotsAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
        {
            ProtoOASubscribeSpotsReq request = new ProtoOASubscribeSpotsReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SubscribeToSpotTimestamp = true
            };

            if (symbolIds != null)
            {
                request.SymbolId.Add(symbolIds);
            }

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.SubscribeSpotsRequest, request, cancellationToken);
            CtraderSpotResult result = new CtraderSpotResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.SubscribeSpotsResponse)
            {
                result.Description = UnexpectedResponse("spot subscription", response);

                return result;
            }

            HashSet<long> remaining = new HashSet<long>(symbolIds ?? Array.Empty<long>());
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);

            while (remaining.Count > 0 && DateTime.UtcNow < deadline)
            {
                while (events.TryDequeue(out ProtoMessage envelope))
                {
                    if (envelope.PayloadType != CtraderPayloadTypes.SpotEvent)
                    {
                        continue;
                    }

                    ProtoOASpotEvent spot = ProtoOASpotEvent.Parser.ParseFrom(envelope.Payload);
                    if (spot.CtidTraderAccountId == ctidTraderAccountId && remaining.Remove(spot.SymbolId))
                    {
                        result.Spots.Add(spot);
                    }
                }

                if (remaining.Count == 0)
                {
                    break;
                }

                TimeSpan wait = deadline - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await eventSignal.WaitAsync(wait, cancellationToken);
                }
            }

            if (remaining.Count > 0)
            {
                result.ErrorCode = "BROKER_SPOT_QUOTE_INCOMPLETE";
                result.Description = "The broker did not provide initial quotes for symbol ids: "
                  + string.Join(",", remaining);

                return result;
            }

            result.IsSuccess = true;
            List<long> restoredSymbolIds = new List<long>(symbolIds ?? Array.Empty<long>());
            if (restoredSymbolIds.Count > 0)
            {
                connection.RegisterSubscription(
                  "spots:" + ctidTraderAccountId,
                  cancellation => RestoreSpotSubscriptionAsync(ctidTraderAccountId, restoredSymbolIds, cancellation));
            }

            return result;
        }

        public async Task<CtraderTrendbarsResult> GetTrendbarsAsync(long ctidTraderAccountId, long symbolId, DateTime fromUtc, DateTime toUtc, ProtoOATrendbarPeriod period, uint count, CancellationToken cancellationToken)
        {
            ProtoOAGetTrendbarsReq request = new ProtoOAGetTrendbarsReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SymbolId = symbolId,
                FromTimestamp = new DateTimeOffset(fromUtc.ToUniversalTime()).ToUnixTimeMilliseconds(),
                ToTimestamp = new DateTimeOffset(toUtc.ToUniversalTime()).ToUnixTimeMilliseconds(),
                Period = period,
                Count = count
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.TrendbarsRequest, request, CtraderRequestKind.ReadOnly, cancellationToken);
            CtraderTrendbarsResult result = new CtraderTrendbarsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.TrendbarsResponse)
            {
                result.Description = UnexpectedResponse("trendbar history", response);

                return result;
            }

            ProtoOAGetTrendbarsRes trendbars = ProtoOAGetTrendbarsRes.Parser.ParseFrom(response.Payload);
            result.Trendbars.AddRange(trendbars.Trendbar);
            result.HasMore = trendbars.HasMore;
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderOrderPlacementResult> PlaceMarketOrderAsync(long ctidTraderAccountId, long symbolId, ProtoOATradeSide tradeSide, long volume, string clientOrderId, CancellationToken cancellationToken)
        {
            ProtoOANewOrderReq request = new ProtoOANewOrderReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SymbolId = symbolId,
                OrderType = ProtoOAOrderType.Market,
                TradeSide = tradeSide,
                Volume = volume,
                ClientOrderId = clientOrderId,
                TimeInForce = ProtoOATimeInForce.ImmediateOrCancel
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.NewOrderRequest, request, cancellationToken);
            CtraderOrderPlacementResult result = new CtraderOrderPlacementResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null)
            {
                result.Description = UnexpectedResponse("market order", response);

                return result;
            }

            if (response.PayloadType == CtraderPayloadTypes.OrderErrorEvent)
            {
                ProtoOAOrderErrorEvent error = ProtoOAOrderErrorEvent.Parser.ParseFrom(response.Payload);
                result.ErrorCode = error.ErrorCode;
                result.Description = error.Description;

                return result;
            }

            if (response.PayloadType != CtraderPayloadTypes.ExecutionEvent)
            {
                result.Description = UnexpectedResponse("market order", response);

                return result;
            }

            result.ExecutionEvent = ProtoOAExecutionEvent.Parser.ParseFrom(response.Payload);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderOrderDetailsResult> GetOrderDetailsAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken)
        {
            ProtoOAOrderDetailsReq request = new ProtoOAOrderDetailsReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                OrderId = orderId
            };
            ProtoMessage response = await SendAsync(CtraderPayloadTypes.OrderDetailsRequest, request, cancellationToken);
            CtraderOrderDetailsResult result = new CtraderOrderDetailsResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.OrderDetailsResponse)
            {
                result.Description = UnexpectedResponse("order details", response);

                return result;
            }

            ProtoOAOrderDetailsRes details = ProtoOAOrderDetailsRes.Parser.ParseFrom(response.Payload);
            result.Order = details.Order;
            result.Deals.AddRange(details.Deal);
            result.IsSuccess = true;

            return result;
        }

        public async Task<CtraderOrderListResult> GetOrdersAsync(long ctidTraderAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            ProtoOAOrderListReq request = new ProtoOAOrderListReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                FromTimestamp = new DateTimeOffset(fromUtc.ToUniversalTime()).ToUnixTimeMilliseconds(),
                ToTimestamp = new DateTimeOffset(toUtc.ToUniversalTime()).ToUnixTimeMilliseconds()
            };

            ProtoMessage response = await SendAsync(CtraderPayloadTypes.OrderListRequest, request, cancellationToken);
            CtraderOrderListResult result = new CtraderOrderListResult();

            if (TryReadError(response, out string errorCode, out string description))
            {
                result.ErrorCode = errorCode;
                result.Description = description;

                return result;
            }

            if (response == null || response.PayloadType != CtraderPayloadTypes.OrderListResponse)
            {
                result.Description = UnexpectedResponse("order list", response);

                return result;
            }

            ProtoOAOrderListRes orders = ProtoOAOrderListRes.Parser.ParseFrom(response.Payload);
            result.Orders.AddRange(orders.Order);
            result.HasMore = orders.HasMore;
            result.IsSuccess = true;

            return result;
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            eventSignal.Dispose();
        }

        private Task<ProtoMessage> SendAsync(uint payloadType, IMessage payload, CancellationToken cancellationToken)
        {
            CtraderRequestKind requestKind = payloadType == CtraderPayloadTypes.NewOrderRequest
              ? CtraderRequestKind.OrderPlacement
              : CtraderRequestKind.ReadOnly;
            return connection.SendAsync(payloadType, payload, requestKind, cancellationToken);
        }

        private Task<ProtoMessage> SendAsync(uint payloadType, IMessage payload, CtraderRequestKind requestKind, CancellationToken cancellationToken)
        {
            return connection.SendAsync(payloadType, payload, requestKind, cancellationToken);
        }

        private void OnMessageReceived(ProtoMessage envelope)
        {
            if (envelope == null)
            {
                return;
            }

            // Event messages do not carry a request correlation id. Keep them until the operation that asked
            // for the event consumes them; this lets a spot subscription observe the technical first quote.
            if (!envelope.HasClientMsgId || string.IsNullOrEmpty(envelope.ClientMsgId))
            {
                events.Enqueue(envelope);
                eventSignal.Release();
            }
        }

        private async Task<bool> ReauthenticateCurrentSessionAsync(CancellationToken cancellationToken)
        {
            ProtoOAApplicationAuthReq applicationRequest = new ProtoOAApplicationAuthReq
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            };
            ProtoMessage applicationResponse = await connection.SendConnectedAsync(
              CtraderPayloadTypes.ApplicationAuthRequest,
              applicationRequest,
              CtraderRequestKind.Authentication,
              cancellationToken);

            if (applicationResponse == null || applicationResponse.PayloadType != CtraderPayloadTypes.ApplicationAuthResponse)
            {
                return false;
            }

            if (authenticatedAccountId.HasValue && !string.IsNullOrWhiteSpace(accessToken))
            {
                ProtoOAAccountAuthReq accountRequest = new ProtoOAAccountAuthReq
                {
                    CtidTraderAccountId = authenticatedAccountId.Value,
                    AccessToken = accessToken
                };
                ProtoMessage accountResponse = await connection.SendConnectedAsync(
                  CtraderPayloadTypes.AccountAuthRequest,
                  accountRequest,
                  CtraderRequestKind.Authentication,
                  cancellationToken);
                return accountResponse != null && accountResponse.PayloadType == CtraderPayloadTypes.AccountAuthResponse;
            }

            return true;
        }

        private async Task<bool> RestoreSpotSubscriptionAsync(long ctidTraderAccountId, IReadOnlyList<long> symbolIds, CancellationToken cancellationToken)
        {
            ProtoOASubscribeSpotsReq request = new ProtoOASubscribeSpotsReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SubscribeToSpotTimestamp = true
            };
            request.SymbolId.Add(symbolIds);

            ProtoMessage response = await connection.SendConnectedAsync(
              CtraderPayloadTypes.SubscribeSpotsRequest,
              request,
              CtraderRequestKind.Subscription,
              cancellationToken);
            return response != null && response.PayloadType == CtraderPayloadTypes.SubscribeSpotsResponse;
        }

        private static bool TryReadError(ProtoMessage response, out string errorCode, out string description)
        {
            errorCode = null;
            description = null;

            if (response == null)
            {
                return false;
            }

            if (response.PayloadType == CtraderPayloadTypes.ErrorResponse)
            {
                ProtoErrorRes error = ProtoErrorRes.Parser.ParseFrom(response.Payload);
                errorCode = error.ErrorCode;
                description = error.Description;

                return true;
            }

            if (response.PayloadType == CtraderPayloadTypes.OpenApiErrorResponse)
            {
                ProtoOAErrorRes error = ProtoOAErrorRes.Parser.ParseFrom(response.Payload);
                errorCode = error.ErrorCode;
                description = error.Description;

                return true;
            }

            return false;
        }

        private static string UnexpectedResponse(string operation, ProtoMessage response)
        {
            return response == null
              ? "The broker did not answer the " + operation + " request."
              : "Unexpected response to the " + operation + " request: " + response.PayloadType + ".";
        }

    }
}
