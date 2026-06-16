import {
  selectHasUnsavedChanges,
  useCharacterEditorStore
} from "@/store/characterEditorStore";

/**
 * Indicador de estado del editor (sección 16):
 * Sin cambios / Cambios pendientes / Guardando / Guardado / Error.
 */
export function SaveStatusBadge() {
  const saveState = useCharacterEditorStore((s) => s.saveState);
  const saveError = useCharacterEditorStore((s) => s.saveError);
  const dirty = useCharacterEditorStore(selectHasUnsavedChanges);

  let label = "Sin cambios";
  let tone = "neutral";

  if (saveState === "saving") {
    label = "Guardando…";
    tone = "info";
  } else if (saveState === "error") {
    label = saveError ?? "Error al guardar";
    tone = "danger";
  } else if (dirty) {
    label = "Cambios pendientes";
    tone = "warning";
  } else if (saveState === "saved") {
    label = "Guardado correctamente";
    tone = "success";
  }

  return <span className={`status-badge status-badge--${tone}`}>{label}</span>;
}
