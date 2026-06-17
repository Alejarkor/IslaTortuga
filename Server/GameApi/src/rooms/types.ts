/** Estados del ciclo de vida de una sala (alineado con la arquitectura). */
export type RoomState =
  | "waiting"
  | "ready_check"
  | "starting"
  | "in_game"
  | "finished"
  | "cancelled";

/** Rol del miembro dentro de la sala. 'master' tendrá acciones reservadas (Fase 7). */
export type RoomRole = "master" | "player";

export interface RoomMember {
  playerId: string;
  nickname: string;
  isReady: boolean;
  role: RoomRole;
  joinedAt: string; // ISO-8601
}

export interface Room {
  roomId: string;
  code: string;
  hostPlayerId: string;
  state: RoomState;
  maxPlayers: number;
  mapId: string;
  isPrivate: boolean;
  matchId: string | null;
  members: RoomMember[];
  createdAt: string; // ISO-8601
  updatedAt: string; // ISO-8601
}

/**
 * Configuración con la que se lanza una partida. Se deriva de la sala y se envía al
 * Game Server al pedir create-match.
 */
export interface MatchConfig {
  maxPlayers: number;
  mapId: string;
  players: string[]; // playerIds
}

export function matchConfigFromRoom(room: Room): MatchConfig {
  return {
    maxPlayers: room.maxPlayers,
    mapId: room.mapId,
    players: room.members.map((m) => m.playerId)
  };
}
