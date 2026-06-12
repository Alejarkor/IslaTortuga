import fs from "fs/promises";
import { SidecarData } from "../types";
import { absolutePath } from "./paths";

/**
 * Sidecars .asset.json: metadatos manuales versionables junto al asset.
 * Ejemplo: textures/player_common_v001.png -> textures/player_common_v001.asset.json
 */

export function sidecarRelativePath(assetRelPath: string): string {
  return assetRelPath.replace(/\.[^./]+$/, "") + ".asset.json";
}

export async function readSidecar(
  assetRelPath: string
): Promise<SidecarData | null> {
  const sidecarAbs = absolutePath(sidecarRelativePath(assetRelPath));

  try {
    const raw = await fs.readFile(sidecarAbs, "utf-8");
    const parsed = JSON.parse(raw);

    return {
      assetKey: String(parsed.assetKey ?? ""),
      assetType: String(parsed.assetType ?? ""),
      version: String(parsed.version ?? ""),
      status: String(parsed.status ?? "draft"),
      manifests: Array.isArray(parsed.manifests) ? parsed.manifests : []
    };
  } catch (error: any) {
    if (error?.code === "ENOENT") {
      return null;
    }

    throw new Error(
      `Sidecar inválido en ${sidecarRelativePath(assetRelPath)}: ${error.message}`
    );
  }
}

export async function writeSidecar(
  assetRelPath: string,
  data: SidecarData
): Promise<string> {
  const relPath = sidecarRelativePath(assetRelPath);
  const sidecarAbs = absolutePath(relPath);

  await fs.writeFile(sidecarAbs, JSON.stringify(data, null, 2) + "\n", "utf-8");

  return relPath;
}
