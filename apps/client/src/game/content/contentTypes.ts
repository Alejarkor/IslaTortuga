export type ContentFileType =
  | 'scene'
  | 'terrain'
  | 'map'
  | 'model'
  | 'image'
  | 'texture'
  | 'material'
  | 'audio'
  | 'animation'
  | 'json'
  | 'spritesheet';

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

export type MapTilesetDefinition = {
  tilesetName: string;
  textureKey: string;
  imageFileId: string;
};

export type MapVisualDefinition = {
  mapFileId: string;
  tilesets: MapTilesetDefinition[];
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

export type TiledSceneDefinition = {
  sceneId: string;
  builder: 'tiled-map';
  mapVisualId: string;
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

export type SceneDefinition = TiledSceneDefinition | PrimitiveSceneDefinition;

export type SceneDefinitions = {
  scenes: Record<string, SceneDefinition>;
  mapVisuals: Record<string, MapVisualDefinition>;
};

export type PlayerAnimationDefinition = {
  idleDown: string;
  idleUp: string;
  idleSide: string;
  walkDown: string;
  walkUp: string;
  walkSide: string;
};

export type SpriteBillboardEntityVisualDefinition = {
  visualId: string;
  builder: 'sprite-billboard';
  spriteSheetFileId: string;
  frameWidth: number;
  frameHeight: number;
  animations: PlayerAnimationDefinition;
};

export type PrimitiveAssemblyEntityVisualDefinition = {
  visualId: string;
  builder: 'primitive-assembly';
  positionYOffset?: number;
  facingMode?: 'rotate-y';
  walkBobAmplitude?: number;
  walkBobSpeed?: number;
  parts: PrimitivePartDefinition[];
};

export type EntityVisualDefinition =
  | SpriteBillboardEntityVisualDefinition
  | PrimitiveAssemblyEntityVisualDefinition;

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
