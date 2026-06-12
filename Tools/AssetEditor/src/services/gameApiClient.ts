import { config } from "../config";
import { DbAssetFile } from "../types";

export interface ApiResult<T = any> {
  ok: boolean;
  status: number;
  data: T;
  error?: string;
}

/**
 * Cliente HTTP para los endpoints internos admin de GameApi.
 * Todas las llamadas viajan con la cabecera x-admin-token.
 */
async function request<T = any>(
  endpoint: string,
  options?: { method?: string; body?: unknown }
): Promise<ApiResult<T>> {
  try {
    const response = await fetch(`${config.gameApiUrl}${endpoint}`, {
      method: options?.method ?? "GET",
      headers: {
        "Content-Type": "application/json",
        "x-admin-token": config.adminToken
      },
      body: options?.body !== undefined ? JSON.stringify(options.body) : undefined
    });

    const data = (await response.json()) as T;

    return { ok: response.ok, status: response.status, data };
  } catch (error: any) {
    return {
      ok: false,
      status: 0,
      data: undefined as unknown as T,
      error: `GameApi no accesible: ${error?.message ?? "error de red"}`
    };
  }
}

export const gameApi = {
  async health(): Promise<boolean> {
    const result = await request("/internal/health");

    return result.status > 0;
  },

  /** Lista completa de asset_files paginando hasta agotar resultados */
  async listAllFiles(): Promise<ApiResult<{ files: DbAssetFile[] }>> {
    const pageSize = 500;
    const all: DbAssetFile[] = [];
    let offset = 0;

    while (true) {
      const result = await request<{ ok: boolean; files: DbAssetFile[] }>(
        `/internal/admin/assets/files?limit=${pageSize}&offset=${offset}`
      );

      if (!result.ok) {
        return { ...result, data: { files: all } };
      }

      const page = result.data.files ?? [];
      all.push(...page);

      if (page.length < pageSize) break;
      offset += pageSize;
    }

    return { ok: true, status: 200, data: { files: all } };
  },

  listFiles(query: string) {
    return request(`/internal/admin/assets/files${query}`);
  },

  getFile(assetFileId: string) {
    return request(
      `/internal/admin/assets/files/${encodeURIComponent(assetFileId)}`
    );
  },

  putFile(assetFileId: string, body: unknown) {
    return request(
      `/internal/admin/assets/files/${encodeURIComponent(assetFileId)}`,
      { method: "PUT", body }
    );
  },

  patchFileStatus(assetFileId: string, status: string) {
    return request(
      `/internal/admin/assets/files/${encodeURIComponent(assetFileId)}/status`,
      { method: "PATCH", body: { status } }
    );
  },

  listManifests(query: string) {
    return request(`/internal/admin/assets/manifests${query}`);
  },

  getManifest(manifestId: string) {
    return request(
      `/internal/admin/assets/manifests/${encodeURIComponent(manifestId)}`
    );
  },

  putManifest(manifestId: string, body: unknown) {
    return request(
      `/internal/admin/assets/manifests/${encodeURIComponent(manifestId)}`,
      { method: "PUT", body }
    );
  },

  putManifestFile(manifestId: string, assetFileId: string, body: unknown) {
    return request(
      `/internal/admin/assets/manifests/${encodeURIComponent(
        manifestId
      )}/files/${encodeURIComponent(assetFileId)}`,
      { method: "PUT", body }
    );
  },

  deleteManifestFile(manifestId: string, assetFileId: string) {
    return request(
      `/internal/admin/assets/manifests/${encodeURIComponent(
        manifestId
      )}/files/${encodeURIComponent(assetFileId)}`,
      { method: "DELETE" }
    );
  },

  setCurrent(manifestId: string) {
    return request(
      `/internal/admin/assets/manifests/${encodeURIComponent(
        manifestId
      )}/set-current`,
      { method: "POST" }
    );
  },

  syncReport(files: unknown[]) {
    return request(`/internal/admin/assets/sync-report`, {
      method: "POST",
      body: { files }
    });
  },

  publicManifest(targetType: string, targetId: string) {
    return request(
      `/internal/assets/manifest?targetType=${encodeURIComponent(
        targetType
      )}&targetId=${encodeURIComponent(targetId)}`
    );
  }
};
