import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

import { fetchMe, logout } from "@/api/auth.api";
import { ApiError } from "@/api/httpClient";
import type { SessionPayload } from "@/types/api";

const ME_KEY = ["auth", "me"] as const;

/**
 * Hook de sesión. Expone la sesión actual (si la hay) y el estado de carga.
 * Un 401 se interpreta como "no autenticado" (no como error a reintentar).
 */
export function useAuth() {
  const queryClient = useQueryClient();

  const query = useQuery<SessionPayload | null>({
    queryKey: ME_KEY,
    queryFn: async ({ signal }) => {
      try {
        const me = await fetchMe(signal);
        return me.session;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return null;
        }
        throw error;
      }
    },
    retry: false,
    staleTime: 60_000
  });

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(ME_KEY, null);
      queryClient.clear();
    }
  });

  return {
    session: query.data ?? null,
    isLoading: query.isLoading,
    isAuthenticated: !!query.data,
    logout: logoutMutation.mutateAsync,
    isLoggingOut: logoutMutation.isPending
  };
}

export const AUTH_ME_KEY = ME_KEY;
