// Vista: Registro de sincronización (Sync Log)

import { clearLog, state } from "../state.js";
import { clear, confirmModal, el } from "../ui.js";

export function renderLog(container) {
  clear(container);

  const layout = el("div", { class: "log-layout" });

  layout.append(
    el(
      "div",
      { style: "display:flex;justify-content:space-between;align-items:center;margin-bottom:14px" },
      el(
        "span",
        { style: "font-size:12px;color:var(--text-dim)" },
        `${state.log.length} entradas`
      ),
      el(
        "button",
        {
          class: "btn btn-sm btn-danger",
          onClick: () =>
            confirmModal("Limpiar registro", "¿Borrar todas las entradas del registro?", clearLog)
        },
        "🗑️ Limpiar"
      )
    )
  );

  if (state.log.length === 0) {
    layout.append(
      el(
        "div",
        { class: "empty-state" },
        el("div", { class: "big" }, "📋"),
        el("p", {}, "Aún no hay operaciones registradas.")
      )
    );
    container.append(layout);
    return;
  }

  for (const entry of state.log) {
    layout.append(
      el(
        "div",
        { class: `log-entry ${entry.ok ? "ok" : "fail"}` },
        el(
          "span",
          { class: "log-time" },
          new Date(entry.time).toLocaleString("es-ES", { hour12: false })
        ),
        el("span", { class: "log-msg" }, `${entry.ok ? "✅" : "❌"} ${entry.message}`)
      )
    );
  }

  container.append(layout);
}
