using IslaTortuga.Server.Rooms;
using IslaTortuga.Server.World;

namespace IslaTortuga.Server.Replication;

public sealed class InterestManager
{
    public IReadOnlyList<NetworkEntity> GetVisibleEntities(GameRoom room, RoomPlayer viewer)
    {
        _ = viewer;
        return room.World.Entities.GetAll().ToArray();
    }
}
