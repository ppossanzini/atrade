using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Broker.Protocol
{
  public enum CtraderRequestKind
  {
    Authentication = 0,
    ReadOnly = 1,
    Subscription = 2,
    OrderPlacement = 3
  }

  public enum CtraderSessionState
  {
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Authenticated = 3,
    Reconnecting = 4,
    Faulted = 5,
    Disposed = 6
  }

  public sealed class CtraderConnectionHealth
  {
    public CtraderSessionState State { get; set; }

    public bool IsHealthy
    {
      get { return State == CtraderSessionState.Connected || State == CtraderSessionState.Authenticated; }
    }

    public bool IsAuthenticated
    {
      get { return State == CtraderSessionState.Authenticated; }
    }

    public DateTime? ConnectedAtUtc { get; set; }

    public DateTime? LastReceivedAtUtc { get; set; }

    public DateTime? LastHeartbeatAtUtc { get; set; }

    public int ReconnectAttempts { get; set; }

    public string LastError { get; set; }
  }

  public interface ICtraderWebSocketTransport : IAsyncDisposable
  {
    WebSocketState State { get; }

    Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken);

    Task SendAsync(ArraySegment<byte> payload, CancellationToken cancellationToken);

    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);

    Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
  }

  public interface ICtraderWebSocketTransportFactory
  {
    ICtraderWebSocketTransport Create();
  }

  public sealed class ClientWebSocketTransportFactory : ICtraderWebSocketTransportFactory
  {
    public ICtraderWebSocketTransport Create()
    {
      return new ClientWebSocketTransport();
    }
  }

  public sealed class ClientWebSocketTransport : ICtraderWebSocketTransport
  {
    private readonly ClientWebSocket socket = new ClientWebSocket();

    public WebSocketState State
    {
      get { return socket.State; }
    }

    public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
      return socket.ConnectAsync(endpoint, cancellationToken);
    }

    public Task SendAsync(ArraySegment<byte> payload, CancellationToken cancellationToken)
    {
      return socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
      return socket.ReceiveAsync(buffer, cancellationToken);
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
      return socket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
      socket.Dispose();
      return ValueTask.CompletedTask;
    }
  }

  public sealed class CtraderConnectionManagerOptions
  {
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan ReconnectBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxReconnectAttempts { get; set; } = 5;

    public double ReconnectJitterRatio { get; set; } = 0.2;
  }

  /// <summary>
  /// Owns one cTrader WebSocket session. Requests are correlated and serialized on the wire, while the
  /// receive loop remains independent from callers. A lost session is re-established and authenticated
  /// through the supplied hook. Order requests are deliberately fail-closed: they are never resent.
  /// </summary>
  public sealed class CtraderConnectionManager : IAsyncDisposable
  {
    private const int ReceiveBufferBytes = 64 * 1024;

    private readonly BrokerOptions options;
    private readonly ILogger logger;
    private readonly ICtraderWebSocketTransportFactory transportFactory;
    private readonly CtraderConnectionManagerOptions managerOptions;
    private readonly Func<double> randomSample;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim connectGate = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtoMessage>> pending = new ConcurrentDictionary<string, TaskCompletionSource<ProtoMessage>>();
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<bool>>> subscriptions = new ConcurrentDictionary<string, Func<CancellationToken, Task<bool>>>();
    private readonly object stateLock = new object();

    private ICtraderWebSocketTransport transport;
    private Task receiveLoop;
    private Task heartbeatLoop;
    private Task reconnectLoop;
    private Func<CancellationToken, Task<bool>> reauthenticate;
    private CtraderConnectionHealth health = new CtraderConnectionHealth { State = CtraderSessionState.Disconnected };
    private int reconnectLoopStarted;

    public CtraderConnectionManager(BrokerOptions options, ILogger logger)
      : this(options, logger, new ClientWebSocketTransportFactory(), new CtraderConnectionManagerOptions(), TimeProvider.System, null)
    {
    }

    public CtraderConnectionManager(
      BrokerOptions options,
      ILogger logger,
      ICtraderWebSocketTransportFactory transportFactory,
      CtraderConnectionManagerOptions managerOptions,
      TimeProvider timeProvider,
      Func<double> randomSample)
    {
      this.options = options ?? throw new ArgumentNullException(nameof(options));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
      this.managerOptions = managerOptions ?? throw new ArgumentNullException(nameof(managerOptions));
      this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
      this.randomSample = randomSample ?? (() => Random.Shared.NextDouble());
    }

    public event Action<ProtoMessage> MessageReceived;

    public CtraderConnectionHealth Health
    {
      get
      {
        lock (stateLock)
        {
          return new CtraderConnectionHealth
          {
            State = health.State,
            ConnectedAtUtc = health.ConnectedAtUtc,
            LastReceivedAtUtc = health.LastReceivedAtUtc,
            LastHeartbeatAtUtc = health.LastHeartbeatAtUtc,
            ReconnectAttempts = health.ReconnectAttempts,
            LastError = health.LastError
          };
        }
      }
    }

    public void SetReauthentication(Func<CancellationToken, Task<bool>> callback)
    {
      reauthenticate = callback;
    }

    public void MarkAuthenticated()
    {
      lock (stateLock)
      {
        if (health.State == CtraderSessionState.Connected)
        {
          health.State = CtraderSessionState.Authenticated;
        }
      }
    }

    public void RegisterSubscription(string key, Func<CancellationToken, Task<bool>> restore)
    {
      if (string.IsNullOrWhiteSpace(key))
      {
        throw new ArgumentException("A subscription key is required.", nameof(key));
      }

      if (restore == null)
      {
        throw new ArgumentNullException(nameof(restore));
      }

      subscriptions[key] = restore;
    }

    public void RemoveSubscription(string key)
    {
      if (!string.IsNullOrWhiteSpace(key))
      {
        subscriptions.TryRemove(key, out _);
      }
    }

    public Task<bool> EnsureConnectedAsync(CtraderRequestKind requestKind, CancellationToken cancellationToken)
    {
      return EnsureConnectedCoreAsync(requestKind, cancellationToken);
    }

    public async Task<ProtoMessage> SendAsync(uint payloadType, IMessage payload, CtraderRequestKind requestKind, CancellationToken cancellationToken)
    {
      if (!await EnsureConnectedCoreAsync(requestKind, cancellationToken))
      {
        return null;
      }

      return await SendConnectedAsync(payloadType, payload, requestKind, cancellationToken);
    }

    /// <summary> Sends only on the current authenticated socket; it never connects or retries. </summary>
    public async Task<ProtoMessage> SendConnectedAsync(uint payloadType, IMessage payload, CtraderRequestKind requestKind, CancellationToken cancellationToken)
    {
      ICtraderWebSocketTransport current = transport;
      if (current == null || current.State != WebSocketState.Open || IsDisposed()
        || (requestKind == CtraderRequestKind.OrderPlacement && !IsAuthenticatedState()))
      {
        return null;
      }

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
        await SendFrameAsync(current, envelope.ToByteArray(), cancellationToken);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        timeout.CancelAfter(managerOptions.RequestTimeout);
        using (timeout.Token.Register(() => completion.TrySetResult(null)))
        {
          return await completion.Task;
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested)
      {
        return null;
      }
      catch (WebSocketException error)
      {
        logger.LogWarning("Broker transport failure while sending payload {PayloadType}: {Message}", payloadType, error.Message);
        await DropTransportAsync(current, error.Message, startReconnect: requestKind != CtraderRequestKind.OrderPlacement);
        return null;
      }
      finally
      {
        pending.TryRemove(correlationId, out _);
      }
    }

    public async ValueTask DisposeAsync()
    {
      lock (stateLock)
      {
        health.State = CtraderSessionState.Disposed;
      }

      lifetime.Cancel();
      foreach (KeyValuePair<string, TaskCompletionSource<ProtoMessage>> item in pending)
      {
        if (pending.TryRemove(item.Key, out TaskCompletionSource<ProtoMessage> completion))
        {
          completion.TrySetResult(null);
        }
      }

      // Cancel and observe the loops before disposing their transport. A receive implementation may be
      // blocked on its own wait primitive; disposing that primitive first can strand the loop forever.
      if (receiveLoop != null)
      {
        await ObserveTaskAsync(receiveLoop);
      }

      if (heartbeatLoop != null)
      {
        await ObserveTaskAsync(heartbeatLoop);
      }

      if (reconnectLoop != null)
      {
        await ObserveTaskAsync(reconnectLoop);
      }

      await DropTransportAsync(transport, "session disposed", startReconnect: false);

      connectGate.Dispose();
      sendGate.Dispose();
      lifetime.Dispose();
    }

    private async Task<bool> EnsureConnectedCoreAsync(CtraderRequestKind requestKind, CancellationToken cancellationToken)
    {
      if (requestKind == CtraderRequestKind.OrderPlacement && !IsAuthenticatedState())
      {
        return false;
      }

      if (IsOpen())
      {
        return true;
      }

      if (requestKind == CtraderRequestKind.OrderPlacement || IsDisposed())
      {
        return false;
      }

      await connectGate.WaitAsync(cancellationToken);
      try
      {
        if (IsOpen())
        {
          return true;
        }

        return await ConnectWithRetryAsync(cancellationToken);
      }
      finally
      {
        connectGate.Release();
      }
    }

    private async Task<bool> ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
      int maxAttempts = Math.Max(1, managerOptions.MaxReconnectAttempts);
      for (int attempt = 1; attempt <= maxAttempts; attempt++)
      {
        if (attempt > 1)
        {
          SetState(CtraderSessionState.Reconnecting, attempt - 1, health.LastError);
          await DelayBeforeReconnectAsync(attempt - 1, cancellationToken);
        }
        else
        {
          SetState(CtraderSessionState.Connecting, 0, null);
        }

        ICtraderWebSocketTransport candidate = transportFactory.Create();
        try
        {
          await candidate.ConnectAsync(CtraderEndpoint.ResolveUri(options.Environment), cancellationToken);
          transport = candidate;
          DateTime now = timeProvider.GetUtcNow().UtcDateTime;
          SetState(CtraderSessionState.Connected, attempt - 1, null, now);
          receiveLoop = Task.Run(() => ReceiveLoopAsync(candidate, lifetime.Token), CancellationToken.None);
          heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(candidate, lifetime.Token), CancellationToken.None);

          if (reauthenticate != null && !await reauthenticate(cancellationToken))
          {
            throw new InvalidOperationException("cTrader session re-authentication failed.");
          }

          if (reauthenticate != null)
          {
            SetState(CtraderSessionState.Authenticated, attempt - 1, null);
          }

          foreach (Func<CancellationToken, Task<bool>> restore in subscriptions.Values)
          {
            if (!await restore(cancellationToken))
            {
              throw new InvalidOperationException("cTrader subscription restoration failed.");
            }
          }

          Interlocked.Exchange(ref reconnectLoopStarted, 0);
          return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested)
        {
          await DisposeCandidateAsync(candidate);
          throw;
        }
        catch (Exception error) when (!(error is OperationCanceledException))
        {
          SetState(CtraderSessionState.Disconnected, attempt, error.Message);
          await DisposeCandidateAsync(candidate);
          if (attempt == maxAttempts)
          {
            SetState(CtraderSessionState.Faulted, attempt, error.Message);
            return false;
          }
        }
      }

      return false;
    }

    private async Task ReceiveLoopAsync(ICtraderWebSocketTransport current, CancellationToken cancellationToken)
    {
      byte[] buffer = new byte[ReceiveBufferBytes];
      try
      {
        while (!cancellationToken.IsCancellationRequested && current.State == WebSocketState.Open && ReferenceEquals(current, transport))
        {
          using MemoryStream frame = new MemoryStream();
          WebSocketReceiveResult received;
          do
          {
            received = await current.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close)
            {
              return;
            }

            frame.Write(buffer, 0, received.Count);
          }
          while (!received.EndOfMessage);

          SetLastReceived();
          Dispatch(ProtoMessage.Parser.ParseFrom(frame.ToArray()));
        }
      }
      catch (OperationCanceledException)
      {
        // Shutdown path.
      }
      catch (Exception error) when (error is WebSocketException || error is InvalidProtocolBufferException || error is IOException)
      {
        logger.LogWarning("Broker connection closed: {Message}", error.Message);
        SetLastError(error.Message);
      }
      finally
      {
        if (!cancellationToken.IsCancellationRequested && ReferenceEquals(current, transport))
        {
          await DropTransportAsync(current, "receive loop ended", startReconnect: true);
        }
      }
    }

    private async Task HeartbeatLoopAsync(ICtraderWebSocketTransport current, CancellationToken cancellationToken)
    {
      try
      {
        while (!cancellationToken.IsCancellationRequested && ReferenceEquals(current, transport))
        {
          await Task.Delay(managerOptions.HeartbeatInterval, cancellationToken);
          if (current.State != WebSocketState.Open)
          {
            return;
          }

          ProtoMessage heartbeat = new ProtoMessage { PayloadType = CtraderPayloadTypes.HeartbeatEvent };
          await SendFrameAsync(current, heartbeat.ToByteArray(), cancellationToken);
          lock (stateLock)
          {
            health.LastHeartbeatAtUtc = timeProvider.GetUtcNow().UtcDateTime;
          }
        }
      }
      catch (OperationCanceledException)
      {
        // Shutdown path.
      }
      catch (WebSocketException error)
      {
        logger.LogWarning("Broker heartbeat stopped: {Message}", error.Message);
        await DropTransportAsync(current, error.Message, startReconnect: true);
      }
    }

    private void Dispatch(ProtoMessage envelope)
    {
      if (envelope.HasClientMsgId
        && !string.IsNullOrEmpty(envelope.ClientMsgId)
        && pending.TryRemove(envelope.ClientMsgId, out TaskCompletionSource<ProtoMessage> completion))
      {
        completion.TrySetResult(envelope);
        return;
      }

      MessageReceived?.Invoke(envelope);
    }

    private async Task DropTransportAsync(ICtraderWebSocketTransport current, string error, bool startReconnect)
    {
      if (current == null)
      {
        return;
      }

      bool wasCurrent = ReferenceEquals(current, transport);
      if (wasCurrent)
      {
        transport = null;
        SetState(CtraderSessionState.Disconnected, health.ReconnectAttempts, error);
        foreach (KeyValuePair<string, TaskCompletionSource<ProtoMessage>> item in pending)
        {
          if (pending.TryRemove(item.Key, out TaskCompletionSource<ProtoMessage> completion))
          {
            completion.TrySetResult(null);
          }
        }
      }

      try
      {
        if (current.State == WebSocketState.Open)
        {
          await current.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnecting", CancellationToken.None);
        }
      }
      catch (WebSocketException)
      {
        // The peer has already gone away.
      }
      finally
      {
        await current.DisposeAsync();
      }

      if (wasCurrent && startReconnect && reauthenticate != null && !IsDisposed())
      {
        StartReconnectLoop();
      }
    }

    private void StartReconnectLoop()
    {
      if (Interlocked.Exchange(ref reconnectLoopStarted, 1) == 0)
      {
        reconnectLoop = Task.Run(async () =>
        {
          try
          {
            await connectGate.WaitAsync(lifetime.Token);
            try
            {
              if (!IsOpen() && !IsDisposed())
              {
                await ConnectWithRetryAsync(lifetime.Token);
              }
            }
            finally
            {
              connectGate.Release();
            }
          }
          catch (OperationCanceledException)
          {
            // Shutdown path.
          }
          finally
          {
            Interlocked.Exchange(ref reconnectLoopStarted, 0);
          }
        }, CancellationToken.None);
      }
    }

    private async Task SendFrameAsync(ICtraderWebSocketTransport current, byte[] payload, CancellationToken cancellationToken)
    {
      await sendGate.WaitAsync(cancellationToken);
      try
      {
        if (!ReferenceEquals(current, transport) || current.State != WebSocketState.Open)
        {
          throw new WebSocketException("The cTrader connection is no longer open.");
        }

        await current.SendAsync(new ArraySegment<byte>(payload), cancellationToken);
      }
      finally
      {
        sendGate.Release();
      }
    }

    private async Task DelayBeforeReconnectAsync(int attempt, CancellationToken cancellationToken)
    {
      double exponential = Math.Pow(2, Math.Max(0, attempt - 1));
      double rawMilliseconds = managerOptions.ReconnectBaseDelay.TotalMilliseconds * exponential;
      rawMilliseconds = Math.Min(rawMilliseconds, managerOptions.ReconnectMaxDelay.TotalMilliseconds);
      double jitter = 1 + ((randomSample() * 2) - 1) * Math.Clamp(managerOptions.ReconnectJitterRatio, 0, 1);
      TimeSpan delay = TimeSpan.FromMilliseconds(Math.Max(0, rawMilliseconds * jitter));
      await Task.Delay(delay, cancellationToken);
    }

    private bool IsOpen()
    {
      ICtraderWebSocketTransport current = transport;
      return !IsDisposed() && current != null && current.State == WebSocketState.Open;
    }

    private bool IsDisposed()
    {
      lock (stateLock)
      {
        return health.State == CtraderSessionState.Disposed;
      }
    }

    private bool IsAuthenticatedState()
    {
      lock (stateLock)
      {
        return health.State == CtraderSessionState.Authenticated;
      }
    }

    private void SetState(CtraderSessionState state, int reconnectAttempts, string error, DateTime? connectedAtUtc = null)
    {
      lock (stateLock)
      {
        health.State = state;
        health.ReconnectAttempts = reconnectAttempts;
        health.LastError = error;
        if (connectedAtUtc.HasValue)
        {
          health.ConnectedAtUtc = connectedAtUtc;
        }
      }
    }

    private void SetLastReceived()
    {
      lock (stateLock)
      {
        health.LastReceivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
      }
    }

    private void SetLastError(string error)
    {
      lock (stateLock)
      {
        health.LastError = error;
      }
    }

    private static async Task DisposeCandidateAsync(ICtraderWebSocketTransport candidate)
    {
      if (candidate != null)
      {
        await candidate.DisposeAsync();
      }
    }

    private static async Task ObserveTaskAsync(Task task)
    {
      try
      {
        await task;
      }
      catch (OperationCanceledException)
      {
        // Shutdown path.
      }
    }
  }
}
