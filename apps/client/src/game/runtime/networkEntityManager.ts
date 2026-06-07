import type { NetworkEntityVisual } from './entityVisualFactory';
import type { EntityVisualFactory } from './entityVisualFactory';
import type {
  EntitySpawnPayload,
  EntityUpdatePayload,
  WorldDeltaPayload,
} from './networkClient';
import type { BuiltSceneContext } from './sceneBuilder';

const PLAYER_MOVE_SPEED = 4.7;
const LOCAL_RECONCILE_FACTOR = 0.22;
const REMOTE_INTERPOLATION_FACTOR = 0.18;
const SNAP_DISTANCE_THRESHOLD = 1.15;

export class NetworkEntityManager {
  private readonly visuals = new Map<string, NetworkEntityVisual>();
  private localEntityId?: string;

  constructor(
    private readonly sceneContext: BuiltSceneContext,
    private readonly visualFactory: EntityVisualFactory,
  ) {}

  setLocalEntityId(entityId: string) {
    this.localEntityId = entityId;
    const visual = this.visuals.get(entityId);
    if (visual) {
      visual.isLocal = true;
    }
  }

  getLocalVisual() {
    if (!this.localEntityId) {
      return null;
    }

    return this.visuals.get(this.localEntityId) ?? null;
  }

  async applyWorldDelta(delta: WorldDeltaPayload) {
    for (const despawn of delta.despawns) {
      this.disposeEntity(despawn.entityId);
    }

    for (const spawn of delta.spawns) {
      await this.spawnEntity(spawn);
    }

    for (const update of delta.updates) {
      this.applyUpdate(update);
    }
  }

  updateLocalInputPrediction(deltaSeconds: number, moveX: number, moveY: number) {
    if (!this.localEntityId) {
      return;
    }

    const visual = this.visuals.get(this.localEntityId);
    if (!visual) {
      return;
    }

    const magnitude = Math.hypot(moveX, moveY);
    let moving = false;

    if (magnitude > 0) {
      const normalizedX = moveX / magnitude;
      const normalizedY = moveY / magnitude;
      visual.currentX += normalizedX * PLAYER_MOVE_SPEED * deltaSeconds;
      visual.currentZ += normalizedY * PLAYER_MOVE_SPEED * deltaSeconds;
      visual.facing = resolveFacingFromInput(moveX, moveY, visual.facing);
      visual.motionAccumulator += deltaSeconds;
      moving = true;
    }

    const distanceToTarget = distanceBetween(
      visual.currentX,
      visual.currentZ,
      visual.targetX,
      visual.targetZ,
    );

    if (distanceToTarget > SNAP_DISTANCE_THRESHOLD) {
      visual.currentX = visual.targetX;
      visual.currentZ = visual.targetZ;
    } else {
      visual.currentX = lerp(visual.currentX, visual.targetX, LOCAL_RECONCILE_FACTOR);
      visual.currentZ = lerp(visual.currentZ, visual.targetZ, LOCAL_RECONCILE_FACTOR);
    }

    visual.currentY = lerp(visual.currentY, visual.targetY, LOCAL_RECONCILE_FACTOR);
    this.applyVisualTransform(visual, moving);
  }

  interpolateRemoteEntities(deltaSeconds: number) {
    for (const visual of this.visuals.values()) {
      if (visual.isLocal) {
        continue;
      }

      visual.currentX = lerp(visual.currentX, visual.targetX, REMOTE_INTERPOLATION_FACTOR);
      visual.currentZ = lerp(visual.currentZ, visual.targetZ, REMOTE_INTERPOLATION_FACTOR);
      visual.currentY = lerp(visual.currentY, visual.targetY, REMOTE_INTERPOLATION_FACTOR);

      const moving =
        distanceBetween(visual.currentX, visual.currentZ, visual.targetX, visual.targetZ) > 0.04;
      if (moving) {
        visual.motionAccumulator += deltaSeconds;
      }

      this.applyVisualTransform(visual, moving);
    }
  }

  dispose() {
    for (const entityId of this.visuals.keys()) {
      this.disposeEntity(entityId);
    }
  }

  private async spawnEntity(spawn: EntitySpawnPayload) {
    this.disposeEntity(spawn.entityId);

    const visual = await this.visualFactory.createVisual(spawn);
    visual.isLocal = spawn.entityId === this.localEntityId;
    this.visuals.set(spawn.entityId, visual);
    this.applyVisualTransform(visual, false);
  }

  private applyUpdate(update: EntityUpdatePayload) {
    const visual = this.visuals.get(update.entityId);
    if (!visual) {
      return;
    }

    const worldPosition = this.sceneContext.toWorldPosition(update.x, update.y);
    visual.targetX = worldPosition.x;
    visual.targetY = visual.baseY + worldPosition.y;
    visual.targetZ = worldPosition.z;

    if (update.facing) {
      visual.facing = update.facing;
    }
  }

  private disposeEntity(entityId: string) {
    const visual = this.visuals.get(entityId);
    if (!visual) {
      return;
    }

    visual.dispose();
    this.visuals.delete(entityId);
  }

  private applyVisualTransform(visual: NetworkEntityVisual, moving: boolean) {
    visual.rootNode.position.x = visual.currentX;
    visual.rootNode.position.z = visual.currentZ;

    const bobAmplitude = moving ? visual.definition.moveBobAmplitude ?? 0.08 : 0;
    const bobSpeed = visual.definition.moveBobSpeed ?? 7.5;
    const bobOffset =
      bobAmplitude > 0 ? Math.sin(visual.motionAccumulator * bobSpeed) * bobAmplitude : 0;

    visual.rootNode.position.y = visual.currentY + bobOffset;
    visual.lastFacingYaw = lerpAngle(visual.lastFacingYaw, facingToYaw(visual.facing), 0.22);
    visual.rootNode.rotation.y = visual.lastFacingYaw;
  }
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

function distanceBetween(ax: number, az: number, bx: number, bz: number) {
  return Math.hypot(bx - ax, bz - az);
}

function lerp(start: number, end: number, factor: number) {
  return start + (end - start) * factor;
}

function lerpAngle(start: number, end: number, factor: number) {
  const delta = normalizeAngle(end - start);
  return start + delta * factor;
}

function normalizeAngle(value: number) {
  let result = value;
  while (result > Math.PI) {
    result -= Math.PI * 2;
  }

  while (result < -Math.PI) {
    result += Math.PI * 2;
  }

  return result;
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
