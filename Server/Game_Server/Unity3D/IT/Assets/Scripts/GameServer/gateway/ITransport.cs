using System.Threading;
using System.Threading.Tasks;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Abstracción de una conexión realtime con un cliente. Permite inyectar un
    /// transporte falso en los tests sin abrir un WebSocket real.
    /// </summary>
    public interface ITransport
    {
        bool IsOpen { get; }
        Task SendAsync(string message, CancellationToken ct = default);
        /// <summary>Devuelve el siguiente mensaje de texto, o null si la conexión se cerró.</summary>
        Task<string> ReceiveAsync(CancellationToken ct = default);
        Task CloseAsync(string reason = null);
    }
}
