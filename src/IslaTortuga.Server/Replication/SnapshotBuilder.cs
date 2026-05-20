using IslaTortuga.Server.Networking.Protocol.Payloads;
using IslaTortuga.Server.Rooms;

namespace IslaTortuga.Server.Replication;

public sealed class SnapshotBuilder
{
    private readonly InterestManager _interestManager;
    private readonly EntityReplicator _entityReplicator;

    public SnapshotBuilder(InterestManager interestManager, EntityReplicator entityReplicator)
    {
        _interestManager = interestManager;
        _entityReplicator = entityReplicator;
    }

    public WorldSnapshotPayload Build(GameRoom room, RoomPlayer viewer)
    {
        var entities = _interestManager.GetVisibleEntities(room, viewer)
            .Select(_entityReplicator.Replicate)
            .ToArray();

        return new WorldSnapshotPayload(room.World.CurrentTick, room.RoomId, entities);
    }
}
