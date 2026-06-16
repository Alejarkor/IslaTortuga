import { create } from "zustand";

import {
  buildDefaultAppearance,
  type Appearance
} from "@/domain/appearance/appearanceSchema";
import {
  cloneAppearance,
  hasUnsavedChanges
} from "@/domain/appearance/appearanceDiff";

/** Estado del proceso de guardado, para feedback en la UI (sección 16). */
export type SaveState = "idle" | "saving" | "saved" | "error";

type CharacterEditorState = {
  /** Apariencia confirmada en backend (fuente de verdad). */
  saved: Appearance | null;
  /** Apariencia que el usuario está editando localmente. */
  editing: Appearance | null;
  /** Estado del último guardado. */
  saveState: SaveState;
  /** Mensaje de error del último guardado, si lo hubo. */
  saveError: string | null;

  /** Inicializa ambos estados con la apariencia cargada del backend. */
  initialize: (appearance: Appearance) => void;
  /** Cambia el color de un slot en el estado de edición. */
  setColor: (slotId: string, hex: string) => void;
  /** Cambia el pelo seleccionado en el estado de edición. */
  setHair: (hairId: string) => void;
  /** Descarta los cambios locales (volver a lo guardado). */
  cancelChanges: () => void;
  /** Restaura los valores por defecto en el estado de edición. */
  restoreDefaults: () => void;
  /** Marca el inicio del guardado. */
  beginSave: () => void;
  /** Confirma guardado: editing pasa a ser la nueva fuente de verdad. */
  commitSaved: (saved: Appearance) => void;
  /** Marca error de guardado. */
  failSave: (message: string) => void;
};

export const useCharacterEditorStore = create<CharacterEditorState>(
  (set, get) => ({
    saved: null,
    editing: null,
    saveState: "idle",
    saveError: null,

    initialize: (appearance) =>
      set({
        saved: cloneAppearance(appearance),
        editing: cloneAppearance(appearance),
        saveState: "idle",
        saveError: null
      }),

    setColor: (slotId, hex) => {
      const editing = get().editing;
      if (!editing) return;
      set({
        editing: {
          ...editing,
          colors: { ...editing.colors, [slotId]: hex }
        },
        saveState: "idle"
      });
    },

    setHair: (hairId) => {
      const editing = get().editing;
      if (!editing) return;
      set({
        editing: { ...editing, hair_id: hairId },
        saveState: "idle"
      });
    },

    cancelChanges: () => {
      const saved = get().saved;
      if (!saved) return;
      set({
        editing: cloneAppearance(saved),
        saveState: "idle",
        saveError: null
      });
    },

    restoreDefaults: () => {
      const editing = get().editing;
      const defaults = buildDefaultAppearance();
      // Conservamos el body_asset_key actual; solo reseteamos colores y pelo.
      set({
        editing: {
          ...defaults,
          body_asset_key: editing?.body_asset_key ?? defaults.body_asset_key
        },
        saveState: "idle"
      });
    },

    beginSave: () => set({ saveState: "saving", saveError: null }),

    commitSaved: (saved) =>
      set({
        saved: cloneAppearance(saved),
        editing: cloneAppearance(saved),
        saveState: "saved",
        saveError: null
      }),

    failSave: (message) => set({ saveState: "error", saveError: message })
  })
);

/** Selector: ¿hay cambios pendientes de guardar? */
export function selectHasUnsavedChanges(
  state: CharacterEditorState
): boolean {
  if (!state.saved || !state.editing) return false;
  return hasUnsavedChanges(state.saved, state.editing);
}
