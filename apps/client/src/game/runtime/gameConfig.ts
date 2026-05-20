import Phaser from 'phaser';
import { WorldScene } from './scenes/WorldScene';

export const createGameConfig = (parent: HTMLElement): Phaser.Types.Core.GameConfig => ({
  type: Phaser.AUTO,
  parent,
  width: window.innerWidth,
  height: window.innerHeight,
  backgroundColor: '#11160f',
  pixelArt: true,
  roundPixels: true,
  physics: {
    default: 'arcade',
    arcade: {
      debug: true,
      gravity: { x: 0, y: 0 },
    },
  },
  scene: [WorldScene],
  scale: {
    mode: Phaser.Scale.RESIZE,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
});
