import { apiRequest } from "./httpClient";
import type { AuthResponse, MeResponse } from "@/types/api";

export type LoginInput = {
  usernameOrEmail: string;
  password: string;
};

export type RegisterInput = {
  username: string;
  email: string;
  password: string;
  nickname: string;
};

/** Devuelve la sesión actual o lanza ApiError 401 si no hay sesión. */
export function fetchMe(signal?: AbortSignal): Promise<MeResponse> {
  return apiRequest<MeResponse>("/api/me", { signal });
}

export function login(input: LoginInput): Promise<AuthResponse> {
  return apiRequest<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: input
  });
}

export function register(input: RegisterInput): Promise<AuthResponse> {
  return apiRequest<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: input
  });
}

export function logout(): Promise<{ ok: boolean }> {
  return apiRequest<{ ok: boolean }>("/api/auth/logout", {
    method: "POST"
  });
}
