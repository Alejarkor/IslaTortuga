import Phaser from 'phaser';

const MAP_KEY = 'test-map';
const PLAYER_SPEED = 150;

export class WorldScene extends Phaser.Scene {
  private player?: Phaser.GameObjects.Rectangle;
  private cursors?: Phaser.Types.Input.Keyboard.CursorKeys;
  private wasd?: Record<string, Phaser.Input.Keyboard.Key>;

  constructor() {
    super('WorldScene');
  }

  preload() {
    this.load.tilemapTiledJSON(MAP_KEY, '/assets/maps/test_map.tmj');

    this.load.image('tx-plant', '/assets/tilesets/TX Plant.png');
    this.load.image('tx-tileset-grass', '/assets/tilesets/TX Tileset Grass.png');
  }

  create() {
    const map = this.make.tilemap({ key: MAP_KEY });

    const tilesets = [
      map.addTilesetImage('TX Plant', 'tx-plant'),
      map.addTilesetImage('TX Tileset Grass', 'tx-tileset-grass'),
    ].filter((tileset): tileset is Phaser.Tilemaps.Tileset => Boolean(tileset));

    if (tilesets.length === 0) {
      this.showDebugText('No se han encontrado tilesets. Revisa nombres en Tiled.');
      return;
    }

    const groundLayer = map.createLayer('Ground', tilesets, 0, 0);
    const vegetationLayer = map.createLayer('Vegetacion', tilesets, 0, 0);

    if (!groundLayer) {
      this.showDebugText('No existe la capa Ground en el mapa.');
      return;
    }

    if (!vegetationLayer) {
      this.showDebugText('No existe la capa Vegetacion en el mapa.');
      return;
    }

    this.configureTileCollisions(vegetationLayer);

    this.physics.world.setBounds(0, 0, map.widthInPixels, map.heightInPixels);
    this.cameras.main.setBounds(0, 0, map.widthInPixels, map.heightInPixels);
    this.cameras.main.setZoom(1);

    this.player = this.add.rectangle(120, 120, 24, 24, 0x3b82f6);
    this.physics.add.existing(this.player);

    const playerBody = this.player.body as Phaser.Physics.Arcade.Body;
    playerBody.setCollideWorldBounds(true);
    playerBody.setSize(24, 24);

    this.physics.add.collider(this.player, vegetationLayer);

    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);

    this.cursors = this.input.keyboard?.createCursorKeys();
    this.wasd = this.input.keyboard?.addKeys('W,A,S,D') as Record<
      string,
      Phaser.Input.Keyboard.Key
    >;

    this.add
      .text(12, 12, 'WASD / Flechas para moverte', {
        fontFamily: 'monospace',
        fontSize: '14px',
        color: '#ffffff',
        backgroundColor: '#00000088',
        padding: { x: 8, y: 6 },
      })
      .setScrollFactor(0)
      .setDepth(1000);
  }

  update() {
    if (!this.player) return;

    const body = this.player.body as Phaser.Physics.Arcade.Body;
    const cursors = this.cursors;
    const wasd = this.wasd;

    if (!body || !cursors || !wasd) return;

    let velocityX = 0;
    let velocityY = 0;

    if (cursors.left.isDown || wasd.A?.isDown) {
      velocityX -= 1;
    }

    if (cursors.right.isDown || wasd.D?.isDown) {
      velocityX += 1;
    }

    if (cursors.up.isDown || wasd.W?.isDown) {
      velocityY -= 1;
    }

    if (cursors.down.isDown || wasd.S?.isDown) {
      velocityY += 1;
    }

    const direction = new Phaser.Math.Vector2(velocityX, velocityY);

    if (direction.lengthSq() > 0) {
      direction.normalize().scale(PLAYER_SPEED);
    }

    body.setVelocity(direction.x, direction.y);
  }

  private configureTileCollisions(layer: Phaser.Tilemaps.TilemapLayer) {
    layer.forEachTile((tile) => {
      const colliderProperty = tile.properties?.collider;
      const collidesProperty = tile.properties?.collides;
      const tileClass = tile.properties?.class ?? tile.properties?.Class;

      const shouldCollide =
        colliderProperty === true ||
        colliderProperty === 'true' ||
        colliderProperty === 'On' ||
        colliderProperty === 'on' ||
        collidesProperty === true ||
        tileClass === 'solid' ||
        tileClass === 'Solid';

      if (shouldCollide) {
        tile.setCollision(true, true, true, true);
      }
    });
  }

  private showDebugText(message: string) {
    this.add.text(24, 24, message, {
      fontFamily: 'monospace',
      fontSize: '18px',
      color: '#ffb4b4',
      backgroundColor: '#000000aa',
      padding: { x: 12, y: 8 },
    });
  }
}
