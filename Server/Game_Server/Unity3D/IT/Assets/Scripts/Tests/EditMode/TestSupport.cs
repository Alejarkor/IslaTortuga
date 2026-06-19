using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    internal static class TestSupport
    {
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

        public static (int control, int gateway) GetTwoFreePorts()
        {
            var a = GetFreeTcpPort();
            int b;
            do { b = GetFreeTcpPort(); } while (b == a);
            return (a, b);
        }

        public static ServerConfig ConfigWithControlPort(int controlPort, int maxMatches = 4, int maxPlayers = 8)
        {
            int gatewayPort;
            do { gatewayPort = GetFreeTcpPort(); } while (gatewayPort == controlPort);
            return new ServerConfig(
                controlHost: "localhost",
                controlPort: controlPort,
                gatewayPort: gatewayPort,
                tickRate: 30,
                maxMatches: maxMatches,
                maxPlayersPerMatch: maxPlayers);
        }
    }

    internal sealed class CapturingLogger : IServerLogger
    {
        public readonly List<(LogLevel Level, string Message)> Entries = new List<(LogLevel, string)>();

        public void Log(LogLevel level, string message, Exception error = null)
        {
            Entries.Add((level, message));
        }
    }

    /// <summary>Validador de tickets de prueba: devuelve un resultado fijo (o null).</summary>
    internal sealed class FakeTicketValidator : ITicketValidator
    {
        private readonly ValidatedTicket _result;
        public int Calls { get; private set; }

        public FakeTicketValidator(ValidatedTicket result)
        {
            _result = result;
        }

        public Task<ValidatedTicket> ValidateAndConsumeAsync(string ticketId)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    /// <summary>
    /// Transporte en memoria para probar el handshake sin un WebSocket real.
    /// Captura lo que el servidor envía y permite inyectar mensajes entrantes y
    /// simular el cierre por parte del cliente.
    /// </summary>
    internal sealed class FakeTransport : ITransport
    {
        private readonly BlockingCollection<string> _incoming = new BlockingCollection<string>();
        private readonly List<string> _sent = new List<string>();

        public bool IsOpen { get; private set; } = true;
        public bool Closed { get; private set; }

        public IReadOnlyList<string> Sent
        {
            get { lock (_sent) { return new List<string>(_sent); } }
        }

        public Task SendAsync(string message, CancellationToken ct = default)
        {
            lock (_sent) { _sent.Add(message); }
            return Task.CompletedTask;
        }

        public Task<string> ReceiveAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                try { return _incoming.Take(); }
                catch { return (string)null; }
            });
        }

        public Task CloseAsync(string reason = null)
        {
            IsOpen = false;
            Closed = true;
            return Task.CompletedTask;
        }

        public void QueueIncoming(string message) => _incoming.Add(message);
        public void SimulateClientClose() => _incoming.CompleteAdding();
    }
}
