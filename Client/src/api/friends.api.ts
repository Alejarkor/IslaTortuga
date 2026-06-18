import { apiRequest } from "./httpClient";

export type Friend = {
  player_id: string;
  nickname: string;
  appearance_json: unknown;
  friends_since?: string;
};

export type IncomingRequest = {
  friend_request_id: string;
  from_player_id: string;
  from_nickname: string;
};

export type OutgoingRequest = {
  friend_request_id: string;
  to_player_id: string;
  to_nickname: string;
};

export function fetchFriends(signal?: AbortSignal) {
  return apiRequest<{ ok: boolean; friends: Friend[] }>("/api/friends", {
    signal
  });
}

export function fetchIncomingRequests(signal?: AbortSignal) {
  return apiRequest<{ ok: boolean; incomingRequests: IncomingRequest[] }>(
    "/api/friends/requests/incoming",
    { signal }
  );
}

export function fetchOutgoingRequests(signal?: AbortSignal) {
  return apiRequest<{ ok: boolean; outgoingRequests: OutgoingRequest[] }>(
    "/api/friends/requests/outgoing",
    { signal }
  );
}

/** Envía una solicitud de amistad por nickname (el fromPlayerId lo pone el WebServer). */
export function sendFriendRequest(nickname: string) {
  return apiRequest<{ ok: boolean }>("/api/friends/requests", {
    method: "POST",
    body: { nickname }
  });
}

export function acceptFriendRequest(requestId: string) {
  return apiRequest<{ ok: boolean }>(
    `/api/friends/requests/${requestId}/accept`,
    { method: "POST" }
  );
}

export function rejectFriendRequest(requestId: string) {
  return apiRequest<{ ok: boolean }>(
    `/api/friends/requests/${requestId}/reject`,
    { method: "POST" }
  );
}
