using System;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Configuración inmutable del Game Server. Validada en el constructor.
    /// </summary>
    public sealed class ServerConfig
    {
        public const int MinPort = 1;
        public const int MaxPort = 65535;

        public string ControlHost { get; }
        public int ControlPort { get; }
        public int GatewayPort { get; }
        public int TickRate { get; }
        public int MaxMatches { get; }
        public int MaxPlayersPerMatch { get; }
        public string ControlToken { get; }
        public string GameApiUrl { get; }

        /// <summary>
        /// Tiempo de vida máximo de una partida en segundos (decisión de diseño,
        /// configurable por GS_MATCH_MAX_SECONDS). Pasado ese tiempo se autodestruye.
        /// 0 = sin límite.
        /// </summary>
        public int MatchMaxSeconds { get; }

        public ServerConfig(
            string controlHost,
            int controlPort,
            int gatewayPort,
            int tickRate,
            int maxMatches,
            int maxPlayersPerMatch,
            string controlToken = "",
            string gameApiUrl = "http://localhost:3001",
            int matchMaxSeconds = 600)
        {
            ControlHost = controlHost;
            ControlPort = controlPort;
            GatewayPort = gatewayPort;
            TickRate = tickRate;
            MaxMatches = maxMatches;
            MaxPlayersPerMatch = maxPlayersPerMatch;
            ControlToken = controlToken ?? string.Empty;
            GameApiUrl = string.IsNullOrWhiteSpace(gameApiUrl) ? "http://localhost:3001" : gameApiUrl;
            MatchMaxSeconds = matchMaxSeconds;

            Validate();
        }

        public static ServerConfig Default()
        {
            return new ServerConfig(
                controlHost: "localhost",
                controlPort: 8090,
                gatewayPort: 9090,
                tickRate: 30,
                maxMatches: 50,
                maxPlayersPerMatch: 8,
                controlToken: "",
                gameApiUrl: "http://localhost:3001",
                matchMaxSeconds: 600);
        }

        public static ServerConfig FromEnvironment(Func<string, string> getEnv = null)
        {
            getEnv = getEnv ?? Environment.GetEnvironmentVariable;
            var defaults = Default();

            return new ServerConfig(
                controlHost: ReadString(getEnv, "GS_CONTROL_HOST", defaults.ControlHost),
                controlPort: ReadInt(getEnv, "GS_CONTROL_PORT", defaults.ControlPort),
                gatewayPort: ReadInt(getEnv, "GS_GATEWAY_PORT", defaults.GatewayPort),
                tickRate: ReadInt(getEnv, "GS_TICK_RATE", defaults.TickRate),
                maxMatches: ReadInt(getEnv, "GS_MAX_MATCHES", defaults.MaxMatches),
                maxPlayersPerMatch: ReadInt(getEnv, "GS_MAX_PLAYERS_PER_MATCH", defaults.MaxPlayersPerMatch),
                controlToken: ReadString(getEnv, "GS_CONTROL_TOKEN", defaults.ControlToken),
                gameApiUrl: ReadString(getEnv, "GS_GAME_API_URL", defaults.GameApiUrl),
                matchMaxSeconds: ReadInt(getEnv, "GS_MATCH_MAX_SECONDS", defaults.MatchMaxSeconds));
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(ControlHost))
            {
                throw new ServerConfigException("ControlHost no puede estar vacío.");
            }
            if (ControlPort < MinPort || ControlPort > MaxPort)
            {
                throw new ServerConfigException(
                    $"ControlPort fuera de rango ({ControlPort}). Debe estar entre {MinPort} y {MaxPort}.");
            }
            if (GatewayPort < MinPort || GatewayPort > MaxPort)
            {
                throw new ServerConfigException(
                    $"GatewayPort fuera de rango ({GatewayPort}). Debe estar entre {MinPort} y {MaxPort}.");
            }
            if (ControlPort == GatewayPort)
            {
                throw new ServerConfigException(
                    $"ControlPort y GatewayPort no pueden ser el mismo puerto ({ControlPort}).");
            }
            if (TickRate <= 0)
            {
                throw new ServerConfigException($"TickRate debe ser mayor que 0 (recibido {TickRate}).");
            }
            if (MaxMatches <= 0)
            {
                throw new ServerConfigException($"MaxMatches debe ser mayor que 0 (recibido {MaxMatches}).");
            }
            if (MaxPlayersPerMatch <= 0)
            {
                throw new ServerConfigException($"MaxPlayersPerMatch debe ser mayor que 0 (recibido {MaxPlayersPerMatch}).");
            }
            if (MatchMaxSeconds < 0)
            {
                throw new ServerConfigException($"MatchMaxSeconds no puede ser negativo (recibido {MatchMaxSeconds}).");
            }
        }

        private static string ReadString(Func<string, string> getEnv, string key, string fallback)
        {
            var raw = getEnv(key);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        }

        private static int ReadInt(Func<string, string> getEnv, string key, int fallback)
        {
            var raw = getEnv(key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }
            if (!int.TryParse(raw.Trim(), out var value))
            {
                throw new ServerConfigException(
                    $"La variable de entorno {key} debe ser un entero (recibido '{raw}').");
            }
            return value;
        }

        public override string ToString()
        {
            return $"ServerConfig(controlHost={ControlHost}, controlPort={ControlPort}, " +
                   $"gatewayPort={GatewayPort}, tickRate={TickRate}, maxMatches={MaxMatches}, " +
                   $"maxPlayersPerMatch={MaxPlayersPerMatch}, gameApiUrl={GameApiUrl}, " +
                   $"matchMaxSeconds={MatchMaxSeconds})";
        }
    }
}
