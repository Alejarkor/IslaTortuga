import type { Appearance } from "./appearanceSchema";
import { COLOR_SLOT_IDS } from "@/config/characterColorSlots";

/**
 * Compara dos apariencias por valor (no por referencia).
 * Separar estado guardado del estado en edición permite detectar
 * cambios pendientes de forma fiable: hasUnsavedChanges = !equals.
 */
export function appearanceEquals(a: Appearance, b: Appearance): boolean {
  if (a.body_asset_key !== b.body_asset_key) return false;
  if (a.hair_id !== b.hair_id) return false;

  for (const id of COLOR_SLOT_IDS) {
    if (a.colors[id]?.toLowerCase() !== b.colors[id]?.toLowerCase()) {
      return false;
    }
  }

  return true;
}

export function hasUnsavedChanges(
  saved: Appearance,
  editing: Appearance
): boolean {
  return !appearanceEquals(saved, editing);
}

/** Clona en profundidad una apariencia (estructura simple, sin referencias). */
export function cloneAppearance(appearance: Appearance): Appearance {
  return {
    schema_version: appearance.schema_version,
    body_asset_key: appearance.body_asset_key,
    hair_id: appearance.hair_id,
    colors: { ...appearance.colors }
  };
}
