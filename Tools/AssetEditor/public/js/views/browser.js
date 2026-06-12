// Vista: Explorador de assets (Asset Browser + Metadata Panel)

import { api } from "../api.js";
import { addLog, getSelectedFile, notify, state } from "../state.js";
import {
  clear,
  el,
  formatBytes,
  shortHash,
  spinnerBlock,
  stateChip,
  statusChip,
  toast,
  typeIcon
} from "../ui.js";

const ASSET_TYPES = [
  "texture",
  "sprite",
  "model",
  "audio",
  "map",
  "shader",
  "material",
  "animation",
  "data"
];

const FILE_STATUSES = ["draft", "published", "deprecated", "deleted"];

const filters = { folder: null, search: "", state: "", type: "" };

export function renderBrowser(container) {
  clear(container);

  if (state.scanning) {
    container.append(spinnerBlock("Escaneando server_assets…"));
    return;
  }

  if (!state.scan) {
    container.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "🏝️"),
        el("p", {}, "Pulsa «Escanear» para explorar la carpeta server_assets."),
      )
    );
    return;
  }

  container.append(renderSummary(), renderLayout());
}

// ---------- Resumen ----------

function renderSummary() {
  const s = state.scan.summary;

  const card = (num, label, cls = "") =>
    el(
      "div",
      { class: `summary-card ${cls}` },
      el("div", { class: "num" }, String(num)),
      el("div", { class: "label" }, label)
    );

  return el(
    "div",
    { class: "summary-row" },
    card(s.total, "Archivos"),
    card(s.new, "Nuevos", "s-new"),
    card(s.changed, "Cambiados", "s-warn"),
    card(s.registered, "Registrados", "s-ok"),
    card(s.withWarnings, "Con avisos", "s-warn"),
    card(s.missingOnDisk, "Faltan en disco", s.missingOnDisk > 0 ? "s-danger" : "")
  );
}

// ---------- Layout principal ----------

function renderLayout() {
  return el(
    "div",
    { class: "browser-layout" },
    renderTree(),
    renderFileList(),
    renderDetail()
  );
}

function getFolders() {
  const counts = new Map();

  for (const f of state.scan.files) {
    const top = f.filePath.includes("/") ? f.filePath.split("/")[0] : "/";
    counts.set(top, (counts.get(top) ?? 0) + 1);
  }

  return [...counts.entries()].sort((a, b) => a[0].localeCompare(b[0]));
}

function renderTree() {
  const tree = el("div", { class: "browser-tree" });

  const allItem = el(
    "div",
    {
      class: `tree-item ${filters.folder === null ? "active" : ""}`,
      onClick: () => {
        filters.folder = null;
        notify("browser");
      }
    },
    "📁 Todos",
    el("span", { class: "tree-count" }, String(state.scan.files.length))
  );

  tree.append(allItem);

  for (const [folder, count] of getFolders()) {
    tree.append(
      el(
        "div",
        {
          class: `tree-item ${filters.folder === folder ? "active" : ""}`,
          onClick: () => {
            filters.folder = folder;
            notify("browser");
          }
        },
        `📂 ${folder}`,
        el("span", { class: "tree-count" }, String(count))
      )
    );
  }

  return tree;
}

// ---------- Lista de archivos ----------

function getVisibleFiles() {
  return state.scan.files.filter((f) => {
    if (filters.folder) {
      const top = f.filePath.includes("/") ? f.filePath.split("/")[0] : "/";
      if (top !== filters.folder) return false;
    }

    if (filters.state && f.state !== filters.state) return false;

    if (filters.type && f.inferred.assetType !== filters.type) return false;

    if (filters.search) {
      const q = filters.search.toLowerCase();
      if (
        !f.filePath.toLowerCase().includes(q) &&
        !f.inferred.assetKey.toLowerCase().includes(q)
      ) {
        return false;
      }
    }

    return true;
  });
}

