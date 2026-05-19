import { useEffect, useRef } from 'react';
import Phaser from 'phaser';
import { createGameConfig } from './gameConfig';

export function PhaserGame() {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);

  useEffect(() => {
    if (!containerRef.current || gameRef.current) return;

    gameRef.current = new Phaser.Game(createGameConfig(containerRef.current));

    return () => {
      gameRef.current?.destroy(true);
      gameRef.current = null;
    };
  }, []);

  return <div className="phaser-container" ref={containerRef} />;
}
