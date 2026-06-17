using System;
using System.Globalization;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Logger por defecto: emite líneas legibles a la consola estándar con
    /// timestamp UTC y nivel. Filtra por nivel mínimo. Thread-safe mediante un lock
    /// simple para que las líneas no se entrelacen entre hilos (tick, gateway, etc.).
    /// </summary>
    public sealed class ConsoleServerLogger : IServerLogger
    {
        private readonly LogLevel _minLevel;
        private readonly object _gate = new object();

        public ConsoleServerLogger(LogLevel minLevel = LogLevel.Info)
        {
            _minLevel = minLevel;
        }

        public void Log(LogLevel level, string message, Exception error = null)
        {
            if (level < _minLevel)
            {
                return;
            }

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var line = $"{timestamp} [{level.ToString().ToUpperInvariant()}] {message}";

            if (error != null)
            {
                line += Environment.NewLine + error;
            }

            lock (_gate)
            {
                if (level >= LogLevel.Error)
                {
                    Console.Error.WriteLine(line);
                }
                else
                {
                    Console.Out.WriteLine(line);
                }
            }
        }
    }
}
