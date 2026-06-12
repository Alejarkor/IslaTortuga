import fs from "fs/promises";
import { hashFile } from "../core/hasher";
import { buildAssetFileId } from "../core/inference";
import { mimeFromExtension } from "../core/mime";
import { absolutePath, safeRelativePath } from "../core/paths";
import { readSidecar, writeSidecar } from "../core/sidecar";
import {
  SidecarData,
  SidecarManifestLink,
  SyncOperation,
  SyncOperationResult
} from "../types";
import { gameApi } from "./gameApiClient";

const VALID_ASSET_TYPES = new Set([
  "map",
  "texture",
  "model",
  "audio",
  "shader",
  "material",
  "sprite",
  "animation",
  "data"
]);

const VALID_FILE_STATUSES = new Set([
  "draft",
  "published",
  "deprecated",
  "deleted"
]);

export interface RegisterFileInput {
  filePath: string;
  assetKey: string;
  assetType: string;
  version: string;
  status: string;
  writeSidecar?: boolean;
}

/**
 * Registra (o actualiza) un archivo físico en la base de datos vía GameApi.
 * Validaciones obligatorias del documento de diseño:
 * - El archivo físico debe existir.
 * - No se puede cambiar el contenido de un asset publicado manteniendo
 *   el mismo asset_file_id y versión: si cambia el hash, nueva versión.
 * - Rutas fuera de server_assets rechazadas.
 */
export async function registerFile(
  input: RegisterFileInput
): Promise<SyncOperationResult> {
  const operation: SyncOperation = { type: "registerFile", ...input };

  try {
    if (!input.assetKey || !input.version) {
      return fail(operation, "assetKey y version son obligatorios");
    }

    if (!VALID_ASSET_TYPES.has(input.assetType)) {
      return fail(operation, `assetType inválido: ${input.assetType}`);
    }

    if (!VALID_FILE_STATUSES.has(input.status)) {
      return fail(operation, `status inválido: ${input.status}`);
    }

    const relPath = safeRelativePath(input.filePath);
    const absPath = absolutePath(relPath);

    let stats;

    try {
      stats = await fs.stat(absPath);
    } catch {
      return fail(operation, `El archivo físico no existe: ${relPath}`);
    }

    const hash = await hashFile(absPath);
    const extension = relPath.split(".").pop() ?? "";
    const assetFileId = buildAssetFileId(input.assetKey, input.version);

    // Regla: no modificar contenido publicado con misma versión
    const existing = await gameApi.getFile(assetFileId);

    if (existing.ok && existing.data?.assetFile) {
      const dbFile = existing.data.assetFile;

      if (dbFile.status === "published" && dbFile.hash !== hash) {
        return fail(
          operation,
          `${assetFileId} ya está publicado con otro hash. Crea una nueva versión (nuevo nombre físico) en lugar de sobrescribir.`
        );
      }
    }

    const payload = {
      assetKey: input.assetKey,
      assetType: input.assetType,
      version: input.version,
      filePath: relPath,
      downloadUrl: `/assets/files/${relPath}`,
      hash,
      sizeBytes: stats.size,
      mimeType: mimeFromExtension(extension),
      status: input.status
    };

    const result = await gameApi.putFile(assetFileId, payload);

    if (!result.ok) {
      return fail(
        operation,
        result.error ?? result.data?.error ?? "Error registrando en GameApi",
        result.data
      );
    }

    if (input.writeSidecar !== false) {
      const current = await readSidecar(relPath).catch(() => null);

      const sidecar: SidecarData = {
        assetKey: input.assetKey,
        assetType: input.assetType,
        version: input.version,
        status: input.status,
        manifests: current?.manifests ?? []
      };

      await writeSidecar(relPath, sidecar);
    }

    return {
      operation,
      ok: true,
      message: `Registrado ${assetFileId} (${input.status})`,
      detail: result.data?.assetFile
    };
  } catch (error: any) {
    return fail(operation, error?.message ?? "Error inesperado");
  }
}

/** Actualiza el vínculo manifest <-> archivo en el sidecar del asset */
async function updateSidecarManifestLink(
  filePath: string,
  manifestId: string,
  link: SidecarManifestLink | null
): Promise<void> {
  const relPath = safeRelativePath(filePath);
  const sidecar = await readSidecar(relPath).catch(() => null);

  if (!sidecar) return;

  const manifests = sidecar.manifests.filter(
    (m) => m.manifestId !== manifestId
  );

  if (link) {
    manifests.push(link);
  }

  await writeSidecar(relPath, { ...sidecar, manifests });
}

