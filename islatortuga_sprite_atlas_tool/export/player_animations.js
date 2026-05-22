// Auto-generado por IslaTortuga Sprite Atlas Tool v1.2.0
// Carga previa requerida:
// this.load.atlas('player', 'player_atlas.png', 'player_atlas.json');

export function createPlayerAnimations(scene, textureKey = 'player') {
  if (!scene.anims.exists('player-idle-down')) {
    scene.anims.create({
      key: 'player-idle-down',
      frames: [
        { key: textureKey, frame: 'idle_down_000.png' },
        { key: textureKey, frame: 'idle_down_001.png' },
        { key: textureKey, frame: 'idle_down_002.png' },
        { key: textureKey, frame: 'idle_down_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-down_left')) {
    scene.anims.create({
      key: 'player-idle-down_left',
      frames: [
        { key: textureKey, frame: 'idle_down_left_000.png' },
        { key: textureKey, frame: 'idle_down_left_001.png' },
        { key: textureKey, frame: 'idle_down_left_002.png' },
        { key: textureKey, frame: 'idle_down_left_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-down_right')) {
    scene.anims.create({
      key: 'player-idle-down_right',
      frames: [
        { key: textureKey, frame: 'idle_down_right_000.png' },
        { key: textureKey, frame: 'idle_down_right_001.png' },
        { key: textureKey, frame: 'idle_down_right_002.png' },
        { key: textureKey, frame: 'idle_down_right_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-left')) {
    scene.anims.create({
      key: 'player-idle-left',
      frames: [
        { key: textureKey, frame: 'idle_left_000.png' },
        { key: textureKey, frame: 'idle_left_001.png' },
        { key: textureKey, frame: 'idle_left_002.png' },
        { key: textureKey, frame: 'idle_left_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-right')) {
    scene.anims.create({
      key: 'player-idle-right',
      frames: [
        { key: textureKey, frame: 'idle_right_000.png' },
        { key: textureKey, frame: 'idle_right_001.png' },
        { key: textureKey, frame: 'idle_right_002.png' },
        { key: textureKey, frame: 'idle_right_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-up')) {
    scene.anims.create({
      key: 'player-idle-up',
      frames: [
        { key: textureKey, frame: 'idle_up_000.png' },
        { key: textureKey, frame: 'idle_up_001.png' },
        { key: textureKey, frame: 'idle_up_002.png' },
        { key: textureKey, frame: 'idle_up_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-up_left')) {
    scene.anims.create({
      key: 'player-idle-up_left',
      frames: [
        { key: textureKey, frame: 'idle_up_left_000.png' },
        { key: textureKey, frame: 'idle_up_left_001.png' },
        { key: textureKey, frame: 'idle_up_left_002.png' },
        { key: textureKey, frame: 'idle_up_left_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-idle-up_right')) {
    scene.anims.create({
      key: 'player-idle-up_right',
      frames: [
        { key: textureKey, frame: 'idle_up_right_000.png' },
        { key: textureKey, frame: 'idle_up_right_001.png' },
        { key: textureKey, frame: 'idle_up_right_002.png' },
        { key: textureKey, frame: 'idle_up_right_003.png' },
      ],
      frameRate: 4,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-down')) {
    scene.anims.create({
      key: 'player-walk-down',
      frames: [
        { key: textureKey, frame: 'walk_down_000.png' },
        { key: textureKey, frame: 'walk_down_001.png' },
        { key: textureKey, frame: 'walk_down_002.png' },
        { key: textureKey, frame: 'walk_down_003.png' },
        { key: textureKey, frame: 'walk_down_004.png' },
        { key: textureKey, frame: 'walk_down_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-down_left')) {
    scene.anims.create({
      key: 'player-walk-down_left',
      frames: [
        { key: textureKey, frame: 'walk_down_left_000.png' },
        { key: textureKey, frame: 'walk_down_left_001.png' },
        { key: textureKey, frame: 'walk_down_left_002.png' },
        { key: textureKey, frame: 'walk_down_left_003.png' },
        { key: textureKey, frame: 'walk_down_left_004.png' },
        { key: textureKey, frame: 'walk_down_left_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-down_right')) {
    scene.anims.create({
      key: 'player-walk-down_right',
      frames: [
        { key: textureKey, frame: 'walk_down_right_000.png' },
        { key: textureKey, frame: 'walk_down_right_001.png' },
        { key: textureKey, frame: 'walk_down_right_002.png' },
        { key: textureKey, frame: 'walk_down_right_003.png' },
        { key: textureKey, frame: 'walk_down_right_004.png' },
        { key: textureKey, frame: 'walk_down_right_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-left')) {
    scene.anims.create({
      key: 'player-walk-left',
      frames: [
        { key: textureKey, frame: 'walk_left_000.png' },
        { key: textureKey, frame: 'walk_left_001.png' },
        { key: textureKey, frame: 'walk_left_002.png' },
        { key: textureKey, frame: 'walk_left_003.png' },
        { key: textureKey, frame: 'walk_left_004.png' },
        { key: textureKey, frame: 'walk_left_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-right')) {
    scene.anims.create({
      key: 'player-walk-right',
      frames: [
        { key: textureKey, frame: 'walk_right_000.png' },
        { key: textureKey, frame: 'walk_right_001.png' },
        { key: textureKey, frame: 'walk_right_002.png' },
        { key: textureKey, frame: 'walk_right_003.png' },
        { key: textureKey, frame: 'walk_right_004.png' },
        { key: textureKey, frame: 'walk_right_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-up')) {
    scene.anims.create({
      key: 'player-walk-up',
      frames: [
        { key: textureKey, frame: 'walk_up_000.png' },
        { key: textureKey, frame: 'walk_up_001.png' },
        { key: textureKey, frame: 'walk_up_002.png' },
        { key: textureKey, frame: 'walk_up_003.png' },
        { key: textureKey, frame: 'walk_up_004.png' },
        { key: textureKey, frame: 'walk_up_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-up_left')) {
    scene.anims.create({
      key: 'player-walk-up_left',
      frames: [
        { key: textureKey, frame: 'walk_up_left_000.png' },
        { key: textureKey, frame: 'walk_up_left_001.png' },
        { key: textureKey, frame: 'walk_up_left_002.png' },
        { key: textureKey, frame: 'walk_up_left_003.png' },
        { key: textureKey, frame: 'walk_up_left_004.png' },
        { key: textureKey, frame: 'walk_up_left_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

  if (!scene.anims.exists('player-walk-up_right')) {
    scene.anims.create({
      key: 'player-walk-up_right',
      frames: [
        { key: textureKey, frame: 'walk_up_right_000.png' },
        { key: textureKey, frame: 'walk_up_right_001.png' },
        { key: textureKey, frame: 'walk_up_right_002.png' },
        { key: textureKey, frame: 'walk_up_right_003.png' },
        { key: textureKey, frame: 'walk_up_right_004.png' },
        { key: textureKey, frame: 'walk_up_right_005.png' },
      ],
      frameRate: 24,
      repeat: -1
    });
  }

}

export default createPlayerAnimations;
