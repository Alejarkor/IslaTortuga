import { useEffect, useRef, useState } from 'react';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { BabylonWorld } from './babylonWorld';

type BabylonGameProps = {
  runtime: GameRuntime;
};

export function BabylonGame({ runtime }: BabylonGameProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const [status, setStatus] = useState('Preparando runtime Babylon...');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;

    if (!canvas) {
      return;
    }

    const world = new BabylonWorld(canvas, runtime, {
      onStatusChange: (message) => {
        setStatus(message);
        setError(null);
      },
      onError: (message) => {
        setStatus(message);
        setError(message);
      },
    });

    let disposed = false;

    world.initialize().catch((err) => {
      if (disposed) {
        return;
      }

      const message =
        err instanceof Error ? err.message : 'No se pudo inicializar la escena Babylon.';
      setStatus(message);
      setError(message);
    });

    return () => {
      disposed = true;
      world.dispose();
    };
  }, [runtime]);

  return (
    <main className="babylon-shell">
      <canvas ref={canvasRef} className="babylon-canvas" />
      <aside className="babylon-overlay">
        <p className="eyebrow">Babylon Client</p>
        <p className="babylon-status">{status}</p>
        <p className="muted">Content pack: {runtime.manifest.contentPackId}</p>
        <p className="muted">Mapa: {runtime.startGame.mapId}</p>
        <p className="muted">Movimiento: WASD o flechas</p>
        {error ? <p className="error">{error}</p> : null}
      </aside>
    </main>
  );
}
