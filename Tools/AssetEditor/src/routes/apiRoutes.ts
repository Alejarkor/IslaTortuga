import { Router } from "express";
import { config } from "../config";
import { safeRelativePath } from "../core/paths";
import { readSidecar, sidecarRelativePath, writeSidecar } from "../core/sidecar";
import { gameApi } from "../services/gameApiClient";
import { performScan } from "../services/scanService";
import { applyOperations, registerFile } from "../services/syncService";

export const apiRoutes = Router();

// ---------- Configuración / salud ----------

apiRoutes.get("/config", async (_req, res) => {
  const gameApiReachable = await gameApi.health();

  res.json({
    ok: true,
    assetsRoot: config.assetsRoot,
    gameApiUrl: config.gameApiUrl,
    hasAdminToken: config.adminToken.length > 0,
    gameApiReachable
  });
});

// ---------- Escaneo ----------

apiRoutes.get("/scan", async (_req, res) => {
  try {
    const result = await performScan();

    res.json(result);
  } catch (error: any) {
    res.status(500).json({ ok: false, error: error?.message ?? "scan failed" });
  }
});

// ---------- Sidecars ----------

apiRoutes.post("/sidecar", async (req, res) => {
  const { filePath, assetKey, assetType, version, status, manifests } =
    req.body ?? {};

  if (!filePath || !assetKey || !assetType || !version || !status) {
    return res.status(400).json({
      ok: false,
      error: "filePath, assetKey, assetType, version y status son obligatorios"
    });
  }

  try {
    const relPath = safeRelativePath(String(filePath));
    const current = await readSidecar(relPath).catch(() => null);

    const written = await writeSidecar(relPath, {
      assetKey: String(assetKey),
      assetType: String(assetType),
      version: String(version),
      status: String(status),
      manifests: Array.isArray(manifests)
        ? manifests
        : current?.manifests ?? []
    });

    res.json({ ok: true, sidecarPath: written });
  } catch (error: any) {
    res.status(400).json({ ok: false, error: error?.message });
  }
});

apiRoutes.get("/sidecar", async (req, res) => {
  const filePath = req.query.filePath;

  if (!filePath) {
    return res.status(400).json({ ok: false, error: "filePath requerido" });
  }

  try {
    const relPath = safeRelativePath(String(filePath));
    const sidecar = await readSidecar(relPath);

    res.json({
      ok: true,
      sidecarPath: sidecarRelativePath(relPath),
      sidecar
    });
  } catch (error: any) {
    res.status(400).json({ ok: false, error: error?.message });
  }
});

// ---------- Registro de archivos ----------

apiRoutes.post("/files/register", async (req, res) => {
  const result = await registerFile(req.body ?? {});

  res.status(result.ok ? 200 : 400).json(result);
});

apiRoutes.patch("/files/:assetFileId/status", async (req, res) => {
  const results = await applyOperations([
    {
      type: "patchFileStatus",
      assetFileId: req.params.assetFileId,
      status: String(req.body?.status ?? ""),
      filePath: req.body?.filePath ? String(req.body.filePath) : undefined
    }
  ]);

  const result = results[0];

  res.status(result.ok ? 200 : 400).json(result);
});

apiRoutes.get("/files", async (req, res) => {
  const params = new URLSearchParams();

  for (const key of ["status", "assetType", "q", "limit", "offset"]) {
    if (req.query[key]) params.set(key, String(req.query[key]));
  }

  const query = params.toString() ? `?${params.toString()}` : "";
  const result = await gameApi.listFiles(query);

  res.status(result.status || 502).json(result.data ?? { ok: false, error: result.error });
});

// ---------- Manifests (proxy a GameApi) ----------

apiRoutes.get("/manifests", async (req, res) => {
  const params = new URLSearchParams();

  for (const key of ["status", "targetType", "targetId", "limit", "offset"]) {
    if (req.query[key]) params.set(key, String(req.query[key]));
  }

  const query = params.toString() ? `?${params.toString()}` : "";
  const result = await gameApi.listManifests(query);

  res.status(result.status || 502).json(result.data ?? { ok: false, error: result.error });
});

