// Vista: Manifest Manager

import { api } from "../api.js";
import { addLog, notify, state } from "../state.js";
import {
  clear,
  confirmModal,
  currentChip,
  el,
  formatBytes,
  openModal,
  spinnerBlock,
  statusChip,
  toast,
  typeIcon
} from "../ui.js";

const TARGET_TYPES = ["global", "scenario", "scenario_set", "game_mode", "event"];
const MANIFEST_STATUSES = ["draft", "published", "deprecated"];

let manifestDetail = null; // { manifest, files }
let loadingDetail = false;

export function renderManifests(container) {
  clear(container);

  const layout = el("div", { class: "manifests-layout" });

  layout.append(renderManifestList(), renderManifestDetail());
  container.append(layout);
}

export async function loadManifests() {
  const result = await api.listManifests("?limit=500");

  if (result.data.ok) {
    state.manifests = result.data.manifests ?? [];
  } else {
    state.manifests = [];
    toast(result.data.error ?? "No se pudieron cargar los manifests", "error");
  }

  notify("manifests");
}

async function loadManifestDetail(manifestId) {
  state.selectedManifestId = manifestId;
  loadingDetail = true;
  notify("manifests");

  const result = await api.getManifest(manifestId);

  loadingDetail = false;

  manifestDetail = result.data.ok
    ? { manifest: result.data.manifest, files: result.data.files ?? [] }
    : null;

  if (!result.data.ok) {
    toast(result.data.error ?? "No se pudo cargar el manifest", "error");
  }

  notify("manifests");
}

// ---------- Columna izquierda: lista ----------

function renderManifestList() {
  const col = el("div", { class: "manifest-list-col" });

  col.append(
    el(
      "button",
      { class: "btn btn-primary", onClick: () => openManifestForm(null) },
      "➕ Nuevo manifest"
    )
  );

  const cards = el("div", { class: "manifest-cards" });

  if (state.manifests.length === 0) {
    cards.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "📜"),
        el("p", {}, "No hay manifests. Crea el primero.")
      )
    );
  }

  for (const m of state.manifests) {
    cards.append(
      el(
        "div",
        {
          class: `manifest-card ${m.manifest_id === state.selectedManifestId ? "selected" : ""}`,
          onClick: () => loadManifestDetail(m.manifest_id)
        },
        el(
          "div",
          { class: "m-name" },
          m.name,
          m.is_current ? currentChip() : null
        ),
        el("div", { class: "m-id" }, m.manifest_id),
        el(
          "div",
          { class: "m-meta" },
          statusChip(m.status),
          el("span", {}, `${m.target_type} / ${m.target_id}`),
          el("span", {}, `v${m.version}`),
          el("span", {}, `· ${m.file_count ?? 0} archivos`)
        )
      )
    );
  }

  col.append(cards);

  return col;
}

// ---------- Detalle de manifest ----------

