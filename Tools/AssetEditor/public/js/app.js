// Punto de entrada de la SPA del AssetEditor

import { api } from "./api.js";
import { state, subscribe } from "./state.js";
import { renderBrowser, rescan } from "./views/browser.js";
import { loadManifests, renderManifests } from "./views/manifests.js";
import { renderSync } from "./views/sync.js";
import { renderLog } from "./views/log.js";

const VIEWS = {
  browser: { title: "Explorador de assets", render: renderBrowser },
  manifests: { title: "Gestor de manifests", render: renderManifests },
  sync: { title: "Sincronización con base de datos", render: renderSync },
  log: { title: "Registro de operaciones", render: renderLog }
};

let currentView = "browser";

const container = document.getElementById("view-container");
const title = document.getElementById("view-title");

function render() {
  title.textContent = VIEWS[currentView].title;
  VIEWS[currentView].render(container);
}

function setView(view) {
  currentView = view;

  document.querySelectorAll(".nav-item").forEach((item) => {
    item.classList.toggle("active", item.dataset.view === view);
  });

  if (view === "manifests") {
    loadManifests();
  }

  render();
}

// Navegación
document.querySelectorAll(".nav-item").forEach((item) => {
  item.addEventListener("click", () => setView(item.dataset.view));
});

// Botón global de escaneo
document.getElementById("btn-scan").addEventListener("click", async () => {
  await rescan();

  if (currentView !== "browser" && currentView !== "sync") {
    setView("browser");
  } else {
    render();
  }
});

// Re-render cuando cambia el estado
subscribe((topic) => {
  if (topic === "browser" && (currentView === "browser" || currentView === "sync")) {
    render();
  } else if (topic === "manifests" && currentView === "manifests") {
    render();
  } else if (topic === "sync" && currentView === "sync") {
    render();
  } else if (topic === "log" && currentView === "log") {
    render();
  }
});

// Estado de conexión con GameApi
async function refreshConnection() {
  const dot = document.getElementById("conn-dot");
  const label = document.getElementById("conn-label");
  const rootLabel = document.getElementById("assets-root");

  try {
    const result = await api.getConfig();

    if (result.data.ok) {
      state.config = result.data;

      rootLabel.textContent = result.data.assetsRoot;
      rootLabel.title = result.data.assetsRoot;

      if (result.data.gameApiReachable) {
        dot.className = "conn-dot online";
        label.textContent = "GameApi conectada";
      } else {
        dot.className = "conn-dot offline";
        label.textContent = "GameApi sin conexión";
      }

      if (!result.data.hasAdminToken) {
        label.textContent += " · sin token";
      }
    }
  } catch {
    dot.className = "conn-dot offline";
    label.textContent = "Herramienta sin conexión";
  }
}

refreshConnection();
setInterval(refreshConnection, 15000);

// Primer renderizado + escaneo inicial
render();
rescan();
