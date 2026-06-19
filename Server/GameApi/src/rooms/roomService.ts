import { genId, genRoomCode } from "../ids";
import { GameServerControlClient } from "../gameserver/controlClient";
import { JoinTicket } from "../tickets/types";
import { TicketService } from "../tickets/ticketService";
import {
  AlreadyMemberError,
  CannotLaunchError,
  NotHostError,
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
 * Orquesta el ciclo de una sala. Regla de lanzamiento (decisión de diseño): el
 * creador puede iniciar la partida cuando hay al menos `minPlayers` jugadores
 * unidos; NO se requiere que estén "ready" (el ready es opcional/cosmético).
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
    this.minPlayers = options.minPlayers ?? 3;
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

    if (room.hostPlayerId === playerId) {
      const newHost = room.members[0];
      room.hostPlayerId = newHost.playerId;
      newHost.role = "master";
    }

    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  /** Marca/desmarca "listo" (cosmético; no condiciona el lanzamiento). */
  async setReady(roomId: string, playerId: string, ready: boolean): Promise<Room> {
    const room = await this.getRoom(roomId);
    const member = room.members.find((m) => m.playerId === playerId);
    if (!member) {
      throw new NotMemberError();
    }
    member.isReady = ready;
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  /** True si quien pide es el creador, hay mínimo de jugadores y hay capacidad. */
  canLaunch(room: Room, hasCapacity: boolean, requesterId: string): boolean {
    return (
      room.hostPlayerId === requesterId &&
      room.members.length >= this.minPlayers &&
      hasCapacity &&
      (room.state === "waiting" || room.state === "ready_check")
    );
  }

  /** Lanza la partida. Solo el creador, con al menos minPlayers jugadores unidos. */
  async launch(roomId: string, requesterId: string): Promise<LaunchResult> {
    const room = await this.getRoom(roomId);

    if (room.hostPlayerId !== requesterId) {
      throw new NotHostError();
    }

    const capacity = await this.control.getCapacity();
    if (!this.canLaunch(room, capacity.canAcceptMatch, requesterId)) {
      const reason =
        room.members.length < this.minPlayers
          ? `hacen falta al menos ${this.minPlayers} jugadores (hay ${room.members.length})`
          : !capacity.canAcceptMatch
            ? "el Game Server no tiene capacidad"
            : "la sala no se puede lanzar en su estado actual";
      throw new CannotLaunchError(reason);
    }

    RoomStateMachine.assertTransition(room.state, "starting");
    room.state = "starting";
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);

    let created;
    try {
      created = await this.control.createMatch(matchConfigFromRoom(room));
    } catch (err) {
      room.state = "waiting";
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

  async listJoinableRooms(): Promise<Room[]> {
    const ids = await this.rooms.listIds();
    const result: Room[] = [];
    for (const id of ids) {
      const room = await this.rooms.get(id);
      if (!room) continue;
      if (room.isPrivate) continue;
      if (room.state !== "waiting" && room.state !== "ready_check") continue;
      if (room.members.length >= room.maxPlayers) continue;
      result.push(room);
    }
    return result;
  }

  async joinByCode(code: string, input: JoinRoomInput): Promise<Room> {
    const room = await this.rooms.getByCode(code);
    if (!room) {
      throw new RoomNotFoundError();
    }
    return this.joinRoom(room.roomId, input);
  }
}
