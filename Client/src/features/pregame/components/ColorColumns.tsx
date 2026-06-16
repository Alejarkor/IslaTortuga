import { CHARACTER_COLOR_SLOTS } from "@/config/characterColorSlots";
import { isValidHexColor } from "@/domain/appearance/appearanceSchema";
import { useCharacterEditorStore } from "@/store/characterEditorStore";

/** Paletas por slot (7 muestras + 1 selector personalizado). */
const SLOT_PALETTES: Record<string, string[]> = {
  skin: ["#FBE0C4", "#F2C29A", "#C98F65", "#A56A43", "#7A4A2B", "#5C3720", "#3D2415"],
  eyes: ["#3A5F85", "#2E7BDC", "#1FA84F", "#6B4E2E", "#8A5A2B", "#3A3A3A", "#6E3B8E"],
  clothes_primary: ["#2E7BDC", "#D64545", "#1FA84F", "#F2C14E", "#9B59B6", "#E8E0C8", "#3A3A3A"],
  clothes_secondary: ["#3A3A3A", "#5C3720", "#1F6F6B", "#7A6A4A", "#9B2E2E", "#1A1A1A", "#E8E0C8"]
};
const FALLBACK = ["#C98F65", "#2E7BDC", "#3A3A3A", "#1FA84F", "#D64545", "#211710", "#E8E0C8"];

/**
 * Columnas de color (Piel, Ojos, Ropa 1, Ropa 2) en una sola línea: todas bajo
 * un minipanel (la rejilla) y cada grupo dentro de su propio minipanel.
 */
export function ColorColumns() {
  const editing = useCharacterEditorStore((s) => s.editing);
  const setColor = useCharacterEditorStore((s) => s.setColor);

  if (!editing) return null;

  const bodySlots = CHARACTER_COLOR_SLOTS.filter(
    (slot) => slot.target.material === "body"
  );

  return (
    <div className="color-columns mini-frame">
      {bodySlots.map((slot) => {
        const current = editing.colors[slot.id] ?? slot.defaultColor;
        const palette = SLOT_PALETTES[slot.id] ?? FALLBACK;

        return (
          <div key={slot.id} className="color-col mini-frame">
            <span className="color-col__label">{slot.displayName}</span>
            <div className="swatch-grid">
              {palette.map((hex) => (
                <button
                  key={hex}
                  type="button"
                  className={`swatch ${
                    hex.toLowerCase() === current.toLowerCase()
                      ? "swatch--active"
                      : ""
                  }`}
                  style={{ backgroundColor: hex }}
                  title={hex}
                  onClick={() => setColor(slot.id, hex)}
                />
              ))}
              <label className="swatch swatch--custom" title="Color personalizado">
                <input
                  type="color"
                  value={isValidHexColor(current) ? current : "#000000"}
                  onChange={(e) => setColor(slot.id, e.target.value.toUpperCase())}
                />
              </label>
            </div>
          </div>
        );
      })}
    </div>
  );
}
