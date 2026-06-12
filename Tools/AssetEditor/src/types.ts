export type AssetType =
  | "map"
  | "texture"
  | "model"
  | "audio"
  | "shader"
  | "material"
  | "sprite"
  | "animation"
  | "data";

export type AssetStatus = "draft" | "published" | "deprecated" | "deleted";

export type ManifestTargetType =
  | "global"
  | "scenario"
  | "scenario_set"
  | "game_mode"
  | "event";

/** Metadatos calculados automáticamente desde disco */
export interface AutoMetadata {
  filePath: string;
  downloadUrl: string;
  hash: string;
  sizeBytes: number;
  mimeType: string;
  modifiedAt: string;
}

/** Vínculo de un asset con un manifest dentro del sidecar */
export interface SidecarManifestLink {
  manifestId: string;
  manifestName?: string;
  manifestVersion?: string;
  targetType?: string;
  targetId?: string;
  manifestStatus?: string;
  isCurrent?: boolean;
  usage?: string | null;
  required?: boolean;
  loadPriority?: number;
}

/** Contenido del archivo sidecar .asset.json */
export interface SidecarData {
  assetKey: string;
  assetType: string;
  version: string;
  status: string;
  manifests: SidecarManifestLink[];
}

/** Fila de asset_files devuelta por GameApi */
export interface DbAssetFile {
  asset_file_id: string;
  asset_key: string;
  asset_type: string;
  version: string;
  file_path: string;
  download_url: string;
  hash: string;
  size_bytes: string | number;
  mime_type: string;
  status: string;
  created_at?: string;
  published_at?: string | null;
}

/** Estado de un archivo detectado en el escaneo */
export type FileState = "new" | "registered" | "changed" | "error";

export interface ScannedFile {
  fileName: string;
  filePath: string;
  folder: string;
  extension: string;
  auto: AutoMetadata;
  inferred: {
    assetKey: string;
    assetType: string;
    version: string;
    assetFileId: string;
  };
  sidecar: SidecarData | null;
  hasSidecar: boolean;
  db: DbAssetFile | null;
  state: FileState;
  warnings: string[];
}

export interface ScanResult {
  ok: boolean;
  assetsRoot: string;
  scannedAt: string;
  gameApiReachable: boolean;
  files: ScannedFile[];
  missingOnDisk: DbAssetFile[];
  summary: {
    total: number;
    new: number;
    registered: number;
    changed: number;
    withWarnings: number;
    missingOnDisk: number;
  };
}

/** Operación de sincronización solicitada por el frontend */
export type SyncOperation =
  | {
      type: "registerFile";
      filePath: string;
      assetKey: string;
      assetType: string;
      version: string;
      status: string;
      writeSidecar?: boolean;
    }
  | { type: "patchFileStatus"; assetFileId: string; status: string; filePath?: string }
  | {
      type: "upsertManifest";
      manifestId: string;
      name: string;
      version: string;
      targetType: string;
      targetId: string;
      status: string;
    }
  | {
      type: "linkFile";
      manifestId: string;
      assetFileId: string;
      filePath?: string;
      required: boolean;
      loadPriority: number;
      usage?: string | null;
    }
  | { type: "unlinkFile"; manifestId: string; assetFileId: string; filePath?: string }
  | { type: "setCurrent"; manifestId: string };

export interface SyncOperationResult {
  operation: SyncOperation;
  ok: boolean;
  message: string;
  detail?: unknown;
}
