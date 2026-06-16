import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

// El cliente corre por defecto en :5173 y habla con el WebServer (:3000).
// Se usa un proxy en desarrollo para evitar problemas de CORS y compartir
// la cookie de sesión HttpOnly en el mismo origen.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.VITE_WEB_SERVER_URL ?? "http://localhost:3000",
        changeOrigin: true
      },
      "/assets": {
        target: process.env.VITE_WEB_SERVER_URL ?? "http://localhost:3000",
        changeOrigin: true
      }
    }
  }
});
