using System;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Se lanza cuando la configuración del servidor es inválida (puerto fuera de
    /// rango, tickRate &lt;= 0, límites no positivos, etc.). Que el arranque falle
    /// de forma ruidosa y temprana es preferible a arrancar con valores corruptos.
    /// </summary>
    public sealed class ServerConfigException : Exception
    {
        public ServerConfigException(string message) : base(message)
        {
        }
    }
}
