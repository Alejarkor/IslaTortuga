import { CHARACTER_COLOR_SLOTS } from "@/config/characterColorSlots";
import { isValidHexColor } from "@/domain/appearance/appearanceSchema";
import { useCharacterEditorStore } from "@/store/characterEditorStore";

const HAIR_PALETTE = [
  "#211710", "#3D2415", "#5C3720", "#7A4A2B", "#A56A43", "#C98F65",
  "#D64545", "#9B2E2E", "#2E2E2E", "#9A9A9A", "#E8E0C8", "#1F6F6B"
];

/** Fila de color para el pelo (slot hair_color). */
export function HairColorRow() {
  const editing = useCharacterEditorStore((s) => s.editing);
  const setColor = useCharacterEditorStore((s) => s.setColor);

  const slot = CHARACTER_COLOR_SLOTS.find((s) => s.target.material === "hair");
  if (!editing || !slot) return null;

  const current = editing.colors[slot.id] ?? slot.defaultColor;

  return (
    <div className="hair-color-row">
      <label
        className="color-col__current"
        style={{ backgroundColor: current }}
        title="Elegir color de pelo"
      >
        <input
          type="color"
          value={isValidHexColor(current) ? current : "#000000"}
          onChange={(e) => setColor(slot.id, e.target.value.toUpperCase())}
        />
      </label>
      {HAIR_PALETTE.map((hex) => (
        <button
          key={hex}
          type="button"
          className={`swatch ${
            hex.toLowerCase() === current.toLowerCase() ? "swatch--active" : ""
          }`}
          style={{ backgroundColor: hex }}
          title={hex}
          onClick={() => setColor(slot.id, hex)}
        />
      ))}
    </div>
  );
}
