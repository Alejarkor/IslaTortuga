import { useEffect, useState } from "react";

import { isValidHexColor } from "@/domain/appearance/appearanceSchema";
import { SUGGESTED_PALETTE } from "@/config/characterColorSlots";

/**
 * Selector de color para un slot.
 *
 * - Selector nativo de color (sin canal alfa: no se puede crear transparencia).
 * - Campo de texto hex con validación #RRGGBB en vivo.
 * - Paleta de colores sugeridos para selección rápida.
 *
 * Solo notifica al padre con un color válido.
 */
export function ColorPicker({
  value,
  onChange
}: {
  value: string;
  onChange: (hex: string) => void;
}) {
  const [text, setText] = useState(value);

  // Sincroniza el campo de texto si el valor externo cambia (cancelar, reset…).
  useEffect(() => {
    setText(value);
  }, [value]);

  const commitText = (raw: string) => {
    const next = raw.startsWith("#") ? raw : `#${raw}`;
    setText(next);
    if (isValidHexColor(next)) {
      onChange(next.toUpperCase());
    }
  };

  const invalid = !isValidHexColor(text);

  return (
    <div className="color-picker">
      <div className="color-picker__row">
        <input
          type="color"
          className="color-picker__native"
          value={isValidHexColor(value) ? value : "#000000"}
          onChange={(e) => onChange(e.target.value.toUpperCase())}
          aria-label="Selector de color"
        />
        <input
          type="text"
          className={`color-picker__hex ${invalid ? "is-invalid" : ""}`}
          value={text}
          maxLength={7}
          spellCheck={false}
          onChange={(e) => commitText(e.target.value)}
          aria-label="Código hexadecimal"
        />
      </div>

      <div className="color-picker__palette">
        {SUGGESTED_PALETTE.map((hex) => (
          <button
            key={hex}
            type="button"
            className={`swatch ${
              hex.toLowerCase() === value.toLowerCase() ? "swatch--active" : ""
            }`}
            style={{ backgroundColor: hex }}
            title={hex}
            onClick={() => onChange(hex)}
          />
        ))}
      </div>
    </div>
  );
}
