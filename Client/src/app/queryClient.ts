import { QueryClient, QueryCache, MutationCache } from "@tanstack/react-query";

import { ApiError } from "@/api/httpClient";
import { AUTH_ME_KEY } from "@/features/auth/useAuth";

/**
 * Manejo global de sesión caducada: si cualquier query o mutación autenticada
 * devuelve 401, marcamos la sesión como nula. RequireAuth lo detecta y redirige
 * a /login en el siguiente render, sin que el usuario quede viendo errores.
 * (El sondeo /api/me trata su propio 401 como "no autenticado", no llega aquí.)
 */
function handleAuthError(error: unknown) {
  if (error instanceof ApiError && error.status === 401) {
    queryClient.setQueryData(AUTH_ME_KEY, null);
  }
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleAuthError }),
  mutationCache: new MutationCache({ onError: handleAuthError }),
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000
    },
    mutations: {
      retry: 0
    }
  }
});
