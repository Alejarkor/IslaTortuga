import { genId, genRoomCode } from "../ids";
import { GameServerControlClient } from "../gameserver/controlClient";
import { JoinTicket } from "../tickets/types";
import { TicketService } from "../tickets/ticketService";
import {
  AlreadyMemberError,
  CannotLaunchError,
  NotMemberError,
  RoomFullError,
  RoomNotFoundError
} from "./errors";
import { RoomRepository } from "./roomRepository";
import { RoomStateMachine } from "./roomStateMachine";
import { RoomSyncAdapter } from "./roomSyncAdapter";
import { Room, RoomMember, matchConfigFromRoom } from "./types";

export interface CreateRoomInput {
  hostPlayerId: string;
  nickname: string;
  maxPlayers?: number;
  mapId?: string;
  isPrivate?: boolean;
}

export interface JoinRoomInput {
  playerId: string;
  nickname: string;
}

export interface LaunchResult {
  room: Room;
  matchId: string;
  gateway: { host: string; port: number };
  tickets: JoinTicket[];
}

export interface RoomServiceOptions {
  minPlayers?: number;
  defaultMaxPlayers?: number;
  defaultMapId?: string;
}

/**
 * Orquesta todo el ciclo de una sala: creación, unión, ready y lanzamiento de
 * partida. En el lanzamiento coordina al Game Server (capacidad + create-match),
 * la emisión de tickets y el cambio de estado a in_game vía RoomSyncAdapter.
 */
export class RoomService {
  private readonly minPlayers: number;
  private readonly defaultMaxPlayers: number;
  private readonly defaultMapId: string;

  constructor(
    private readonly rooms: RoomRepository,
    private readonly tickets: TicketService,
    private readonly control: GameServerControlClient,
    private readonly sync: RoomSyncAdapter,
    options: RoomServiceOptions = {}
  ) {
    this.minPlayers = options.minPlayers ?? 1;
    this.defaultMaxPlayers = options.defaultMaxPlayers ?? 8;
    this.defaultMapId = options.defaultMapId ?? "beach_map_01";
  }

  async createRoom(input: CreateRoomInput): Promise<Room> {
    const now = new Date().toISOString();
    const host: RoomMember = {
      playerId: input.hostPlayerId,
      nickname: input.nickname,
      isReady: false,
      role: "master",
      joinedAt: now
    };

    const room: Room = {
      roomId: genId("room"),
      code: genRoomCode(),
      hostPlayerId: input.hostPlayerId,
      state: "waiting",
      maxPlayers: input.maxPlayers ?? this.defaultMaxPlayers,
      mapId: input.mapId ?? this.defaultMapId,
      isPrivate: input.isPrivate ?? false,
      matchId: null,
      members: [host],
      createdAt: now,
      updatedAt: now
    };

    await this.rooms.save(room);
    return room;
  }

  async getRoom(roomId: string): Promise<Room> {
    const room = await this.rooms.get(roomId);
    if (!room) {
      throw new RoomNotFoundError();
    }
    return room;
  }

