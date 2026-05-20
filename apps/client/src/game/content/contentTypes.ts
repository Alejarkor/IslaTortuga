export type ContentFileType = 'map' | 'image' | 'json' | 'audio' | 'spritesheet';

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
  mapId: string;
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

export type PlayerAnimationDefinition = {
  idleDown: string;
  idleUp: string;
  idleSide: string;
  walkDown: string;
  walkUp: string;
  walkSide: string;
};

export type PlayerVisualDefinition = {
  textureKey: string;
  imageFileId: string;
  frameWidth: number;
  frameHeight: number;
  animations: PlayerAnimationDefinition;
};

export type VisualDefinitions = {
  maps: Record<string, MapVisualDefinition>;
  players: {
    default: PlayerVisualDefinition;
  };
};

export type AssetCatalogDefinitionFiles = {
  visualDefinitions: string;
  entityArchetypes: string;
  itemDefinitions: string;
  rules: string;
};
