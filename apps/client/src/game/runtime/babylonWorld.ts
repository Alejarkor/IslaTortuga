import {
  ArcRotateCamera,
  Color3,
  Color4,
  DynamicTexture,
  Engine,
  HemisphericLight,
  Mesh,
  MeshBuilder,
  Scene,
  StandardMaterial,
  Texture,
  Vector3,
} from '@babylonjs/core';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { PayloadBuilder } from './payloadBuilder';
import {
  GameNetworkClient,
  type AuthAcceptedPayload,
  type EntityStatePayload,
  type WorldSnapshotPayload,
} from './networkClient';

const PLAYER_MOVE_SPEED = 4.7;
const LOCAL_RECONCILE_FACTOR = 0.22;
const REMOTE_INTERPOLATION_FACTOR = 0.18;
const SNAP_DISTANCE_THRESHOLD = 1.15;
const WALK_ANIMATION_FPS = 6;
const TILE_FLIP_MASK = 0xe0000000;

type RuntimeCallbacks = {
  onStatusChange: (message: string) => void;
  onError: (message: string) => void;
};

type TiledMap = {
  width: number;
  height: number;
  tilewidth: number;
  tileheight: number;
  orientation: string;
  layers: TiledLayer[];
  tilesets: TiledTileset[];
};

type TiledLayer = TiledTileLayer | TiledObjectLayer;

type TiledTileLayer = {
  type: 'tilelayer';
  name: string;
  data: number[];
  width: number;
  height: number;
  visible?: boolean;
  opacity?: number;
};

type TiledObjectLayer = {
  type: 'objectgroup';
  name: string;
  objects: Array<{
    x: number;
    y: number;
  }>;
};

type TiledTileset = {
  firstgid: number;
  columns: number;
  image?: string;
  imageheight: number;
  imagewidth: number;
  margin?: number;
  name: string;
  spacing?: number;
  tilecount: number;
  tileheight: number;
  tilewidth: number;
};

type TiledTilesetWithImage = TiledTileset & {
  imageElement: HTMLImageElement;
};

type PlayerVisual = {
  id: string;
  plane: Mesh;
  texture: DynamicTexture;
  material: StandardMaterial;
  currentX: number;
  currentZ: number;
  targetX: number;
  targetZ: number;
  facing: string;
  isLocal: boolean;
  lastFrameKey: string;
  flipX: boolean;
  walkAccumulator: number;
};

export class BabylonWorld {
  private readonly keyState: Record<string, boolean> = {};
  private readonly playerVisuals = new Map<string, PlayerVisual>();
  private readonly canvas: HTMLCanvasElement;
  private readonly runtime: GameRuntime;
  private readonly callbacks: RuntimeCallbacks;

  private camera?: ArcRotateCamera;
  private engine?: Engine;
  private localPlayerEntityId?: string;
  private mapDefinition?: TiledMap;
  private networkClient?: GameNetworkClient;
  private playerImage?: HTMLImageElement;
  private scene?: Scene;
  private lastFrameTime = 0;
  private lastSentInput = '0:0';

  constructor(canvas: HTMLCanvasElement, runtime: GameRuntime, callbacks: RuntimeCallbacks) {
    this.canvas = canvas;
    this.runtime = runtime;
    this.callbacks = callbacks;
  }

  async initialize() {
    this.engine = new Engine(this.canvas, true, {
      adaptToDeviceRatio: true,
      antialias: true,
    });

    this.scene = new Scene(this.engine);
    this.scene.clearColor = new Color4(0.05, 0.08, 0.05, 1);

    await this.buildScene();
    this.attachInputHandlers();
    this.connectToGameServer(
      this.runtime.startGame.webSocketUrl,
      this.runtime.startGame.gameTicket,
    );

    this.lastFrameTime = performance.now();
    this.engine.runRenderLoop(() => this.renderFrame());
    window.addEventListener('resize', this.handleResize);
  }

