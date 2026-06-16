import { useCallback, useMemo, useState } from "react";

import { CharacterPreview } from "./CharacterPreview";
import { HairSelector } from "./HairSelector";
import { ColorColumns } from "./ColorColumns";
import { HairColorRow } from "./HairColorRow";
import { SaveStatusBadge } from "./SaveStatusBadge";
import { useCustomizationOptions } from "../hooks/useCustomizationOptions";
import { useSaveAppearance } from "../hooks/useAppearance";
import { NO_HAIR_ID } from "@/config/characterColorSlots";
import {
  defaultHairDisplayName,
  type HairOption
} from "@/domain/character/customizationOptions";
import {
  selectHasUnsavedChanges,
  useCharacterEditorStore
} from "@/store/characterEditorStore";

const NONE_OPTION: HairOption = {
  hairId: NO_HAIR_ID,
  displayName: "Sin pelo",
  previewUrl: null
};

/** Panel central: visor arriba + subpanel con 5 filas (todo visible, sin scroll). */
export function CharacterEditorPanel() {
  const customizationQuery = useCustomizationOptions();
  const customization = customizationQuery.data;

  const [hairOptions, setHairOptions] = useState<HairOption[]>([NONE_OPTION]);
  const onHairDiscovered = useCallback((ids: string[]) => {
    setHairOptions([
      NONE_OPTION,
      ...ids.map((id) => ({
        hairId: id,
        displayName: defaultHairDisplayName(id),
        previewUrl: null
      }))
    ]);
  }, []);

  const validHairIds = useMemo(
    () => new Set(hairOptions.map((o) => o.hairId)),
    [hairOptions]
  );

  const editing = useCharacterEditorStore((s) => s.editing);
  const dirty = useCharacterEditorStore(selectHasUnsavedChanges);
  const saveState = useCharacterEditorStore((s) => s.saveState);
  const cancelChanges = useCharacterEditorStore((s) => s.cancelChanges);
  const restoreDefaults = useCharacterEditorStore((s) => s.restoreDefaults);
  const saveMutation = useSaveAppearance(validHairIds);
  const saving = saveState === "saving";

  return (
    <div className="lobby-panel char-col wood-frame">
      <div className="lobby-banner">Personaje</div>
      <div className="parch lobby-panel__inner">

        <CharacterPreview
          customization={customization}
          onHairDiscovered={onHairDiscovered}
        />

        <div className="color-subpanel char-controls">
          {/* Fila 1: colores del cuerpo (sin titulo) */}
          <div className="cc-row cc-colors">
            <ColorColumns />
          </div>

          {/* Fila 2: tipos de peinado (titulo dentro) */}
          <div className="cc-row cc-hair">
            <div className="mini-frame hair-frame">
              <p className="section-label inframe-label">Peinado</p>
              <HairSelector hairOptions={hairOptions} />
            </div>
          </div>

          {/* Fila 3: color del pelo (titulo dentro) */}
          <div className="cc-row cc-haircolor">
            <div className="mini-frame haircolor-frame">
              <p className="section-label inframe-label">Color de pelo</p>
              <HairColorRow />
            </div>
          </div>

          {/* Fila 4: estado + acciones */}
          <div className="cc-row cc-actions">
            <SaveStatusBadge />
            <div className="reset-actions">
              <button
                className="mini-btn mini-btn--ghost"
                disabled={!dirty}
                onClick={cancelChanges}
              >
                Cancelar
              </button>
              <button
                className="mini-btn mini-btn--ghost"
                onClick={restoreDefaults}
              >
                Restaurar
              </button>
            </div>
          </div>

          {/* Fila 5: guardar */}
          <button
            className="big-btn cc-save"
            disabled={!dirty || saving || !editing}
            onClick={() => editing && saveMutation.mutate(editing)}
          >
            {saving ? "Guardando…" : "Guardar apariencia"}
          </button>
        </div>
      </div>
    </div>
  );
}
