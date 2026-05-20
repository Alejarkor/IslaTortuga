using IslaTortuga.Server.Networking;
using IslaTortuga.Server.Networking.Protocol;
using IslaTortuga.Server.Replication;
using IslaTortuga.Server.Rooms;

namespace IslaTortuga.Server.GameLoop;

public sealed class GameTickService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

    private readonly GameRoomManager _gameRoomManager;
    private readonly ConnectionManager _connectionManager;
    private readonly SnapshotBuilder _snapshotBuilder;

    public GameTickService(
        GameRoomManager gameRoomManager,
        ConnectionManager connectionManager,
        SnapshotBuilder snapshotBuilder)
    {
        _gameRoomManager = gameRoomManager;
        _connectionManager = connectionManager;
        _snapshotBuilder = snapshotBuilder;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            _gameRoomManager.TickAll((float)TickInterval.TotalSeconds);

            foreach (var room in _gameRoomManager.GetAllRooms())
            {
                foreach (var roomPlayer in room.Players)
                {
                    var connectionId = roomPlayer.Session.ConnectionId;
                    if (string.IsNullOrWhiteSpace(connectionId))
                    {
                        continue;
                    }

                    if (!_connectionManager.TryGet(connectionId, out var connection) || connection is null)
                    {
                        continue;
                    }

                    await connection.SendAsync(
                        ProtocolTypes.WorldSnapshot,
                        _snapshotBuilder.Build(room, roomPlayer),
                        cancellationToken: stoppingToken);
                }
            }
        }
    }
}
