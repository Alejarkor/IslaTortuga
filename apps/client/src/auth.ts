import { getMe, type AuthResponse, type UserDto } from './api';

const TOKEN_KEY = 'isla_tortuga_access_token';

export function saveAuth(response: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, response.accessToken);
}

export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY);
}

export async function loadCurrentUser(): Promise<UserDto | null> {
  const token = getStoredToken();

  if (!token) {
    return null;
  }

  try {
    return await getMe(token);
  } catch {
    clearAuth();
    return null;
  }
}
