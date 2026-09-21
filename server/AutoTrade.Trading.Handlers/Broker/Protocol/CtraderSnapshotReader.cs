using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.MarketData;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Broker.Protocol
{
    /// <summary>
    /// A complete read-only capture of the provider state needed by the risk and operations surfaces.
    /// Raw protobuf messages stay behind this adapter; callers receive an explicit failure instead of a
    /// partially populated snapshot that could be mistaken for a valid account state.
    /// </summary>
    public sealed class CtraderSnapshotReadResult
    {
        public bool IsSuccess { get; set; }

        public DateTime CapturedAtUtc { get; set; }

        public long CtidTraderAccountId { get; set; }

        public AutoTrade.Trading.Core.Enums.TradingEnvironment Environment { get; set; }

        public ProtoOATrader Trader { get; set; }

        public List<ProtoOAAsset> Assets { get; } = new List<ProtoOAAsset>();

        public List<ProtoOALightSymbol> Symbols { get; } = new List<ProtoOALightSymbol>();

        public List<ProtoOASymbol> SymbolDetails { get; } = new List<ProtoOASymbol>();

        public List<ProtoOAPosition> Positions { get; } = new List<ProtoOAPosition>();

        public List<ProtoOAOrder> PendingOrders { get; } = new List<ProtoOAOrder>();

        public List<ProtoOAPositionUnrealizedPnL> UnrealizedPnls { get; } = new List<ProtoOAPositionUnrealizedPnL>();

        public List<ProtoOASpotEvent> Spots { get; } = new List<ProtoOASpotEvent>();

        public Dictionary<long, List<ProtoOATrendbar>> TrendbarsBySymbolId { get; } = new Dictionary<long, List<ProtoOATrendbar>>();

        public List<ProtoOADeal> DealsToday { get; } = new List<ProtoOADeal>();

        public uint MoneyDigits { get; set; }

        public string ErrorCode { get; set; }

        public string Description { get; set; }
    }

    public interface ICtraderSnapshotReader
    {
        Task<CtraderSnapshotReadResult> ReadAsync(IReadOnlyList<SymbolRequest> requestedSymbols, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Coordinates the provider handshake and the read-only account snapshot. Token rotation is deliberately
    /// kept here, at the credential boundary: a refreshed pair is encrypted and persisted before the new access
    /// token is used, so a reconnect never resurrects the invalidated pair.
    /// </summary>
    public sealed class CtraderSnapshotReader(
      DB db,
      BrokerOptions options,
      IBrokerTokenProtector tokenProtector,
      OAuth.IBrokerTokenClient tokenClient,
      ICtraderProtocolClientFactory clientFactory,
      TimeProvider timeProvider,
      ILogger<CtraderSnapshotReader> logger) : ICtraderSnapshotReader
    {
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
        public async Task<CtraderSnapshotReadResult> ReadAsync(IReadOnlyList<SymbolRequest> requestedSymbols, CancellationToken cancellationToken)
        {
            CtraderSnapshotReadResult result = new CtraderSnapshotReadResult
            {
                CapturedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Environment = options.Environment
            };

            BrokerAuthorization authorization = await db.BrokerAuthorizations
              .FirstOrDefaultAsync(item => item.Environment == options.Environment, cancellationToken);

            if (authorization == null)
            {
                return Failure(result, "BROKER_NOT_AUTHORIZED", "No cTrader authorization is stored for the configured environment.");
            }

            string accessToken;

            if (!tokenProtector.TryUnprotect(authorization.AccessTokenCipher, out accessToken))
            {
                return Failure(result, "BROKER_TOKEN_UNREADABLE", "The stored cTrader access token could not be decrypted.");
            }

            if (authorization.AccessTokenExpiresAtUtc <= result.CapturedAtUtc.Add(RefreshSkew))
            {
                accessToken = await TryRefreshAsync(authorization, cancellationToken);
                if (accessToken == null)
                {
                    return Failure(result, "BROKER_TOKEN_REFRESH_FAILED", authorization.LastError ?? "The cTrader access token could not be refreshed.");
                }
            }

            await using ICtraderProtocolClient client = clientFactory.Create();

            CtraderHandshakeResult handshake = await client.AuthenticateApplicationAsync(cancellationToken);
            if (!handshake.IsAuthenticated)
            {
                return Failure(result, handshake.ErrorCode, handshake.Description ?? "cTrader application authentication failed.");
            }

            long accountId;

            if (authorization.CtidTraderAccountId.HasValue)
            {
                accountId = authorization.CtidTraderAccountId.Value;
            }
            else
            {
                CtraderAccountListResult accounts = await client.GetAccountsAsync(accessToken, cancellationToken);
                if (!accounts.IsSuccess)
                {
                    return Failure(result, accounts.ErrorCode, accounts.Description ?? "cTrader account discovery failed.");
                }

                if (accounts.CtidTraderAccountIds.Count != 1)
                {
                    return Failure(result, "BROKER_ACCOUNT_SELECTION_REQUIRED", "Exactly one cTrader account must be authorized before a snapshot can be consumed.");
                }

                accountId = accounts.CtidTraderAccountIds[0];
                authorization.CtidTraderAccountId = accountId;
                authorization.UpdatedAtUtc = result.CapturedAtUtc;
                await db.SaveChangesAsync(cancellationToken);
            }

            CtraderAccountAuthResult accountAuth = await client.AuthenticateAccountAsync(accountId, accessToken, cancellationToken);
            if (!accountAuth.IsAuthenticated)
            {
                return Failure(result, accountAuth.ErrorCode, accountAuth.Description ?? "cTrader account authentication failed.");
            }

            CtraderTraderResult trader = await client.GetTraderAsync(accountId, cancellationToken);
            if (!trader.IsSuccess)
            {
                return Failure(result, trader.ErrorCode, trader.Description ?? "cTrader trader snapshot failed.");
            }

            CtraderAssetsResult assets = await client.GetAssetsAsync(accountId, cancellationToken);
            if (!assets.IsSuccess)
            {
                return Failure(result, assets.ErrorCode, assets.Description ?? "cTrader asset snapshot failed.");
            }

            CtraderSymbolsResult symbols = await client.GetSymbolsAsync(accountId, cancellationToken);
            if (!symbols.IsSuccess)
            {
                return Failure(result, symbols.ErrorCode, symbols.Description ?? "cTrader symbol snapshot failed.");
            }

            List<long> symbolIds = SelectSymbolIds(symbols.Symbols, requestedSymbols);
            CtraderSymbolDetailsResult details = symbolIds.Count == 0
              ? new CtraderSymbolDetailsResult { IsSuccess = true }
              : await client.GetSymbolDetailsAsync(accountId, symbolIds, cancellationToken);
            if (!details.IsSuccess)
            {
                return Failure(result, details.ErrorCode, details.Description ?? "cTrader symbol details failed.");
            }

            CtraderReconcileResult reconciliation = await client.ReconcileAsync(accountId, cancellationToken);
            if (!reconciliation.IsSuccess)
            {
                return Failure(result, reconciliation.ErrorCode, reconciliation.Description ?? "cTrader reconciliation failed.");
            }

            CtraderUnrealizedPnlResult pnl = await client.GetUnrealizedPnlAsync(accountId, cancellationToken);
            if (!pnl.IsSuccess)
            {
                return Failure(result, pnl.ErrorCode, pnl.Description ?? "cTrader unrealized PnL failed.");
            }

            DateTime fromUtc = result.CapturedAtUtc.Date;
            CtraderDealsResult deals = await client.GetDealsAsync(accountId, fromUtc, result.CapturedAtUtc, cancellationToken);
            if (!deals.IsSuccess || deals.HasMore)
            {
                return Failure(result, deals.ErrorCode ?? "BROKER_DEALS_INCOMPLETE", deals.Description ?? "The cTrader deal history is incomplete; daily PnL is not safe to use.");
            }

            CtraderSpotResult spots = requestedSymbols == null || symbolIds.Count == 0
              ? new CtraderSpotResult { IsSuccess = true }
              : await client.SubscribeSpotsAsync(accountId, symbolIds, cancellationToken);

            if (!spots.IsSuccess)
            {
                return Failure(result, spots.ErrorCode, spots.Description ?? "cTrader spot subscription failed.");
            }

            Dictionary<long, List<ProtoOATrendbar>> trendbarsBySymbolId = new Dictionary<long, List<ProtoOATrendbar>>();
            if (symbolIds.Count > 0)
            {
                DateTime historyFromUtc = result.CapturedAtUtc - CtraderVolatilityContract.HistoryWindow;
                foreach (long symbolId in symbolIds)
                {
                    CtraderTrendbarsResult trendbars = await client.GetTrendbarsAsync(
                      accountId,
                      symbolId,
                      historyFromUtc,
                      result.CapturedAtUtc,
                      CtraderVolatilityContract.Period,
                      CtraderVolatilityContract.BarCount,
                      cancellationToken);

                    if (!trendbars.IsSuccess || trendbars.HasMore)
                    {
                        return Failure(result, trendbars.ErrorCode ?? "BROKER_TRENDBARS_INCOMPLETE", trendbars.Description ?? "The cTrader trendbar history is incomplete; volatility is not safe to use.");
                    }

                    trendbarsBySymbolId[symbolId] = trendbars.Trendbars.ToList();
                }
            }

            result.IsSuccess = true;
            result.CtidTraderAccountId = accountId;
            result.Trader = trader.Trader;
            result.Assets.AddRange(assets.Assets);
            result.Symbols.AddRange(symbols.Symbols);
            result.SymbolDetails.AddRange(details.Symbols);
            result.Positions.AddRange(reconciliation.Positions);
            result.PendingOrders.AddRange(reconciliation.Orders);
            result.UnrealizedPnls.AddRange(pnl.Positions);
            result.Spots.AddRange(spots.Spots);
            result.DealsToday.AddRange(deals.Deals);
            foreach (KeyValuePair<long, List<ProtoOATrendbar>> trendbars in trendbarsBySymbolId)
            {
                result.TrendbarsBySymbolId[trendbars.Key] = trendbars.Value;
            }
            result.MoneyDigits = pnl.MoneyDigits;

            return result;
        }

        private async Task<string> TryRefreshAsync(BrokerAuthorization authorization, CancellationToken cancellationToken)
        {
            await CtraderTokenRefreshGate.Instance.WaitAsync(cancellationToken);

            try
            {
                // The scoped context already tracks the row loaded before waiting on the process-wide gate.
                // A normal query would return that same stale instance, so force a database reload before
                // deciding whether another request has already rotated the refresh token.
                await db.Entry(authorization).ReloadAsync(cancellationToken);

                DateTime now = timeProvider.GetUtcNow().UtcDateTime;
                if (authorization.AccessTokenExpiresAtUtc > now.Add(RefreshSkew)
                  && tokenProtector.TryUnprotect(authorization.AccessTokenCipher, out string currentAccessToken))
                {
                    return currentAccessToken;
                }

                string refreshToken;

                if (!tokenProtector.TryUnprotect(authorization.RefreshTokenCipher, out refreshToken))
                {
                    authorization.LastError = "The stored cTrader refresh token could not be decrypted.";
                    await db.SaveChangesAsync(cancellationToken);

                    return null;
                }

                OAuth.BrokerTokenExchangeResult refreshed = await tokenClient.RefreshAsync(refreshToken, cancellationToken);
                if (!refreshed.IsSuccess || refreshed.Tokens == null)
                {
                    authorization.LastError = refreshed.ErrorDetail ?? "The cTrader token endpoint rejected the refresh.";
                    await db.SaveChangesAsync(cancellationToken);

                    return null;
                }

                authorization.AccessTokenCipher = tokenProtector.Protect(refreshed.Tokens.AccessToken);
                authorization.RefreshTokenCipher = tokenProtector.Protect(refreshed.Tokens.RefreshToken);
                authorization.AccessTokenExpiresAtUtc = now.AddSeconds(refreshed.Tokens.ExpiresIn);
                authorization.UpdatedAtUtc = now;
                authorization.LastError = null;
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Rotated the cTrader access token for environment {Environment}.", options.Environment);

                return refreshed.Tokens.AccessToken;
            }
            finally
            {
                CtraderTokenRefreshGate.Instance.Release();
            }
        }

        private static List<long> SelectSymbolIds(List<ProtoOALightSymbol> symbols, IReadOnlyList<SymbolRequest> requestedSymbols)
        {
            if (requestedSymbols == null)
            {
                return symbols.Select(item => item.SymbolId).Distinct().ToList();
            }

            if (requestedSymbols.Count == 0)
            {
                return new List<long>();
            }

            HashSet<string> requested = new HashSet<string>(requestedSymbols.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Symbol)).Select(item => item.Symbol), StringComparer.OrdinalIgnoreCase);

            return symbols
              .Where(item => requested.Contains(item.SymbolName))
              .Select(item => item.SymbolId)
              .Distinct()
              .ToList();
        }

        private static CtraderSnapshotReadResult Failure(CtraderSnapshotReadResult result, string errorCode, string description)
        {
            result.IsSuccess = false;
            result.ErrorCode = errorCode;
            result.Description = description;

            return result;
        }
    }
}
