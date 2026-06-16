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
