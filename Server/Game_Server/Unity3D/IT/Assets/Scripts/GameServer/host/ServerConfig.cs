using System;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Configuración inmutable del Game Server. Es lo primero que se construye en el
    /// arranque: sin ella nada se puede configurar. Se valida en el constructor, de
    /// modo que una instancia de ServerConfig es, por definición, válida.
    /// </summary>
    public sealed class ServerConfig
    {
        public const int MinPort = 1;
        public const int MaxPort = 65535;

        /// <summary>Interfaz/host donde escucha la ControlApi. "localhost" en dev.</summary>
        public string ControlHost { get; }

        /// <summary>Puerto HTTP del plano de control (/health, /capacity).</summary>
        public int ControlPort { get; }

        /// <summary>Puerto del PlayerGateway realtime (se usará a partir de la Fase 2).</summary>
        public int GatewayPort { get; }

        /// <summary>Frecuencia de tick de la simulación (ticks por segundo). Debe ser &gt; 0.</summary>
        public int TickRate { get; }

        /// <summary>Número máximo de partidas (MatchInstance) simultáneas en este host.</summary>
        public int MaxMatches { get; }

        /// <summary>Número máximo de jugadores por partida.</summary>
        public int MaxPlayersPerMatch { get; }

        /// <summary>
        /// Token compartido para proteger los endpoints de control (create/stop-match).
        /// Vacío = sin autenticación (dev local).
        /// </summary>
        public string ControlToken { get; }

        public ServerConfig(
            string controlHost,
            int controlPort,
            int gatewayPort,
            int tickRate,
            int maxMatches,
            int maxPlayersPerMatch,
            string controlToken = "")
        {
            ControlHost = controlHost;
            ControlPort = controlPort;
            GatewayPort = gatewayPort;
            TickRate = tickRate;
            MaxMatches = maxMatches;
            MaxPlayersPerMatch = maxPlayersPerMatch;
            ControlToken = controlToken ?? string.Empty;

            Validate();
        }

        /// <summary>
        /// Valores por defecto razonables para desarrollo local.
        /// </summary>
        public static ServerConfig Default()
        {
            return new ServerConfig(
                controlHost: "localhost",
                controlPort: 8090,
                gatewayPort: 9090,
                tickRate: 30,
                maxMatches: 50,
                maxPlayersPerMatch: 8,
                controlToken: "");
        }

        /// <summary>
        /// Construye la configuración a partir de variables de entorno, cayendo a los
        /// valores por defecto cuando una variable no está presente. Lanza
        /// ServerConfigException si algún valor presente es inválido (no numérico o
        /// fuera de rango), tras pasar por Validate().
        /// </summary>
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
                controlToken: ReadString(getEnv, "GS_CONTROL_TOKEN", defaults.ControlToken));
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
                throw new ServerConfigException(
                    $"TickRate debe ser mayor que 0 (recibido {TickRate}).");
            }

            if (MaxMatches <= 0)
            {
                throw new ServerConfigException(
                    $"MaxMatches debe ser mayor que 0 (recibido {MaxMatches}).");
            }

            if (MaxPlayersPerMatch <= 0)
            {
                throw new ServerConfigException(
                    $"MaxPlayersPerMatch debe ser mayor que 0 (recibido {MaxPlayersPerMatch}).");
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
                   $"maxPlayersPerMatch={MaxPlayersPerMatch})";
        }
    }
}
