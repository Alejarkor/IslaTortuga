import { apiRequest } from "./httpClient";
import { env } from "@/config/env";
import type { ManifestResponse } from "@/types/api";

/**
 * Descarga el manifest del personaje base.
 * El manifest define qué assets (cuerpo, máscara, pelos y previews) hay que
 * descargar del servidor de assets. El cliente nunca guarda rutas físicas:
 * resuelve los assets a través de este manifest.
 */
export function fetchCharacterManifest(
  signal?: AbortSignal
): Promise<ManifestResponse> {
  const params = new URLSearchParams({
    targetType: env.characterManifestTargetType,
    targetId: env.characterManifestTargetId
  });

  return apiRequest<ManifestResponse>(`/assets/manifest?${params.toString()}`, {
    signal
  });
}

/**
 * Descarga el manifest de la pantalla de login (fondo + panel).
 * Es público (no requiere sesión).
 */
export function fetchLoginManifest(
  signal?: AbortSignal
): Promise<ManifestResponse> {
  const params = new URLSearchParams({
    targetType: env.loginManifestTargetType,
    targetId: env.loginManifestTargetId
  });

  return apiRequest<ManifestResponse>(`/assets/manifest?${params.toString()}`, {
    signal
  });
}

/** Manifest de assets de interfaz (marcos, botones, iconos…). Público. */
export function fetchUiManifest(signal?: AbortSignal): Promise<ManifestResponse> {
  const params = new URLSearchParams({
    targetType: env.uiManifestTargetType,
    targetId: env.uiManifestTargetId
  });
  return apiRequest<ManifestResponse>(`/assets/manifest?${params.toString()}`, {
    signal
  });
}
