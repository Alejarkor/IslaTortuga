import { createBrowserRouter, Navigate } from "react-router-dom";

import { LoginPage } from "@/features/auth/LoginPage";
import { RegisterPage } from "@/features/auth/RegisterPage";
import { PreGamePage } from "@/features/pregame/PreGamePage";
import { RequireAuth } from "@/features/auth/RequireAuth";

/**
 * Rutas de la aplicación.
 * /pre-game queda protegida por sesión (RequireAuth).
 */
export const router = createBrowserRouter([
  {
    path: "/login",
    element: <LoginPage />
  },
  {
    path: "/register",
    element: <RegisterPage />
  },
  {
    path: "/pre-game",
    element: (
      <RequireAuth>
        <PreGamePage />
      </RequireAuth>
    )
  },
  {
    path: "/",
    element: <Navigate to="/pre-game" replace />
  },
  {
    path: "*",
    element: <Navigate to="/pre-game" replace />
  }
]);
