using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Match;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Puerta de entrada realtime: un endpoint WebSocket en el GatewayPort,
    /// implementado sobre TcpListener + handshake/framing a mano (RFC 6455), porque el
    /// WebSocket de HttpListener no funciona en el Mono del editor de Unity. Valida el
    /// ticket, liga al jugador a su MatchInstance, hace el handshake
    /// MATCH_WELCOME -> CLIENT_READY_FOR_SNAPSHOT y, desde la Fase 4, spawnea/despawnea
    /// la entidad del jugador y la difunde.
    ///
    /// HandleSessionAsync está separado del aceptado del socket para poder probarlo
    /// con un ITransport en memoria.
    /// </summary>
    public sealed class PlayerGateway
    {
        public const string MsgMatchWelcome = "MATCH_WELCOME";
        public const string MsgClientReady = "CLIENT_READY_FOR_SNAPSHOT";
        public const string MsgPlayerInput = "PLAYER_INPUT";
        private const string ConnectionsCounter = "gateway_connections_total";
        private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly ServerConfig _config;
        private readonly MatchOrchestrator _orchestrator;
        private readonly ITicketValidator _validator;
        private readonly PlayerSessionManager _sessions;
        private readonly IServerLogger _logger;
        private readonly MetricsRegistry _metrics;

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoop;
        private readonly object _gate = new object();

        public bool IsRunning { get; private set; }

        public PlayerGateway(
            ServerConfig config,
            MatchOrchestrator orchestrator,
            ITicketValidator validator,
            PlayerSessionManager sessions,
            IServerLogger logger,
            MetricsRegistry metrics = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics;
        }

        public void Start()
        {
            lock (_gate)
            {
                if (IsRunning) return;
                var local = _config.ControlHost == "localhost" || _config.ControlHost == "127.0.0.1"
                    ? IPAddress.Any   // acepta 127.0.0.1 y la LAN
                    : IPAddress.Any;
                _listener = new TcpListener(local, _config.GatewayPort);
                _listener.Start();
                _cts = new CancellationTokenSource();
                IsRunning = true;
                var listener = _listener;
                var token = _cts.Token;
                _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, token));
                _logger.Info($"PlayerGateway (WebSocket/TCP) escuchando en ws://{_config.ControlHost}:{_config.GatewayPort}/");
            }
        }

        public async Task StopAsync()
        {
            Task loop;
            TcpListener listener;
            CancellationTokenSource cts;
            lock (_gate)
            {
                if (!IsRunning) return;
                IsRunning = false;
                loop = _acceptLoop;
                listener = _listener;
                cts = _cts;
                _acceptLoop = null;
                _listener = null;
                _cts = null;
            }
            try
            {
                cts?.Cancel();
                listener?.Stop();
                if (loop != null) await loop.ConfigureAwait(false);
            }
            finally
            {
                cts?.Dispose();
                _logger.Info("PlayerGateway detenido.");
            }
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (InvalidOperationException) { break; }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();

                var request = await ReadHttpHeadersAsync(stream).ConfigureAwait(false);
                if (request == null)
                {
                    client.Close();
                    return;
                }

                string wsKey = null;
                bool isUpgrade = false;
                string target = "/";
                var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length > 0)
                {
                    var parts = lines[0].Split(' ');
                    if (parts.Length >= 2) target = parts[1];
                }
                foreach (var line in lines)
                {
                    var idx = line.IndexOf(':');
                    if (idx <= 0) continue;
                    var name = line.Substring(0, idx).Trim().ToLowerInvariant();
                    var val = line.Substring(idx + 1).Trim();
                    if (name == "sec-websocket-key") wsKey = val;
                    if (name == "upgrade" && val.ToLowerInvariant().Contains("websocket")) isUpgrade = true;
                }

                if (!isUpgrade || wsKey == null)
                {
                    var bad = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(bad, 0, bad.Length).ConfigureAwait(false);
                    client.Close();
                    return;
                }

                var accept = ComputeAccept(wsKey);
                var handshake =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
                var hb = Encoding.ASCII.GetBytes(handshake);
                await stream.WriteAsync(hb, 0, hb.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                var ticket = ParseTicket(target);
                var transport = new RawWebSocketTransport(stream, client);
                await HandleSessionAsync(transport, ticket).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("Fallo en la conexión WebSocket.", ex);
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static string ComputeAccept(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + WsGuid));
                return Convert.ToBase64String(hash);
            }
        }

        private static string ParseTicket(string target)
        {
            var q = target.IndexOf('?');
            if (q < 0) return null;
            var query = target.Substring(q + 1);
            foreach (var pair in query.Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2 && kv[0] == "ticket")
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            return null;
        }

        private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream)
        {
            var all = new List<byte>(1024);
            var buf = new byte[1];
            while (all.Count < 16384)
            {
                int r;
                try { r = await stream.ReadAsync(buf, 0, 1).ConfigureAwait(false); }
                catch { return null; }
                if (r <= 0) return null;
                all.Add(buf[0]);
                int n = all.Count;
                if (n >= 4 && all[n - 4] == 13 && all[n - 3] == 10 && all[n - 2] == 13 && all[n - 1] == 10)
                {
                    break;
                }
            }
            return Encoding.ASCII.GetString(all.ToArray());
        }

        /// <summary>Núcleo testeable del ciclo de una sesión sobre un transporte cualquiera.</summary>
        public async Task HandleSessionAsync(ITransport transport, string ticketId)
        {
            var ticket = await _validator.ValidateAndConsumeAsync(ticketId).ConfigureAwait(false);
            if (ticket == null)
            {
                _logger.Warn("Conexión rechazada: ticket inválido o ya consumido.");
                await transport.CloseAsync("invalid ticket").ConfigureAwait(false);
                return;
            }

            var match = _orchestrator.GetMatch(ticket.MatchId);
            if (match == null)
            {
                _logger.Warn($"Conexión rechazada: la partida {ticket.MatchId} no existe.");
                await transport.CloseAsync("match not found").ConfigureAwait(false);
                return;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var session = new PlayerSession(sessionId, ticket.PlayerId, ticket.MatchId, transport);
            _sessions.Add(session);
            match.AddPlayer(ticket.PlayerId, sessionId);
            _metrics?.IncrementCounter(ConnectionsCounter);
            _logger.Info($"Jugador {ticket.PlayerId} conectado a {ticket.MatchId} (sesión {sessionId}).");

            try
            {
                await transport.SendAsync(BuildWelcome(match, ticket.PlayerId)).ConfigureAwait(false);

                while (true)
                {
                    var text = await transport.ReceiveAsync().ConfigureAwait(false);
                    if (text == null) break;

                    NetMessage msg;
                    try { msg = MessageCodec.Decode(text); }
                    catch (Exception) { continue; }

                    if (msg.Type == MsgClientReady && session.State == SessionState.Connecting)
                    {
                        session.State = SessionState.Connected;
                        _logger.Info($"Jugador {ticket.PlayerId} listo (handshake completo).");
                        await OnPlayerReadyAsync(session, match).ConfigureAwait(false);
                    }
                    else if (msg.Type == MsgPlayerInput && session.State == SessionState.Connected)
                    {
                        var rt = match.Runtime;
                        if (rt != null && msg.Payload != null)
                        {
                            var seq = (long)GetDouble(msg.Payload, "seq");
                            var mx = (float)GetDouble(msg.Payload, "moveX");
                            var mz = (float)GetDouble(msg.Payload, "moveZ");
                            rt.Input.SetInput(ticket.PlayerId, seq, mx, mz);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error en la sesión {sessionId}.", ex);
            }
            finally
            {
                session.State = SessionState.Disconnected;

                var rt = match.Runtime;
                if (rt != null && session.EntityId != null)
                {
                    rt.Despawn.Despawn(session.EntityId);
                    await BroadcastToMatchAsync(
                        session.MatchId,
                        NetworkMessages.DespawnEntity(session.EntityId),
                        session.SessionId).ConfigureAwait(false);
                }

                match.RemovePlayer(ticket.PlayerId);
                _sessions.Remove(sessionId);
                await transport.CloseAsync().ConfigureAwait(false);
                _logger.Info($"Jugador {ticket.PlayerId} desconectado de {ticket.MatchId}.");
            }
        }

        private async Task OnPlayerReadyAsync(PlayerSession session, MatchInstance match)
        {
            var rt = match.Runtime;
            if (rt == null) return;

            // Conecta la salida del runtime (STATE_DELTA por tick) a las sesiones.
            rt.Broadcaster = (m) => BroadcastStateToMatch(session.MatchId, m);

            var entity = rt.Spawn.SpawnPlayer(session.PlayerId);
            session.EntityId = entity.Id;

            foreach (var e in rt.World.All())
            {
                await session.Transport.SendAsync(NetworkMessages.SpawnEntity(e)).ConfigureAwait(false);
            }

            await BroadcastToMatchAsync(
                session.MatchId,
                NetworkMessages.SpawnEntity(entity),
                session.SessionId).ConfigureAwait(false);
        }

        private async Task BroadcastToMatchAsync(string matchId, string message, string exceptSessionId)
        {
            foreach (var s in _sessions.ForMatch(matchId))
            {
                if (s.SessionId == exceptSessionId) continue;
                if (!s.Transport.IsOpen) continue;
                try { await s.Transport.SendAsync(message).ConfigureAwait(false); }
                catch (Exception ex) { _logger.Error("Error difundiendo a una sesión.", ex); }
            }
        }

        private static double GetDouble(System.Collections.Generic.IDictionary<string, object> payload, string key)
        {
            return payload != null && payload.TryGetValue(key, out var v) && v is double d ? d : 0d;
        }

        /// <summary>Difunde un mensaje (p. ej. STATE_DELTA) a TODAS las sesiones de la partida (fire-and-forget).</summary>
        private void BroadcastStateToMatch(string matchId, string message)
        {
            foreach (var s in _sessions.ForMatch(matchId))
            {
                if (!s.Transport.IsOpen) continue;
                _ = s.Transport.SendAsync(message);
            }
        }

        private static string BuildWelcome(MatchInstance match, string playerId)
        {
            var payload =
                "{" +
                "\"playerId\":" + Json.Str(playerId) + "," +
                "\"matchId\":" + Json.Str(match.MatchId) + "," +
                "\"mapId\":" + Json.Str(match.Config.MapId) + "," +
                "\"requiredManifestVersion\":" + Json.Str("1.0.0") + "," +
                "\"requiredAssetPackIds\":[" + Json.Str("base") + "]" +
                "}";
            return MessageCodec.Encode(MsgMatchWelcome, payload);
        }
    }
}
