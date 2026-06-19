using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Transporte WebSocket (RFC 6455) implementado a mano sobre un Stream TCP. Se usa
    /// en lugar del WebSocket de HttpListener porque el runtime Mono del editor de
    /// Unity no soporta su upgrade. Hace el framing: lee tramas enmascaradas del
    /// cliente y escribe tramas de texto sin máscara hacia el cliente.
    /// Asume mensajes no fragmentados (FIN=1), suficiente para JSON pequeños.
    /// </summary>
    public sealed class RawWebSocketTransport : ITransport
    {
        private readonly Stream _stream;
        private readonly TcpClient _client;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private volatile bool _open = true;

        public bool IsOpen => _open && _client != null && _client.Connected;

        public RawWebSocketTransport(Stream stream, TcpClient client)
        {
            _stream = stream;
            _client = client;
        }

        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            await SendFrameAsync(0x1, Encoding.UTF8.GetBytes(message), ct).ConfigureAwait(false);
        }

        public async Task<string> ReceiveAsync(CancellationToken ct = default)
        {
            while (true)
            {
                var frame = await ReadFrameAsync(ct).ConfigureAwait(false);
                if (frame == null)
                {
                    _open = false;
                    return null;
                }
                switch (frame.Opcode)
                {
                    case 0x1: // text
                    case 0x2: // binary
                        return Encoding.UTF8.GetString(frame.Payload);
                    case 0x8: // close
                        _open = false;
                        return null;
                    case 0x9: // ping -> pong
                        await SendFrameAsync(0xA, frame.Payload, ct).ConfigureAwait(false);
                        continue;
                    default:
                        continue; // pong / continuación: ignorar
                }
            }
        }

        public async Task CloseAsync(string reason = null)
        {
            if (_open)
            {
                _open = false;
                try { await SendFrameAsync(0x8, new byte[] { 0x03, 0xE8 }, CancellationToken.None).ConfigureAwait(false); }
                catch { /* el cliente puede haberse ido */ }
            }
            try { _client?.Close(); } catch { }
        }

        private async Task SendFrameAsync(byte opcode, byte[] payload, CancellationToken ct)
        {
            payload = payload ?? Array.Empty<byte>();
            var frame = BuildFrame(opcode, payload);
            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(frame, 0, frame.Length, ct).ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _open = false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static byte[] BuildFrame(byte opcode, byte[] payload)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte((byte)(0x80 | opcode)); // FIN + opcode
                int len = payload.Length;
                if (len < 126)
                {
                    ms.WriteByte((byte)len);
                }
                else if (len <= 0xFFFF)
                {
                    ms.WriteByte(126);
                    ms.WriteByte((byte)((len >> 8) & 0xFF));
                    ms.WriteByte((byte)(len & 0xFF));
                }
                else
                {
                    ms.WriteByte(127);
                    for (int i = 7; i >= 0; i--)
                    {
                        ms.WriteByte((byte)(((long)len >> (8 * i)) & 0xFF));
                    }
                }
                ms.Write(payload, 0, payload.Length);
                return ms.ToArray();
            }
        }

        private sealed class Frame
        {
            public byte Opcode;
            public byte[] Payload;
        }

        private async Task<Frame> ReadFrameAsync(CancellationToken ct)
        {
            var head = await ReadExactAsync(2, ct).ConfigureAwait(false);
            if (head == null) return null;

            var opcode = (byte)(head[0] & 0x0F);
            bool masked = (head[1] & 0x80) != 0;
            long len = head[1] & 0x7F;

            if (len == 126)
            {
                var ext = await ReadExactAsync(2, ct).ConfigureAwait(false);
                if (ext == null) return null;
                len = (ext[0] << 8) | ext[1];
            }
            else if (len == 127)
            {
                var ext = await ReadExactAsync(8, ct).ConfigureAwait(false);
                if (ext == null) return null;
                len = 0;
                for (int i = 0; i < 8; i++) len = (len << 8) | ext[i];
            }

            byte[] mask = null;
            if (masked)
            {
                mask = await ReadExactAsync(4, ct).ConfigureAwait(false);
                if (mask == null) return null;
            }

            var payload = len > 0 ? await ReadExactAsync((int)len, ct).ConfigureAwait(false) : Array.Empty<byte>();
            if (payload == null) return null;

            if (masked && mask != null)
            {
                for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i % 4];
            }
            return new Frame { Opcode = opcode, Payload = payload };
        }

        private async Task<byte[]> ReadExactAsync(int n, CancellationToken ct)
        {
            var buf = new byte[n];
            int off = 0;
            while (off < n)
            {
                int r;
                try { r = await _stream.ReadAsync(buf, off, n - off, ct).ConfigureAwait(false); }
                catch { return null; }
                if (r <= 0) return null;
                off += r;
            }
            return buf;
        }
    }
}
