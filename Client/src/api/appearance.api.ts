import { apiRequest } from "./httpClient";
import type { ProfileResponse } from "@/types/api";
import {
  appearanceSchema,
  coerceAppearance,
  type Appearance
} from "@/domain/appearance/appearanceSchema";
import { fetchProfile } from "./profile.api";

/**
 * Carga la apariencia guardada del jugador.
 * La fuente es player_profiles.appearance_json (jsonb libre), que se
 * normaliza a una Appearance válida y completa mediante coerceAppearance.
 */
export async function fetchAppearance(
  signal?: AbortSignal
): Promise<Appearance> {
  const profile = await fetchProfile(signal);
  return coerceAppearance(profile.profile.appearance_json);
}

/**
 * Persiste la apariencia.
 * Valida en cliente con Zod antes de enviar (colores #RRGGBB, hair_id, etc.)
 * y usa el endpoint existente PATCH /api/profile/appearance, cuyo backend
 * guarda el objeto tal cual en appearance_json.
 *
 * La validación de existencia/activación del hair_id se realiza contra el
 * catálogo derivado del manifest antes de llamar aquí (ver useAppearance).
 */
export async function saveAppearance(
  appearance: Appearance
): Promise<Appearance> {
  // Lanza ZodError si algún color o campo es inválido (defensa en cliente).
  const validated = appearanceSchema.parse(appearance);

  const response = await apiRequest<ProfileResponse>(
    "/api/profile/appearance",
    {
      method: "PATCH",
      body: { appearance: validated }
    }
  );

  return coerceAppearance(response.profile.appearance_json);
}
