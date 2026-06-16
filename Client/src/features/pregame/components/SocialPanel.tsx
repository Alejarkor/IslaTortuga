import { Panel } from "@/ui/Panel";

/**
 * Panel derecho: social/lobby. Fuera del alcance del editor de personaje
 * (no es MVP de esta fase), se deja como marcador del layout (sección 5).
 */
export function SocialPanel() {
  return (
    <Panel title="Social" className="panel--social">
      <p className="muted">Chat, amigos y partidas próximamente.</p>
    </Panel>
  );
}
