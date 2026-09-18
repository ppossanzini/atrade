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
  }
}
