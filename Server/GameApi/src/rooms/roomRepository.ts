import { RedisLike } from "../redis";
import { Room } from "./types";

const ROOM_KEY = (roomId: string) => `room:${roomId}`;
const CODE_KEY = (code: string) => `roomcode:${code.toUpperCase()}`;
const INDEX_KEY = "rooms:index";

/**
 * Persistencia de salas en Redis. La sala completa (incluidos sus miembros) se
 * guarda como un único documento JSON; se mantiene además un índice de código ->
 * roomId para poder unirse por código, y un set índice de todas las salas vivas.
 * Las salas llevan TTL para que no se acumulen salas zombi si algo se queda a medias.
 */
export class RoomRepository {
  constructor(
    private readonly redis: RedisLike,
    private readonly roomTtlSeconds = 6 * 60 * 60
  ) {}

  async save(room: Room): Promise<void> {
    const json = JSON.stringify(room);
    await this.redis.set(ROOM_KEY(room.roomId), json, "EX", this.roomTtlSeconds);
    await this.redis.set(CODE_KEY(room.code), room.roomId, "EX", this.roomTtlSeconds);
    await this.redis.sadd(INDEX_KEY, room.roomId);
  }

  async get(roomId: string): Promise<Room | null> {
    const raw = await this.redis.get(ROOM_KEY(roomId));
    return raw ? (JSON.parse(raw) as Room) : null;
  }

  async getByCode(code: string): Promise<Room | null> {
    const roomId = await this.redis.get(CODE_KEY(code));
    return roomId ? this.get(roomId) : null;
  }

  async delete(roomId: string): Promise<void> {
    const room = await this.get(roomId);
    await this.redis.del(ROOM_KEY(roomId));
    if (room) {
      await this.redis.del(CODE_KEY(room.code));
    }
    await this.redis.srem(INDEX_KEY, roomId);
  }

  async listIds(): Promise<string[]> {
    return this.redis.smembers(INDEX_KEY);
  }
}
