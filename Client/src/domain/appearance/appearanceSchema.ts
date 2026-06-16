import { z } from "zod";

import {
  COLOR_SLOT_IDS,
  CHARACTER_COLOR_SLOTS,
  DEFAULT_BODY_ASSET_KEY,
  NO_HAIR_ID
} from "@/config/characterColorSlots";

/**
 * Patrón de color hexadecimal SIN canal alfa.
 * Acepta únicamente #RRGGBB (6 dígitos). Rechaza explícitamente:
 *   red, rgb(...), #FFF, #RRGGBBAA, 123456...
 * No permitir alfa evita que el jugador se vuelva invisible.
 */
export const HEX_COLOR_REGEX = /^#[0-9A-Fa-f]{6}$/;

export const hexColorSchema = z
  .string()
  .regex(HEX_COLOR_REGEX, "El color debe tener formato #RRGGBB");

/** hair_id: 'none' o una clave lógica tipo hair_001. */
export const hairIdSchema = z
  .string()
  .min(1)
  .regex(/^[a-z0-9_]+$/i, "hair_id inválido");

/**
 * Construye dinámicamente el esquema de colores a partir de los slots
 * declarados en la configuración. Cada slot es obligatorio y debe ser hex.
 */
const colorsShape = Object.fromEntries(
  COLOR_SLOT_IDS.map((id) => [id, hexColorSchema])
) as Record<string, typeof hexColorSchema>;

export const colorsSchema = z.object(colorsShape);

export const appearanceSchema = z.object({
  schema_version: z.literal(1).default(1),
  body_asset_key: z.string().min(1).default(DEFAULT_BODY_ASSET_KEY),
  hair_id: hairIdSchema.default(NO_HAIR_ID),
  colors: colorsSchema
});

export type Appearance = z.infer<typeof appearanceSchema>;
export type AppearanceColors = z.infer<typeof colorsSchema>;

/** Valida un color individual; útil para feedback inmediato en la UI. */
export function isValidHexColor(value: string): boolean {
  return HEX_COLOR_REGEX.test(value);
}

/**
 * Normaliza/parsea una apariencia desconocida (p.ej. la guardada en
 * appearance_json, que es jsonb libre y puede venir de un esquema antiguo).
 * Rellena los slots faltantes con sus valores por defecto para garantizar
 * robustez ante datos legacy. Devuelve siempre una apariencia válida.
 */
export function coerceAppearance(raw: unknown): Appearance {
  const base = buildDefaultAppearance();

  if (!raw || typeof raw !== "object") {
    return base;
  }

  const obj = raw as Record<string, unknown>;

  const merged: Appearance = {
    schema_version: 1,
    body_asset_key:
      typeof obj.body_asset_key === "string" && obj.body_asset_key.length > 0
        ? obj.body_asset_key
        : base.body_asset_key,
    hair_id:
      typeof obj.hair_id === "string" && obj.hair_id.length > 0
        ? obj.hair_id
        : base.hair_id,
    colors: { ...base.colors }
  };

  const rawColors =
    obj.colors && typeof obj.colors === "object"
      ? (obj.colors as Record<string, unknown>)
      : {};

  for (const slot of CHARACTER_COLOR_SLOTS) {
    const candidate = rawColors[slot.id];
    if (typeof candidate === "string" && isValidHexColor(candidate)) {
      merged.colors[slot.id] = candidate;
    }
  }

  return merged;
}

/** Construye la apariencia por defecto a partir de la configuración de slots. */
export function buildDefaultAppearance(): Appearance {
  const colors: Record<string, string> = {};
  for (const slot of CHARACTER_COLOR_SLOTS) {
    colors[slot.id] = slot.defaultColor;
  }

  return {
    schema_version: 1,
    body_asset_key: DEFAULT_BODY_ASSET_KEY,
    hair_id: NO_HAIR_ID,
    colors
  };
}
