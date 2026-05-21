function getDefaultApiBaseUrl() {
  if (typeof window === 'undefined') {
    return 'http://localhost:3000';
  }

  return '/api';
}

const API_BASE_URL = import.meta.env.VITE_API_URL ?? getDefaultApiBaseUrl();

export type UserDto = {
  id: string;
  email: string;
  profile: {
    id: string;
    nickname: string;
    avatarId: string | null;
  } | null;
};

export type AuthResponse = {
  accessToken: string;
  user: UserDto;
};

export type StartGameResponse = {
  roomId: string;
  gameUrl: string;
  gameTicket: string;
  contentPackId: string;
  contentVersion: string;
  mapId: string;
  manifestUrl: string;
  webSocketUrl: string;
};

export async function request<TResponse>(
  path: string,
  options: RequestInit = {},
): Promise<TResponse> {
  let response: Response;

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });
  } catch {
    throw new Error('No se pudo conectar con la API. Comprueba que el backend este levantado en el puerto 3000.');
  }

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const message = data?.message ?? 'Request failed';
    throw new Error(Array.isArray(message) ? message.join(', ') : message);
  }

  return data as TResponse;
}

export function registerUser(input: {
  email: string;
  password: string;
  nickname: string;
}) {
  return request<AuthResponse>('/auth/register', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function loginUser(input: { email: string; password: string }) {
  return request<AuthResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function getMe(token: string) {
  return request<UserDto>('/auth/me', {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });
}

export function startDevGame(token: string) {
  return request<StartGameResponse>('/dev/start-game', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });
}
