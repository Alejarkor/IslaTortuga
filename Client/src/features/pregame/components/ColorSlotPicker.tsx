import { useState } from "react";

import { CHARACTER_COLOR_SLOTS } from "@/config/characterColorSlots";
import { useCharacterEditorStore } from "@/store/characterEditorStore";
import { ColorPicker } from "./ColorPicker";

/**
 * Lista de slots de color. Cada fila abre un ColorPicker.
 * Solo se muestra un selector expandido a la vez para mantener la pantalla
 * limpia y poco técnica (sección 18).
 */
export function ColorSlotPicker() {
  const editing = useCharacterEditorStore((s) => s.editing);
  const setColor = useCharacterEditorStore((s) => s.setColor);
  const [openSlot, setOpenSlot] = useState<string | null>(null);

  if (!editing) return null;

  return (
    <div className="color-slots">
      {CHARACTER_COLOR_SLOTS.map((slot) => {
        const current = editing.colors[slot.id] ?? slot.defaultColor;
        const isOpen = openSlot === slot.id;

        return (
          <div key={slot.id} className="color-slots__item">
            <button
              type="button"
              className="color-slots__header"
              onClick={() => setOpenSlot(isOpen ? null : slot.id)}
              aria-expanded={isOpen}
            >
              <span
                className="color-slots__chip"
                style={{ backgroundColor: current }}
              />
              <span className="color-slots__name">{slot.displayName}</span>
              <span className="color-slots__value">{current.toUpperCase()}</span>
            </button>

            {isOpen && (
              <ColorPicker
                value={current}
                onChange={(hex) => setColor(slot.id, hex)}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