function renderFileList() {
  const wrapper = el("div", { class: "browser-list" });

  const searchInput = el("input", {
    type: "search",
    placeholder: "Buscar por nombre o asset_key…",
    value: filters.search,
    onInput: (e) => {
      filters.search = e.target.value;
      renderRows();
    }
  });

  const stateSelect = el(
    "select",
    {
      class: "select-sm",
      style: "width:130px",
      onChange: (e) => {
        filters.state = e.target.value;
        renderRows();
      }
    },
    el("option", { value: "" }, "Estado: todos"),
    el("option", { value: "new", selected: filters.state === "new" ? "" : undefined }, "Nuevos"),
    el("option", { value: "changed", selected: filters.state === "changed" ? "" : undefined }, "Cambiados"),
    el("option", { value: "registered", selected: filters.state === "registered" ? "" : undefined }, "Registrados")
  );

  const typeSelect = el(
    "select",
    {
      class: "select-sm",
      style: "width:130px",
      onChange: (e) => {
        filters.type = e.target.value;
        renderRows();
      }
    },
    el("option", { value: "" }, "Tipo: todos"),
    ASSET_TYPES.map((t) =>
      el("option", { value: t, selected: filters.type === t ? "" : undefined }, t)
    )
  );

  const rows = el("div", { class: "file-rows" });

  function renderRows() {
    clear(rows);

    const files = getVisibleFiles();

    if (files.length === 0) {
      rows.append(
        el(
          "div",
          { class: "empty-state" },
          el("div", { class: "big" }, "🌊"),
          el("p", {}, "No hay archivos que coincidan con el filtro.")
        )
      );
      return;
    }

    for (const f of files) {
      rows.append(
        el(
          "div",
          {
            class: `file-row ${f.filePath === state.selectedFilePath ? "selected" : ""}`,
            onClick: () => {
              state.selectedFilePath = f.filePath;
              notify("browser");
            }
          },
          el("div", { class: "f-icon" }, typeIcon(f.inferred.assetType)),
          el(
            "div",
            { style: "min-width:0" },
            el("div", { class: "f-name" }, f.fileName),
            el("div", { class: "f-key" }, f.inferred.assetKey)
          ),
          el("div", { class: "f-size" }, formatBytes(f.auto.sizeBytes)),
          stateChip(f.state),
          el(
            "div",
            { class: "f-warn", title: f.warnings.join("\n") },
            f.warnings.length > 0 ? "⚠️" : ""
          )
        )
      );
    }
  }

  renderRows();

  wrapper.append(
    el("div", { class: "list-toolbar" }, searchInput, stateSelect, typeSelect),
    rows
  );

  return wrapper;
}

// ---------- Panel de detalle ----------