  dispose() {
    window.removeEventListener('resize', this.handleResize);
    window.removeEventListener('keydown', this.handleKeyDown);
    window.removeEventListener('keyup', this.handleKeyUp);

    this.networkClient?.close();
    this.networkClient = undefined;

    this.scene?.dispose();
    this.scene = undefined;

    this.engine?.dispose();
    this.engine = undefined;
  }

  private async buildScene() {
    if (!this.scene) {
      throw new Error('La escena Babylon no esta disponible.');
    }

    const mapVisual = this.runtime.catalog.maps[this.runtime.startGame.mapId];

    if (!mapVisual) {
      throw new Error(`No existe definicion visual para el mapa ${this.runtime.startGame.mapId}.`);
    }

    const mapFile = this.runtime.catalog.resolveFile(mapVisual.mapFileId);
    const playerFile = this.runtime.catalog.resolveFile(
      this.runtime.catalog.players.default.imageFileId,
    );

    const [mapDefinition, playerImage] = await Promise.all([
      this.fetchJson<TiledMap>(mapFile.url),
      loadImage(playerFile.url),
    ]);

    if (mapDefinition.orientation !== 'orthogonal') {
      throw new Error('Solo se soportan mapas Tiled ortogonales en el runtime Babylon inicial.');
    }

    this.mapDefinition = mapDefinition;
    this.playerImage = playerImage;

    const tilesets = await Promise.all(
      mapDefinition.tilesets.map(async (tileset) => ({
        ...tileset,
        imageElement: await loadImage(this.resolveTilesetUrl(tileset.name, tileset.image, mapFile.url)),
      })),
    );

    this.createCameraAndLighting(mapDefinition);
    this.createMapPlanes(mapDefinition, tilesets);
    this.callbacks.onStatusChange(
      `Mapa ${this.runtime.startGame.mapId} listo en Babylon. Conectando al game server...`,
    );
  }

  private createCameraAndLighting(mapDefinition: TiledMap) {
    if (!this.scene) {
      return;
    }

    const center = new Vector3(mapDefinition.width / 2, 0.35, mapDefinition.height / 2);
    const radius = Math.max(mapDefinition.width, mapDefinition.height) * 0.95;
    const camera = new ArcRotateCamera(
      'world-camera',
      -Math.PI / 2,
      1.05,
      radius,
      center,
      this.scene,
    );

    camera.attachControl(this.canvas, true);
    camera.lowerRadiusLimit = radius * 0.65;
    camera.upperRadiusLimit = radius * 1.4;
    camera.lowerBetaLimit = 0.65;
    camera.upperBetaLimit = 1.25;
    camera.panningSensibility = 0;
    camera.wheelPrecision = 32;

    this.camera = camera;

    const skyLight = new HemisphericLight('sky-light', new Vector3(0.25, 1, -0.35), this.scene);
    skyLight.intensity = 1.05;
    skyLight.diffuse = new Color3(0.95, 0.95, 0.9);
    skyLight.groundColor = new Color3(0.25, 0.28, 0.22);
  }

  private createMapPlanes(mapDefinition: TiledMap, tilesets: TiledTilesetWithImage[]) {
    if (!this.scene) {
      return;
    }

    const { baseCanvas, overlayCanvas } = rasterizeMapLayers(mapDefinition, tilesets);
    const mapWidth = mapDefinition.width;
    const mapHeight = mapDefinition.height;

    const baseTexture = new DynamicTexture(
      'map-base-texture',
      baseCanvas,
      this.scene,
      false,
      Texture.NEAREST_SAMPLINGMODE,
    );
    baseTexture.hasAlpha = true;

    const baseMaterial = new StandardMaterial('map-base-material', this.scene);
    baseMaterial.diffuseTexture = baseTexture;
    baseMaterial.opacityTexture = baseTexture;
    baseMaterial.disableLighting = true;
    baseMaterial.emissiveColor = Color3.White();

    const ground = MeshBuilder.CreateGround(
      'map-ground',
      {
        width: mapWidth,
        height: mapHeight,
      },
      this.scene,
    );
    ground.position = new Vector3(mapWidth / 2, 0, mapHeight / 2);
    ground.material = baseMaterial;

    if (!isCanvasTransparent(overlayCanvas)) {
      const overlayTexture = new DynamicTexture(
        'map-overlay-texture',
        overlayCanvas,
        this.scene,
        false,
        Texture.NEAREST_SAMPLINGMODE,
      );
      overlayTexture.hasAlpha = true;

      const overlayMaterial = new StandardMaterial('map-overlay-material', this.scene);
      overlayMaterial.diffuseTexture = overlayTexture;
      overlayMaterial.opacityTexture = overlayTexture;
      overlayMaterial.disableLighting = true;
      overlayMaterial.emissiveColor = Color3.White();

      const overlay = MeshBuilder.CreateGround(
        'map-overlay',
        {
          width: mapWidth,
          height: mapHeight,
        },
        this.scene,
      );
      overlay.position = new Vector3(mapWidth / 2, 1.12, mapHeight / 2);
      overlay.material = overlayMaterial;
    }
  }

