import { useEffect, useState } from 'react';
import { PhaserGame } from '../../game/runtime/PhaserGame';
import {
  bootstrapGameRuntime,
  type GameRuntime,
} from '../../game/bootstrap/gameBootstrapper';
import { getStoredToken } from '../auth/authSession';

export function GameBootstrapPage() {
  const [runtime, setRuntime] = useState<GameRuntime | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const token = getStoredToken();

    if (!token) {
      setError('No hay sesion activa para iniciar partida.');
      return;
    }

    bootstrapGameRuntime(token)
      .then(setRuntime)
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'No se pudo preparar la partida.');
      });
  }, []);

  if (error) {
    return <main className="center-page">{error}</main>;
  }

  if (!runtime) {
    return <main className="center-page">Preparando content pack y arranque del juego...</main>;
  }

  return <PhaserGame runtime={runtime} />;
}
