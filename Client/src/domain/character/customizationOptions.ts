import type { ManifestFile, ManifestResponse } from "@/types/api";
import { env } from "@/config/env";

/**
 * Una opción de pelo. El pelo NO es un fichero independiente: los 8 estilos
 * viven como nodos (Pelo1..Pelo8) dentro de un único GLB ("pack" de pelo).
 * Por eso las opciones se DESCUBREN en tiempo de ejecución al cargar el pack,
 * y el `hairId` es el nombre del nodo dentro del GLB.
 */
export type HairOption = {
  hairId: string;
  displayName: string;
  /** URL de preview, si algún día se añade al manifest. */
  previewUrl: string | null;
};

/** Assets del cuerpo resueltos desde el manifest. */
export type BodyAssets = {
  /** GLB del cuerpo. */
  modelUrl: string | null;
  /** Textura máscara RGBA del cuerpo (opcional; aún no presente). */
  maskUrl: string | null;
  /** Textura base/albedo del cuerpo (opcional). */
  baseColorUrl: string | null;
};

export type CharacterCustomization = {
  body: BodyAssets;
  /** GLB único que contiene todos los estilos de pelo. */
  hairPackUrl: string | null;
};

const HAIR_HINT = /pelo|hair/i;
const MASK_HINT = /mask|mascara|máscara/i;
const BASECOLOR_HINT = /albedo|base.?color|basecolor/i;

/** Versión numérica de un fichero (string -> número, 0 si no parsea). */
function versionNum(file: ManifestFile): number {
  const n = parseInt(String(file.version ?? ""), 10);
  return Number.isFinite(n) ? n : 0;
}

/** De varios candidatos, elige el de versión más alta. */
function pickLatest(files: ManifestFile[]): ManifestFile | undefined {
  if (files.length === 0) return undefined;
  return [...files].sort((a, b) => versionNum(b) - versionNum(a))[0];
}

/** Mejor fichero para un assetKey: el de versión más alta con ese key. */
function bestByKey(
  files: ManifestFile[],
  assetKey: string
): ManifestFile | undefined {
  return pickLatest(files.filter((f) => f.assetKey === assetKey));
}

function isModel(file: ManifestFile): boolean {
  return (
    file.assetType === "model" ||
    /\.(glb|gltf)(\?|$)/i.test(file.downloadUrl ?? "")
  );
}

/**
 * Construye el catálogo de assets a partir del manifest.
 *
 * Como la herramienta de assets deja `usage` a null, identificamos cada GLB
 * por su `assetKey` (configurable en .env) con heurística de respaldo:
 *   - pack de pelo: assetKey == VITE_CHARACTER_HAIR_ASSET_KEY, o que contenga
 *     "pelo"/"hair".
 *   - cuerpo: assetKey == VITE_CHARACTER_BODY_ASSET_KEY, o el otro modelo.
 *   - máscara/albedo del cuerpo: por `usage` (body_mask/body_base) o por nombre.
 */
export function buildCustomizationFromManifest(
  manifest: ManifestResponse | null
): CharacterCustomization {
  const files = manifest?.files ?? [];
  const models = files.filter(isModel);

  // --- Pack de pelo (versión más alta si hay varias) ---
  const hairFile =
    bestByKey(models, env.hairAssetKey) ??
    pickLatest(models.filter((f) => HAIR_HINT.test(f.assetKey)));

  // --- Cuerpo (versión más alta si hay varias) ---
  const bodyFile =
    bestByKey(models, env.bodyAssetKey) ??
    pickLatest(models.filter((f) => f !== hairFile));

  // --- Texturas opcionales del cuerpo (máscara / albedo) ---
  const maskFile =
    files.find((f) => f.usage === "body_mask") ??
    files.find((f) => f.assetType === "texture" && MASK_HINT.test(f.assetKey));

  const baseColorFile =
    files.find((f) => f.usage === "body_base") ??
    files.find(
      (f) => f.assetType === "texture" && BASECOLOR_HINT.test(f.assetKey)
    );

  return {
    body: {
      modelUrl: bodyFile?.downloadUrl ?? null,
      maskUrl: maskFile?.downloadUrl ?? null,
      baseColorUrl: baseColorFile?.downloadUrl ?? null
    },
    hairPackUrl: hairFile?.downloadUrl ?? null
  };
}

/** Nombre legible por defecto a partir del id de nodo (Pelo3 -> "Pelo 3"). */
export function defaultHairDisplayName(hairId: string): string {
  const numeric = hairId.match(/(\d+)/)?.[1];
  if (numeric) return `Pelo ${numeric}`;
  return hairId.charAt(0).toUpperCase() + hairId.slice(1);
}