  async joinRoom(roomId: string, input: JoinRoomInput): Promise<Room> {
    const room = await this.getRoom(roomId);

    if (room.state !== "waiting" && room.state !== "ready_check") {
      throw new CannotLaunchError("la sala no admite nuevas uniones en su estado actual");
    }
    if (room.members.some((m) => m.playerId === input.playerId)) {
      throw new AlreadyMemberError();
    }
    if (room.members.length >= room.maxPlayers) {
      throw new RoomFullError();
    }

    room.members.push({
      playerId: input.playerId,
      nickname: input.nickname,
      isReady: false,
      role: "player",
      joinedAt: new Date().toISOString()
    });

    this.syncReadyState(room);
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  async leaveRoom(roomId: string, playerId: string): Promise<Room | null> {
    const room = await this.getRoom(roomId);
    const index = room.members.findIndex((m) => m.playerId === playerId);
    if (index === -1) {
      throw new NotMemberError();
    }

    room.members.splice(index, 1);

    if (room.members.length === 0) {
      await this.rooms.delete(roomId);
      return null;
    }

    // Si se fue el host, promociona al siguiente miembro a master/host.
    if (room.hostPlayerId === playerId) {
      const newHost = room.members[0];
      room.hostPlayerId = newHost.playerId;
      newHost.role = "master";
    }

    this.syncReadyState(room);
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  async setReady(roomId: string, playerId: string, ready: boolean): Promise<Room> {
    const room = await this.getRoom(roomId);
    const member = room.members.find((m) => m.playerId === playerId);
    if (!member) {
      throw new NotMemberError();
    }

    member.isReady = ready;
    this.syncReadyState(room);
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  /** True solo si todos están ready, hay mínimo de jugadores y hay capacidad. */
  canLaunch(room: Room, hasCapacity: boolean): boolean {
    return (
      room.state === "ready_check" &&
      room.members.length >= this.minPlayers &&
      room.members.every((m) => m.isReady) &&
      hasCapacity
    );
  }

  async launch(roomId: string): Promise<LaunchResult> {
    const room = await this.getRoom(roomId);

    const capacity = await this.control.getCapacity();
    if (!this.canLaunch(room, capacity.canAcceptMatch)) {
      const reason = !capacity.canAcceptMatch
        ? "el Game Server no tiene capacidad"
        : "no todos los jugadores están listos";
      throw new CannotLaunchError(reason);
    }

    // ready_check -> starting (intención de lanzar, aún reversible).
    RoomStateMachine.assertTransition(room.state, "starting");
    room.state = "starting";
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);

    let created;
    try {
      created = await this.control.createMatch(matchConfigFromRoom(room));
    } catch (err) {
      // Falló la creación: revertimos a ready_check para poder reintentar.
      room.state = "ready_check";
      room.updatedAt = new Date().toISOString();
      await this.rooms.save(room);
      throw err;
    }

    const tickets = await this.tickets.issueForMatch(
      created.matchId,
      room.members.map((m) => m.playerId)
    );

    const updatedRoom = await this.sync.onMatchCreated(room, created.matchId);

    return {
      room: updatedRoom,
      matchId: created.matchId,
      gateway: { host: created.gatewayHost, port: created.gatewayPort },
      tickets
    };
  }

  /**
   * Lista las salas a las que un jugador podría unirse: públicas, en pre-juego
   * (waiting/ready_check) y con hueco libre. Las salas caducadas (TTL) se ignoran.
   */
  async listJoinableRooms(): Promise<Room[]> {
    const ids = await this.rooms.listIds();
    const result: Room[] = [];
    for (const id of ids) {
      const room = await this.rooms.get(id);
      if (!room) {
        continue;
      }
      if (room.isPrivate) {
        continue;
      }
      if (room.state !== "waiting" && room.state !== "ready_check") {
        continue;
      }
      if (room.members.length >= room.maxPlayers) {
        continue;
      }
      result.push(room);
    }
    return result;
  }

  /** Une a un jugador a una sala localizada por su código. */
  async joinByCode(code: string, input: JoinRoomInput): Promise<Room> {
    const room = await this.rooms.getByCode(code);
    if (!room) {
      throw new RoomNotFoundError();
    }
    return this.joinRoom(room.roomId, input);
  }

  /**
   * Sincroniza el estado de pre-juego (waiting <-> ready_check) según si todos los
   * miembros están listos. No toca estados que no sean de pre-juego.
   */
  private syncReadyState(room: Room): void {
    if (room.state !== "waiting" && room.state !== "ready_check") {
      return;
    }

    const allReady =
      room.members.length >= this.minPlayers && room.members.every((m) => m.isReady);
    const target = allReady ? "ready_check" : "waiting";

    if (target !== room.state && RoomStateMachine.canTransition(room.state, target)) {
      room.state = target;
    }
  }
}
