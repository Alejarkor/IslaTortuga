using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IslaTortuga.Server.Core.Embedded;
using IslaTortuga.Server.Core.Protocol;
using IslaTortuga.Unity.Networking.Protocol;
using UnityEngine;

namespace IslaTortuga.Unity.Networking
{
    public sealed class EmbeddedServerNetworkingHost : IDisposable
    {
        private readonly EmbeddedGameServerHost _server;
        private readonly string _contentRoot;
        private readonly string _listenHost;
        private readonly int _listenPort;
        private readonly bool _serveContent;
        private readonly ConnectionManager _connectionManager;
        private readonly MessageDispatcher _messageDispatcher;
        private readonly ConcurrentQueue<InboundMessage> _inboundMessages;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private HttpListener _listener;
        private Task _listenLoopTask;
        private bool _isDisposed;

        public EmbeddedServerNetworkingHost(
            EmbeddedGameServerHost server,
            string contentRoot,
            string listenHost,
            int listenPort,
            bool serveContent)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _contentRoot = string.IsNullOrWhiteSpace(contentRoot) ? string.Empty : Path.GetFullPath(contentRoot);
            _listenHost = string.IsNullOrWhiteSpace(listenHost) ? "127.0.0.1" : listenHost;
            _listenPort = listenPort <= 0 ? 5055 : listenPort;
            _serveContent = serveContent;
            _connectionManager = new ConnectionManager();
            _messageDispatcher = new MessageDispatcher(_server, _connectionManager);
            _inboundMessages = new ConcurrentQueue<InboundMessage>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public bool IsRunning
        {
            get { return _listener != null && _listener.IsListening; }
        }

        public string BaseHttpUrl
        {
            get { return "http://" + _listenHost + ":" + _listenPort; }
        }

        public string WebSocketUrl
        {
            get { return "ws://" + _listenHost + ":" + _listenPort + "/ws/game"; }
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return;
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseHttpUrl + "/");
            _listener.Start();
            _listenLoopTask = Task.Run(() => ListenLoopAsync(_cancellationTokenSource.Token));
        }

        public void PumpInboundMessages()
        {
            ThrowIfDisposed();

            while (_inboundMessages.TryDequeue(out var message))
            {
                ObserveTask(_messageDispatcher.DispatchAsync(message.Connection, message.RawMessage, _cancellationTokenSource.Token));
            }
        }

        public void BroadcastSnapshots(IReadOnlyList<EmbeddedGameServerTickResult> snapshots)
        {
            ThrowIfDisposed();

            if (snapshots == null)
            {
                return;
            }

            for (var index = 0; index < snapshots.Count; index++)
            {
                var tickResult = snapshots[index];
                if (!_connectionManager.TryGetBySessionId(tickResult.SessionId, out var connection) || connection == null)
                {
                    continue;
                }

                ObserveTask(connection.SendAsync(
                    ProtocolTypes.WorldSnapshot,
                    tickResult.Snapshot,
                    null,
                    _cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            if (_isDisposed)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch
                {
                }

                _listener = null;
            }

            foreach (var connection in _connectionManager.GetAll())
            {
                connection.Dispose();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Stop();
            _cancellationTokenSource.Dispose();
            _isDisposed = true;
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    break;
                }

                ObserveTask(HandleRequestAsync(context, cancellationToken));
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url == null ? "/" : request.Url.AbsolutePath;

            if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(
                    response,
                    "{\"status\":\"ok\",\"server\":\"IslaTortuga.Unity\",\"mode\":\"embedded\",\"utcNow\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}",
                    cancellationToken);
                return;
            }

            if (string.Equals(path, "/ws/game", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.IsWebSocketRequest)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Close();
                    return;
                }

                await AcceptWebSocketAsync(context, cancellationToken);
                return;
            }

            if (_serveContent && path.StartsWith("/content/", StringComparison.OrdinalIgnoreCase))
            {
                var handled = await TryServeContentAsync(response, path, cancellationToken);
                if (handled)
                {
                    return;
                }
            }

            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
        }

        private async Task AcceptWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            HttpListenerWebSocketContext webSocketContext;
            try
            {
                webSocketContext = await context.AcceptWebSocketAsync(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.Close();
                return;
            }

            var connection = _connectionManager.Add(webSocketContext.WebSocket);

            try
            {
                await ReceiveLoopAsync(connection, cancellationToken);
            }
            finally
            {
                _server.MarkDisconnected(connection.ConnectionId);
                _connectionManager.Remove(connection.ConnectionId, out _);
                connection.Dispose();
            }
        }

        private async Task ReceiveLoopAsync(ClientConnection connection, CancellationToken cancellationToken)
        {
            var buffer = new byte[8 * 1024];

            while (!cancellationToken.IsCancellationRequested && connection.Socket.State == WebSocketState.Open)
            {
                using (var messageStream = new MemoryStream())
                {
                    WebSocketReceiveResult receiveResult;

                    do
                    {
                        receiveResult = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        await messageStream.WriteAsync(buffer, 0, receiveResult.Count, cancellationToken);
                    }
                    while (!receiveResult.EndOfMessage);

                    var message = Encoding.UTF8.GetString(messageStream.ToArray());
                    _inboundMessages.Enqueue(new InboundMessage(connection, message));
                }
            }
        }

