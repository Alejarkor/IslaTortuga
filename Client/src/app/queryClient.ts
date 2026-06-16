import { QueryClient } from "@tanstack/react-query";

/**
 * Cliente de TanStack Query compartido por toda la app.
 * Reintentos conservadores: la apariencia y el perfil no cambian
 * con frecuencia, así que evitamos refetch agresivos.
 */
export const queryClient = new QueryClient({
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
