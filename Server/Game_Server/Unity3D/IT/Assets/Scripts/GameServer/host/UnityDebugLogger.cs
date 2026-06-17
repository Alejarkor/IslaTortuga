using System;
using UnityEngine;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Logger que vuelca al Console del Editor de Unity (UnityEngine.Debug). Útil en
    /// Play mode, donde la salida de System.Console va al Editor.log y no a la ventana
    /// Console. Vive en la capa de Unity a propósito: el núcleo del servidor sigue sin
    /// depender de UnityEngine. En builds dedicadas headless conviene seguir usando
    /// ConsoleServerLogger (stdout → Player.log).
    /// </summary>
    public sealed class UnityDebugLogger : IServerLogger
    {
        private const string Prefix = "[GameServer]";

        private readonly LogLevel _minLevel;

        public UnityDebugLogger(LogLevel minLevel = LogLevel.Info)
        {
            _minLevel = minLevel;
        }

        public void Log(LogLevel level, string message, Exception error = null)
        {
            if (level < _minLevel)
            {
                return;
            }

            var line = $"{Prefix} {message}";

            switch (level)
            {
                case LogLevel.Warn:
                    Debug.LogWarning(line);
                    break;
                case LogLevel.Error:
                    Debug.LogError(line);
                    if (error != null)
                    {
                        Debug.LogException(error);
                    }
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
