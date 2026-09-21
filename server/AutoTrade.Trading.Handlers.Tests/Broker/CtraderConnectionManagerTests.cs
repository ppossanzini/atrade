using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
    public class CtraderConnectionManagerTests
    {
        [Fact]
        public async Task Send_UsesOneSerializedWireWriterAndCorrelatesResponses()
        {
            FakeTransport transport = new FakeTransport { ResponsePayloadType = 999 };
            CtraderConnectionManager manager = CreateManager(new SingleTransportFactory(transport));

            Task<ProtoMessage>[] requests = new Task<ProtoMessage>[5];
            for (int index = 0; index < requests.Length; index++)
            {
                requests[index] = manager.SendAsync(100 + (uint)index, new ProtoMessage(), CtraderRequestKind.ReadOnly, CancellationToken.None);
            }

            ProtoMessage[] responses = await Task.WhenAll(requests);

            Assert.All(responses, response => Assert.Equal(999u, response.PayloadType));
            Assert.Equal(1, transport.MaxConcurrentSends);
            Assert.Equal(CtraderSessionState.Connected, manager.Health.State);

            await manager.DisposeAsync();
        }

        [Fact]
        public async Task OrderPlacement_WhenDisconnected_FailsClosedWithoutConnectingOrRetrying()
        {
            CountingTransportFactory factory = new CountingTransportFactory();
            CtraderConnectionManager manager = CreateManager(factory);

            ProtoMessage response = await manager.SendAsync(
              CtraderPayloadTypes.NewOrderRequest,
              new ProtoOANewOrderReq(),
              CtraderRequestKind.OrderPlacement,
              CancellationToken.None);

            Assert.Null(response);
            Assert.Equal(0, factory.Created);

            await manager.DisposeAsync();
        }

        [Fact]
        public async Task Reconnect_RunsReauthenticationAndRestoresSubscriptions()
        {
            FakeTransport first = new FakeTransport { ResponsePayloadType = 501 };
            FakeTransport second = new FakeTransport { ResponsePayloadType = 502 };
            SequenceTransportFactory factory = new SequenceTransportFactory(first, second);
            CtraderConnectionManager manager = CreateManager(factory);
            int reauthenticationCount = 0;
            int restoreCount = 0;

            ProtoMessage firstResponse = await manager.SendAsync(300, new ProtoMessage(), CtraderRequestKind.ReadOnly, CancellationToken.None);
            Assert.Equal(501u, firstResponse.PayloadType);

            manager.SetReauthentication(cancellationToken =>
            {
                reauthenticationCount++;
                return Task.FromResult(true);
            });
            manager.RegisterSubscription("quotes", cancellationToken =>
            {
                restoreCount++;
                return Task.FromResult(true);
            });

            first.CloseFromPeer();
            await WaitUntilAsync(() => reauthenticationCount == 1 && restoreCount == 1, TimeSpan.FromSeconds(2));

            ProtoMessage secondResponse = await manager.SendAsync(301, new ProtoMessage(), CtraderRequestKind.ReadOnly, CancellationToken.None);
            Assert.Equal(502u, secondResponse.PayloadType);
            Assert.Equal(CtraderSessionState.Authenticated, manager.Health.State);

            await manager.DisposeAsync();
        }

        private static CtraderConnectionManager CreateManager(ICtraderWebSocketTransportFactory factory)
        {
            return new CtraderConnectionManager(
              new BrokerOptions { Environment = TradingEnvironment.Demo },
              NullLogger.Instance,
              factory,
              new CtraderConnectionManagerOptions
              {
                  HeartbeatInterval = TimeSpan.FromHours(1),
                  RequestTimeout = TimeSpan.FromSeconds(2),
                  ReconnectBaseDelay = TimeSpan.Zero,
                  ReconnectMaxDelay = TimeSpan.Zero,
                  MaxReconnectAttempts = 2,
                  ReconnectJitterRatio = 0
              },
              TimeProvider.System,
              () => 0.5);
        }

        private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (!predicate() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.True(predicate());
        }

        private sealed class SingleTransportFactory(FakeTransport transport) : ICtraderWebSocketTransportFactory
        {
            public ICtraderWebSocketTransport Create()
            {
                return transport;
            }
        }

        private sealed class CountingTransportFactory : ICtraderWebSocketTransportFactory
        {
            public int Created { get; private set; }

            public ICtraderWebSocketTransport Create()
            {
                Created++;
                return new FakeTransport();
            }
        }

        private sealed class SequenceTransportFactory(params FakeTransport[] transports) : ICtraderWebSocketTransportFactory
        {
            private int index;

            public ICtraderWebSocketTransport Create()
            {
                return transports[Math.Min(Interlocked.Increment(ref index) - 1, transports.Length - 1)];
            }
        }

        private sealed class FakeTransport : ICtraderWebSocketTransport
        {
            private readonly SemaphoreSlim receivedSignal = new SemaphoreSlim(0);
            private readonly ConcurrentQueue<WebSocketReceiveResult> receiveResults = new ConcurrentQueue<WebSocketReceiveResult>();
            private readonly object sendLock = new object();
            private bool disposed;
            private int activeSends;

            public uint ResponsePayloadType { get; set; }

            public int MaxConcurrentSends { get; private set; }

            public WebSocketState State { get; private set; } = WebSocketState.None;

            public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
            {
                State = WebSocketState.Open;
                return Task.CompletedTask;
            }

            public async Task SendAsync(ArraySegment<byte> payload, CancellationToken cancellationToken)
            {
                int concurrent = Interlocked.Increment(ref activeSends);
                MaxConcurrentSends = Math.Max(MaxConcurrentSends, concurrent);
                try
                {
                    await Task.Delay(5, cancellationToken);
                    ProtoMessage request = ProtoMessage.Parser.ParseFrom(payload.Array, payload.Offset, payload.Count);
                    ProtoMessage response = new ProtoMessage
                    {
                        ClientMsgId = request.ClientMsgId,
                        PayloadType = ResponsePayloadType
                    };
                    byte[] bytes = response.ToByteArray();
                    lock (sendLock)
                    {
                        ArraySegment<byte> segment = new ArraySegment<byte>(bytes);
                        receiveResults.Enqueue(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Binary, true));
                        responseBytes.Enqueue(segment);
                    }
                    receivedSignal.Release();
                }
                finally
                {
                    Interlocked.Decrement(ref activeSends);
                }
            }

            private readonly ConcurrentQueue<ArraySegment<byte>> responseBytes = new ConcurrentQueue<ArraySegment<byte>>();

            public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                await receivedSignal.WaitAsync(cancellationToken);
                if (receiveResults.TryDequeue(out WebSocketReceiveResult result)
                  && responseBytes.TryDequeue(out ArraySegment<byte> bytes))
                {
                    Array.Copy(bytes.Array, bytes.Offset, buffer.Array, buffer.Offset, bytes.Count);
                    return result;
                }

                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }

            public void CloseFromPeer()
            {
                State = WebSocketState.CloseReceived;
                receiveResults.Enqueue(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
                receivedSignal.Release();
            }

            public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                State = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!disposed)
                {
                    disposed = true;
                    State = WebSocketState.Closed;
                    receivedSignal.Dispose();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