function renderDetail() {
  const panel = el("div", { class: "browser-detail" });
  const file = getSelectedFile();

  if (!file) {
    panel.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "👈"),
        el("p", {}, "Selecciona un archivo para ver y editar sus metadatos.")
      )
    );
    return panel;
  }

  // Cabecera
  const chips = el("div", { class: "chips" }, stateChip(file.state));

  if (file.db) chips.append(statusChip(file.db.status));
  if (file.hasSidecar) {
    chips.append(el("span", { class: "chip chip-draft" }, "sidecar ✓"));
  }

  panel.append(
    el(
      "div",
      { class: "detail-header" },
      el("div", { class: "name" }, `${typeIcon(file.inferred.assetType)} ${file.fileName}`),
      el("div", { class: "path" }, file.filePath),
      chips
    )
  );

  // Avisos
  if (file.warnings.length > 0) {
    panel.append(
      el(
        "div",
        { class: "detail-section" },
        el("h3", {}, "Avisos"),
        el(
          "div",
          { class: "warning-list" },
          file.warnings.map((w) => el("div", { class: "warning-item" }, w))
        )
      )
    );
  }

  // Metadatos automáticos
  panel.append(
    el(
      "div",
      { class: "detail-section" },
      el("h3", {}, "Metadatos automáticos"),
      el(
        "table",
        { class: "meta-table" },
        metaRow("ID", file.db?.asset_file_id ?? file.inferred.assetFileId),
        metaRow("URL", file.auto.downloadUrl),
        el(
          "tr",
          {},
          el("td", {}, "Hash"),
          el(
            "td",
            {},
            el(
              "span",
              {
                class: "copy-hash",
                title: "Copiar hash completo",
                onClick: () => {
                  navigator.clipboard?.writeText(file.auto.hash);
                  toast("Hash copiado al portapapeles", "ok", 1800);
                }
              },
              shortHash(file.auto.hash)
            )
          )
        ),
        metaRow("Tamaño", formatBytes(file.auto.sizeBytes)),
        metaRow("MIME", file.auto.mimeType),
        metaRow("Modificado", new Date(file.auto.modifiedAt).toLocaleString("es-ES"))
      )
    )
  );

  // Formulario de metadatos manuales
  const initial = {
    assetKey: file.sidecar?.assetKey ?? file.db?.asset_key ?? file.inferred.assetKey,
    assetType: file.sidecar?.assetType ?? file.db?.asset_type ?? file.inferred.assetType,
    version: file.sidecar?.version ?? file.db?.version ?? file.inferred.version,
    status: file.sidecar?.status ?? file.db?.status ?? "draft"
  };

  const keyInput = el("input", { type: "text", value: initial.assetKey });
  const versionInput = el("input", { type: "text", value: initial.version });

  const typeSelect = el(
    "select",
    {},
    ASSET_TYPES.map((t) =>
      el("option", { value: t, selected: t === initial.assetType ? "" : undefined }, t)
    )
  );

  const statusSelect = el(
    "select",
    {},
    FILE_STATUSES.map((s) =>
      el("option", { value: s, selected: s === initial.status ? "" : undefined }, s)
    )
  );

  panel.append(
    el(
      "div",
      { class: "detail-section" },
      el("h3", {}, "Metadatos manuales"),
      field("asset_key", keyInput),
      el(
        "div",
        { style: "display:flex;gap:10px" },
        el("div", { class: "form-field", style: "flex:1" }, el("label", {}, "asset_type"), typeSelect),
        el("div", { class: "form-field", style: "width:90px" }, el("label", {}, "version"), versionInput)
      ),
      field("status", statusSelect)
    )
  );

  // Manifests vinculados (desde sidecar)
  const links = file.sidecar?.manifests ?? [];

  if (links.length > 0) {
    panel.append(
      el(
        "div",
        { class: "detail-section" },
        el("h3", {}, "Manifests vinculados"),
        links.map((m) =>
          el(
            "div",
            { class: "manifest-link-item" },
            el(
              "div",
              {},
              el("div", { class: "mono" }, m.manifestId),
              el(
                "div",
                { style: "color:var(--text-faint);font-size:11px" },
                `${m.usage ?? "-"} · prio ${m.loadPriority ?? "-"} · ${m.required === false ? "opcional" : "requerido"}`
              )
            ),
            m.isCurrent ? el("span", { class: "chip chip-current" }, "current") : null
          )
        )
      )
    );
  }

  // Acciones
  const getForm = () => ({
    filePath: file.filePath,
    assetKey: keyInput.value.trim(),
    assetType: typeSelect.value,
    version: versionInput.value.trim(),
    status: statusSelect.value
  });

  const btnSidecar = el(
    "button",
    {
      class: "btn",
      onClick: async () => {
        const form = getForm();
        const result = await api.saveSidecar(form);

        if (result.data.ok) {
          toast(`Sidecar guardado: ${result.data.sidecarPath}`, "ok");
          addLog({ ok: true, message: `Sidecar guardado para ${file.filePath}` });
          await rescan();
        } else {
          toast(result.data.error ?? "Error guardando sidecar", "error");
        }
      }
    },
    "💾 Guardar sidecar"
  );

  const btnRegister = el(
    "button",
    {
      class: "btn btn-primary",
      onClick: async () => {
        const form = getForm();
        const result = await api.registerFile({ ...form, writeSidecar: true });

        addLog({ ok: result.data.ok, message: result.data.message ?? "registro" });

        if (result.data.ok) {
          toast(result.data.message, "ok");
          await rescan();
        } else {
          toast(result.data.message ?? "Error registrando", "error", 6000);
        }
      }
    },
    file.db ? "⬆️ Actualizar en DB" : "📡 Registrar en DB"
  );

  const actions = el(
    "div",
    { class: "detail-actions" },
    el("div", { class: "row" }, btnSidecar, btnRegister)
  );

  // Acciones rápidas de estado si ya está en DB
  if (file.db) {
    const quick = el("div", { class: "row" });

    if (file.db.status !== "published") {
      quick.append(
        el(
          "button",
          {
            class: "btn",
            style: "color:var(--ok);border-color:var(--ok)",
            onClick: () => quickStatus(file, "published")
          },
          "✅ Publicar"
        )
      );
    }

    if (file.db.status === "published") {
      quick.append(
        el(
          "button",
          {
            class: "btn",
            style: "color:var(--warn);border-color:var(--warn)",
            onClick: () => quickStatus(file, "deprecated")
          },
          "🗃️ Deprecar"
        )
      );
    }

    if (quick.children.length > 0) actions.append(quick);
  }

  panel.append(actions);

  return panel;
}

async function quickStatus(file, status) {
  const result = await api.patchFileStatus(
    file.db.asset_file_id,
    status,
    file.filePath
  );

  addLog({ ok: result.data.ok, message: result.data.message ?? "cambio de estado" });

  if (result.data.ok) {
    toast(result.data.message, "ok");
    await rescan();
  } else {
    toast(result.data.message ?? "Error", "error");
  }
}

function metaRow(label, value) {
  return el("tr", {}, el("td", {}, label), el("td", {}, value ?? "-"));
}

function field(label, input) {
  return el("div", { class: "form-field" }, el("label", {}, label), input);
}

export async function rescan() {
  state.scanning = true;
  notify("browser");

  const result = await api.scan();

  state.scanning = false;

  if (result.data.ok) {
    state.scan = result.data;

    if (!result.data.gameApiReachable) {
      toast("GameApi no accesible: el escaneo no incluye datos de DB", "warn", 5000);
    }
  } else {
    toast(result.data.error ?? "Error en el escaneo", "error");
  }

  notify("browser");
}
