import Phaser from 'phaser';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';

export function loadWorldAssets(
  scene: Phaser.Scene,
  runtime: GameRuntime,
  mapKey: string,
  playerKey: string,
) {
  const mapDefinition = runtime.catalog.maps[runtime.startGame.mapId];
  const playerDefinition = runtime.catalog.players.default;

  if (!mapDefinition) {
    throw new Error(`No existe definicion de mapa para ${runtime.startGame.mapId}.`);
  }

  scene.load.tilemapTiledJSON(
    mapKey,
    runtime.catalog.resolveFile(mapDefinition.mapFileId).url,
  );

  for (const tileset of mapDefinition.tilesets) {
    scene.load.image(
      tileset.textureKey,
      runtime.catalog.resolveFile(tileset.imageFileId).url,
    );
  }

  scene.load.spritesheet(
    playerKey,
    runtime.catalog.resolveFile(playerDefinition.imageFileId).url,
    {
      frameWidth: playerDefinition.frameWidth,
      frameHeight: playerDefinition.frameHeight,
    },
  );
}
