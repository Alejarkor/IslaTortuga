export type ContentFileType =
  | 'scene'
  | 'model'
  | 'image'
  | 'texture'
  | 'material'
  | 'audio'
  | 'animation'
  | 'json';

export type ContentFileEntry = {
  id: string;
  type: ContentFileType;
  url: string;
  hash: string;
  size: number;
};

export type ContentManifest = {
  contentPackId: string;
  version: string;
  sceneId: string;
  files: ContentFileEntry[];
};

export type Vector3Definition = {
  x: number;
  y: number;
  z: number;
};

export type PrimitiveShape = 'box' | 'sphere' | 'capsule' | 'cylinder';

export type PrimitivePartDefinition = {
  shape: PrimitiveShape;
  position?: Partial<Vector3Definition>;
  rotation?: Partial<Vector3Definition>;
  scale?: Partial<Vector3Definition>;
  dimensions?: Partial<Vector3Definition>;
  color?: string;
  emissiveColor?: string;
  alpha?: number;
};

export type PrimitiveScenePropDefinition = {
  propId: string;
  position: Vector3Definition;
  rotation?: Partial<Vector3Definition>;
  scale?: Partial<Vector3Definition>;
  parts: PrimitivePartDefinition[];
};

export type PrimitiveSceneDefinition = {
  sceneId: string;
  builder: 'primitive-scene';
  worldWidth: number;
  worldDepth: number;
  coordinateScale?: number;
  skyColor?: string;
  ambientColor?: string;
  groundColor?: string;
  props?: PrimitiveScenePropDefinition[];
  camera?: {
    radius?: number;
    alpha?: number;
    beta?: number;
    target?: Partial<Vector3Definition>;
  };
};

export type UnitySceneExportSceneDefinition = {
  sceneId: string;
  builder: 'unity-scene-export';
  sceneDataFileId: string;
};

export type SceneDefinition = PrimitiveSceneDefinition | UnitySceneExportSceneDefinition;

export type SceneDefinitions = {
  scenes: Record<string, SceneDefinition>;
};

export type UnitySceneExportBoundsDefinition = {
  width: number;
  depth: number;
};

export type UnitySceneExportSpawnPointDefinition = {
  spawnId: string;
  spawnType: string;
  facing?: string;
  position: Vector3Definition;
};

export type UnitySceneExportTransitionTriggerDefinition = {
  type: PrimitiveShape;
  center: Vector3Definition;
  size?: Vector3Definition;
  radius?: number;
  height?: number;
  axis?: 'x' | 'y' | 'z';
};

export type UnitySceneExportTransitionDefinition = {
  transitionId: string;
  targetSceneId: string;
  targetSpawnId: string;
  instanceMode: 'shared' | 'per_player' | 'per_party' | 'named' | string;
  namedInstanceId?: string | null;
  trigger?: UnitySceneExportTransitionTriggerDefinition;
};

export type UnitySceneExportColliderShapeDefinition =
  | {
      type: 'box';
      center: Vector3Definition;
      size: Vector3Definition;
    }
  | {
      type: 'sphere';
      center: Vector3Definition;
      radius: number;
    }
  | {
      type: 'capsule' | 'cylinder';
      center: Vector3Definition;
      radius: number;
      height: number;
      axis?: 'x' | 'y' | 'z';
    };

export type UnitySceneExportColliderDefinition = {
  colliderId: string;
  colliderKind: string;
  clientCollision: 'none' | 'simple' | 'full' | string;
  shape: UnitySceneExportColliderShapeDefinition;
};

export type UnitySceneExportPropDefinition = {
  propId: string;
  visualAssetId?: string;
  exportMode?: string;
  staticCollisionSource?: string;
  position: Vector3Definition;
  rotation?: Partial<Vector3Definition>;
  scale?: Partial<Vector3Definition>;
  linkedColliderIds?: string[];
};

export type UnitySceneExportLightDefinition = {
  lightType: 'directional' | 'point' | 'spot' | 'hemispheric' | string;
  position?: Partial<Vector3Definition>;
  rotation?: Partial<Vector3Definition>;
  color?: string;
  intensity?: number;
  range?: number;
};

export type UnitySceneExportSceneData = {
  sceneId: string;
  displayName?: string;
  builder: 'unity-scene-export';
  coordinateScale?: number;
  bounds: UnitySceneExportBoundsDefinition;
  spawnPoints?: UnitySceneExportSpawnPointDefinition[];
  transitions?: UnitySceneExportTransitionDefinition[];
  colliders?: UnitySceneExportColliderDefinition[];
  props?: UnitySceneExportPropDefinition[];
  audioEmitters?: unknown[];
  lights?: UnitySceneExportLightDefinition[];
};

export type PrimitiveAssemblyEntityVisualDefinition = {
  visualId: string;
  builder: 'primitive-assembly';
  positionYOffset?: number;
  facingMode?: 'rotate-y';
  moveBobAmplitude?: number;
  moveBobSpeed?: number;
  parts: PrimitivePartDefinition[];
};

export type EntityVisualDefinition = PrimitiveAssemblyEntityVisualDefinition;

export type EntityVisualDefinitions = {
  entities: Record<string, Record<string, EntityVisualDefinition>>;
};

export type EntityArchetypeDefinition = {
  archetypeId: string;
  entityType: string;
  defaultVisualId?: string | null;
};

export type EntityArchetypeDefinitions = {
  archetypes: Record<string, EntityArchetypeDefinition>;
};

export type AssetCatalogDefinitionFiles = {
  sceneDefinitions: string;
  entityVisualDefinitions: string;
  entityArchetypes: string;
  itemDefinitions: string;
  rules: string;
};
