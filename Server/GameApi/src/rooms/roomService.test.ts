import { describe, it, expect } from "vitest";
import { InMemoryRedis } from "../testing/inMemoryRedis";
import { FakeControlClient } from "../testing/fakeControlClient";
import { buildRoomServices } from "./routes";
import { CannotLaunchError, NotHostError } from "./errors";

function setup(maxMatches = 10) {
  const redis = new InMemoryRedis();
  const control = new FakeControlClient(maxMatches);
  // minPlayers por defecto = 3
  const { service } = buildRoomServices(redis, control);
  return { redis, control, service };
}

async function roomWith3(service: ReturnType<typeof setup>["service"]) {
  const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
  await service.joinRoom(room.roomId, { playerId: "p2", nickname: "Beto" });
  await service.joinRoom(room.roomId, { playerId: "p3", nickname: "Caro" });
  return service.getRoom(room.roomId); // recargada, con los 3 miembros
}

describe("RoomService", () => {
  it("crea sala con el host como master y estado waiting", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    expect(room.state).toBe("waiting");
    expect(room.members[0].role).toBe("master");
    expect(room.code).toHaveLength(6);
  });

  it("el creador lanza con 3 jugadores (sin ready) y genera un ticket por jugador", async () => {
    const { redis, control, service } = setup();
    const room = await roomWith3(service);

    const result = await service.launch(room.roomId, "p1");

    expect(result.matchId).toMatch(/^match_/);
    expect(control.created).toHaveLength(1);
    expect(control.created[0].config.players).toEqual(["p1", "p2", "p3"]);
    expect(result.tickets).toHaveLength(3);
    for (const t of result.tickets) {
      expect(redis.ttlSeconds(`ticket:${t.ticketId}`)).toBeGreaterThan(0);
    }
    const stored = await service.getRoom(room.roomId);
    expect(stored.state).toBe("in_game");
    expect(stored.matchId).toBe(result.matchId);
  });

  it("solo el creador puede lanzar", async () => {
    const { service } = setup();
    const room = await roomWith3(service);
    await expect(service.launch(room.roomId, "p2")).rejects.toBeInstanceOf(NotHostError);
  });

  it("no se puede lanzar con menos de 3 jugadores", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    await service.joinRoom(room.roomId, { playerId: "p2", nickname: "Beto" }); // solo 2
    await expect(service.launch(room.roomId, "p1")).rejects.toBeInstanceOf(CannotLaunchError);
  });

  it("si el Game Server no tiene capacidad, no se lanza y la sala vuelve a waiting", async () => {
    const { service } = setup(0); // capacidad cero
    const room = await roomWith3(service);
    await expect(service.launch(room.roomId, "p1")).rejects.toBeInstanceOf(CannotLaunchError);
    const stored = await service.getRoom(room.roomId);
    expect(stored.state).toBe("waiting");
    expect(stored.matchId).toBeNull();
  });

  it("canLaunch: true solo si es el host, hay mínimo y hay capacidad", async () => {
    const { service } = setup();
    const room = await roomWith3(service);
    expect(service.canLaunch(room, true, "p1")).toBe(true);
    expect(service.canLaunch(room, true, "p2")).toBe(false); // no es host
    expect(service.canLaunch(room, false, "p1")).toBe(false); // sin capacidad
  });

  it("setReady solo marca el flag (no condiciona el lanzamiento)", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    const after = await service.setReady(room.roomId, "p1", true);
    expect(after.members[0].isReady).toBe(true);
    expect(after.state).toBe("waiting"); // el estado no cambia por el ready
  });

  it("listJoinableRooms devuelve solo públicas en pre-juego con hueco", async () => {
    const { service } = setup();
    const a = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana", isPrivate: false });
    await service.createRoom({ hostPlayerId: "p2", nickname: "Beto", isPrivate: true });
    const list = await service.listJoinableRooms();
    expect(list.map((r) => r.roomId)).toContain(a.roomId);
    expect(list.every((r) => !r.isPrivate)).toBe(true);
  });

  it("joinByCode une por código y rechaza un código inexistente", async () => {
    const { service } = setup();
    const room = await service.createRoom({ hostPlayerId: "p1", nickname: "Ana" });
    const joined = await service.joinByCode(room.code, { playerId: "p2", nickname: "Beto" });
    expect(joined.members.map((m) => m.playerId)).toContain("p2");
    await expect(
      service.joinByCode("NOEXISTE", { playerId: "p3", nickname: "Caro" })
    ).rejects.toMatchObject({ name: "RoomNotFoundError" });
  });
});