        private async Task<bool> TryServeContentAsync(HttpListenerResponse response, string fullPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_contentRoot) || !Directory.Exists(_contentRoot))
            {
                return false;
            }

            var contentPrefix = "/content/";
            var relativePath = Uri.UnescapeDataString(fullPath.Substring(contentPrefix.Length))
                .Replace('/', Path.DirectorySeparatorChar);

            var candidatePath = Path.GetFullPath(Path.Combine(_contentRoot, relativePath));
            if (!candidatePath.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidatePath))
            {
                return false;
            }

            var bytes = await File.ReadAllBytesAsync(candidatePath, cancellationToken);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = ResolveContentType(candidatePath);
            response.ContentLength64 = bytes.LongLength;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            response.OutputStream.Close();
            response.Close();
            return true;
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, string json, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.LongLength;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            response.OutputStream.Close();
            response.Close();
        }

        private static string ResolveContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".json":
                case ".tmj":
                    return "application/json";
                case ".tsx":
                case ".xml":
                    return "application/xml";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                default:
                    return "application/octet-stream";
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(EmbeddedServerNetworkingHost));
            }
        }

        private static void ObserveTask(Task task)
        {
            task.ContinueWith(
                continuation =>
                {
                    if (continuation.Exception != null)
                    {
                        Debug.LogException(continuation.Exception.GetBaseException());
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private sealed class ConnectionManager
        {
            private readonly ConcurrentDictionary<string, ClientConnection> _connections = new ConcurrentDictionary<string, ClientConnection>();
            private readonly ConcurrentDictionary<string, string> _connectionIdsBySessionId = new ConcurrentDictionary<string, string>();

            public ClientConnection Add(WebSocket socket)
            {
                var connection = new ClientConnection(socket);
                _connections[connection.ConnectionId] = connection;
                return connection;
            }

            public void BindSession(ClientConnection connection, string sessionId)
            {
                connection.BindSession(sessionId);
                _connectionIdsBySessionId[sessionId] = connection.ConnectionId;
            }

            public bool TryGetBySessionId(string sessionId, out ClientConnection connection)
            {
                connection = null;

                string connectionId;
                if (!_connectionIdsBySessionId.TryGetValue(sessionId, out connectionId))
                {
                    return false;
                }

                return _connections.TryGetValue(connectionId, out connection);
            }

            public IReadOnlyCollection<ClientConnection> GetAll()
            {
                return _connections.Values.ToArray();
            }

            public bool Remove(string connectionId, out ClientConnection connection)
            {
                if (!_connections.TryRemove(connectionId, out connection))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(connection.SessionId))
                {
                    string mappedConnectionId;
                    if (_connectionIdsBySessionId.TryGetValue(connection.SessionId, out mappedConnectionId) &&
                        string.Equals(mappedConnectionId, connection.ConnectionId, StringComparison.Ordinal))
                    {
                        _connectionIdsBySessionId.TryRemove(connection.SessionId, out _);
                    }
                }

                return true;
            }
        }

        private sealed class ClientConnection : IDisposable
        {
            private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

            public ClientConnection(WebSocket socket)
            {
                Socket = socket;
            }

            public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

            public WebSocket Socket { get; }

            public string SessionId { get; private set; }

            public void BindSession(string sessionId)
            {
                SessionId = sessionId;
            }

            public async Task SendAsync(string op, object payload, string requestId, CancellationToken cancellationToken)
            {
                var json = ProtocolEnvelopeJson.SerializeEnvelope(op, payload, requestId);
                var buffer = Encoding.UTF8.GetBytes(json);

                await _sendLock.WaitAsync(cancellationToken);
                try
                {
                    if (Socket.State != WebSocketState.Open)
                    {
                        return;
                    }

                    await Socket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        cancellationToken);
                }
                finally
                {
                    _sendLock.Release();
                }
            }

            public void Dispose()
            {
                try
                {
                    if (Socket.State == WebSocketState.Open || Socket.State == WebSocketState.CloseReceived)
                    {
                        Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None).Wait(1000);
                    }
                }
                catch
                {
                }

                Socket.Dispose();
                _sendLock.Dispose();
            }
        }

        private sealed class MessageDispatcher
        {
            private readonly EmbeddedGameServerHost _server;
            private readonly ConnectionManager _connectionManager;

            public MessageDispatcher(EmbeddedGameServerHost server, ConnectionManager connectionManager)
            {
                _server = server;
                _connectionManager = connectionManager;
            }

            public async Task DispatchAsync(ClientConnection connection, string rawMessage, CancellationToken cancellationToken)
            {
                IncomingEnvelope envelope;
                string errorCode;
                string errorMessage;
                if (!ProtocolEnvelopeJson.TryParseEnvelope(rawMessage, out envelope, out errorCode, out errorMessage))
                {
                    await connection.SendAsync(
                        ProtocolTypes.Error,
                        new ErrorPayload(errorCode, errorMessage, false),
                        null,
                        cancellationToken);
                    return;
                }

                switch (envelope.Op)
                {
                    case ProtocolTypes.AuthJoin:
                        await HandleJoinAsync(connection, envelope, cancellationToken);
                        break;
                    case ProtocolTypes.AuthReconnect:
                        await HandleReconnectAsync(connection, envelope, cancellationToken);
                        break;
                    case ProtocolTypes.PlayerInput:
                        await HandlePlayerInputAsync(connection, envelope, cancellationToken);
                        break;
                    case ProtocolTypes.Ping:
                        await connection.SendAsync(ProtocolTypes.Pong, null, envelope.RequestId, cancellationToken);
                        break;
                    default:
                        await connection.SendAsync(
                            ProtocolTypes.Error,
                            new ErrorPayload("unknown_op", "Unsupported operation '" + envelope.Op + "'.", false),
                            envelope.RequestId,
                            cancellationToken);
                        break;
                }
            }

            private async Task HandleJoinAsync(ClientConnection connection, IncomingEnvelope envelope, CancellationToken cancellationToken)
            {
                JoinGamePayload payload;
                if (!ProtocolEnvelopeJson.TryDeserializeJoinPayload(envelope.PayloadJson, out payload))
                {
                    await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
                    return;
                }

                EmbeddedGameServerJoinResult result;
                string errorCode;
                if (!_server.TryJoin(payload.GameTicket, connection.ConnectionId, out result, out errorCode))
                {
                    await connection.SendAsync(
                        ProtocolTypes.AuthRejected,
                        new ErrorPayload(errorCode, "The game ticket is invalid or expired.", true),
                        envelope.RequestId,
                        cancellationToken);
                    return;
                }

                _connectionManager.BindSession(connection, result.Auth.SessionId);

                await connection.SendAsync(ProtocolTypes.AuthAccepted, result.Auth, envelope.RequestId, cancellationToken);
                await connection.SendAsync(ProtocolTypes.WorldSnapshot, result.Snapshot, null, cancellationToken);
            }

            private async Task HandleReconnectAsync(ClientConnection connection, IncomingEnvelope envelope, CancellationToken cancellationToken)
            {
                ReconnectPayload payload;
                if (!ProtocolEnvelopeJson.TryDeserializeReconnectPayload(envelope.PayloadJson, out payload))
                {
                    await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
                    return;
                }

                EmbeddedGameServerJoinResult result;
                string errorCode;
                if (!_server.TryReconnect(payload.GameTicket, connection.ConnectionId, out result, out errorCode))
                {
                    await connection.SendAsync(
                        ProtocolTypes.AuthRejected,
                        new ErrorPayload(errorCode, "The reconnect ticket is invalid or expired.", true),
                        envelope.RequestId,
                        cancellationToken);
                    return;
                }

                _connectionManager.BindSession(connection, result.Auth.SessionId);

                await connection.SendAsync(ProtocolTypes.AuthAccepted, result.Auth, envelope.RequestId, cancellationToken);
                await connection.SendAsync(ProtocolTypes.WorldSnapshot, result.Snapshot, null, cancellationToken);
            }

            private async Task HandlePlayerInputAsync(ClientConnection connection, IncomingEnvelope envelope, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(connection.SessionId))
                {
                    await connection.SendAsync(
                        ProtocolTypes.Error,
                        new ErrorPayload("not_authenticated", "Join the game before sending input.", false),
                        envelope.RequestId,
                        cancellationToken);
                    return;
                }

                PlayerInputPayload payload;
                if (!ProtocolEnvelopeJson.TryDeserializePlayerInputPayload(envelope.PayloadJson, out payload))
                {
                    await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
                    return;
                }

                if (!_server.ApplyPlayerInput(connection.SessionId, payload.MoveX, payload.MoveY))
                {
                    await connection.SendAsync(
                        ProtocolTypes.Error,
                        new ErrorPayload("session_not_found", "The player session no longer exists.", true),
                        envelope.RequestId,
                        cancellationToken);
                }
            }

            private static Task SendInvalidPayloadAsync(ClientConnection connection, IncomingEnvelope envelope, CancellationToken cancellationToken)
            {
                return connection.SendAsync(
                    ProtocolTypes.Error,
                    new ErrorPayload("invalid_payload", "Invalid payload for operation '" + envelope.Op + "'.", false),
                    envelope.RequestId,
                    cancellationToken);
            }
        }

        private sealed class InboundMessage
        {
            public InboundMessage(ClientConnection connection, string rawMessage)
            {
                Connection = connection;
                RawMessage = rawMessage;
            }

            public ClientConnection Connection { get; }

            public string RawMessage { get; }
        }
    }
}