  private attachInputHandlers() {
    window.addEventListener('keydown', this.handleKeyDown);
    window.addEventListener('keyup', this.handleKeyUp);
  }

  private connectToGameServer(webSocketUrl: string, gameTicket: string) {
    this.networkClient = new GameNetworkClient(webSocketUrl, {
      onAuthAccepted: (payload) => this.handleAuthAccepted(payload),
      onSnapshot: (payload) => this.applySnapshot(payload),
      onError: (payload) => {
        this.callbacks.onError(`Game server error: ${payload.message}`);
      },
      onClose: () => {
        this.callbacks.onStatusChange('Conexion al game server cerrada.');
      },
    });

    this.networkClient
      .waitUntilOpen()
      .then(() => {
        this.networkClient?.sendJoin(PayloadBuilder.joinGame(gameTicket));
      })
      .catch((error) => {
        this.callbacks.onError(
          error instanceof Error ? error.message : 'No se pudo abrir el game socket.',
        );
      });
  }

  private handleAuthAccepted(payload: AuthAcceptedPayload) {
    this.localPlayerEntityId = payload.playerEntityId;
    const playerVisual = this.playerVisuals.get(payload.playerEntityId);

    if (playerVisual) {
      playerVisual.isLocal = true;
    }

    this.callbacks.onStatusChange(`Conectado a ${payload.roomId}. Esperando snapshots...`);
  }

  private applySnapshot(snapshot: WorldSnapshotPayload) {
    const activeIds = new Set<string>();

    for (const entity of snapshot.entities) {
      if (entity.entityType !== 'player') {
        continue;
      }

      activeIds.add(entity.entityId);
      const visual = this.ensurePlayerVisual(entity);
      const worldPosition = this.toWorldPosition(entity.x, entity.y);

      visual.targetX = worldPosition.x;
      visual.targetZ = worldPosition.z;
      visual.facing = entity.facing;
    }

    for (const [playerId, visual] of this.playerVisuals.entries()) {
      if (activeIds.has(playerId)) {
        continue;
      }

      visual.plane.dispose();
      visual.material.dispose();
      visual.texture.dispose();
      this.playerVisuals.delete(playerId);
    }

    this.callbacks.onStatusChange(`Snapshot ${snapshot.serverTick} recibido desde ${snapshot.roomId}.`);
  }

