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
 * Pantalla de pre-juego (lobby).
 * Escritorio: 3 columnas (Amigos y Chat · Personaje · Salas).
 * Móvil: cada sección ocupa la pantalla y se cambia con swipe lateral
 * (scroll-snap) + indicadores. La rotación del personaje (arrastre sobre el
 * canvas) no dispara el swipe gracias a touch-action en el lienzo.
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

  return (
    <div className="lobby">
      <LobbyHeader />

      {appearanceQuery.isLoading ? (
        <div className="centered-screen">
          <Spinner label="Cargando tu personaje…" />
        </div>
      ) : appearanceQuery.isError ? (
        <div className="centered-screen">
          <p className="form-error">No se pudo cargar tu apariencia.</p>
        </div>
      ) : (
        <>
          <main className="lobby-grid" ref={gridRef} onScroll={onScroll}>
            <FriendsPanel />
            <CharacterEditorPanel />
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
        </>
      )}
    </div>
  );
}