/** Ejecuta una lista de operaciones de sincronización en orden */
export async function applyOperations(
  operations: SyncOperation[]
): Promise<SyncOperationResult[]> {
  const results: SyncOperationResult[] = [];

  for (const op of operations) {
    results.push(await applyOperation(op));
  }

  return results;
}

async function applyOperation(op: SyncOperation): Promise<SyncOperationResult> {
  try {
    switch (op.type) {
      case "registerFile":
        return registerFile(op);

      case "patchFileStatus": {
        const result = await gameApi.patchFileStatus(op.assetFileId, op.status);

        if (!result.ok) {
          return fail(op, result.error ?? result.data?.error ?? "Error", result.data);
        }

        if (op.filePath) {
          try {
            const relPath = safeRelativePath(op.filePath);
            const sidecar = await readSidecar(relPath).catch(() => null);

            if (sidecar) {
              await writeSidecar(relPath, { ...sidecar, status: op.status });
            }
          } catch {
            // El sidecar es secundario: no bloquea la operación
          }
        }

        return {
          operation: op,
          ok: true,
          message: `Estado de ${op.assetFileId} -> ${op.status}`
        };
      }

      case "upsertManifest": {
        const result = await gameApi.putManifest(op.manifestId, {
          name: op.name,
          version: op.version,
          targetType: op.targetType,
          targetId: op.targetId,
          status: op.status
        });

        if (!result.ok) {
          return fail(op, result.error ?? result.data?.error ?? "Error", result.data);
        }

        return {
          operation: op,
          ok: true,
          message: `Manifest ${op.manifestId} guardado (${op.status})`,
          detail: result.data?.manifest
        };
      }

      case "linkFile": {
        const result = await gameApi.putManifestFile(
          op.manifestId,
          op.assetFileId,
          {
            required: op.required,
            loadPriority: op.loadPriority,
            usage: op.usage ?? null
          }
        );

        if (!result.ok) {
          return fail(op, result.error ?? result.data?.error ?? "Error", result.data);
        }

        if (op.filePath) {
          const manifestMeta = await gameApi.getManifest(op.manifestId);
          const m = manifestMeta.data?.manifest;

          await updateSidecarManifestLink(op.filePath, op.manifestId, {
            manifestId: op.manifestId,
            manifestName: m?.name,
            manifestVersion: m?.version,
            targetType: m?.target_type,
            targetId: m?.target_id,
            manifestStatus: m?.status,
            isCurrent: m?.is_current,
            usage: op.usage ?? null,
            required: op.required,
            loadPriority: op.loadPriority
          }).catch(() => undefined);
        }

        return {
          operation: op,
          ok: true,
          message: `${op.assetFileId} vinculado a ${op.manifestId}`
        };
      }

      case "unlinkFile": {
        const result = await gameApi.deleteManifestFile(
          op.manifestId,
          op.assetFileId
        );

        if (!result.ok) {
          return fail(op, result.error ?? result.data?.error ?? "Error", result.data);
        }

        if (op.filePath) {
          await updateSidecarManifestLink(op.filePath, op.manifestId, null).catch(
            () => undefined
          );
        }

        return {
          operation: op,
          ok: true,
          message: `${op.assetFileId} desvinculado de ${op.manifestId}`
        };
      }

      case "setCurrent": {
        // Validación: un manifest current no debe incluir archivos sin publicar
        const manifest = await gameApi.getManifest(op.manifestId);

        if (manifest.ok && Array.isArray(manifest.data?.files)) {
          const unpublished = manifest.data.files.filter(
            (f: any) => f.status !== "published"
          );

          if (unpublished.length > 0) {
            return fail(
              op,
              `El manifest incluye ${unpublished.length} archivo(s) sin publicar: ` +
                unpublished.map((f: any) => f.asset_file_id).join(", ")
            );
          }
        }

        const result = await gameApi.setCurrent(op.manifestId);

        if (!result.ok) {
          return fail(op, result.error ?? result.data?.error ?? "Error", result.data);
        }

        return {
          operation: op,
          ok: true,
          message: `${op.manifestId} marcado como vigente (current)`,
          detail: result.data?.manifest
        };
      }

      default:
        return fail(op, "Operación desconocida");
    }
  } catch (error: any) {
    return fail(op, error?.message ?? "Error inesperado");
  }
}

function fail(
  operation: SyncOperation,
  message: string,
  detail?: unknown
): SyncOperationResult {
  return { operation, ok: false, message, detail };
}
