using System;

namespace IslaTortuga.GameServer.Host
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// Abstracción de logging del Game Server. Deliberadamente NO depende de
    /// UnityEngine para que el núcleo del servidor sea probable en modo headless y
    /// reutilizable fuera del editor. La implementación por defecto escribe a la
    /// consola estándar, que Unity recoge en el Player.log en builds dedicadas.
    /// </summary>
    public interface IServerLogger
    {
        void Log(LogLevel level, string message, Exception error = null);
    }

    public static class ServerLoggerExtensions
    {
        public static void Debug(this IServerLogger logger, string message) =>
            logger.Log(LogLevel.Debug, message);

        public static void Info(this IServerLogger logger, string message) =>
            logger.Log(LogLevel.Info, message);

        public static void Warn(this IServerLogger logger, string message) =>
            logger.Log(LogLevel.Warn, message);

        public static void Error(this IServerLogger logger, string message, Exception error = null) =>
            logger.Log(LogLevel.Error, message, error);
    }
}
