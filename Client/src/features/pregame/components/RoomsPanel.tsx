import { useState } from "react";

import { RefreshIcon, PlusIcon } from "@/features/auth/PirateIcons";

type Tab = "publicas" | "privadas";

/** Salas de ejemplo (no hay backend de salas todavía). */
const MOCK_ROOMS = [
  { id: "1", name: "Aventura en Tortuga", mode: "Aventura", map: "Isla Tortuga", count: "3/4", icon: "☠" },
  { id: "2", name: "Cazadores de Tesoros", mode: "PvE", map: "Arrecife del Naufragio", count: "2/4", icon: "☠" },
  { id: "3", name: "Batalla Pirata", mode: "PvP", map: "Fuerte del Olimpo", count: "4/8", icon: "⚔" },
  { id: "4", name: "Expedición Nocturna", mode: "Aventura", map: "Selva Esmeralda", count: "2/4", icon: "⚓" },
  { id: "5", name: "Duelo de Corsarios", mode: "PvP", map: "Bahía del Cañón", count: "1/2", icon: "⚓" }
];

/**
 * Panel derecho: Salas.
 */
export function RoomsPanel() {
  const [tab, setTab] = useState<Tab>("publicas");

  return (
    <div className="lobby-panel wood-frame">
      <div className="lobby-banner">Salas</div>
      <div className="parch lobby-panel__inner">

        <div className="seg-tabs">
          <button
            className={`seg-tab ${tab === "publicas" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("publicas")}
          >
            Salas públicas
          </button>
          <button
            className={`seg-tab ${tab === "privadas" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("privadas")}
          >
            Salas privadas
          </button>
        </div>

        <div className="rooms-filters">
          <button className="icon-btn" aria-label="Actualizar" title="Actualizar">
            <RefreshIcon />
          </button>
        </div>

        <div className="rooms-list">
          {(tab === "publicas" ? MOCK_ROOMS : []).map((r) => (
            <div key={r.id} className="room-row">
              <span className="room-icon">{r.icon}</span>
              <div className="room-info">
                <p className="room-info__name">{r.name}</p>
                <p className="room-info__meta">
                  {r.mode} · {r.map}
                </p>
              </div>
              <span className="room-count">👤 {r.count}</span>
              <button className="mini-btn">Unirse</button>
            </div>
          ))}
          {tab === "privadas" && (
            <p className="friend-info__status">
              Usa un código para unirte a una sala privada.
            </p>
          )}
        </div>

        <div className="rooms-foot">
          <button className="big-btn big-btn--gold">
            <PlusIcon /> Crear sala
          </button>
          <div className="code-join color-subpanel">
            <input placeholder="Ingresa el código de la sala…" />
            <button className="mini-btn">Unirse</button>
          </div>
          <button className="big-btn big-btn--play">⚓ JUGAR</button>
        </div>
      </div>
    </div>
  );
}
