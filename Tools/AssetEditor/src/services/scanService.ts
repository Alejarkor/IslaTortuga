import { config } from "../config";
import {
  buildAssetFileId,
  inferAssetKey,
  inferAssetType,
  inferVersion
} from "../core/inference";
import { scanAssetsRoot } from "../core/scanner";
import { readSidecar } from "../core/sidecar";
import { DbAssetFile, ScannedFile, ScanResult } from "../types";
import { gameApi } from "./gameApiClient";

/**
 * Combina el estado en disco, los sidecars y la base de datos
 * para clasificar cada archivo: new / registered / changed / error.
 */
export async function performScan(): Promise<ScanResult> {
  const diskFiles = await scanAssetsRoot();

  const dbResult = await gameApi.listAllFiles();
  const gameApiReachable = dbResult.status > 0;
  const dbFiles = dbResult.data?.files ?? [];

  const dbByPath = new Map<string, DbAssetFile>();
  const dbById = new Map<string, DbAssetFile>();

  for (const row of dbFiles) {
    dbByPath.set(row.file_path, row);
    dbById.set(row.asset_file_id, row);
  }

  const seenDbIds = new Set<string>();
  const files: ScannedFile[] = [];

  for (const disk of diskFiles) {
    const warnings: string[] = [];
    let sidecar = null;

    try {
      sidecar = await readSidecar(disk.filePath);
    } catch (error: any) {
      warnings.push(error.message);
    }

    const fileNameNoExt = disk.fileName.replace(/\.[^./]+$/, "");

    const assetKey = sidecar?.assetKey || inferAssetKey(disk.filePath);
    const assetType =
      sidecar?.assetType || inferAssetType(disk.filePath, disk.extension);
    const version = sidecar?.version || inferVersion(fileNameNoExt);
    const assetFileId = buildAssetFileId(assetKey, version);

    // Buscar registro en DB: primero por ruta física, después por id
    const db = dbByPath.get(disk.filePath) ?? dbById.get(assetFileId) ?? null;

    if (db) {
      seenDbIds.add(db.asset_file_id);
    }

    let state: ScannedFile["state"];

    if (!db) {
      state = "new";

      if (!sidecar) {
        warnings.push("Sin sidecar y sin registro en base de datos");
      }
    } else if (db.hash !== disk.auto.hash) {
      state = "changed";

      if (db.status === "published") {
        warnings.push(
          "El contenido cambió pero el archivo ya está publicado: debe crearse una nueva versión con nuevo nombre físico"
        );
      }
    } else {
      state = "registered";
    }

    // Avisos de divergencia entre sidecar y DB
    if (sidecar && db) {
      if (sidecar.assetKey && sidecar.assetKey !== db.asset_key) {
        warnings.push(
          `Sidecar y DB difieren en asset_key (${sidecar.assetKey} vs ${db.asset_key})`
        );
      }

      if (sidecar.version && sidecar.version !== db.version) {
        warnings.push(
          `Sidecar y DB difieren en version (${sidecar.version} vs ${db.version})`
        );
      }

      if (sidecar.status && sidecar.status !== db.status) {
        warnings.push(
          `Sidecar y DB difieren en status (${sidecar.status} vs ${db.status})`
        );
      }
    }

    files.push({
      fileName: disk.fileName,
      filePath: disk.filePath,
      folder: disk.folder,
      extension: disk.extension,
      auto: disk.auto,
      inferred: { assetKey, assetType, version, assetFileId },
      sidecar,
      hasSidecar: sidecar !== null,
      db,
      state,
      warnings
    });
  }

  // Archivos registrados en DB que no existen en disco
  const missingOnDisk = dbFiles.filter(
    (row) => row.status !== "deleted" && !seenDbIds.has(row.asset_file_id)
  );

  return {
    ok: true,
    assetsRoot: config.assetsRoot,
    scannedAt: new Date().toISOString(),
    gameApiReachable,
    files,
    missingOnDisk,
    summary: {
      total: files.length,
      new: files.filter((f) => f.state === "new").length,
      registered: files.filter((f) => f.state === "registered").length,
      changed: files.filter((f) => f.state === "changed").length,
      withWarnings: files.filter((f) => f.warnings.length > 0).length,
      missingOnDisk: missingOnDisk.length
    }
  };
}
