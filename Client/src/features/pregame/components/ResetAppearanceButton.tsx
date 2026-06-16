import { Button } from "@/ui/Button";
import {
  selectHasUnsavedChanges,
  useCharacterEditorStore
} from "@/store/characterEditorStore";

/**
 * Acciones de descarte: cancelar cambios (volver a lo guardado) y
 * restaurar valores por defecto.
 */
export function ResetAppearanceButton() {
  const dirty = useCharacterEditorStore(selectHasUnsavedChanges);
  const cancelChanges = useCharacterEditorStore((s) => s.cancelChanges);
  const restoreDefaults = useCharacterEditorStore((s) => s.restoreDefaults);

  return (
    <div className="reset-actions">
      <Button variant="ghost" disabled={!dirty} onClick={cancelChanges}>
        Cancelar cambios
      </Button>
      <Button variant="ghost" onClick={restoreDefaults}>
        Restaurar por defecto
      </Button>
    </div>
  );
}
