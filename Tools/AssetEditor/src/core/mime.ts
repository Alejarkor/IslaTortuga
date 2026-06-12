const MIME_BY_EXTENSION: Record<string, string> = {
  png: "image/png",
  jpg: "image/jpeg",
  jpeg: "image/jpeg",
  webp: "image/webp",
  gif: "image/gif",
  svg: "image/svg+xml",
  glb: "model/gltf-binary",
  gltf: "model/gltf+json",
  ogg: "audio/ogg",
  mp3: "audio/mpeg",
  wav: "audio/wav",
  json: "application/json",
  glsl: "text/plain",
  vert: "text/plain",
  frag: "text/plain",
  txt: "text/plain",
  atlas: "text/plain",
  fnt: "text/plain",
  bin: "application/octet-stream"
};

export function mimeFromExtension(extension: string): string {
  return MIME_BY_EXTENSION[extension.toLowerCase()] ?? "application/octet-stream";
}

/** Extensiones de archivos de asset soportadas por el escáner */
export const SUPPORTED_EXTENSIONS = new Set(Object.keys(MIME_BY_EXTENSION));
