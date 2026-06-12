import fs from "fs/promises";
import path from "path";
import { config } from "../config";
import { AutoMetadata } from "../types";
import { hashFile } from "./hasher";
import { mimeFromExtension, SUPPORTED_EXTENSIONS } from "./mime";

export interface DiskFile {
  fileName: string;
  filePath: string;
  folder: string;
  extension: string;
  auto: AutoMetadata;
}

const IGNORED_DIRECTORIES = new Set([".git", "node_modules", ".vs", ".idea"]);

function isSidecar(fileName: string): boolean {
  return fileName.toLowerCase().endsWith(".asset.json");
}

async function walk(absDir: string, relDir: string, out: DiskFile[]): Promise<void> {
  let entries;

  try {
    entries = await fs.readdir(absDir, { withFileTypes: true });
  } catch {
    return;
  }

  for (const entry of entries) {
    if (entry.name.startsWith(".")) continue;

    const absEntry = path.join(absDir, entry.name);
    const relEntry = relDir ? `${relDir}/${entry.name}` : entry.name;

    if (entry.isDirectory()) {
      if (!IGNORED_DIRECTORIES.has(entry.name.toLowerCase())) {
        await walk(absEntry, relEntry, out);
      }
      continue;
    }

    if (!entry.isFile() || isSidecar(entry.name)) continue;

    const extension = path.extname(entry.name).slice(1).toLowerCase();

    if (!SUPPORTED_EXTENSIONS.has(extension)) continue;

    const stats = await fs.stat(absEntry);
    const hash = await hashFile(absEntry);

    out.push({
      fileName: entry.name,
      filePath: relEntry,
      folder: relDir || "/",
      extension,
      auto: {
        filePath: relEntry,
        downloadUrl: `/assets/files/${relEntry}`,
        hash,
        sizeBytes: stats.size,
        mimeType: mimeFromExtension(extension),
        modifiedAt: stats.mtime.toISOString()
      }
    });
  }
}

/** Escanea recursivamente server_assets y calcula metadatos técnicos */
export async function scanAssetsRoot(): Promise<DiskFile[]> {
  const out: DiskFile[] = [];

  await walk(config.assetsRoot, "", out);

  out.sort((a, b) => a.filePath.localeCompare(b.filePath));

  return out;
}
