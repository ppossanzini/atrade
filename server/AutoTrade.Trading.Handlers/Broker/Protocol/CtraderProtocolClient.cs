using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
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

  public interface ICtraderProtocolClient : IAsyncDisposable
  {
    Task<CtraderHandshakeResult> AuthenticateApplicationAsync(CancellationToken cancellationToken);

    Task<CtraderAccountListResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken);
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
    private const int RequestTimeoutSeconds = 15;
    private const int ReceiveBufferBytes = 64 * 1024;

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private readonly BrokerOptions options;
    private readonly ILogger<CtraderProtocolClient> logger;
    private readonly ClientWebSocket socket = new ClientWebSocket();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoMessage>> pending = new ConcurrentDictionary<string, TaskCompletionSource<ProtoMessage>>();
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

    private Task receiveLoop;
    private Task heartbeatLoop;

    public CtraderProtocolClient(BrokerOptions options, ILogger<CtraderProtocolClient> logger)
    {
      this.options = options;
      this.logger = logger;
    }

    public async Task<CtraderHandshakeResult> AuthenticateApplicationAsync(CancellationToken cancellationToken)
    {
      await ConnectAsync(cancellationToken);

      ProtoOAApplicationAuthReq request = new ProtoOAApplicationAuthReq
      {
        ClientId = options.ClientId,
        ClientSecret = options.ClientSecret
      };

      ProtoMessage response = await SendAsync(CtraderPayloadTypes.ApplicationAuthRequest, request, cancellationToken);

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

      // The connection is only useful while it is kept alive, so the heartbeat starts only after a
      // successful authentication.
      heartbeatLoop = Task.Run(() => SendHeartbeatsAsync(lifetime.Token), CancellationToken.None);

      return result;
    }

    public async Task<CtraderAccountListResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken)
    {
      ProtoOAGetAccountListByAccessTokenReq request = new ProtoOAGetAccountListByAccessTokenReq
      {
        AccessToken = accessToken
      };

      ProtoMessage response = await SendAsync(CtraderPayloadTypes.AccountsByAccessTokenRequest, request, cancellationToken);

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

    public async ValueTask DisposeAsync()
    {
      lifetime.Cancel();

      try
      {
        if (socket.State == WebSocketState.Open)
        {
          await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
        }
      }
      catch (WebSocketException)
      {
        // The peer may already be gone; closing is best effort by definition.
      }

      socket.Dispose();
      lifetime.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
      Uri endpoint = CtraderEndpoint.ResolveUri(options.Environment);

      socket.Options.KeepAliveInterval = HeartbeatInterval;

      logger.LogInformation("Connecting to the broker endpoint for environment {Environment}.", options.Environment);

      await socket.ConnectAsync(endpoint, cancellationToken);

      receiveLoop = Task.Run(() => ReceiveAsync(lifetime.Token), CancellationToken.None);
    }

    private async Task<ProtoMessage> SendAsync(uint payloadType, IMessage payload, CancellationToken cancellationToken)
    {
      string correlationId = Guid.CreateVersion7().ToString();

      ProtoMessage envelope = new ProtoMessage
      {
        PayloadType = payloadType,
        Payload = payload.ToByteString(),
        ClientMsgId = correlationId
      };

      TaskCompletionSource<ProtoMessage> completion = new TaskCompletionSource<ProtoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending[correlationId] = completion;

      try
      {
        await socket.SendAsync(new ArraySegment<byte>(envelope.ToByteArray()), WebSocketMessageType.Binary, true, cancellationToken);

        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
          timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

          using (timeout.Token.Register(() => completion.TrySetResult(null)))
          {
            return await completion.Task;
          }
        }
      }
      catch (WebSocketException error)
      {
        logger.LogWarning("Broker transport failure while sending payload {PayloadType}: {Message}", payloadType, error.Message);

        return null;
      }
      finally
      {
        pending.TryRemove(correlationId, out _);
      }
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
      byte[] buffer = new byte[ReceiveBufferBytes];

      try
      {
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
          using (MemoryStream frame = new MemoryStream())
          {
            WebSocketReceiveResult received;

            do
            {
              received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

              if (received.MessageType == WebSocketMessageType.Close)
              {
                return;
              }

              frame.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);

            Dispatch(ProtoMessage.Parser.ParseFrom(frame.ToArray()));
          }
        }
      }
      catch (OperationCanceledException)
      {
        // Shutdown path.
      }
      catch (WebSocketException error)
      {
        logger.LogWarning("Broker connection closed: {Message}", error.Message);
      }
      catch (InvalidProtocolBufferException error)
      {
        logger.LogWarning("Discarded an unreadable broker frame: {Message}", error.Message);
      }
    }

    /// <summary>
    /// Completes the pending request carrying the same correlation id. Event frames without a correlation
    /// id (heartbeats, market data) need no handling yet and are ignored.
    /// </summary>
    private void Dispatch(ProtoMessage envelope)
    {
      if (!envelope.HasClientMsgId || string.IsNullOrEmpty(envelope.ClientMsgId))
      {
        return;
      }

      TaskCompletionSource<ProtoMessage> completion;

      if (pending.TryRemove(envelope.ClientMsgId, out completion))
      {
        completion.TrySetResult(envelope);
      }
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
      try
      {
        while (!cancellationToken.IsCancellationRequested)
        {
          await Task.Delay(HeartbeatInterval, cancellationToken);

          if (socket.State != WebSocketState.Open)
          {
            return;
          }

          ProtoMessage heartbeat = new ProtoMessage
          {
            PayloadType = CtraderPayloadTypes.HeartbeatEvent
          };

          await socket.SendAsync(new ArraySegment<byte>(heartbeat.ToByteArray()), WebSocketMessageType.Binary, true, cancellationToken);
        }
      }
      catch (OperationCanceledException)
      {
        // Shutdown path.
      }
      catch (WebSocketException error)
      {
        logger.LogWarning("Heartbeat stopped: {Message}", error.Message);
      }
    }
  }
}
