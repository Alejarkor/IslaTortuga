using IslaTortuga.Server.Sessions;
using IslaTortuga.Server.World;

namespace IslaTortuga.Server.Rooms;

public sealed class RoomPlayer
{
    public RoomPlayer(GameRoom room, PlayerSession session, PlayerEntity playerEntity)
    {
        Room = room;
        Session = session;
        PlayerEntity = playerEntity;
    }

    public GameRoom Room { get; }

    public PlayerSession Session { get; }

    public PlayerEntity PlayerEntity { get; }
}
