using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Tests
{
    internal static class TestSupport
    {
        /// <summary>
        /// Reserva un puerto TCP libre del SO y lo devuelve. Se usa para que las
        /// pruebas de integración no choquen con puertos fijos ya ocupados.
        /// </summary>
        public static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// Construye una ServerConfig de pruebas con un controlPort dado y un
        /// gatewayPort distinto y válido.
        /// </summary>
        public static ServerConfig ConfigWithControlPort(int controlPort, int maxMatches = 4, int maxPlayers = 8)
        {
            var gatewayPort = controlPort >= ServerConfig.MaxPort ? controlPort - 1 : controlPort + 1;
            return new ServerConfig(
                controlHost: "localhost",
                controlPort: controlPort,
                gatewayPort: gatewayPort,
                tickRate: 30,
                maxMatches: maxMatches,
                maxPlayersPerMatch: maxPlayers);
        }
    }

    /// <summary>Logger de pruebas que captura las entradas en memoria.</summary>
    internal sealed class CapturingLogger : IServerLogger
    {
        public readonly List<(LogLevel Level, string Message)> Entries = new List<(LogLevel, string)>();

        public void Log(LogLevel level, string message, Exception error = null)
        {
            Entries.Add((level, message));
        }
    }
}
