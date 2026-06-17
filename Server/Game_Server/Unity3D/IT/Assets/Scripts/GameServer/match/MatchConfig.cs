using System.Collections.Generic;

namespace IslaTortuga.GameServer.Match
{
    /// <summary>
    /// Configuración con la que el backend pide crear una partida. El Game Server solo
    /// maneja identificadores y números: nada de binarios de asset (el cliente resuelve
    /// el mapId/prefabId por su manifest).
    /// </summary>
    public sealed class MatchConfig
    {
        public int MaxPlayers { get; }
        public string MapId { get; }
        public IReadOnlyList<string> Players { get; }

        public MatchConfig(int maxPlayers, string mapId, IReadOnlyList<string> players)
        {
            MaxPlayers = maxPlayers;
            MapId = mapId ?? string.Empty;
            Players = players ?? new List<string>();
        }
    }
}
