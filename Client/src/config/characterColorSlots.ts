/**
 * Definición de los canales de color editables del personaje.
 *
 * Modelo de color (según definición del proyecto):
 *  - El material del CUERPO usa una textura máscara RGBA. Cada canal (R, G, B, A)
 *    delimita una zona del cuerpo que se tiñe con un color independiente.
 *  - El material del PELO no usa máscara: se tiñe por completo con un único color.
 *
 * Esta tabla es el único punto donde se declara qué slots existen, cómo se
 * pintan (a qué material/canal afectan) y su valor por defecto. Añadir un
 * segundo mapa de máscara en el futuro (tatuajes, sombras, marcas) es tan
 * simple como añadir más entradas con otra `maskTexture`.
 */

export type ColorSlotTarget =
  | { material: "body"; channel: "r" | "g" | "b" | "a" }
  | { material: "hair" };

export type ColorSlotDef = {
  /** Identificador estable usado como clave en appearance.colors. */
  id: string;
  /** Nombre legible mostrado en la interfaz. */
  displayName: string;
  /** Color por defecto en formato #RRGGBB. */
  defaultColor: string;
  /** A qué material/canal afecta este slot. */
  target: ColorSlotTarget;
};

export const CHARACTER_COLOR_SLOTS: ColorSlotDef[] = [
  {
    id: "skin",
    displayName: "Piel",
    defaultColor: "#C98F65",
    target: { material: "body", channel: "r" }
  },
  {
    id: "eyes",
    displayName: "Ojos",
    defaultColor: "#3A5F85",
    target: { material: "body", channel: "g" }
  },
  {
    id: "clothes_primary",
    displayName: "Ropa 1",
    defaultColor: "#2E7BDC",
    target: { material: "body", channel: "b" }
  },
  {
    id: "clothes_secondary",
    displayName: "Ropa 2",
    defaultColor: "#3A3A3A",
    target: { material: "body", channel: "a" }
  },
  {
    id: "hair_color",
    displayName: "Color del pelo",
    defaultColor: "#211710",
    target: { material: "hair" }
  }
];

/** Mapa id -> definición, para acceso O(1). */
export const COLOR_SLOTS_BY_ID: Readonly<Record<string, ColorSlotDef>> =
  Object.fromEntries(CHARACTER_COLOR_SLOTS.map((slot) => [slot.id, slot]));

/** Lista de ids de slots, en orden de presentación. */
export const COLOR_SLOT_IDS = CHARACTER_COLOR_SLOTS.map((slot) => slot.id);

/** Asset key por defecto del cuerpo base. */
export const DEFAULT_BODY_ASSET_KEY = "models/IT_Character - Rigged";

/** hair_id que representa "sin pelo". */
export const NO_HAIR_ID = "none";

/**
 * Paleta sugerida de colores rápidos. El usuario puede elegir cualquier color
 * con el selector avanzado; esta paleta solo acelera la selección habitual.
 */
export const SUGGESTED_PALETTE: string[] = [
  "#C98F65", "#8D5524", "#FFDBAC", "#3A3A3A",
  "#211710", "#2E7BDC", "#3A5F85", "#1FA84F",
  "#D64545", "#F2C14E", "#9B59B6", "#FFFFFF",
  "#E91E63", "#00BCD4", "#7F8C8D", "#000000"
];
