// Vista: Sincronización (Sync Preview + aplicación de operaciones)

import { api } from "../api.js";
import { addLogResults, notify, state } from "../state.js";
import { clear, el, openModal, spinnerBlock, statusChip, toast } from "../ui.js";
import { rescan } from "./browser.js";

let applying = false;

export function renderSync(container) {
  clear(container);

  const layout = el("div", { class: "sync-layout" });

  layout.append(
    el(
      "div",
      { class: "sync-toolbar" },
      el(
        "button",
        {
          class: "btn btn-primary",
          onClick: async () => {
            await rescan();
            notify("sync");
          }
        },
        "🔍 Generar plan (re-escanear)"
      ),
      el(
        "button",
        {
          class: "btn",
          onClick: async () => {
            const result = await api.syncReport();

            openModal({
              title: "Dry-run · sync-report de GameApi",
              wide: true,
              body: el(
                "pre",
                { class: "json-view" },
                JSON.stringify(result.data, null, 2)
              )
            });
          }
        },
        "🧪 Dry-run (sync-report)"
      )
    )
  );

  if (state.scanning) {
    layout.append(spinnerBlock("Escaneando…"));
    container.append(layout);
    return;
  }

  if (!state.scan) {
    layout.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "🔄"),
        el("p", {}, "Genera un plan de sincronización para ver las operaciones pendientes.")
      )
    );
    container.append(layout);
    return;
  }

  layout.append(renderPlan());
  container.append(layout);
}

function buildPlan() {
  const registrable = [];
  const blocked = [];

  for (const f of state.scan.files) {
    const meta = {
      filePath: f.filePath,
      assetKey: f.sidecar?.assetKey ?? f.db?.asset_key ?? f.inferred.assetKey,
      assetType: f.sidecar?.assetType ?? f.db?.asset_type ?? f.inferred.assetType,
      version: f.sidecar?.version ?? f.db?.version ?? f.inferred.version,
      status: f.sidecar?.status ?? f.db?.status ?? "draft"
    };

    if (f.state === "new") {
      registrable.push({
        kind: "Registrar nuevo",
        file: f,
        operation: { type: "registerFile", ...meta, writeSidecar: true }
      });
    } else if (f.state === "changed") {
      if (f.db?.status === "published") {
        blocked.push({
          file: f,
          reason:
            "Publicado con contenido distinto: crea una nueva versión con nuevo nombre físico"
        });
      } else {
        registrable.push({
          kind: "Actualizar hash/metadatos",
          file: f,
          operation: { type: "registerFile", ...meta, writeSidecar: true }
        });
      }
    }
  }

  const missing = (state.scan.missingOnDisk ?? [])
    .filter((row) => row.status !== "deprecated")
    .map((row) => ({
      kind: "Deprecar (no existe en disco)",
      row,
      operation: {
        type: "patchFileStatus",
        assetFileId: row.asset_file_id,
        status: "deprecated"
      }
    }));

  return { registrable, blocked, missing };
}

function renderPlan() {
  const wrap = el("div", {});
  const plan = buildPlan();
  const selected = new Map(); // key -> operation

  const total = plan.registrable.length + plan.missing.length;

  if (total === 0 && plan.blocked.length === 0) {
    wrap.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "✅"),
        el("p", {}, "Todo sincronizado: no hay operaciones pendientes.")
      )
    );
    return wrap;
  }

  const applyBtn = el(
    "button",
    {
      class: "btn btn-primary",
      disabled: "",
      onClick: async () => {
        if (applying || selected.size === 0) return;

        applying = true;
        applyBtn.disabled = true;
        applyBtn.textContent = "Aplicando…";

        const operations = [...selected.values()];
        const result = await api.syncApply(operations);

        applying = false;

        if (result.data.results) {
          addLogResults(result.data.results);
        }

        const okCount = result.data.applied ?? 0;
        const failCount = result.data.failed ?? 0;

        toast(
          `Sincronización: ${okCount} ok, ${failCount} con error`,
          failCount > 0 ? "warn" : "ok",
          5000
        );

        await rescan();
        notify("sync");
      }
    },
    "⚡ Aplicar seleccionadas (0)"
  );

  function refreshApplyButton() {
    applyBtn.disabled = selected.size === 0 ? "" : undefined;

    if (selected.size === 0) {
      applyBtn.setAttribute("disabled", "");
    } else {
      applyBtn.removeAttribute("disabled");
    }

    applyBtn.textContent = `⚡ Aplicar seleccionadas (${selected.size})`;
  }

  const opRow = (key, operation, pathText, descText, checkedByDefault) => {
    const checkbox = el("input", {
      type: "checkbox",
      ...(checkedByDefault ? { checked: "" } : {}),
      onChange: (e) => {
        if (e.target.checked) selected.set(key, operation);
        else selected.delete(key);
        refreshApplyButton();
      }
    });

    if (checkedByDefault) selected.set(key, operation);

    return el(
      "div",
      { class: "sync-op" },
      checkbox,
      el("span", { class: "op-path" }, pathText),
      el("span", { class: "op-desc" }, descText)
    );
  };

  // Grupo: registrables
  if (plan.registrable.length > 0) {
    const group = el(
      "div",
      { class: "sync-group" },
      el("h2", {}, "📡 Registrar / actualizar en base de datos", el("span", { class: "chip chip-new" }, String(plan.registrable.length)))
    );

    for (const item of plan.registrable) {
      group.append(
        opRow(
          `reg:${item.file.filePath}`,
          item.operation,
          item.file.filePath,
          `${item.kind} → ${item.operation.assetKey} v${item.operation.version} (${item.operation.status})`,
          true
        )
      );
    }

    wrap.append(group);
  }

  // Grupo: bloqueados
  if (plan.blocked.length > 0) {
    const group = el(
      "div",
      { class: "sync-group" },
      el("h2", {}, "⛔ Bloqueados (requieren nueva versión)", el("span", { class: "chip chip-error" }, String(plan.blocked.length)))
    );

    for (const item of plan.blocked) {
      group.append(
        el(
          "div",
          { class: "sync-op blocked" },
          el("span", {}, "⛔"),
          el("span", { class: "op-path" }, item.file.filePath),
          el("span", { class: "op-desc" }, item.reason)
        )
      );
    }

    wrap.append(group);
  }

  // Grupo: faltan en disco
  if (plan.missing.length > 0) {
    const group = el(
      "div",
      { class: "sync-group" },
      el("h2", {}, "👻 En DB pero no en disco", el("span", { class: "chip chip-changed" }, String(plan.missing.length)))
    );

    for (const item of plan.missing) {
      group.append(
        el(
          "div",
          { class: "sync-op" },
          el("input", {
            type: "checkbox",
            onChange: (e) => {
              const key = `dep:${item.row.asset_file_id}`;
              if (e.target.checked) selected.set(key, item.operation);
              else selected.delete(key);
              refreshApplyButton();
            }
          }),
          el("span", { class: "op-path" }, item.row.file_path),
          statusChip(item.row.status),
          el("span", { class: "op-desc" }, `${item.row.asset_file_id} → marcar deprecated`)
        )
      );
    }

    wrap.append(group);
  }

  refreshApplyButton();

  wrap.append(
    el("div", { style: "margin-top:6px;display:flex;gap:10px" }, applyBtn)
  );

  return wrap;
}
