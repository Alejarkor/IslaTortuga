import { apiRequest } from "./httpClient";
import type {
  RoomsListResponse,
  RoomResponse,
  LaunchResponse
} from "@/types/api";

export type CreateRoomInput = {
  maxPlayers?: number;
  mapId?: string;
  isPrivate?: boolean;
};

/** Lista las salas públicas a las que se puede unir. */
export function fetchRooms(signal?: AbortSignal): Promise<RoomsListResponse> {
  return apiRequest<RoomsListResponse>("/api/rooms", { signal });
}

export function fetchRoom(roomId: string, signal?: AbortSignal): Promise<RoomResponse> {
  return apiRequest<RoomResponse>(`/api/rooms/${roomId}`, { signal });
}

/** Crea una sala. El host (playerId/nickname) lo deduce el WebServer de la sesión. */
export function createRoom(input: CreateRoomInput = {}): Promise<RoomResponse> {
  return apiRequest<RoomResponse>("/api/rooms", { method: "POST", body: input });
}

export function joinRoom(roomId: string): Promise<RoomResponse> {
  return apiRequest<RoomResponse>(`/api/rooms/${roomId}/join`, { method: "POST" });
}

export function joinRoomByCode(code: string): Promise<RoomResponse> {
  return apiRequest<RoomResponse>("/api/rooms/join-by-code", {
    method: "POST",
    body: { code }
  });
}

export function leaveRoom(roomId: string): Promise<RoomResponse> {
  return apiRequest<RoomResponse>(`/api/rooms/${roomId}/leave`, { method: "POST" });
}

export function setReady(roomId: string, ready: boolean): Promise<RoomResponse> {
  return apiRequest<RoomResponse>(`/api/rooms/${roomId}/ready`, {
    method: "POST",
    body: { ready }
  });
}

export function launchRoom(roomId: string): Promise<LaunchResponse> {
  return apiRequest<LaunchResponse>(`/api/rooms/${roomId}/launch`, { method: "POST" });
}
