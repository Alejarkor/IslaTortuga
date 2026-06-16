import { apiRequest } from "./httpClient";
import type { ProfileResponse, StatsResponse } from "@/types/api";

export function fetchProfile(signal?: AbortSignal): Promise<ProfileResponse> {
  return apiRequest<ProfileResponse>("/api/profile", { signal });
}

export function fetchStats(signal?: AbortSignal): Promise<StatsResponse> {
  return apiRequest<StatsResponse>("/api/stats", { signal });
}
