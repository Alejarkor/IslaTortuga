using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Transporte sobre System.Net.WebSockets.WebSocket (el que entrega HttpListener
    /// al aceptar el upgrade). Envía/recibe mensajes de texto UTF-8 completos.
    /// </summary>
    public sealed class WebSocketTransport : ITransport
    {
        private readonly WebSocket _ws;

        public WebSocketTransport(WebSocket ws)
        {
            _ws = ws ?? throw new ArgumentNullException(nameof(ws));
        }

        public bool IsOpen => _ws.State == WebSocketState.Open;

        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: ct).ConfigureAwait(false);
        }

        public async Task<string> ReceiveAsync(CancellationToken ct = default)
        {
            var buffer = new byte[4096];
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public async Task CloseAsync(string reason = null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        reason ?? "bye",
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // El cliente puede haberse ido de golpe; nada más que hacer.
            }
        }
    }
}
