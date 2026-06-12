import { AssetType } from "../types";

/** asset_type según carpeta raíz del archivo */
const TYPE_BY_FOLDER: Record<string, AssetType> = {
  textures: "texture",
  texture: "texture",
  sprites: "sprite",
  sprite: "sprite",
  models: "model",
  model: "model",
  audio: "audio",
  sounds: "audio",
  maps: "map",
  map: "map",
  shaders: "shader",
  shader: "shader",
  materials: "material",
  animations: "animation",
  data: "data"
};

/** asset_type según extensión, como fallback */
const TYPE_BY_EXTENSION: Record<string, AssetType> = {
  png: "texture",
  jpg: "texture",
  jpeg: "texture",
  webp: "texture",
  gif: "sprite",
  glb: "model",
  gltf: "model",
  ogg: "audio",
  mp3: "audio",
  wav: "audio",
  glsl: "shader",
  vert: "shader",
  frag: "shader",
  json: "data",
  txt: "data",
  atlas: "data",
  fnt: "data"
};

export function inferAssetType(relPath: string, extension: string): string {
  const topFolder = relPath.split("/")[0]?.toLowerCase() ?? "";

  return (
    TYPE_BY_FOLDER[topFolder] ??
    TYPE_BY_EXTENSION[extension.toLowerCase()] ??
    "data"
  );
}

/** Detecta sufijos de versión tipo _v001, _v1, _v2 en el nombre del archivo */
const VERSION_PATTERN = /_v(\d+)$/i;

export function inferVersion(fileNameWithoutExtension: string): string {
  const match = fileNameWithoutExtension.match(VERSION_PATTERN);

  return match ? String(Number(match[1])) : "1";
}

/** asset_key: ruta relativa sin extensión y sin sufijo de versión */
export function inferAssetKey(relPath: string): string {
  const withoutExtension = relPath.replace(/\.[^./]+$/, "");

  return withoutExtension.replace(VERSION_PATTERN, "");
}

/**
 * asset_file_id estable generado desde asset_key + version.
 * Ejemplo: textures/player_common + 1 -> file_textures_player_common_v001
 */
export function buildAssetFileId(assetKey: string, version: string): string {
  const keyPart = assetKey
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");

  const versionPart = /^\d+$/.test(version)
    ? `v${version.padStart(3, "0")}`
    : `v${version.replace(/[^a-zA-Z0-9]+/g, "_")}`;

  return `file_${keyPart}_${versionPart}`;
}
