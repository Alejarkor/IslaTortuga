import { Panel } from "@/ui/Panel";
import { Button } from "@/ui/Button";

/**
 * Acción de entrar en partida. La integración con el servidor de juego
 * (que usará la apariencia guardada) queda fuera de esta fase (sección 17).
 */
export function MatchPanel() {
  return (
    <Panel title="Partida" className="panel--match">
      <p className="muted">Tu apariencia guardada se usará en la partida.</p>
      <Button variant="primary" disabled title="Disponible próximamente">
        Buscar partida
      </Button>
    </Panel>
  );
}
