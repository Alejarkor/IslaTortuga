import { useRef, useState } from "react";

import { Spinner } from "@/ui/Spinner";
import { LobbyHeader } from "./components/LobbyHeader";
import { FriendsPanel } from "./components/FriendsPanel";
import { CharacterEditorPanel } from "./components/CharacterEditorPanel";
import { RoomsPanel } from "./components/RoomsPanel";
import { useLoadAppearance } from "./hooks/useAppearance";
import "@/styles/pregame.css";

const SECTIONS = ["Amigos", "Personaje", "Salas"];

/**
 * Pantalla de pre-juego (lobby). Amigos y Salas se cargan siempre; un fallo al
 * cargar la apariencia solo afecta a la columna del personaje, no bloquea el resto.
 */
export function PreGamePage() {
  const appearanceQuery = useLoadAppearance();
  const gridRef = useRef<HTMLDivElement | null>(null);
  const [active, setActive] = useState(0);

  const onScroll = () => {
    const el = gridRef.current;
    if (!el || el.clientWidth === 0) return;
    setActive(Math.round(el.scrollLeft / el.clientWidth));
  };

  const goTo = (i: number) => {
    const el = gridRef.current;
    if (!el) return;
    el.scrollTo({ left: i * el.clientWidth, behavior: "smooth" });
  };

  let characterColumn;
  if (appearanceQuery.isLoading) {
    characterColumn = (
      <div className="lobby-panel char-panel wood-frame">
        <div className="lobby-banner">Personaje</div>
        <div className="parch lobby-panel__inner centered-screen">
          <Spinner label="Cargando tu personaje…" />
        </div>
      </div>
    );
  } else if (appearanceQuery.isError) {
    characterColumn = (
      <div className="lobby-panel char-panel wood-frame">
        <div className="lobby-banner">Personaje</div>
        <div className="parch lobby-panel__inner">
          <p className="form-error">
            No se pudo cargar tu apariencia. Puedes seguir usando amigos y salas.
          </p>
          <button className="mini-btn" onClick={() => appearanceQuery.refetch()}>
            Reintentar
          </button>
        </div>
      </div>
    );
  } else {
    characterColumn = <CharacterEditorPanel />;
  }

  return (
    <div className="lobby">
      <LobbyHeader />

      <main className="lobby-grid" ref={gridRef} onScroll={onScroll}>
        <FriendsPanel />
        {characterColumn}
        <RoomsPanel />
      </main>

      <nav className="lobby-dots" aria-label="Secciones">
        {SECTIONS.map((label, i) => (
          <button
            key={label}
            type="button"
            className={`lobby-dot ${i === active ? "lobby-dot--active" : ""}`}
            aria-label={label}
            aria-current={i === active}
            onClick={() => goTo(i)}
          />
        ))}
      </nav>
    </div>
  );
}
