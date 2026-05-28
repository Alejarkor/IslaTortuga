import {
  ArcRotateCamera,
  Color3,
  Color4,
  DirectionalLight,
  DynamicTexture,
  HemisphericLight,
  MeshBuilder,
  Scene,
  StandardMaterial,
  Texture,
  Vector3,
} from '@babylonjs/core';
import type {
  PrimitiveSceneDefinition,
  SceneDefinition,
} from '../content/contentTypes';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { buildPrimitiveAssembly } from './primitiveAssembly';

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

export type BuiltSceneContext = {
  sceneId: string;
  toWorldPosition(x: number, y: number): { x: number; y: number; z: number };
  measureSprite(frameWidth: number, frameHeight: number): { width: number; height: number };
};

export class SceneBuilder {
  constructor(
    private readonly runtime: GameRuntime,
    private readonly canvas: HTMLCanvasElement,
  ) {}

  async build(scene: Scene, sceneId = this.runtime.startGame.sceneId): Promise<BuiltSceneContext> {
    const sceneDefinition = this.runtime.catalog.resolveScene(sceneId);

    if (sceneDefinition.builder === 'primitive-scene') {
      return this.buildPrimitiveScene(scene, sceneDefinition);
    }

    if (sceneDefinition.builder === 'tiled-map') {
      return this.buildTiledScene(scene, sceneDefinition);
    }

    throw new Error(`El builder de escena ${stringifyBuilder(sceneDefinition)} no esta soportado.`);
  }

  private async buildTiledScene(scene: Scene, sceneDefinition: Extract<SceneDefinition, { builder: 'tiled-map' }>) {
    const mapVisual = this.runtime.catalog.mapVisuals[sceneDefinition.mapVisualId];
    if (!mapVisual) {
      throw new Error(`No existe mapVisual ${sceneDefinition.mapVisualId} para la escena ${sceneDefinition.sceneId}.`);
    }

    const mapFile = this.runtime.catalog.resolveFile(mapVisual.mapFileId);
    const mapDefinition = await this.fetchJson<TiledMap>(mapFile.url);

    if (mapDefinition.orientation !== 'orthogonal') {
      throw new Error('Solo se soportan mapas Tiled ortogonales en el runtime Babylon inicial.');
    }

    const tilesets = await Promise.all(
      mapDefinition.tilesets.map(async (tileset) => ({
        ...tileset,
        imageElement: await loadImage(
          this.resolveTilesetUrl(sceneDefinition.sceneId, tileset.name, tileset.image, mapFile.url),
        ),
      })),
    );

    this.createTiledCameraAndLighting(scene, mapDefinition);
    this.createMapPlanes(scene, mapDefinition, tilesets);

    return {
      sceneId: sceneDefinition.sceneId,
      toWorldPosition: (x: number, y: number) => ({
        x: x / mapDefinition.tilewidth,
        y: 0,
        z: mapDefinition.height - y / mapDefinition.tileheight,
      }),
      measureSprite: (frameWidth: number, frameHeight: number) => ({
        width: frameWidth / mapDefinition.tilewidth,
        height: frameHeight / mapDefinition.tileheight,
      }),
    };
  }

  private buildPrimitiveScene(scene: Scene, sceneDefinition: PrimitiveSceneDefinition) {
    const coordinateScale = sceneDefinition.coordinateScale ?? 1;
    const worldWidth = sceneDefinition.worldWidth;
    const worldDepth = sceneDefinition.worldDepth;
    const groundColor = parseColor(sceneDefinition.groundColor, '#4e7a41');
    const skyColor = parseColor(sceneDefinition.skyColor, '#8ab8d8');
    const ambientColor = parseColor(sceneDefinition.ambientColor, '#efe9cf');

    scene.clearColor = new Color4(skyColor.r, skyColor.g, skyColor.b, 1);

    const center = new Vector3(worldWidth / 2, 0, worldDepth / 2);
    const radius =
      sceneDefinition.camera?.radius ?? Math.max(worldWidth, worldDepth) * 1.15;
    const camera = new ArcRotateCamera(
      'world-camera',
      sceneDefinition.camera?.alpha ?? -Math.PI / 2,
      sceneDefinition.camera?.beta ?? 1.05,
      radius,
      new Vector3(
        sceneDefinition.camera?.target?.x ?? center.x,
        sceneDefinition.camera?.target?.y ?? 0.3,
        sceneDefinition.camera?.target?.z ?? center.z,
      ),
      scene,
    );

    camera.attachControl(this.canvas, true);
    camera.lowerRadiusLimit = radius * 0.55;
    camera.upperRadiusLimit = radius * 1.45;
    camera.lowerBetaLimit = 0.55;
    camera.upperBetaLimit = 1.25;
    camera.wheelPrecision = 24;
    camera.panningSensibility = 0;

    const sunLight = new DirectionalLight('sun-light', new Vector3(-0.35, -1, 0.2), scene);
    sunLight.intensity = 1.15;
    sunLight.diffuse = ambientColor;

    const skyLight = new HemisphericLight('sky-light', new Vector3(0.2, 1, -0.1), scene);
    skyLight.intensity = 0.65;
    skyLight.diffuse = ambientColor;
    skyLight.groundColor = groundColor.scale(0.55);

    const groundMaterial = new StandardMaterial('scene-ground-material', scene);
    groundMaterial.diffuseColor = groundColor;
    groundMaterial.specularColor = Color3.Black();

    const ground = MeshBuilder.CreateGround(
      'scene-ground',
      {
        width: worldWidth,
        height: worldDepth,
        subdivisions: Math.max(worldWidth, worldDepth),
      },
      scene,
    );
    ground.position = new Vector3(worldWidth / 2, 0, worldDepth / 2);
    ground.material = groundMaterial;

    for (const prop of sceneDefinition.props ?? []) {
      const assembly = buildPrimitiveAssembly(scene, prop.propId, prop.parts);
      assembly.rootNode.position = new Vector3(prop.position.x, prop.position.y, prop.position.z);
      assembly.rootNode.rotation = new Vector3(
        degreesToRadians(prop.rotation?.x ?? 0),
        degreesToRadians(prop.rotation?.y ?? 0),
        degreesToRadians(prop.rotation?.z ?? 0),
      );
      assembly.rootNode.scaling = new Vector3(
        prop.scale?.x ?? 1,
        prop.scale?.y ?? 1,
        prop.scale?.z ?? 1,
      );
    }

    return {
      sceneId: sceneDefinition.sceneId,
      toWorldPosition: (x: number, y: number) => ({
        x: x * coordinateScale,
        y: 0,
        z: worldDepth - y * coordinateScale,
      }),
      measureSprite: (frameWidth: number, frameHeight: number) => ({
        width: (frameWidth / 32) * coordinateScale,
        height: (frameHeight / 32) * coordinateScale,
      }),
    };
  }

