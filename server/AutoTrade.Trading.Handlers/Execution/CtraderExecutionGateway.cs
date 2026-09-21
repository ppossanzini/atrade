using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>
  /// cTrader execution realization. It sends only market orders, uses the execution engine's deterministic
  /// client order id as the provider idempotency key, and turns an incomplete response into an unknown outcome
  /// that the caller must reconcile. The default promotion guard permits Demo only.
  /// </summary>
  public sealed class CtraderExecutionGateway(
    DB db,
    BrokerOptions brokerOptions,
    ExecutionOptions executionOptions,
    IBrokerTokenProtector tokenProtector,
    IBrokerTokenClient tokenClient,
    ICtraderProtocolClientFactory clientFactory,
    TimeProvider timeProvider,
    ILogger<CtraderExecutionGateway> logger) : IExecutionGateway
  {
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    public async Task<OrderDispatchResult> SendAsync(OrderRequest request, CancellationToken cancellationToken)
    {
      if (request == null || string.IsNullOrWhiteSpace(request.ClientOrderId) || request.ClientOrderId.Length > 50)
      {
        return Rejected("BROKER_CLIENT_ORDER_ID_INVALID");
      }

      if (string.IsNullOrWhiteSpace(request.Symbol) || request.VolumeUnits <= 0)
      {
        return Rejected("BROKER_ORDER_REQUEST_INVALID");
      }

      SessionResult session = await OpenSessionAsync(cancellationToken);
      if (!session.IsSuccess)
      {
        return Rejected(session.ErrorCode);
      }

      await using ICtraderProtocolClient client = session.Client;

      ProtoOALightSymbol symbol = FindSymbol(session.Symbols, request.Symbol);
      if (symbol == null || !symbol.Enabled)
      {
        return Rejected("BROKER_SYMBOL_UNAVAILABLE");
      }

      CtraderSymbolDetailsResult details = await client.GetSymbolDetailsAsync(session.AccountId, new[] { symbol.SymbolId }, cancellationToken);
      if (!details.IsSuccess || details.Symbols.Count != 1 || details.Symbols[0].TradingMode != ProtoOATradingMode.Enabled)
      {
        return Rejected(details.ErrorCode ?? "BROKER_SYMBOL_NOT_TRADABLE");
      }

      ProtoOATradeSide side = request.Direction == LegDirection.Long
        ? ProtoOATradeSide.Buy
        : request.Direction == LegDirection.Short
          ? ProtoOATradeSide.Sell
          : (ProtoOATradeSide)(-1);

      if (side == (ProtoOATradeSide)(-1))
      {
        return Rejected("BROKER_ORDER_DIRECTION_INVALID");
      }

      CtraderOrderPlacementResult placement = await client.PlaceMarketOrderAsync(
        session.AccountId,
        symbol.SymbolId,
        side,
        request.VolumeUnits,
        request.ClientOrderId,
        cancellationToken);

      if (!placement.IsSuccess)
      {
        if (!string.IsNullOrWhiteSpace(placement.ErrorCode))
        {
          return new OrderDispatchResult
          {
            Outcome = OrderDispatchOutcome.Rejected,
            ErrorCode = placement.ErrorCode
          };
        }

        return NoResponse("BROKER_ORDER_NO_RESPONSE", placement.Description ?? "The cTrader order result was not received.");
      }

      return MapExecutionEvent(request, placement.ExecutionEvent);
    }

    public async Task<OrderQueryResult> QueryAsync(string clientOrderId, string symbol, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(clientOrderId) || clientOrderId.Length > 50 || string.IsNullOrWhiteSpace(symbol))
      {
        return Unknown("BROKER_RECONCILIATION_REQUEST_INVALID", "The cTrader reconciliation request is incomplete.");
      }

      SessionResult session = await OpenSessionAsync(cancellationToken);
      if (!session.IsSuccess)
      {
        return Unknown(session.ErrorCode, session.Description);
      }

      await using ICtraderProtocolClient client = session.Client;

      ProtoOALightSymbol requestedSymbol = FindSymbol(session.Symbols, symbol);
      if (requestedSymbol == null)
      {
        return Unknown("BROKER_SYMBOL_UNAVAILABLE", "The requested cTrader symbol is not available for reconciliation.");
      }

      DateTime toUtc = timeProvider.GetUtcNow().UtcDateTime;
      CtraderOrderListResult orders = await client.GetOrdersAsync(session.AccountId, toUtc.AddDays(-1), toUtc, cancellationToken);
      if (!orders.IsSuccess)
      {
        return Unknown(orders.ErrorCode ?? "BROKER_RECONCILIATION_FAILED", orders.Description ?? "The cTrader order list could not be read.");
      }

      if (orders.HasMore)
      {
        return Unknown("BROKER_ORDER_RECONCILIATION_INCOMPLETE", "The cTrader order list was truncated; the order outcome remains unknown.");
      }

      List<ProtoOAOrder> matches = orders.Orders
        .Where(item => item.TradeData != null && string.Equals(item.ClientOrderId, clientOrderId, StringComparison.Ordinal))
        .ToList();

      if (matches.Count == 0)
      {
        return Unknown("BROKER_ORDER_NOT_FOUND", "The cTrader order list contains no order with the requested client order id.");
      }

      if (matches.Count != 1 || matches[0].TradeData.SymbolId != requestedSymbol.SymbolId)
      {
        return Unknown("BROKER_ORDER_AMBIGUOUS", "The cTrader order list does not identify exactly one order for the requested symbol.");
      }

      CtraderOrderDetailsResult details = await client.GetOrderDetailsAsync(session.AccountId, matches[0].OrderId, cancellationToken);
      if (!details.IsSuccess
        || details.Order == null
        || details.Order.TradeData == null
        || details.Order.TradeData.SymbolId != requestedSymbol.SymbolId
        || !string.Equals(details.Order.ClientOrderId, clientOrderId, StringComparison.Ordinal))
      {
        return Unknown(details.ErrorCode ?? "BROKER_ORDER_DETAILS_INCOMPLETE", details.Description ?? "The cTrader order details could not be correlated safely.");
      }

      return MapOrderState(details.Order, details.Deals);
    }

    private async Task<SessionResult> OpenSessionAsync(CancellationToken cancellationToken)
    {
      if (brokerOptions.Environment == TradingEnvironment.Live && !(executionOptions.Ctrader?.AllowLive ?? false))
      {
        return SessionResult.Failure("BROKER_LIVE_EXECUTION_DISABLED", "Live cTrader execution is disabled by the promotion guard.");
      }

      if (!HasTradingScope(brokerOptions.Scope))
      {
        return SessionResult.Failure("BROKER_TRADING_SCOPE_REQUIRED", "The cTrader authorization does not include the trading scope.");
      }

      BrokerAuthorization authorization = await db.BrokerAuthorizations
        .FirstOrDefaultAsync(item => item.Environment == brokerOptions.Environment, cancellationToken);
      if (authorization == null)
      {
        return SessionResult.Failure("BROKER_NOT_AUTHORIZED", "No cTrader authorization is stored for the configured environment.");
      }

      if (!tokenProtector.TryUnprotect(authorization.AccessTokenCipher, out string accessToken))
      {
        return SessionResult.Failure("BROKER_TOKEN_UNREADABLE", "The stored cTrader access token could not be decrypted.");
      }

      if (authorization.AccessTokenExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime.Add(RefreshSkew))
      {
        accessToken = await TryRefreshAsync(authorization, cancellationToken);
        if (accessToken == null)
        {
          return SessionResult.Failure("BROKER_TOKEN_REFRESH_FAILED", authorization.LastError ?? "The cTrader access token could not be refreshed.");
        }
      }

      ICtraderProtocolClient client = clientFactory.Create();
      CtraderHandshakeResult handshake = await client.AuthenticateApplicationAsync(cancellationToken);
      if (!handshake.IsAuthenticated)
      {
        await client.DisposeAsync();

        return SessionResult.Failure(handshake.ErrorCode ?? "BROKER_APP_AUTH_FAILED", handshake.Description ?? "cTrader application authentication failed.");
      }

      long accountId;
      if (authorization.CtidTraderAccountId.HasValue)
      {
        accountId = authorization.CtidTraderAccountId.Value;
      }
      else
      {
        CtraderAccountListResult accounts = await client.GetAccountsAsync(accessToken, cancellationToken);
        if (!accounts.IsSuccess || accounts.CtidTraderAccountIds.Count != 1)
        {
          await client.DisposeAsync();

          return SessionResult.Failure(accounts.ErrorCode ?? "BROKER_ACCOUNT_SELECTION_REQUIRED", accounts.Description ?? "Exactly one cTrader account must be authorized before execution.");
        }

        accountId = accounts.CtidTraderAccountIds[0];
        authorization.CtidTraderAccountId = accountId;
        authorization.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
      }

      CtraderAccountAuthResult accountAuth = await client.AuthenticateAccountAsync(accountId, accessToken, cancellationToken);
      if (!accountAuth.IsAuthenticated)
      {
        await client.DisposeAsync();

        return SessionResult.Failure(accountAuth.ErrorCode ?? "BROKER_ACCOUNT_AUTH_FAILED", accountAuth.Description ?? "cTrader account authentication failed.");
      }

      CtraderSymbolsResult symbols = await client.GetSymbolsAsync(accountId, cancellationToken);
      if (!symbols.IsSuccess)
      {
        await client.DisposeAsync();

        return SessionResult.Failure(symbols.ErrorCode ?? "BROKER_SYMBOL_LIST_FAILED", symbols.Description ?? "cTrader symbols could not be read.");
      }

      logger.LogDebug("Opened a cTrader execution session for {Environment} account {AccountId}.", brokerOptions.Environment, accountId);

      return SessionResult.Success(client, accountId, symbols.Symbols);
    }

    private async Task<string> TryRefreshAsync(BrokerAuthorization authorization, CancellationToken cancellationToken)
    {
      await CtraderTokenRefreshGate.Instance.WaitAsync(cancellationToken);

      try
      {
        await db.Entry(authorization).ReloadAsync(cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (authorization.AccessTokenExpiresAtUtc > now.Add(RefreshSkew)
          && tokenProtector.TryUnprotect(authorization.AccessTokenCipher, out string currentAccessToken))
        {
          return currentAccessToken;
        }

        if (!tokenProtector.TryUnprotect(authorization.RefreshTokenCipher, out string refreshToken))
        {
          authorization.LastError = "The stored cTrader refresh token could not be decrypted.";
          await db.SaveChangesAsync(cancellationToken);

          return null;
        }

        BrokerTokenExchangeResult refreshed = await tokenClient.RefreshAsync(refreshToken, cancellationToken);
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

        return refreshed.Tokens.AccessToken;
      }
      finally
      {
        CtraderTokenRefreshGate.Instance.Release();
      }
    }

    private static bool HasTradingScope(string scope)
    {
      return (scope ?? string.Empty)
        .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
        .Any(item => string.Equals(item, BrokerOptions.TradingScope, StringComparison.OrdinalIgnoreCase));
    }

    private static ProtoOALightSymbol FindSymbol(IReadOnlyList<ProtoOALightSymbol> symbols, string requested)
    {
      if (symbols == null)
      {
        return null;
      }

      List<ProtoOALightSymbol> matches = symbols
        .Where(item => string.Equals(item.SymbolName, requested, StringComparison.OrdinalIgnoreCase))
        .Take(2)
        .ToList();

      return matches.Count == 1 ? matches[0] : null;
    }

    private static OrderDispatchResult MapExecutionEvent(OrderRequest request, ProtoOAExecutionEvent execution)
    {
      if (execution?.Order == null || execution.Order.OrderId <= 0 || !string.Equals(execution.Order.ClientOrderId, request.ClientOrderId, StringComparison.Ordinal))
      {
        return NoResponse("BROKER_EXECUTION_EVENT_INCOMPLETE", "The cTrader execution event could not be correlated to the submitted client order id.");
      }

      int filled = ToInt(execution.Deal != null && execution.Deal.FilledVolume > 0 ? execution.Deal.FilledVolume : execution.Order.ExecutedVolume);
      double? averagePrice = execution.Deal != null && execution.Deal.HasExecutionPrice
        ? execution.Deal.ExecutionPrice
        : execution.Order.HasExecutionPrice ? execution.Order.ExecutionPrice : (double?)null;
      string eventId = "CTRADER:" + execution.Order.OrderId + ":" + execution.ExecutionType + ":" + (execution.Deal != null ? execution.Deal.DealId.ToString() : "none");
      OrderEventPayload payload = new OrderEventPayload
      {
        BrokerEventId = eventId,
        Symbol = request.Symbol,
        FilledVolumeUnits = filled,
        AveragePrice = averagePrice,
        Payload = "orderId=" + execution.Order.OrderId + ";executionType=" + execution.ExecutionType
      };

      switch (execution.ExecutionType)
      {
        case ProtoOAExecutionType.OrderAccepted:
          payload.Kind = ExecutionEventKind.OrderAccepted;
          return Dispatch(OrderDispatchOutcome.Accepted, execution.Order.OrderId, payload);

        case ProtoOAExecutionType.OrderPartialFill:
          if (filled <= 0)
          {
            return NoResponse("BROKER_EXECUTION_EVENT_INCOMPLETE", "The cTrader partial-fill event did not include filled volume.");
          }

          payload.Kind = ExecutionEventKind.OrderPartiallyFilled;
          return Dispatch(OrderDispatchOutcome.PartiallyFilled, execution.Order.OrderId, payload);

        case ProtoOAExecutionType.OrderFilled:
          if (filled <= 0)
          {
            return NoResponse("BROKER_EXECUTION_EVENT_INCOMPLETE", "The cTrader fill event did not include filled volume.");
          }

          payload.Kind = ExecutionEventKind.OrderFilled;
          return Dispatch(OrderDispatchOutcome.Filled, execution.Order.OrderId, payload);

        case ProtoOAExecutionType.OrderRejected:
          payload.Kind = ExecutionEventKind.OrderRejected;
          return Dispatch(OrderDispatchOutcome.Rejected, execution.Order.OrderId, payload);

        case ProtoOAExecutionType.OrderCancelled:
        case ProtoOAExecutionType.OrderExpired:
          payload.Kind = ExecutionEventKind.OrderCancelled;
          return Dispatch(OrderDispatchOutcome.Rejected, execution.Order.OrderId, payload);

        default:
          return NoResponse("BROKER_EXECUTION_EVENT_UNSUPPORTED", "The cTrader execution event is not a terminal order state supported by the gateway.");
      }
    }

    private static OrderQueryResult MapOrderState(ProtoOAOrder order, IReadOnlyList<ProtoOADeal> deals)
    {
      int filled = ToInt(order.ExecutedVolume);
      ProtoOADeal deal = deals != null ? deals.OrderByDescending(item => item.ExecutionTimestamp).FirstOrDefault() : null;
      double? averagePrice = order.HasExecutionPrice
        ? order.ExecutionPrice
        : deal != null && deal.HasExecutionPrice ? deal.ExecutionPrice : (double?)null;
      string dealId = deal != null && deal.DealId > 0 ? deal.DealId.ToString() : "none";
      OrderEventPayload payload = new OrderEventPayload
      {
        BrokerEventId = "CTRADER:" + order.OrderId + ":" + ExecutionType(order) + ":" + dealId,
        Kind = ExecutionEventKind.Unknown,
        FilledVolumeUnits = filled,
        AveragePrice = averagePrice,
        Payload = "orderId=" + order.OrderId + ";orderStatus=" + order.OrderStatus
      };

      switch (order.OrderStatus)
      {
        case ProtoOAOrderStatus.OrderStatusAccepted when filled > 0:
          payload.Kind = ExecutionEventKind.OrderPartiallyFilled;
          return Query(OrderQueryOutcome.PartiallyFilled, order.OrderId, filled, averagePrice, payload);

        case ProtoOAOrderStatus.OrderStatusAccepted:
          payload.Kind = ExecutionEventKind.OrderAccepted;
          return Query(OrderQueryOutcome.Accepted, order.OrderId, filled, averagePrice, payload);

        case ProtoOAOrderStatus.OrderStatusFilled:
          payload.Kind = ExecutionEventKind.OrderFilled;
          return Query(OrderQueryOutcome.Filled, order.OrderId, filled, averagePrice, payload);

        case ProtoOAOrderStatus.OrderStatusRejected:
          payload.Kind = ExecutionEventKind.OrderRejected;
          return Query(OrderQueryOutcome.Rejected, order.OrderId, filled, averagePrice, payload);

        case ProtoOAOrderStatus.OrderStatusCancelled:
        case ProtoOAOrderStatus.OrderStatusExpired:
          payload.Kind = ExecutionEventKind.OrderCancelled;
          return Query(OrderQueryOutcome.Rejected, order.OrderId, filled, averagePrice, payload);

        default:
          return Unknown("BROKER_ORDER_STATUS_UNSUPPORTED", "The cTrader order status is not supported by the gateway.");
      }
    }

    private static ProtoOAExecutionType ExecutionType(ProtoOAOrder order)
    {
      if (order.OrderStatus == ProtoOAOrderStatus.OrderStatusFilled)
      {
        return ProtoOAExecutionType.OrderFilled;
      }

      if (order.OrderStatus == ProtoOAOrderStatus.OrderStatusRejected)
      {
        return ProtoOAExecutionType.OrderRejected;
      }

      if (order.OrderStatus == ProtoOAOrderStatus.OrderStatusCancelled || order.OrderStatus == ProtoOAOrderStatus.OrderStatusExpired)
      {
        return ProtoOAExecutionType.OrderCancelled;
      }

      return order.ExecutedVolume > 0 ? ProtoOAExecutionType.OrderPartialFill : ProtoOAExecutionType.OrderAccepted;
    }

    private static int ToInt(long volume)
    {
      return volume <= 0 ? 0 : volume > int.MaxValue ? int.MaxValue : (int)volume;
    }

    private static OrderDispatchResult Dispatch(OrderDispatchOutcome outcome, long brokerOrderId, OrderEventPayload payload)
    {
      return new OrderDispatchResult
      {
        Outcome = outcome,
        BrokerOrderId = brokerOrderId.ToString(),
        Events = new List<OrderEventPayload> { payload }
      };
    }

    private static OrderDispatchResult NoResponse(string errorCode, string description)
    {
      return new OrderDispatchResult
      {
        Outcome = OrderDispatchOutcome.NoResponse,
        ErrorCode = errorCode,
        Events = new List<OrderEventPayload>()
      };
    }

    private static OrderDispatchResult Rejected(string errorCode)
    {
      return new OrderDispatchResult
      {
        Outcome = OrderDispatchOutcome.Rejected,
        ErrorCode = errorCode,
        Events = new List<OrderEventPayload>()
      };
    }

    private static OrderQueryResult Query(OrderQueryOutcome outcome, long brokerOrderId, int filled, double? averagePrice, OrderEventPayload payload)
    {
      return new OrderQueryResult
      {
        Outcome = outcome,
        BrokerOrderId = brokerOrderId.ToString(),
        FilledVolumeUnits = filled,
        AveragePrice = averagePrice,
        Events = new List<OrderEventPayload> { payload }
      };
    }

    private static OrderQueryResult Unknown(string errorCode, string description)
    {
      return new OrderQueryResult
      {
        Outcome = OrderQueryOutcome.Unknown,
        ErrorCode = errorCode,
        Events = new List<OrderEventPayload>()
      };
    }

    private sealed class SessionResult
    {
      public bool IsSuccess { get; private init; }

      public ICtraderProtocolClient Client { get; private init; }

      public long AccountId { get; private init; }

      public IReadOnlyList<ProtoOALightSymbol> Symbols { get; private init; }

      public string ErrorCode { get; private init; }

      public string Description { get; private init; }

      public static SessionResult Success(ICtraderProtocolClient client, long accountId, IReadOnlyList<ProtoOALightSymbol> symbols)
      {
        return new SessionResult { IsSuccess = true, Client = client, AccountId = accountId, Symbols = symbols };
      }

      public static SessionResult Failure(string errorCode, string description)
      {
        return new SessionResult { ErrorCode = errorCode, Description = description };
      }
    }
  }
}
