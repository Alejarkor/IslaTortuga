import { useQuery } from "@tanstack/react-query";

import { fetchLoginManifest } from "@/api/assets.api";
import { ApiError } from "@/api/httpClient";
import { env } from "@/config/env";
import type { ManifestFile } from "@/types/api";

export type LoginAssets = {
  backgroundUrl: string | null;
  panelUrl: string | null;
};

const BG_HINT = /loginbg|background|fondo|_bg(_|\.|$)/i;
const PANEL_HINT = /panel|pergamino|parchment/i;

function pick(files: ManifestFile[], hint: RegExp): string | null {
  const file = files.find((f) => hint.test(f.assetKey));
  return file?.downloadUrl ?? null;
}

/**
 * Carga los assets de la pantalla de login (fondo + panel pergamino) desde el
 * manifest de login. Si el manifest no existe (404), devuelve null y la UI usa
 * el fondo/panel de respaldo en CSS.
 */
export function useLoginAssets() {
  return useQuery<LoginAssets>({
    queryKey: ["login", "assets"],
    queryFn: async ({ signal }) => {
      try {
        const manifest = await fetchLoginManifest(signal);
        const files = manifest.files ?? [];
        const assets: LoginAssets = {
          backgroundUrl: pick(files, BG_HINT),
          panelUrl: pick(files, PANEL_HINT)
        };
        console.info("[IslaTortuga] Assets de login:", {
          targetId: env.loginManifestTargetId,
          ficheros: files.map((f) => f.assetKey),
          ...assets
        });
        return assets;
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          console.warn(
            `[IslaTortuga] Manifest de login no encontrado (targetType=${env.loginManifestTargetType} targetId=${env.loginManifestTargetId}). Usando fondo CSS de respaldo.`
          );
          return { backgroundUrl: null, panelUrl: null };
        }
        throw error;
      }
    },
    staleTime: 10 * 60_000,
    retry: false
  });
}
