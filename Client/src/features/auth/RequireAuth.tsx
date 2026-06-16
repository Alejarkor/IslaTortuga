import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";

import { useAuth } from "./useAuth";
import { Spinner } from "@/ui/Spinner";

/**
 * Guard de ruta: protege /pre-game.
 * Mientras se resuelve la sesión muestra un spinner; si no hay sesión,
 * redirige a /login.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isLoading, isAuthenticated } = useAuth();

  if (isLoading) {
    return (
      <div className="centered-screen">
        <Spinner label="Comprobando sesión…" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}