function renderManifestDetail() {
  const panel = el("div", { class: "manifest-detail" });

  if (loadingDetail) {
    panel.append(spinnerBlock("Cargando manifest…"));
    return panel;
  }

  if (!manifestDetail) {
    panel.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "🐚"),
        el("p", {}, "Selecciona un manifest o crea uno nuevo.")
      )
    );
    return panel;
  }

  const { manifest, files } = manifestDetail;

  // Cabecera + acciones
  panel.append(
    el(
      "div",
      { style: "display:flex;align-items:center;gap:10px;margin-bottom:14px;flex-wrap:wrap" },
      el("h2", { style: "font-size:16px" }, manifest.name),
      statusChip(manifest.status),
      manifest.is_current ? currentChip() : null,
      el("span", { style: "font-family:var(--mono);font-size:11px;color:var(--text-faint)" }, manifest.manifest_id)
    )
  );

  panel.append(
    el(
      "div",
      { class: "manifest-actions" },
      el(
        "button",
        { class: "btn", onClick: () => openManifestForm(manifest) },
        "✏️ Editar datos"
      ),
      el(
        "button",
        {
          class: "btn",
          style: manifest.is_current ? "" : "color:var(--accent);border-color:var(--accent)",
          disabled: manifest.is_current ? "" : undefined,
          onClick: () =>
            confirmModal(
              "Marcar como vigente",
              `Se publicará ${manifest.manifest_id} y se desmarcará el manifest current anterior de ${manifest.target_type}/${manifest.target_id}. Los archivos del manifest deben estar publicados.`,
              async () => {
                const result = await api.setCurrent(manifest.manifest_id);

                addLog({ ok: result.data.ok, message: result.data.message ?? "set-current" });

                if (result.data.ok) {
                  toast(result.data.message, "ok");
                  await loadManifests();
                  await loadManifestDetail(manifest.manifest_id);
                } else {
                  toast(result.data.message ?? "Error", "error", 7000);
                }
              }
            )
        },
        manifest.is_current ? "★ Ya es current" : "★ Marcar como current"
      ),
      el(
        "button",
        { class: "btn", onClick: () => openAddFilesModal(manifest) },
        "📎 Añadir archivos"
      ),
      el(
        "button",
        {
          class: "btn btn-ghost",
          onClick: async () => {
            const result = await api.previewManifest(
              manifest.target_type,
              manifest.target_id
            );

            openModal({
              title: `Manifest público · ${manifest.target_type}/${manifest.target_id}`,
              wide: true,
              body: el(
                "pre",
                { class: "json-view" },
                JSON.stringify(result.data, null, 2)
              )
            });
          }
        },
        "👁️ Probar manifest público"
      )
    )
  );

  // Tabla de archivos
  panel.append(el("h3", { style: "font-size:12px;text-transform:uppercase;letter-spacing:0.8px;color:var(--text-faint);margin-bottom:8px" }, `Archivos del manifest (${files.length})`));

  if (files.length === 0) {
    panel.append(
      el(
        "div",
        { class: "empty-state" },
        el("p", {}, "Este manifest no tiene archivos. Usa «Añadir archivos».")
      )
    );
    return panel;
  }

  const table = el(
    "table",
    { class: "mf-table" },
    el(
      "thead",
      {},
      el(
        "tr",
        {},
        el("th", {}, "Archivo"),
        el("th", {}, "Estado"),
        el("th", {}, "Req."),
        el("th", {}, "Prioridad"),
        el("th", {}, "Uso"),
        el("th", {}, ""),
        el("th", {}, "")
      )
    )
  );

  const tbody = el("tbody", {});

  for (const f of files) {
    const requiredCheck = el("input", {
      type: "checkbox",
      ...(f.required ? { checked: "" } : {})
    });

    const priorityInput = el("input", {
      type: "number",
      class: "input-sm",
      value: String(f.load_priority ?? 100),
      min: "0"
    });

    const usageInput = el("input", {
      type: "text",
      class: "input-sm",
      value: f.usage ?? "",
      placeholder: "usage"
    });

    const saveBtn = el(
      "button",
      {
        class: "btn btn-sm",
        title: "Guardar cambios de la fila",
        onClick: async () => {
          const result = await api.linkFile(manifest.manifest_id, f.asset_file_id, {
            filePath: f.file_path,
            required: requiredCheck.checked,
            loadPriority: Number(priorityInput.value || 100),
            usage: usageInput.value.trim() || null
          });

          addLog({ ok: result.data.ok, message: result.data.message ?? "vínculo" });
          toast(
            result.data.ok ? "Vínculo actualizado" : result.data.message ?? "Error",
            result.data.ok ? "ok" : "error"
          );
        }
      },
      "💾"
    );

    const removeBtn = el(
      "button",
      {
        class: "btn btn-sm btn-danger",
        title: "Quitar del manifest (no borra el asset)",
        onClick: () =>
          confirmModal(
            "Quitar archivo",
            `¿Quitar ${f.asset_file_id} de ${manifest.manifest_id}? El archivo físico y su registro no se borran.`,
            async () => {
              const result = await api.unlinkFile(
                manifest.manifest_id,
                f.asset_file_id,
                f.file_path
              );

              addLog({ ok: result.data.ok, message: result.data.message ?? "desvincular" });

              if (result.data.ok) {
                toast(result.data.message, "ok");
                await loadManifests();
                await loadManifestDetail(manifest.manifest_id);
              } else {
                toast(result.data.message ?? "Error", "error");
              }
            }
          )
      },
      "✕"
    );

    tbody.append(
      el(
        "tr",
        {},
        el(
          "td",
          {},
          el("div", {}, `${typeIcon(f.asset_type)} ${f.asset_key}`),
          el("div", { class: "mono", style: "color:var(--text-faint)" }, `${f.asset_file_id} · v${f.version} · ${formatBytes(f.size_bytes)}`)
        ),
        el("td", {}, statusChip(f.status)),
        el("td", {}, requiredCheck),
        el("td", {}, priorityInput),
        el("td", {}, usageInput),
        el("td", {}, saveBtn),
        el("td", {}, removeBtn)
      )
    );
  }

  table.append(tbody);
  panel.append(table);

  return panel;
}

// ---------- Formulario crear/editar manifest ----------

