using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Broker.Protocol
{
  /// <summary>
  /// Transport endpoint for one environment. Demo and live are fully separated environments: a demo
  /// connection cannot reach live accounts, and the reverse is equally true.
  /// </summary>
  public static class CtraderEndpoint
  {
    /// <summary>Protobuf traffic always uses this port, over TCP or WebSocket.</summary>
    public const int ProtobufPort = 5035;

    public const string DemoHost = "demo.ctraderapi.com";

    public const string LiveHost = "live.ctraderapi.com";

    public static string ResolveHost(TradingEnvironment environment)
    {
      return environment == TradingEnvironment.Live ? LiveHost : DemoHost;
    }

    public static Uri ResolveUri(TradingEnvironment environment)
    {
      return new Uri("wss://" + ResolveHost(environment) + ":" + ProtobufPort);
    }
  }

  /// <summary>
  /// Payload identifiers taken from the vendored schema. They are named constants because the transport
  /// envelope carries a raw integer, and a wrong literal would be silently accepted by the compiler.
  /// </summary>
  public static class CtraderPayloadTypes
  {
    /// <summary>ProtoPayloadType.ERROR_RES: sent by the proxy when a request cannot be served.</summary>
    public const uint ErrorResponse = 50;

    /// <summary>
    /// ProtoOAPayloadType.PROTO_OA_ERROR_RES: the Open API level error, distinct from the proxy level one.
    /// Verified against the live demo endpoint on 2026-09-19, where an unhandled value of 2142 was the
    /// answer to an application authentication request.
    /// </summary>
    public const uint OpenApiErrorResponse = 2142;

    /// <summary>ProtoPayloadType.HEARTBEAT_EVENT: the server drops connections idle for more than 10 seconds.</summary>
    public const uint HeartbeatEvent = 51;

    /// <summary>ProtoOAPayloadType.PROTO_OA_APPLICATION_AUTH_REQ.</summary>
    public const uint ApplicationAuthRequest = 2100;

    /// <summary>ProtoOAPayloadType.PROTO_OA_APPLICATION_AUTH_RES.</summary>
    public const uint ApplicationAuthResponse = 2101;

    /// <summary>ProtoOAPayloadType.PROTO_OA_ACCOUNT_AUTH_REQ.</summary>
    public const uint AccountAuthRequest = 2102;

    /// <summary>ProtoOAPayloadType.PROTO_OA_ACCOUNT_AUTH_RES.</summary>
    public const uint AccountAuthResponse = 2103;

    /// <summary>ProtoOAPayloadType.PROTO_OA_GET_ACCOUNTS_BY_ACCESS_TOKEN_REQ.</summary>
    public const uint AccountsByAccessTokenRequest = 2149;

    /// <summary>ProtoOAPayloadType.PROTO_OA_GET_ACCOUNTS_BY_ACCESS_TOKEN_RES.</summary>
    public const uint AccountsByAccessTokenResponse = 2150;

    public const uint DealListRequest = 2133;

    public const uint DealListResponse = 2134;

    public const uint TrendbarsRequest = 2137;

    public const uint TrendbarsResponse = 2138;

    public const uint TraderRequest = 2121;

    public const uint TraderResponse = 2122;

    public const uint ReconcileRequest = 2124;

    public const uint ReconcileResponse = 2125;

    public const uint SymbolsListRequest = 2114;

    public const uint SymbolsListResponse = 2115;

    public const uint SymbolByIdRequest = 2116;

    public const uint SymbolByIdResponse = 2117;

    public const uint SubscribeSpotsRequest = 2127;

    public const uint SubscribeSpotsResponse = 2128;

    public const uint SpotEvent = 2131;

    public const uint AssetListRequest = 2112;

    public const uint AssetListResponse = 2113;

    public const uint PositionUnrealizedPnlRequest = 2187;

    public const uint PositionUnrealizedPnlResponse = 2188;

    public const uint NewOrderRequest = 2106;

    public const uint ExecutionEvent = 2126;

    public const uint OrderErrorEvent = 2132;

    public const uint OrderListRequest = 2175;

    public const uint OrderListResponse = 2176;

    public const uint OrderDetailsRequest = 2181;

    public const uint OrderDetailsResponse = 2182;
  }
}
