import { Button } from "@/ui/Button";
import {
  selectHasUnsavedChanges,
  useCharacterEditorStore
} from "@/store/characterEditorStore";
import { useSaveAppearance } from "../hooks/useAppearance";

/**
 * Botón de guardado explícito (sin autosave).
 * Solo se habilita cuando hay cambios pendientes.
 */
export function SaveAppearanceButton({
  validHairIds
}: {
  validHairIds: Set<string>;
}) {
  const editing = useCharacterEditorStore((s) => s.editing);
  const dirty = useCharacterEditorStore(selectHasUnsavedChanges);
  const saveState = useCharacterEditorStore((s) => s.saveState);
  const saveMutation = useSaveAppearance(validHairIds);

  const saving = saveState === "saving";

  return (
    <Button
      variant="primary"
      disabled={!dirty || saving || !editing}
      onClick={() => editing && saveMutation.mutate(editing)}
    >
      {saving ? "Guardando…" : "Guardar cambios"}
    </Button>
  );
}
