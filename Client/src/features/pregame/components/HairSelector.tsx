import { useCharacterEditorStore } from "@/store/characterEditorStore";
import { NO_HAIR_ID } from "@/config/characterColorSlots";
import type { HairOption } from "@/domain/character/customizationOptions";

/**
 * Selector de modelo de pelo.
 * Las opciones provienen de los nodos descubiertos en el pack de pelo
 * (Pelo1..Pelo8) más la opción "Sin pelo", que se añade siempre.
 */
export function HairSelector({
  hairOptions
}: {
  hairOptions: HairOption[];
}) {
  const editing = useCharacterEditorStore((s) => s.editing);
  const setHair = useCharacterEditorStore((s) => s.setHair);

  if (!editing) return null;

  return (
    <div className="hair-selector">
      {hairOptions.map((option) => {
        const selected = option.hairId === editing.hair_id;
        return (
          <button
            key={option.hairId}
            type="button"
            className={`hair-option ${selected ? "hair-option--selected" : ""}`}
            onClick={() => setHair(option.hairId)}
            title={option.displayName}
          >
            <span className="hair-option__thumb">
              {option.previewUrl ? (
                <img src={option.previewUrl} alt={option.displayName} />
              ) : (
                <span className="hair-option__placeholder">
                  {option.hairId === NO_HAIR_ID
                    ? "∅"
                    : (option.displayName.match(/\d+/)?.[0] ??
                      option.displayName.charAt(0))}
                </span>
              )}
            </span>
            <span className="hair-option__label">{option.displayName}</span>
          </button>
        );
      })}
    </div>
  );
}
