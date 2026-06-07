import {
  TransformNode,
  Vector3,
} from '@babylonjs/core';
import type {
  PrimitiveAssemblyEntityVisualDefinition,
} from '../content/contentTypes';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import type { EntitySpawnPayload } from './networkClient';
import type { BuiltSceneContext } from './sceneBuilder';
import { buildPrimitiveAssembly } from './primitiveAssembly';

export type NetworkEntityVisual = {
  entityId: string;
  entityType: string;
  archetypeId?: string | null;
  visualId?: string | null;
  displayName?: string | null;
  builder: 'primitive-assembly';
  definition: PrimitiveAssemblyEntityVisualDefinition;
  rootNode: TransformNode;
  currentX: number;
  currentY: number;
  currentZ: number;
  targetX: number;
  targetY: number;
  targetZ: number;
  baseY: number;
  facing: string;
  isLocal: boolean;
  motionAccumulator: number;
  lastFacingYaw: number;
  dispose(): void;
};

export class EntityVisualFactory {
  constructor(
    private readonly runtime: GameRuntime,
    private readonly sceneContext: BuiltSceneContext,
  ) {}

  async createVisual(spawn: EntitySpawnPayload): Promise<NetworkEntityVisual> {
    const definition = this.runtime.catalog.resolveEntityVisual(
      spawn.entityType,
      spawn.visualId,
      spawn.archetypeId,
    );

    return this.createPrimitiveAssemblyVisual(spawn, definition);
  }

  private createPrimitiveAssemblyVisual(
    spawn: EntitySpawnPayload,
    definition: PrimitiveAssemblyEntityVisualDefinition,
  ): NetworkEntityVisual {
    const position = this.sceneContext.toWorldPosition(spawn.x, spawn.y);
    const baseY = position.y + (definition.positionYOffset ?? 0);
    const assembly = buildPrimitiveAssembly(this.sceneContext.scene, spawn.entityId, definition.parts);
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
      motionAccumulator: 0,
      lastFacingYaw: facingToYaw(spawn.facing),
      dispose: assembly.dispose,
    };
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
