import {
  Color3,
  DynamicTexture,
  Mesh,
  MeshBuilder,
  Scene,
  StandardMaterial,
  Texture,
  TransformNode,
  Vector3,
} from '@babylonjs/core';
import type {
  EntityVisualDefinition,
  PrimitiveAssemblyEntityVisualDefinition,
  SpriteBillboardEntityVisualDefinition,
} from '../content/contentTypes';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import type { EntitySpawnPayload } from './networkClient';
import type { BuiltSceneContext } from './sceneBuilder';
import { buildPrimitiveAssembly } from './primitiveAssembly';

type BaseNetworkEntityVisual = {
  entityId: string;
  entityType: string;
  archetypeId?: string | null;
  visualId?: string | null;
  displayName?: string | null;
  definition: EntityVisualDefinition;
  rootNode: TransformNode | Mesh;
  currentX: number;
  currentY: number;
  currentZ: number;
  targetX: number;
  targetY: number;
  targetZ: number;
  baseY: number;
  facing: string;
  isLocal: boolean;
  walkAccumulator: number;
  dispose(): void;
};

export type SpriteBillboardEntityVisual = BaseNetworkEntityVisual & {
  builder: 'sprite-billboard';
  definition: SpriteBillboardEntityVisualDefinition;
  image: HTMLImageElement;
  plane: Mesh;
  texture: DynamicTexture;
  material: StandardMaterial;
  lastFrameKey: string;
  flipX: boolean;
};

export type PrimitiveAssemblyEntityVisual = BaseNetworkEntityVisual & {
  builder: 'primitive-assembly';
  definition: PrimitiveAssemblyEntityVisualDefinition;
  rootNode: TransformNode;
  lastFacingYaw: number;
};

export type NetworkEntityVisual =
  | SpriteBillboardEntityVisual
  | PrimitiveAssemblyEntityVisual;

export class EntityVisualFactory {
  private readonly imagePromises = new Map<string, Promise<HTMLImageElement>>();

  constructor(
    private readonly runtime: GameRuntime,
    private readonly scene: Scene,
    private readonly sceneContext: BuiltSceneContext,
  ) {}

  async createVisual(spawn: EntitySpawnPayload): Promise<NetworkEntityVisual> {
    const definition = this.runtime.catalog.resolveEntityVisual(
      spawn.entityType,
      spawn.visualId,
      spawn.archetypeId,
    );

    if (definition.builder === 'primitive-assembly') {
      return this.createPrimitiveAssemblyVisual(spawn, definition);
    }

    if (definition.builder === 'sprite-billboard') {
      return this.createSpriteBillboardVisual(spawn, definition);
    }

    throw new Error(`El builder visual ${stringifyBuilder(definition)} no esta soportado.`);
  }

  private async createSpriteBillboardVisual(
    spawn: EntitySpawnPayload,
    definition: SpriteBillboardEntityVisualDefinition,
  ): Promise<SpriteBillboardEntityVisual> {
    const imageFile = this.runtime.catalog.resolveFile(definition.spriteSheetFileId);
    const image = await this.loadImage(imageFile.url);
    const position = this.sceneContext.toWorldPosition(spawn.x, spawn.y);
    const dimensions = this.sceneContext.measureSprite(definition.frameWidth, definition.frameHeight);
    const baseY = position.y + dimensions.height * 0.5;

    const texture = new DynamicTexture(
      `${spawn.entityId}-entity-texture`,
      {
        width: definition.frameWidth,
        height: definition.frameHeight,
      },
      this.scene,
      false,
      Texture.NEAREST_SAMPLINGMODE,
    );
    texture.hasAlpha = true;
    texture.wrapU = Texture.CLAMP_ADDRESSMODE;
    texture.wrapV = Texture.CLAMP_ADDRESSMODE;

    const material = new StandardMaterial(`${spawn.entityId}-entity-material`, this.scene);
    material.diffuseTexture = texture;
    material.opacityTexture = texture;
    material.disableLighting = true;
    material.emissiveColor = Color3.White();
    material.backFaceCulling = false;

    const plane = MeshBuilder.CreatePlane(
      `${spawn.entityId}-entity`,
      {
        width: dimensions.width,
        height: dimensions.height,
      },
      this.scene,
    );
    plane.billboardMode = Mesh.BILLBOARDMODE_Y;
    plane.material = material;
    plane.position = new Vector3(position.x, baseY, position.z);

    return {
      entityId: spawn.entityId,
      entityType: spawn.entityType,
      archetypeId: spawn.archetypeId,
      visualId: spawn.visualId,
      displayName: spawn.displayName,
      builder: 'sprite-billboard',
      definition,
      image,
      plane,
      rootNode: plane,
      texture,
      material,
      currentX: position.x,
      currentY: baseY,
      currentZ: position.z,
      targetX: position.x,
      targetY: baseY,
      targetZ: position.z,
      baseY,
      facing: spawn.facing,
      isLocal: false,
      lastFrameKey: '',
      flipX: false,
      walkAccumulator: 0,
      dispose: () => {
        plane.dispose();
        material.dispose();
        texture.dispose();
      },
    };
  }

  private createPrimitiveAssemblyVisual(
    spawn: EntitySpawnPayload,
    definition: PrimitiveAssemblyEntityVisualDefinition,
  ): PrimitiveAssemblyEntityVisual {
    const position = this.sceneContext.toWorldPosition(spawn.x, spawn.y);
    const baseY = position.y + (definition.positionYOffset ?? 0);
    const assembly = buildPrimitiveAssembly(this.scene, spawn.entityId, definition.parts);
    assembly.rootNode.position = new Vector3(position.x, baseY, position.z);
    assembly.rootNode.rotation = new Vector3(0, facingToYaw(spawn.facing), 0);

    return {
      entityId: spawn.entityId,
      entityType: spawn.entityType,
      archetypeId: spawn.archetypeId,
      visualId: spawn.visualId,
      displayName: spawn.displayName,
      builder: 'primitive-assembly',
      definition,
      rootNode: assembly.rootNode,
      currentX: position.x,
      currentY: baseY,
      currentZ: position.z,
      targetX: position.x,
      targetY: baseY,
      targetZ: position.z,
      baseY,
      facing: spawn.facing,
      isLocal: false,
      walkAccumulator: 0,
      lastFacingYaw: facingToYaw(spawn.facing),
      dispose: assembly.dispose,
    };
  }

  private loadImage(url: string) {
    const existingPromise = this.imagePromises.get(url);
    if (existingPromise) {
      return existingPromise;
    }

    const promise = new Promise<HTMLImageElement>((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error(`No se pudo cargar la imagen ${url}.`));
      image.src = url;
    });

    this.imagePromises.set(url, promise);
    return promise;
  }
}

function facingToYaw(facing: string) {
  switch (facing) {
    case 'up':
      return Math.PI;
    case 'left':
      return -Math.PI * 0.5;
    case 'right':
      return Math.PI * 0.5;
    case 'down':
    default:
      return 0;
  }
}

function stringifyBuilder(definition: EntityVisualDefinition) {
  return definition.builder;
}