  private createTiledCameraAndLighting(scene: Scene, mapDefinition: TiledMap) {
    const center = new Vector3(mapDefinition.width / 2, 0.35, mapDefinition.height / 2);
    const radius = Math.max(mapDefinition.width, mapDefinition.height) * 0.95;
    const camera = new ArcRotateCamera(
      'world-camera',
      -Math.PI / 2,
      1.05,
      radius,
      center,
      scene,
    );

    camera.attachControl(this.canvas, true);
    camera.lowerRadiusLimit = radius * 0.65;
    camera.upperRadiusLimit = radius * 1.4;
    camera.lowerBetaLimit = 0.65;
    camera.upperBetaLimit = 1.25;
    camera.panningSensibility = 0;
    camera.wheelPrecision = 32;

    const skyLight = new HemisphericLight('sky-light', new Vector3(0.25, 1, -0.35), scene);
    skyLight.intensity = 1.05;
    skyLight.diffuse = new Color3(0.95, 0.95, 0.9);
    skyLight.groundColor = new Color3(0.25, 0.28, 0.22);
  }

  private createMapPlanes(scene: Scene, mapDefinition: TiledMap, tilesets: TiledTilesetWithImage[]) {
    const { baseCanvas, overlayCanvas } = rasterizeMapLayers(mapDefinition, tilesets);
    const mapWidth = mapDefinition.width;
    const mapHeight = mapDefinition.height;

    const baseTexture = new DynamicTexture(
      'map-base-texture',
      baseCanvas,
      scene,
      false,
      Texture.NEAREST_SAMPLINGMODE,
    );
    baseTexture.hasAlpha = true;

    const baseMaterial = new StandardMaterial('map-base-material', scene);
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
      scene,
    );
    ground.position = new Vector3(mapWidth / 2, 0, mapHeight / 2);
    ground.material = baseMaterial;

    if (!isCanvasTransparent(overlayCanvas)) {
      const overlayTexture = new DynamicTexture(
        'map-overlay-texture',
        overlayCanvas,
        scene,
        false,
        Texture.NEAREST_SAMPLINGMODE,
      );
      overlayTexture.hasAlpha = true;

      const overlayMaterial = new StandardMaterial('map-overlay-material', scene);
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
        scene,
      );
      overlay.position = new Vector3(mapWidth / 2, 1.12, mapHeight / 2);
      overlay.material = overlayMaterial;
    }
  }

  private resolveTilesetUrl(
    sceneId: string,
    tilesetName: string,
    imagePath: string | undefined,
    mapUrl: string,
  ) {
    const sceneDefinition = this.runtime.catalog.resolveScene(sceneId);
    if (sceneDefinition.builder !== 'tiled-map') {
      throw new Error(`La escena ${sceneId} no usa tilesets Tiled.`);
    }

    const mapVisual = this.runtime.catalog.mapVisuals[sceneDefinition.mapVisualId];
    const contentTileset = mapVisual?.tilesets.find((entry) => entry.tilesetName === tilesetName);

    if (contentTileset) {
      return this.runtime.catalog.resolveFile(contentTileset.imageFileId).url;
    }

    if (!imagePath) {
      throw new Error(`No se pudo resolver la imagen del tileset ${tilesetName}.`);
    }

    return resolveRelativeUrl(imagePath, mapUrl);
  }

  private fetchJson<TResponse>(url: string) {
    return fetch(url).then(async (response) => {
      if (!response.ok) {
        throw new Error(`No se pudo cargar ${url}.`);
      }

      return (await response.json()) as TResponse;
    });
  }
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
        const gid = rawGid & ~0xe0000000;

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

function parseColor(color: string | undefined, fallback: string) {
  return Color3.FromHexString(normalizeHex(color ?? fallback));
}

function normalizeHex(value: string) {
  if (!value.startsWith('#')) {
    return `#${value}`;
  }

  return value;
}

function degreesToRadians(value: number) {
  return (value * Math.PI) / 180;
}

function stringifyBuilder(sceneDefinition: SceneDefinition) {
  return sceneDefinition.builder;
}