function openManifestForm(existing) {
  const isNew = !existing;

  const idInput = el("input", {
    type: "text",
    value: existing?.manifest_id ?? "",
    placeholder: "manifest_player_editor_v001",
    ...(isNew ? {} : { disabled: "" })
  });

  const nameInput = el("input", {
    type: "text",
    value: existing?.name ?? "",
    placeholder: "Player Editor Assets"
  });

  const versionInput = el("input", {
    type: "text",
    value: existing?.version ?? "1",
    placeholder: "1"
  });

  const targetTypeSelect = el(
    "select",
    {},
    TARGET_TYPES.map((t) =>
      el(
        "option",
        { value: t, selected: t === (existing?.target_type ?? "global") ? "" : undefined },
        t
      )
    )
  );

  const targetIdInput = el("input", {
    type: "text",
    value: existing?.target_id ?? "",
    placeholder: "player_editor"
  });

  const statusSelect = el(
    "select",
    {},
    MANIFEST_STATUSES.map((s) =>
      el(
        "option",
        { value: s, selected: s === (existing?.status ?? "draft") ? "" : undefined },
        s
      )
    )
  );

  const fieldBlock = (label, input) =>
    el("div", { class: "form-field" }, el("label", {}, label), input);

  const modal = openModal({
    title: isNew ? "Nuevo manifest" : `Editar ${existing.manifest_id}`,
    body: el(
      "div",
      {},
      fieldBlock("manifest_id", idInput),
      fieldBlock("name", nameInput),
      el(
        "div",
        { style: "display:flex;gap:10px" },
        el("div", { style: "flex:1" }, fieldBlock("target_type", targetTypeSelect)),
        el("div", { style: "flex:1" }, fieldBlock("target_id", targetIdInput))
      ),
      el(
        "div",
        { style: "display:flex;gap:10px" },
        el("div", { style: "width:100px" }, fieldBlock("version", versionInput)),
        el("div", { style: "flex:1" }, fieldBlock("status", statusSelect))
      )
    ),
    footer: [
      el("button", { class: "btn", onClick: () => modal.close() }, "Cancelar"),
      el(
        "button",
        {
          class: "btn btn-primary",
          onClick: async () => {
            const manifestId = idInput.value.trim();

            if (!manifestId || !nameInput.value.trim() || !targetIdInput.value.trim()) {
              toast("manifest_id, name y target_id son obligatorios", "warn");
              return;
            }

            const result = await api.saveManifest(manifestId, {
              name: nameInput.value.trim(),
              version: versionInput.value.trim() || "1",
              targetType: targetTypeSelect.value,
              targetId: targetIdInput.value.trim(),
              status: statusSelect.value
            });

            addLog({ ok: result.data.ok, message: result.data.message ?? "manifest" });

            if (result.data.ok) {
              toast(result.data.message, "ok");
              modal.close();
              await loadManifests();
              await loadManifestDetail(manifestId);
            } else {
              toast(result.data.message ?? "Error guardando manifest", "error", 6000);
            }
          }
        },
        "Guardar"
      )
    ]
  });
}

// ---------- Modal añadir archivos ----------

async function openAddFilesModal(manifest) {
  const alreadyLinked = new Set(
    (manifestDetail?.files ?? []).map((f) => f.asset_file_id)
  );

  const searchInput = el("input", {
    type: "search",
    placeholder: "Buscar archivos registrados…",
    onInput: () => renderList()
  });

  const listBox = el("div", { style: "margin-top:12px;max-height:46vh;overflow-y:auto" });

  let dbFiles = [];

  const result = await api.listDbFiles("?limit=500");

  if (result.data.ok) {
    dbFiles = result.data.files ?? [];
  }

  function renderList() {
    clear(listBox);

    const q = searchInput.value.toLowerCase();

    const visible = dbFiles.filter(
      (f) =>
        !alreadyLinked.has(f.asset_file_id) &&
        f.status !== "deleted" &&
        (!q ||
          f.asset_file_id.toLowerCase().includes(q) ||
          f.asset_key.toLowerCase().includes(q))
    );

    if (visible.length === 0) {
      listBox.append(
        el("div", { class: "empty-state" }, el("p", {}, "Sin resultados."))
      );
      return;
    }

    for (const f of visible) {
      listBox.append(
        el(
          "div",
          {
            class: "manifest-link-item",
            style: "cursor:pointer",
            onClick: async () => {
              const linkResult = await api.linkFile(
                manifest.manifest_id,
                f.asset_file_id,
                {
                  filePath: f.file_path,
                  required: true,
                  loadPriority: 100,
                  usage: null
                }
              );

              addLog({
                ok: linkResult.data.ok,
                message: linkResult.data.message ?? "vínculo"
              });

              if (linkResult.data.ok) {
                toast(`Añadido ${f.asset_key}`, "ok", 2000);
                alreadyLinked.add(f.asset_file_id);
                renderList();
                await loadManifests();
                await loadManifestDetail(manifest.manifest_id);
              } else {
                toast(linkResult.data.message ?? "Error", "error");
              }
            }
          },
          el(
            "div",
            {},
            el("div", {}, `${typeIcon(f.asset_type)} ${f.asset_key}`),
            el(
              "div",
              { class: "mono", style: "color:var(--text-faint)" },
              `${f.asset_file_id} · v${f.version}`
            )
          ),
          statusChip(f.status)
        )
      );
    }
  }

  renderList();

  openModal({
    title: `Añadir archivos a ${manifest.manifest_id}`,
    wide: true,
    body: el("div", {}, searchInput, listBox)
  });
}
