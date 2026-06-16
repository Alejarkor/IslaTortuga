import { useQuery } from "@tanstack/react-query";

import { fetchCharacterManifest } from "@/api/assets.api";
import { ApiError } from "@/api/httpClient";
import { env } from "@/config/env";
import {
  buildCustomizationFromManifest,
  type CharacterCustomization
} from "@/domain/character/customizationOptions";

const MANIFEST_KEY = ["character", "manifest"] as const;

/**
 * Obtiene las opciones de personalización derivadas del manifest del personaje.
 * Si el manifest no existe / no está vigente (404), devuelve un catálogo mínimo
 * para funcionar con el maniquí procedural de respaldo.
 *
 * Registra diagnóstico en consola para depurar por qué se cae al fallback.
 */
export function useCustomizationOptions() {
  return useQuery<CharacterCustomization>({
    queryKey: MANIFEST_KEY,
    queryFn: async ({ signal }) => {
      try {
        const manifest = await fetchCharacterManifest(signal);
        const customization = buildCustomizationFromManifest(manifest);

        console.info(
          "[IslaTortuga] Manifest cargado:",
          {
            targetType: env.characterManifestTargetType,
            targetId: env.characterManifestTargetId,
            ficheros: manifest.files?.map((f) => ({
              assetKey: f.assetKey,
              assetType: f.assetType,
              usage: f.usage
            })),
            cuerpoResuelto: customization.body.modelUrl,
            packPeloResuelto: customization.hairPackUrl
          }
        );

        if (!customization.body.modelUrl) {
          console.warn(
            "[IslaTortuga] El manifest no contiene un GLB de cuerpo que case con",
            env.bodyAssetKey,
            "→ se usa el maniquí de respaldo. Revisa VITE_CHARACTER_BODY_ASSET_KEY."
          );
        }

        return customization;
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          console.warn(
            `[IslaTortuga] Manifest no encontrado (404) para targetType=${env.characterManifestTargetType} targetId=${env.characterManifestTargetId}.`,
            "Comprueba que está publicado Y marcado como current, y que el .env apunta al targetId correcto. Usando maniquí de respaldo."
          );
          return buildCustomizationFromManifest(null);
        }
        console.error("[IslaTortuga] Error al cargar el manifest:", error);
        throw error;
      }
    },
    staleTime: 5 * 60_000
  });
}
