import path from "path";
import { config } from "../config";

/**
 * Normaliza una ruta relativa dentro de server_assets y garantiza
 * que no escapa de la raíz (protección contra path traversal).
 * Devuelve la ruta relativa con separadores '/' o lanza Error.
 */
export function safeRelativePath(relPath: string): string {
  const normalized = relPath.replace(/\\/g, "/").replace(/^\/+/, "");

  if (!normalized || normalized.includes("..")) {
    throw new Error(`Ruta no permitida: ${relPath}`);
  }

  const absolute = path.resolve(config.assetsRoot, normalized);
  const rootWithSep = config.assetsRoot.endsWith(path.sep)
    ? config.assetsRoot
    : config.assetsRoot + path.sep;

  if (!absolute.startsWith(rootWithSep)) {
    throw new Error(`Ruta fuera de server_assets: ${relPath}`);
  }

  return normalized;
}

/** Ruta absoluta en disco para una ruta relativa ya validada */
export function absolutePath(relPath: string): string {
  return path.resolve(config.assetsRoot, safeRelativePath(relPath));
}
