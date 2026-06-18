import { describe, it, expect } from "vitest";
import { InMemoryRedis } from "../testing/inMemoryRedis";
import { FakeControlClient } from "../testing/fakeControlClient";
import { buildRoomServices } from "./routes";
import { CannotLaunchError, RoomNotFoundError } from "./errors";

function setup(maxMatches = 10) {
  const redis = new InMemoryRedis();
  const control = new FakeControlClient(maxMatches);
  const { service } = buildRoomServices(redis, control);
  return { redis, control, service };
}

describe("RoomService", () => {
  it("crea sala con el host como master y estado waiting", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });

    expect(room.state).toBe("waiting");
    expect(room.members[0].role).toBe("master");
    expect(room.code).toHaveLength(6);
  });

  it("flujo completo: crear, unir, ready y lanzar genera MatchInstance y un ticket por jugador", async () => {
    const { redis, control, service } = setup();

    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    await service.joinRoom(room.roomId, { playerId: "p2", nickname: "Beto" });

    await service.setReady(room.roomId, "p1", true);
    const afterReady = await service.setReady(room.roomId, "p2", true);
    expect(afterReady.state).toBe("ready_check");

    const result = await service.launch(room.roomId);

    expect(result.matchId).toMatch(/^match_/);
    expect(control.created).toHaveLength(1);
    expect(control.created[0].config.players).toEqual(["p1", "p2"]);

    expect(result.tickets).toHaveLength(2);
    for (const ticket of result.tickets) {
      expect(redis.ttlSeconds(`ticket:${ticket.ticketId}`)).toBeGreaterThan(0);
    }

    const stored = await service.getRoom(room.roomId);
    expect(stored.state).toBe("in_game");
    expect(stored.matchId).toBe(result.matchId);
  });

  it("canLaunch es true solo si todos están ready y hay capacidad", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });

    expect(service.canLaunch(room, true)).toBe(false);

    const ready = await service.setReady(room.roomId, "p1", true);
    expect(service.canLaunch(ready, true)).toBe(true);
    expect(service.canLaunch(ready, false)).toBe(false);
  });

  it("lanzar sin estar todos ready falla", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    await service.joinRoom(room.roomId, { playerId: "p2", nickname: "Beto" });
    await service.setReady(room.roomId, "p1", true);

    await expect(service.launch(room.roomId)).rejects.toBeInstanceOf(CannotLaunchError);
  });

  it("si el Game Server no tiene capacidad, no se lanza y la sala no queda in_game", async () => {
    const { service } = setup(0);
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    await service.setReady(room.roomId, "p1", true);

    await expect(service.launch(room.roomId)).rejects.toBeInstanceOf(CannotLaunchError);

    const stored = await service.getRoom(room.roomId);
    expect(stored.state).toBe("ready_check");
    expect(stored.matchId).toBeNull();
  });

  it("desmarcar ready vuelve la sala a waiting", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    await service.setReady(room.roomId, "p1", true);
    const back = await service.setReady(room.roomId, "p1", false);
    expect(back.state).toBe("waiting");
  });

  it("listJoinableRooms devuelve solo públicas, en pre-juego y con hueco", async () => {
    const { service } = setup();
    const a = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana", isPrivate: false });
    await service.createRoom({ hostPlayerId: "p2", nickname: "Beto", isPrivate: true });

    const list = await service.listJoinableRooms();
    const ids = list.map((r) => r.roomId);
    expect(ids).toContain(a.roomId);
    expect(list.every((r) => !r.isPrivate)).toBe(true);
  });

  it("joinByCode une a la sala correcta y rechaza un código inexistente", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });

    const joined = await service.joinByCode(room.code, { playerId: "p2", nickname: "Beto" });
    expect(joined.members.map((m) => m.playerId)).toContain("p2");

    await expect(
      service.joinByCode("NOEXISTE", { playerId: "p3", nickname: "Caro" })
    ).rejects.toBeInstanceOf(RoomNotFoundError);
  });
});