  private ensurePlayerVisual(entity: EntityStatePayload) {
    const existing = this.playerVisuals.get(entity.entityId);

    if (existing) {
      if (entity.entityId === this.localPlayerEntityId) {
        existing.isLocal = true;
      }

      return existing;
    }

    if (!this.scene || !this.mapDefinition || !this.playerImage) {
      throw new Error('La escena no esta lista para instanciar jugadores.');
    }

    const playerDefinition = this.runtime.catalog.players.default;
    const position = this.toWorldPosition(entity.x, entity.y);
    const planeWidth = playerDefinition.frameWidth / this.mapDefinition.tilewidth;
    const planeHeight = playerDefinition.frameHeight / this.mapDefinition.tileheight;
    const texture = new DynamicTexture(
      `${entity.entityId}-player-texture`,
      {
        width: playerDefinition.frameWidth,
        height: playerDefinition.frameHeight,
      },
      this.scene,
      false,
      Texture.NEAREST_SAMPLINGMODE,
    );
    texture.hasAlpha = true;
    texture.wrapU = Texture.CLAMP_ADDRESSMODE;
    texture.wrapV = Texture.CLAMP_ADDRESSMODE;

    const material = new StandardMaterial(`${entity.entityId}-player-material`, this.scene);
    material.diffuseTexture = texture;
    material.opacityTexture = texture;
    material.disableLighting = true;
    material.emissiveColor = Color3.White();
    material.backFaceCulling = false;

    const plane = MeshBuilder.CreatePlane(
      `${entity.entityId}-player`,
      {
        width: planeWidth,
        height: planeHeight,
      },
      this.scene,
    );
    plane.billboardMode = Mesh.BILLBOARDMODE_Y;
    plane.material = material;
    plane.position = new Vector3(position.x, planeHeight * 0.5, position.z);

    const visual: PlayerVisual = {
      id: entity.entityId,
      plane,
      texture,
      material,
      currentX: position.x,
      currentZ: position.z,
      targetX: position.x,
      targetZ: position.z,
      facing: entity.facing,
      isLocal: entity.entityId === this.localPlayerEntityId,
      lastFrameKey: '',
      flipX: false,
      walkAccumulator: 0,
    };

    this.playerVisuals.set(entity.entityId, visual);
    this.renderPlayerFrame(visual, false);
    return visual;
  }

  private renderFrame() {
    if (!this.scene) {
      return;
    }

    const now = performance.now();
    const deltaSeconds = Math.min((now - this.lastFrameTime) / 1000, 0.05);
    this.lastFrameTime = now;

    this.updateLocalInput(deltaSeconds);
    this.interpolatePlayers(deltaSeconds);
    this.scene.render();
  }

  private updateLocalInput(deltaSeconds: number) {
    const localPlayer = this.localPlayerEntityId
      ? this.playerVisuals.get(this.localPlayerEntityId)
      : undefined;

    const moveX =
      (this.isKeyDown('ArrowRight') || this.isKeyDown('KeyD') ? 1 : 0) -
      (this.isKeyDown('ArrowLeft') || this.isKeyDown('KeyA') ? 1 : 0);
    const moveY =
      (this.isKeyDown('ArrowDown') || this.isKeyDown('KeyS') ? 1 : 0) -
      (this.isKeyDown('ArrowUp') || this.isKeyDown('KeyW') ? 1 : 0);

    const inputSignature = `${moveX}:${moveY}`;
    if (this.networkClient && inputSignature !== this.lastSentInput) {
      this.networkClient.sendPlayerInput(PayloadBuilder.playerInput(moveX, moveY));
      this.lastSentInput = inputSignature;
    }

    if (!localPlayer) {
      return;
    }

    const magnitude = Math.hypot(moveX, moveY);
    let moving = false;

    if (magnitude > 0) {
      const normalizedX = moveX / magnitude;
      const normalizedY = moveY / magnitude;
      localPlayer.currentX += normalizedX * PLAYER_MOVE_SPEED * deltaSeconds;
      localPlayer.currentZ += -normalizedY * PLAYER_MOVE_SPEED * deltaSeconds;
      localPlayer.facing = resolveFacingFromInput(moveX, moveY, localPlayer.facing);
      moving = true;
    }

    const distanceToTarget = distanceBetween(
      localPlayer.currentX,
      localPlayer.currentZ,
      localPlayer.targetX,
      localPlayer.targetZ,
    );

    if (distanceToTarget > SNAP_DISTANCE_THRESHOLD) {
      localPlayer.currentX = localPlayer.targetX;
      localPlayer.currentZ = localPlayer.targetZ;
    } else {
      localPlayer.currentX = lerp(localPlayer.currentX, localPlayer.targetX, LOCAL_RECONCILE_FACTOR);
      localPlayer.currentZ = lerp(localPlayer.currentZ, localPlayer.targetZ, LOCAL_RECONCILE_FACTOR);
    }

    localPlayer.plane.position.x = localPlayer.currentX;
    localPlayer.plane.position.z = localPlayer.currentZ;
    this.renderPlayerFrame(localPlayer, moving);
  }

