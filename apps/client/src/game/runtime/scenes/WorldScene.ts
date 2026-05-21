import Phaser from 'phaser';
import { getCurrentGameRuntime } from '../../bootstrap/gameRuntimeRegistry';
import { PayloadBuilder } from '../payloadBuilder';
import {
  GameNetworkClient,
  type EntityStatePayload,
  type WorldSnapshotPayload,
} from '../networkClient';
import { loadWorldAssets } from '../phaserAssetLoader';

const MAP_KEY = 'test-map';
const PLAYER_SPEED = 150;
const PLAYER_KEY = 'player';

export class WorldScene extends Phaser.Scene {
  private lastSentInput = '0:0';
  private localPlayerEntityId?: string;
  private networkClient?: GameNetworkClient;
  private networkPlayers = new Map<string, Phaser.GameObjects.Sprite>();
  private playerTargets = new Map<string, EntityStatePayload>();
  private player?: Phaser.Physics.Arcade.Sprite;
  private cursors?: Phaser.Types.Input.Keyboard.CursorKeys;
  private infoText?: Phaser.GameObjects.Text;
  private trunksLayer?: Phaser.Tilemaps.TilemapLayer;
  private wasd?: Record<string, Phaser.Input.Keyboard.Key>;

  constructor() {
    super('WorldScene');
  }

  preload() {
    const runtime = getCurrentGameRuntime();
    if (!runtime) {
      return;
    }

    loadWorldAssets(this, runtime, MAP_KEY, PLAYER_KEY);
  }

