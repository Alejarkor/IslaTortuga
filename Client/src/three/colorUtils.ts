import { Color3 } from "@babylonjs/core/Maths/math.color";

/**
 * Convierte un color #RRGGBB a Color3 de Babylon.
 * Babylon trabaja en espacio lineal; toLinearSpace evita que los colores
 * se vean lavados respecto al valor hex elegido por el usuario.
 */
export function hexToColor3(hex: string): Color3 {
  const clean = hex.replace("#", "");
  const r = parseInt(clean.substring(0, 2), 16) / 255;
  const g = parseInt(clean.substring(2, 4), 16) / 255;
  const b = parseInt(clean.substring(4, 6), 16) / 255;
  return new Color3(r, g, b).toLinearSpace();
}
