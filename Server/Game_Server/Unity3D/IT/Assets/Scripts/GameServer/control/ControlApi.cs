using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Match;

namespace IslaTortuga.GameServer.Control
{
    /// <summary>
    /// Plano de control HTTP del Game Server. Expone endpoints de operación —no de
    /// juego—: estado del host (/health), capacidad (/capacity) y gestión de partidas
    /// (/control/create-match, /control/stop-match). Implementado sobre HttpListener,
    /// sin framework web, para mantener el servidor ligero.
    ///
    /// Es deliberadamente independiente de UnityEngine: se puede arrancar y probar
    /// en modo headless.
    /// </summary>
    public sealed class ControlApi
    {
        private readonly ServerConfig _config;
        private readonly CapacityManager _capacity;
        private readonly IServerLogger _logger;
        private readonly Func<double> _uptimeSecondsProvider;
        private readonly MatchOrchestrator _orchestrator;

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoop;
        private readonly object _gate = new object();

        public bool IsRunning { get; private set; }

        public string Prefix => $"http://{_config.ControlHost}:{_config.ControlPort}/";

        public ControlApi(
            ServerConfig config,
            CapacityManager capacity,
            IServerLogger logger,
            Func<double> uptimeSecondsProvider = null,
            MatchOrchestrator orchestrator = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orchestrator = orchestrator;

            if (uptimeSecondsProvider == null)
            {
                var stopwatch = Stopwatch.StartNew();
                _uptimeSecondsProvider = () => stopwatch.Elapsed.TotalSeconds;
            }
            else
            {
                _uptimeSecondsProvider = uptimeSecondsProvider;
            }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (IsRunning)
                {
                    return;
                }

                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();

                _cts = new CancellationTokenSource();
                IsRunning = true;

                var listener = _listener;
                var token = _cts.Token;
                _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, token));