  create() {
    const runtime = getCurrentGameRuntime();
    if (!runtime) {
      this.showDebugText('No hay runtime de contenido cargado.');
      return;
    }

    const map = this.make.tilemap({ key: MAP_KEY });
    const mapDefinition = runtime.catalog.maps[runtime.startGame.mapId];

    if (!mapDefinition) {
      this.showDebugText(`No existe definicion visual para el mapa ${runtime.startGame.mapId}.`);
      return;
    }

    const tilesets = [
      ...mapDefinition.tilesets.map((tileset) =>
        map.addTilesetImage(tileset.tilesetName, tileset.textureKey),
      ),
    ].filter((tileset): tileset is Phaser.Tilemaps.Tileset => Boolean(tileset));

    if (tilesets.length === 0) {
      this.showDebugText('No se han encontrado tilesets. Revisa nombres en Tiled.');
      return;
    }

    const warnings: string[] = [];
    const groundLayer = this.createGroundLayer(map, tilesets);
    const trunksLayer = this.createOptionalLayer(map, tilesets, ['Trunks', 'Truncks']);
    const abovePlayerLayer = this.createOptionalLayer(map, tilesets, ['AbovePlayer']);

    if (!groundLayer) {
      this.showDebugText('El mapa no contiene ninguna tile layer renderizable.');
      return;
    }

    if (!trunksLayer) {
      warnings.push('Sin capa Trunks/Truncks: no habra colision de troncos.');
    }

    if (!abovePlayerLayer) {
      warnings.push('Sin capa AbovePlayer: no habra ocultacion tras copas.');
    }

    groundLayer.setDepth(0);
    trunksLayer?.setDepth(10);
    abovePlayerLayer?.setDepth(1000);
    this.trunksLayer = this.asArcadeLayer(trunksLayer);

    if (this.trunksLayer) {
      this.configureTileCollisions(this.trunksLayer);
    }
    this.createPlayerAnimations();

    this.physics.world.setBounds(0, 0, map.widthInPixels, map.heightInPixels);
    this.cameras.main.setBounds(0, 0, map.widthInPixels, map.heightInPixels);
    this.cameras.main.setZoom(1);

    this.cursors = this.input.keyboard?.createCursorKeys();
    this.wasd = this.input.keyboard?.addKeys('W,A,S,D') as Record<
      string,
      Phaser.Input.Keyboard.Key
    >;

    this.infoText = this.add
      .text(
        12,
        12,
        this.buildInitialStatusMessage(runtime.manifest.contentPackId, warnings),
        {
          fontFamily: 'monospace',
          fontSize: '14px',
          color: '#ffffff',
          backgroundColor: '#00000088',
          padding: { x: 8, y: 6 },
        },
      )
      .setScrollFactor(0)
      .setDepth(1000);

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      this.networkClient?.close();
      this.networkClient = undefined;
    });

    this.connectToGameServer(runtime.startGame.webSocketUrl, runtime.startGame.gameTicket);
  }

  update() {
    this.updateLocalPlayerInput();
    this.interpolateNetworkPlayers();
  }

  private updateLocalPlayerInput() {
    if (!this.player || !this.networkClient) return;

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

    const inputSignature = `${velocityX}:${velocityY}`;
    if (inputSignature !== this.lastSentInput) {
      this.networkClient.sendPlayerInput(PayloadBuilder.playerInput(velocityX, velocityY));
      this.lastSentInput = inputSignature;
    }

    const serverState = this.localPlayerEntityId
      ? this.playerTargets.get(this.localPlayerEntityId)
      : undefined;

    if (serverState) {
      const distance = Phaser.Math.Distance.Between(
        this.player.x,
        this.player.y,
        serverState.x,
        serverState.y,
      );

      if (distance > 24) {
        this.player.setPosition(serverState.x, serverState.y);
      } else {
        this.player.x = Phaser.Math.Linear(this.player.x, serverState.x, 0.25);
        this.player.y = Phaser.Math.Linear(this.player.y, serverState.y, 0.25);
      }
    }

    this.player.setDepth(this.player.y);
    this.applyAnimationState(this.player, direction.x, direction.y);
  }

  private createPlayerAnimations() {
    const animations = [
      { key: 'player-idle-down', frames: [0], frameRate: 1, repeat: -1 },
      { key: 'player-idle-up', frames: [1], frameRate: 1, repeat: -1 },
      { key: 'player-idle-side', frames: [2], frameRate: 1, repeat: -1 },
      { key: 'player-walk-down', frames: [0, 1], frameRate: 6, repeat: -1 },
      { key: 'player-walk-up', frames: [1, 0], frameRate: 6, repeat: -1 },
      { key: 'player-walk-side', frames: [2, 3], frameRate: 6, repeat: -1 },
    ];

    for (const animation of animations) {
      if (this.anims.exists(animation.key)) {
        continue;
      }

      this.anims.create({
        key: animation.key,
        frames: this.anims.generateFrameNumbers(PLAYER_KEY, {
          frames: animation.frames,
        }),
        frameRate: animation.frameRate,
        repeat: animation.repeat,
      });
    }
  }

  private applyAnimationState(
    sprite: Phaser.GameObjects.Sprite,
    velocityX: number,
    velocityY: number,
  ) {
    if (velocityX === 0 && velocityY === 0) {
      const currentKey = sprite.anims.currentAnim?.key;

      if (currentKey === 'player-walk-up') {
        sprite.play('player-idle-up', true);
        return;
      }

      if (currentKey === 'player-walk-side') {
        sprite.play('player-idle-side', true);
        return;
      }

      sprite.play('player-idle-down', true);
      return;
    }

    if (Math.abs(velocityX) > Math.abs(velocityY)) {
      sprite.setFlipX(velocityX < 0);
      sprite.play('player-walk-side', true);
      return;
    }

    sprite.setFlipX(false);
    sprite.play(velocityY < 0 ? 'player-walk-up' : 'player-walk-down', true);
  }

  private configureTileCollisions(layer: Phaser.Tilemaps.TilemapLayer) {
    layer.setCollisionByProperty({ Collider: true });
    layer.setCollisionByProperty({ collider: true });
    layer.setCollisionByProperty({ collides: true });
    layer.setCollisionFromCollisionGroup();
  }

  private createGroundLayer(
    map: Phaser.Tilemaps.Tilemap,
    tilesets: Phaser.Tilemaps.Tileset[],
  ) {
    const preferredLayer = this.createOptionalLayer(map, tilesets, ['Ground']);
    if (preferredLayer) {
      return preferredLayer;
    }

    for (const layerData of map.layers) {
      const layer = map.createLayer(layerData.name, tilesets, 0, 0);
      if (layer) {
        return layer;
      }
    }

    return null;
  }

  private createOptionalLayer(
    map: Phaser.Tilemaps.Tilemap,
    tilesets: Phaser.Tilemaps.Tileset[],
    layerNames: string[],
  ) {
    for (const layerName of layerNames) {
      const layer = map.createLayer(layerName, tilesets, 0, 0);
      if (layer) {
        return layer;
      }
    }

    return null;
  }

  private buildInitialStatusMessage(contentPackId: string, warnings: string[]) {
    if (warnings.length === 0) {
      return `Content pack listo: ${contentPackId}. Conectando a game...`;
    }

    return [
      `Content pack listo: ${contentPackId}. Conectando a game...`,
      ...warnings,
    ].join('\n');
  }

  private asArcadeLayer(
    layer: Phaser.Tilemaps.TilemapLayer | Phaser.Tilemaps.TilemapGPULayer | null,
  ) {
    if (!layer || !('setCollisionByProperty' in layer)) {
      return undefined;
    }

    return layer as Phaser.Tilemaps.TilemapLayer;
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

  private updateInfoText(message: string) {
    this.infoText?.setText(message);
  }

  private connectToGameServer(webSocketUrl: string, gameTicket: string) {
    this.networkClient = new GameNetworkClient(webSocketUrl, {
      onAuthAccepted: (payload) => {
        this.localPlayerEntityId = payload.playerEntityId;
        this.updateInfoText(`Conectado a ${payload.roomId}. Esperando snapshot...`);
      },
      onSnapshot: (payload) => {
        this.applySnapshot(payload);
      },
      onError: (payload) => {
        this.updateInfoText(`Game server error: ${payload.message}`);
      },
      onClose: () => {
        this.updateInfoText('Conexion al game server cerrada.');
      },
    });

    this.networkClient
      .waitUntilOpen()
      .then(() => {
        this.networkClient?.sendJoin(PayloadBuilder.joinGame(gameTicket));
      })
      .catch((error) => {
        this.updateInfoText(error instanceof Error ? error.message : 'No se pudo abrir el game socket.');
      });
  }

  private applySnapshot(snapshot: WorldSnapshotPayload) {
    const activeIds = new Set(snapshot.entities.map((entity) => entity.entityId));

    for (const entityState of snapshot.entities) {
      if (entityState.entityType !== 'player') {
        continue;
      }

      this.playerTargets.set(entityState.entityId, entityState);

      if (entityState.entityId === this.localPlayerEntityId) {
        this.ensureLocalPlayer(entityState);
        continue;
      }

      if (!this.networkPlayers.has(entityState.entityId)) {
        const sprite = this.add.sprite(entityState.x, entityState.y, PLAYER_KEY, 0);
        sprite.setOrigin(0.5, 1);
        this.networkPlayers.set(entityState.entityId, sprite);
      }
    }

    for (const [playerId, sprite] of this.networkPlayers.entries()) {
      if (activeIds.has(playerId)) {
        continue;
      }

      sprite.destroy();
      this.networkPlayers.delete(playerId);
      this.playerTargets.delete(playerId);
    }

    if (this.localPlayerEntityId) {
      this.updateInfoText(`Snapshot ${snapshot.serverTick} recibido.`);
    }
  }

  private ensureLocalPlayer(state: EntityStatePayload) {
    if (this.player) {
      return;
    }

    const runtime = getCurrentGameRuntime();
    if (!runtime) {
      return;
    }

    this.player = this.physics.add.sprite(state.x, state.y, PLAYER_KEY, 0);
    this.player.setCollideWorldBounds(true);
    this.player.setOrigin(0.5, 1);
    this.player.play(runtime.catalog.players.default.animations.idleDown);

    const playerBody = this.player.body as Phaser.Physics.Arcade.Body;
    playerBody.setCollideWorldBounds(true);
    playerBody.setSize(12, 12);
    playerBody.setOffset(10, 20);

    if (this.trunksLayer) {
      this.physics.add.collider(this.player, this.trunksLayer);
    }

    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
  }

  private interpolateNetworkPlayers() {
    for (const [playerId, sprite] of this.networkPlayers.entries()) {
      const target = this.playerTargets.get(playerId);

      if (!target) {
        continue;
      }

      sprite.x = Phaser.Math.Linear(sprite.x, target.x, 0.25);
      sprite.y = Phaser.Math.Linear(sprite.y, target.y, 0.25);
      sprite.setDepth(sprite.y);
      this.applyFacingState(sprite, target.facing, true);
    }
  }

  private applyFacingState(
    sprite: Phaser.GameObjects.Sprite,
    facing: string,
    moving: boolean,
  ) {
    if (facing === 'left') {
      sprite.setFlipX(true);
      sprite.play(moving ? 'player-walk-side' : 'player-idle-side', true);
      return;
    }

    if (facing === 'right') {
      sprite.setFlipX(false);
      sprite.play(moving ? 'player-walk-side' : 'player-idle-side', true);
      return;
    }

    sprite.setFlipX(false);
    sprite.play(
      facing === 'up'
        ? moving
          ? 'player-walk-up'
          : 'player-idle-up'
        : moving
          ? 'player-walk-down'
          : 'player-idle-down',
      true,
    );
  }
}