  private interpolatePlayers(deltaSeconds: number) {
    for (const visual of this.playerVisuals.values()) {
      if (visual.isLocal) {
        continue;
      }

      visual.currentX = lerp(visual.currentX, visual.targetX, REMOTE_INTERPOLATION_FACTOR);
      visual.currentZ = lerp(visual.currentZ, visual.targetZ, REMOTE_INTERPOLATION_FACTOR);
      visual.plane.position.x = visual.currentX;
      visual.plane.position.z = visual.currentZ;

      const moving =
        distanceBetween(visual.currentX, visual.currentZ, visual.targetX, visual.targetZ) > 0.04;
      visual.walkAccumulator += deltaSeconds;
      this.renderPlayerFrame(visual, moving);
    }
  }

  private renderPlayerFrame(visual: PlayerVisual, moving: boolean) {
    if (!this.playerImage) {
      return;
    }

    const playerDefinition = this.runtime.catalog.players.default;
    const frame = getAnimationFrameIndex(visual.facing, moving, visual.walkAccumulator);
    const flipX = visual.facing === 'left';
    const frameKey = `${frame}:${flipX ? '1' : '0'}:${moving ? '1' : '0'}`;

    if (frameKey === visual.lastFrameKey) {
      return;
    }

    const context = visual.texture.getContext();
    context.clearRect(0, 0, playerDefinition.frameWidth, playerDefinition.frameHeight);

    if (flipX) {
      context.save();
      context.translate(playerDefinition.frameWidth, 0);
      context.scale(-1, 1);
    }

    context.drawImage(
      this.playerImage,
      frame * playerDefinition.frameWidth,
      0,
      playerDefinition.frameWidth,
      playerDefinition.frameHeight,
      0,
      0,
      playerDefinition.frameWidth,
      playerDefinition.frameHeight,
    );

    if (flipX) {
      context.restore();
    }

    visual.texture.update(false);
    visual.flipX = flipX;
    visual.lastFrameKey = frameKey;
  }

  private toWorldPosition(x: number, y: number) {
    if (!this.mapDefinition) {
      return { x: 0, z: 0 };
    }

    return {
      x: x / this.mapDefinition.tilewidth,
      z: this.mapDefinition.height - y / this.mapDefinition.tileheight,
    };
  }

  private resolveTilesetUrl(tilesetName: string, imagePath: string | undefined, mapUrl: string) {
    const mapVisual = this.runtime.catalog.maps[this.runtime.startGame.mapId];
    const contentTileset = mapVisual?.tilesets.find((entry) => entry.tilesetName === tilesetName);

    if (contentTileset) {
      return this.runtime.catalog.resolveFile(contentTileset.imageFileId).url;
    }

    if (!imagePath) {
      throw new Error(`No se pudo resolver la imagen del tileset ${tilesetName}.`);
    }

    return resolveRelativeUrl(imagePath, mapUrl);
  }

  private isKeyDown(code: string) {
    return this.keyState[code] === true;
  }

  private fetchJson<TResponse>(url: string) {
    return fetch(url).then(async (response) => {
      if (!response.ok) {
        throw new Error(`No se pudo cargar ${url}.`);
      }

      return (await response.json()) as TResponse;
    });
  }

  private readonly handleResize = () => {
    this.engine?.resize();
  };

  private readonly handleKeyDown = (event: KeyboardEvent) => {
    this.keyState[event.code] = true;
  };

  private readonly handleKeyUp = (event: KeyboardEvent) => {
    this.keyState[event.code] = false;
  };
}

