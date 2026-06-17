import { describe, it, expect } from "vitest";
import { InMemoryRedis } from "../testing/inMemoryRedis";
import { RoomRepository } from "./roomRepository";
import { Room } from "./types";

function makeRoom(): Room {
  const now = new Date().toISOString();
  return {
    roomId: "room_1",
    code: "ABCDEF",
    hostPlayerId: "player_1",
    state: "waiting",
    maxPlayers: 8,
    mapId: "beach_map_01",
    isPrivate: false,
    matchId: null,
    members: [
      { playerId: "player_1", nickname: "Host", isReady: false, role: "master", joinedAt: now }
    ],
    createdAt: now,
    updatedAt: now
  };
}

describe("RoomRepository", () => {
  it("guarda y recupera por id", async () => {
    const repo = new RoomRepository(new InMemoryRedis());
    const room = makeRoom();
    await repo.save(room);

    const loaded = await repo.get("room_1");
    expect(loaded?.code).toBe("ABCDEF");
    expect(loaded?.members).toHaveLength(1);
  });

  it("recupera por código (case-insensitive)", async () => {
    const repo = new RoomRepository(new InMemoryRedis());
    await repo.save(makeRoom());

    const loaded = await repo.getByCode("abcdef");
    expect(loaded?.roomId).toBe("room_1");
  });

  it("borra la sala y su índice de código", async () => {
    const redis = new InMemoryRedis();
    const repo = new RoomRepository(redis);
    await repo.save(makeRoom());

    await repo.delete("room_1");

    expect(await repo.get("room_1")).toBeNull();
    expect(await repo.getByCode("ABCDEF")).toBeNull();
    expect(await repo.listIds()).not.toContain("room_1");
  });
});