                _logger.Info($"ControlApi escuchando en {Prefix} (rutas: /health, /capacity, /control/create-match, /control/stop-match)");
            }
        }

        public async Task StopAsync()
        {
            Task loop;
            HttpListener listener;
            CancellationTokenSource cts;

            lock (_gate)
            {
                if (!IsRunning)
                {
                    return;
                }

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

                if (loop != null)
                {
                    await loop.ConfigureAwait(false);
                }
            }
            finally
            {
                listener?.Close();
                cts?.Dispose();
                _logger.Info("ControlApi detenida y puerto liberado.");
            }
        }

        private async Task AcceptLoopAsync(HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error procesando petición de control.", ex);
                    TryWriteError(context);
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var method = request.HttpMethod.ToUpperInvariant();
            var path = request.Url.AbsolutePath.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            switch (path)
            {
                case "/health":
                    if (RequireMethod(context, method, "GET")) WriteJson(context, 200, BuildHealthJson());
                    break;
                case "/capacity":
                    if (RequireMethod(context, method, "GET")) WriteJson(context, 200, BuildCapacityJson());
                    break;
                case "/control/create-match":
                    if (RequireMethod(context, method, "POST")) HandleCreateMatch(context);
                    break;
                case "/control/stop-match":
                    if (RequireMethod(context, method, "POST")) HandleStopMatch(context);
                    break;
                default:
                    WriteJson(context, 404, Json.Object(new[]
                    {
                        Field("ok", Json.Bool(false)),
                        Field("error", Json.Str("not found"))
                    }));
                    break;
            }
        }

        private bool RequireMethod(HttpListenerContext context, string actual, string expected)
        {
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            WriteJson(context, 405, Json.Object(new[]
            {
                Field("ok", Json.Bool(false)),
                Field("error", Json.Str("method not allowed"))
            }));
            return false;
        }

        /// <summary>Comprueba el token de control si está configurado. Devuelve false (y escribe 401) si falta o no coincide.</summary>
        private bool Authorized(HttpListenerContext context)
        {
            if (string.IsNullOrEmpty(_config.ControlToken))
            {
                return true;
            }
            var provided = context.Request.Headers["x-control-token"];
            if (string.Equals(provided, _config.ControlToken, StringComparison.Ordinal))
            {
                return true;
            }
            WriteJson(context, 401, Json.Object(new[]
            {
                Field("ok", Json.Bool(false)),
                Field("error", Json.Str("invalid control token"))
            }));
            return false;
        }

        private void HandleCreateMatch(HttpListenerContext context)
        {
            if (!Authorized(context)) return;

            if (_orchestrator == null)
            {
                WriteJson(context, 503, Json.Object(new[]
                {
                    Field("ok", Json.Bool(false)),
                    Field("error", Json.Str("orchestrator not available"))
                }));
                return;
            }

            Dictionary<string, object> body;
            try
            {
                body = JsonReader.Parse(ReadBody(context.Request)) as Dictionary<string, object>;
            }
            catch (Exception)
            {
                body = null;
            }

            if (body == null)
            {
                WriteJson(context, 400, Json.Object(new[]
                {
                    Field("ok", Json.Bool(false)),
                    Field("error", Json.Str("invalid json body"))
                }));
                return;
            }

            var maxPlayers = JsonReader.GetInt(body, "maxPlayers", _config.MaxPlayersPerMatch);
            var mapId = JsonReader.GetString(body, "mapId", string.Empty);
            var players = JsonReader.GetStringList(body, "players");
            var config = new MatchConfig(maxPlayers, mapId, players);

            var instance = _orchestrator.CreateMatch(config);
            if (instance == null)
            {
                WriteJson(context, 409, Json.Object(new[]
                {
                    Field("ok", Json.Bool(false)),
                    Field("error", Json.Str("no capacity"))
                }));
                return;
            }

            WriteJson(context, 200, Json.Object(new[]
            {
                Field("ok", Json.Bool(true)),
                Field("matchId", Json.Str(instance.MatchId)),
                Field("gatewayHost", Json.Str(_config.ControlHost)),
                Field("gatewayPort", Json.Num(_config.GatewayPort))
            }));
        }

        private void HandleStopMatch(HttpListenerContext context)
        {
            if (!Authorized(context)) return;

            if (_orchestrator == null)
            {
                WriteJson(context, 503, Json.Object(new[]
                {
                    Field("ok", Json.Bool(false)),
                    Field("error", Json.Str("orchestrator not available"))
                }));
                return;
            }

            Dictionary<string, object> body;
            try
            {
                body = JsonReader.Parse(ReadBody(context.Request)) as Dictionary<string, object>;
            }
            catch (Exception)
            {
                body = null;
            }

            var matchId = JsonReader.GetString(body, "matchId", string.Empty);
            if (string.IsNullOrWhiteSpace(matchId))
            {
                WriteJson(context, 400, Json.Object(new[]
                {
                    Field("ok", Json.Bool(false)),
                    Field("error", Json.Str("matchId required"))
                }));
                return;
            }

            var stopped = _orchestrator.StopMatch(matchId);
            WriteJson(context, stopped ? 200 : 404, Json.Object(new[]
            {
                Field("ok", Json.Bool(stopped)),
                Field("error", stopped ? "null" : Json.Str("match not found"))
            }));
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private string BuildHealthJson()
        {
            return Json.Object(new[]
            {
                Field("status", Json.Str("ok")),
                Field("service", Json.Str("game-server")),
                Field("uptimeSeconds", Json.Num(Math.Round(_uptimeSecondsProvider(), 3)))
            });
        }

        private string BuildCapacityJson()
        {
            var snapshot = _capacity.Snapshot();
            return Json.Object(new[]
            {
                Field("ok", Json.Bool(true)),
                Field("activeMatches", Json.Num(snapshot.ActiveMatches)),
                Field("maxMatches", Json.Num(snapshot.MaxMatches)),
                Field("availableSlots", Json.Num(snapshot.AvailableSlots)),
                Field("maxPlayersPerMatch", Json.Num(snapshot.MaxPlayersPerMatch)),
                Field("canAcceptMatch", Json.Bool(snapshot.CanAcceptMatch))
            });
        }

        private static KeyValuePair<string, string> Field(string key, string jsonValue) =>
            new KeyValuePair<string, string>(key, jsonValue);

        private static void WriteJson(HttpListenerContext context, int statusCode, string body)
        {
            var response = context.Response;
            var buffer = Encoding.UTF8.GetBytes(body);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static void TryWriteError(HttpListenerContext context)
        {
            try
            {
                WriteJson(context, 500, "{\"ok\":false,\"error\":\"internal server error\"}");
            }
            catch
            {
            }
        }
    }
}