function rasterizeMapLayers(map: TiledMap, tilesets: TiledTilesetWithImage[]) {
  const baseCanvas = document.createElement('canvas');
  baseCanvas.width = map.width * map.tilewidth;
  baseCanvas.height = map.height * map.tileheight;

  const overlayCanvas = document.createElement('canvas');
  overlayCanvas.width = baseCanvas.width;
  overlayCanvas.height = baseCanvas.height;

  const baseContext = baseCanvas.getContext('2d');
  const overlayContext = overlayCanvas.getContext('2d');

  if (!baseContext || !overlayContext) {
    throw new Error('No se pudo crear el contexto 2D para rasterizar el mapa.');
  }

  for (const layer of map.layers) {
    if (layer.type !== 'tilelayer' || layer.visible === false) {
      continue;
    }

    const targetContext = isOverlayLayer(layer.name) ? overlayContext : baseContext;
    const layerOpacity = layer.opacity ?? 1;
    targetContext.save();
    targetContext.globalAlpha = layerOpacity;

    for (let tileY = 0; tileY < layer.height; tileY += 1) {
      for (let tileX = 0; tileX < layer.width; tileX += 1) {
        const tileIndex = tileY * layer.width + tileX;
        const rawGid = layer.data[tileIndex] ?? 0;
        const gid = rawGid & ~TILE_FLIP_MASK;

        if (gid === 0) {
          continue;
        }

        const tileset = findTilesetForGid(tilesets, gid);
        if (!tileset) {
          continue;
        }

        const localTileId = gid - tileset.firstgid;
        const sourceX =
          (tileset.margin ?? 0) +
          (localTileId % tileset.columns) * (tileset.tilewidth + (tileset.spacing ?? 0));
        const sourceY =
          (tileset.margin ?? 0) +
          Math.floor(localTileId / tileset.columns) *
            (tileset.tileheight + (tileset.spacing ?? 0));

        targetContext.drawImage(
          tileset.imageElement,
          sourceX,
          sourceY,
          tileset.tilewidth,
          tileset.tileheight,
          tileX * map.tilewidth,
          tileY * map.tileheight,
          map.tilewidth,
          map.tileheight,
        );
      }
    }

    targetContext.restore();
  }

  return {
    baseCanvas,
    overlayCanvas,
  };
}

function isOverlayLayer(layerName: string) {
  return layerName.toLowerCase().includes('aboveplayer');
}

function findTilesetForGid(tilesets: TiledTilesetWithImage[], gid: number) {
  for (let index = tilesets.length - 1; index >= 0; index -= 1) {
    const tileset = tilesets[index];

    if (gid >= tileset.firstgid) {
      return tileset;
    }
  }

  return undefined;
}

function resolveFacingFromInput(moveX: number, moveY: number, previousFacing: string) {
  if (moveX === 0 && moveY === 0) {
    return previousFacing;
  }

  if (Math.abs(moveX) > Math.abs(moveY)) {
    return moveX < 0 ? 'left' : 'right';
  }

  return moveY < 0 ? 'up' : 'down';
}

function getAnimationFrameIndex(facing: string, moving: boolean, walkAccumulator: number) {
  if (!moving) {
    if (facing === 'up') {
      return 1;
    }

    if (facing === 'left' || facing === 'right') {
      return 2;
    }

    return 0;
  }

  const walkFrame = Math.floor(walkAccumulator * WALK_ANIMATION_FPS) % 2;

  if (facing === 'left' || facing === 'right') {
    return walkFrame === 0 ? 2 : 3;
  }

  if (facing === 'up') {
    return walkFrame === 0 ? 1 : 0;
  }

  return walkFrame === 0 ? 0 : 1;
}

function isCanvasTransparent(canvas: HTMLCanvasElement) {
  const context = canvas.getContext('2d');

  if (!context) {
    return true;
  }

  const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;

  for (let index = 3; index < pixels.length; index += 4) {
    if (pixels[index] !== 0) {
      return false;
    }
  }

  return true;
}

function distanceBetween(ax: number, az: number, bx: number, bz: number) {
  return Math.hypot(bx - ax, bz - az);
}

function lerp(start: number, end: number, factor: number) {
  return start + (end - start) * factor;
}

function resolveRelativeUrl(path: string, baseUrl: string) {
  return new URL(path, new URL(baseUrl, window.location.href)).toString();
}

function loadImage(url: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error(`No se pudo cargar la imagen ${url}.`));
    image.src = url;
  });
}