apiRoutes.get("/manifests/:manifestId", async (req, res) => {
  const result = await gameApi.getManifest(req.params.manifestId);

  res.status(result.status || 502).json(result.data ?? { ok: false, error: result.error });
});

apiRoutes.put("/manifests/:manifestId", async (req, res) => {
  const results = await applyOperations([
    {
      type: "upsertManifest",
      manifestId: req.params.manifestId,
      name: String(req.body?.name ?? ""),
      version: String(req.body?.version ?? ""),
      targetType: String(req.body?.targetType ?? ""),
      targetId: String(req.body?.targetId ?? ""),
      status: String(req.body?.status ?? "draft")
    }
  ]);

  const result = results[0];

  res.status(result.ok ? 200 : 400).json(result);
});

apiRoutes.put("/manifests/:manifestId/files/:assetFileId", async (req, res) => {
  const results = await applyOperations([
    {
      type: "linkFile",
      manifestId: req.params.manifestId,
      assetFileId: req.params.assetFileId,
      filePath: req.body?.filePath ? String(req.body.filePath) : undefined,
      required: req.body?.required ?? true,
      loadPriority: Number(req.body?.loadPriority ?? 100),
      usage: req.body?.usage ?? null
    }
  ]);

  const result = results[0];

  res.status(result.ok ? 200 : 400).json(result);
});

apiRoutes.delete(
  "/manifests/:manifestId/files/:assetFileId",
  async (req, res) => {
    const results = await applyOperations([
      {
        type: "unlinkFile",
        manifestId: req.params.manifestId,
        assetFileId: req.params.assetFileId,
        filePath: req.query.filePath ? String(req.query.filePath) : undefined
      }
    ]);

    const result = results[0];

    res.status(result.ok ? 200 : 400).json(result);
  }
);

apiRoutes.post("/manifests/:manifestId/set-current", async (req, res) => {
  const results = await applyOperations([
    { type: "setCurrent", manifestId: req.params.manifestId }
  ]);

  const result = results[0];

  res.status(result.ok ? 200 : 400).json(result);
});

// ---------- Sincronización en lote ----------

apiRoutes.post("/sync/apply", async (req, res) => {
  const operations = Array.isArray(req.body?.operations)
    ? req.body.operations
    : [];

  if (operations.length === 0) {
    return res
      .status(400)
      .json({ ok: false, error: "operations array requerido" });
  }

  const results = await applyOperations(operations);

  res.json({
    ok: results.every((r) => r.ok),
    applied: results.filter((r) => r.ok).length,
    failed: results.filter((r) => !r.ok).length,
    results
  });
});

/** Dry-run: compara el estado de disco contra la DB usando sync-report */
apiRoutes.post("/sync/report", async (_req, res) => {
  try {
    const scan = await performScan();

    const files = scan.files.map((f) => ({
      assetFileId: f.db?.asset_file_id ?? f.inferred.assetFileId,
      filePath: f.filePath,
      hash: f.auto.hash,
      sizeBytes: f.auto.sizeBytes
    }));

    if (files.length === 0) {
      return res.json({ ok: true, summary: null, message: "No hay archivos en disco" });
    }

    const result = await gameApi.syncReport(files);

    res.status(result.status || 502).json(result.data ?? { ok: false, error: result.error });
  } catch (error: any) {
    res.status(500).json({ ok: false, error: error?.message });
  }
});

// ---------- Vista previa de manifest público ----------

apiRoutes.get("/preview-manifest", async (req, res) => {
  const targetType = String(req.query.targetType ?? "");
  const targetId = String(req.query.targetId ?? "");

  if (!targetType || !targetId) {
    return res
      .status(400)
      .json({ ok: false, error: "targetType y targetId requeridos" });
  }

  const result = await gameApi.publicManifest(targetType, targetId);

  res.status(result.status || 502).json(result.data ?? { ok: false, error: result.error });
});
