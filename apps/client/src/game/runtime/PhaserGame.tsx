import { useEffect, useRef } from 'react';
import Phaser from 'phaser';
import { setCurrentGameRuntime, type GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { createGameConfig } from './gameConfig';

type PhaserGameProps = {
  runtime: GameRuntime;
};

export function PhaserGame({ runtime }: PhaserGameProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);

  useEffect(() => {
    if (!containerRef.current || gameRef.current) return;

    setCurrentGameRuntime(runtime);
    gameRef.current = new Phaser.Game(createGameConfig(containerRef.current));

    return () => {
      gameRef.current?.destroy(true);
      gameRef.current = null;
      setCurrentGameRuntime(null);
    };
  }, [runtime]);

  return <div className="phaser-container" ref={containerRef} />;
}
