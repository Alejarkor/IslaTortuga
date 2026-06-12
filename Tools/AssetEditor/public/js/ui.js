// Helpers de UI compartidos

export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);

  for (const [key, value] of Object.entries(attrs)) {
    if (key === "class") node.className = value;
    else if (key === "html") node.innerHTML = value;
    else if (key.startsWith("on") && typeof value === "function") {
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else if (value !== undefined && value !== null) {
      node.setAttribute(key, value);
    }
  }

  for (const child of children.flat()) {
    if (child === null || child === undefined) continue;
    node.append(child.nodeType ? child : document.createTextNode(child));
  }

  return node;
}

export function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

// ---------- Chips ----------

const STATE_LABELS = {
  new: "Nuevo",
  registered: "Registrado",
  changed: "Cambiado",
  error: "Error"
};

const STATUS_LABELS = {
  draft: "Borrador",
  published: "Publicado",
  deprecated: "Obsoleto",
  deleted: "Eliminado"
};

export function stateChip(state) {
  return el("span", { class: `chip chip-${state}` }, STATE_LABELS[state] ?? state);
}

export function statusChip(status) {
  return el(
    "span",
    { class: `chip chip-${status}` },
    STATUS_LABELS[status] ?? status
  );
}

export function currentChip() {
  return el("span", { class: "chip chip-current" }, "★ current");
}

// ---------- Formato ----------

export function formatBytes(bytes) {
  const n = Number(bytes);

  if (!Number.isFinite(n)) return "-";
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / (1024 * 1024)).toFixed(2)} MB`;
}

export function shortHash(hash) {
  if (!hash) return "-";
  const hex = hash.replace(/^sha256-/, "");
  return `sha256-${hex.slice(0, 10)}…`;
}

export function timeNow() {
  return new Date().toLocaleTimeString("es-ES", { hour12: false });
}

export const TYPE_ICONS = {
  texture: "🖼️",
  sprite: "🧩",
  model: "🧊",
  audio: "🔊",
  map: "🗺️",
  shader: "✨",
  material: "🎨",
  animation: "🎞️",
  data: "📄"
};

export function typeIcon(type) {
  return TYPE_ICONS[type] ?? "📦";
}

// ---------- Toasts ----------

export function toast(message, kind = "ok", ms = 3500) {
  const root = document.getElementById("toast-root");
  const node = el("div", { class: `toast ${kind}` }, message);

  root.append(node);
  setTimeout(() => node.remove(), ms);
}

// ---------- Modal ----------

export function openModal({ title, body, footer, wide }) {
  const root = document.getElementById("modal-root");

  const close = () => backdrop.remove();

  const backdrop = el(
    "div",
    {
      class: "modal-backdrop",
      onClick: (e) => {
        if (e.target === backdrop) close();
      }
    },
    el(
      "div",
      { class: "modal", style: wide ? "width:min(840px,94vw)" : "" },
      el(
        "div",
        { class: "modal-header" },
        el("h2", {}, title),
        el("button", { class: "modal-close", onClick: close }, "✕")
      ),
      el("div", { class: "modal-body" }, body),
      footer ? el("div", { class: "modal-footer" }, footer) : null
    )
  );

  root.append(backdrop);

  return { close };
}

export function confirmModal(title, message, onConfirm) {
  const modal = openModal({
    title,
    body: el("p", { style: "font-size:13px;color:var(--text-dim)" }, message),
    footer: [
      el("button", { class: "btn", onClick: () => modal.close() }, "Cancelar"),
      el(
        "button",
        {
          class: "btn btn-primary",
          onClick: () => {
            modal.close();
            onConfirm();
          }
        },
        "Confirmar"
      )
    ]
  });
}

export function spinnerBlock(text = "Cargando…") {
  return el(
    "div",
    { class: "loading-block" },
    el("span", { class: "spinner" }),
    text
  );
}
