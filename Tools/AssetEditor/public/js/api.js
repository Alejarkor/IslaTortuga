// Cliente HTTP de la API local del AssetEditor

async function request(url, options = {}) {
  const response = await fetch(url, {
    method: options.method ?? "GET",
    headers: { "Content-Type": "application/json" },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined
  });

  let data = null;

  try {
    data = await response.json();
  } catch {
    data = { ok: false, error: "respuesta no válida del servidor" };
  }

  return { status: response.status, ok: response.ok, data };
}

export const api = {
  getConfig: () => request("/api/config"),
  scan: () => request("/api/scan"),

  getSidecar: (filePath) =>
    request(`/api/sidecar?filePath=${encodeURIComponent(filePath)}`),

  saveSidecar: (body) => request("/api/sidecar", { method: "POST", body }),

  registerFile: (body) =>
    request("/api/files/register", { method: "POST", body }),

  patchFileStatus: (assetFileId, status, filePath) =>
    request(`/api/files/${encodeURIComponent(assetFileId)}/status`, {
      method: "PATCH",
      body: { status, filePath }
    }),

  listDbFiles: (query = "") => request(`/api/files${query}`),

  listManifests: (query = "") => request(`/api/manifests${query}`),

  getManifest: (manifestId) =>
    request(`/api/manifests/${encodeURIComponent(manifestId)}`),

  saveManifest: (manifestId, body) =>
    request(`/api/manifests/${encodeURIComponent(manifestId)}`, {
      method: "PUT",
      body
    }),

  linkFile: (manifestId, assetFileId, body) =>
    request(
      `/api/manifests/${encodeURIComponent(manifestId)}/files/${encodeURIComponent(assetFileId)}`,
      { method: "PUT", body }
    ),

  unlinkFile: (manifestId, assetFileId, filePath) =>
    request(
      `/api/manifests/${encodeURIComponent(manifestId)}/files/${encodeURIComponent(assetFileId)}` +
        (filePath ? `?filePath=${encodeURIComponent(filePath)}` : ""),
      { method: "DELETE" }
    ),

  setCurrent: (manifestId) =>
    request(`/api/manifests/${encodeURIComponent(manifestId)}/set-current`, {
      method: "POST"
    }),

  syncApply: (operations) =>
    request("/api/sync/apply", { method: "POST", body: { operations } }),

  syncReport: () => request("/api/sync/report", { method: "POST" }),

  previewManifest: (targetType, targetId) =>
    request(
      `/api/preview-manifest?targetType=${encodeURIComponent(targetType)}&targetId=${encodeURIComponent(targetId)}`
    )
};
