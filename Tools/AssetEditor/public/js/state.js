// Estado compartido de la aplicación + log persistente

const LOG_KEY = "islat_asseteditor_log";

function loadLog() {
  try {
    const raw = localStorage.getItem(LOG_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

export const state = {
  config: null,
  scan: null,
  scanning: false,
  selectedFilePath: null,
  manifests: [],
  selectedManifestId: null,
  log: loadLog()
};

const listeners = new Set();

export function subscribe(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export function notify(topic) {
  for (const fn of listeners) fn(topic);
}

export function addLog(entry) {
  state.log.unshift({
    time: new Date().toISOString(),
    ok: entry.ok,
    message: entry.message
  });

  state.log = state.log.slice(0, 500);

  try {
    localStorage.setItem(LOG_KEY, JSON.stringify(state.log));
  } catch {
    // sin persistencia disponible
  }

  notify("log");
}

export function addLogResults(results) {
  // Los resultados llegan en orden de ejecución; el log es LIFO
  for (const r of [...results].reverse()) {
    addLog({ ok: r.ok, message: r.message });
  }
}

export function clearLog() {
  state.log = [];

  try {
    localStorage.removeItem(LOG_KEY);
  } catch {
    // ignorar
  }

  notify("log");
}

export function getSelectedFile() {
  if (!state.scan || !state.selectedFilePath) return null;

  return (
    state.scan.files.find((f) => f.filePath === state.selectedFilePath) ?? null
  );
}
