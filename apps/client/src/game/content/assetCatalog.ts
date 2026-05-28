import type {
  AssetCatalogDefinitionFiles,
  ContentManifest,
  ContentFileEntry,
  EntityArchetypeDefinition,
  EntityArchetypeDefinitions,
  EntityVisualDefinition,
  EntityVisualDefinitions,
  SceneDefinition,
  SceneDefinitions,
} from './contentTypes';

export type AssetCatalog = {
  scenes: Record<string, SceneDefinition>;
  mapVisuals: SceneDefinitions['mapVisuals'];
  entityVisuals: Record<string, Record<string, EntityVisualDefinition>>;
  entityArchetypes: Record<string, EntityArchetypeDefinition>;
  definitions: AssetCatalogDefinitionFiles;
  manifest: ContentManifest;
  resolveFile(fileId: string): ContentFileEntry;
  resolveScene(sceneId: string): SceneDefinition;
  resolveArchetype(archetypeId: string | null | undefined): EntityArchetypeDefinition | null;
  resolveEntityVisual(
    entityType: string,
    visualId?: string | null,
    archetypeId?: string | null,
  ): EntityVisualDefinition;
};

export class AssetCatalogLoader {
  async load(manifest: ContentManifest): Promise<AssetCatalog> {
    const sceneDefinitionsFile = this.requireFile(manifest, 'definitions.scenes');
    const entityVisualDefinitionsFile = this.requireFile(manifest, 'definitions.entityVisuals');
    const entityArchetypesFile = this.requireFile(manifest, 'definitions.entityArchetypes');
    const itemDefinitionsFile = this.requireFile(manifest, 'definitions.itemDefinitions');
    const rulesFile = this.requireFile(manifest, 'definitions.rules');

    const sceneDefinitions = (await this.fetchJson<SceneDefinitions>(
      sceneDefinitionsFile.url,
    )) ?? { scenes: {}, mapVisuals: {} };
    const entityVisualDefinitions = (await this.fetchJson<EntityVisualDefinitions>(
      entityVisualDefinitionsFile.url,
    )) ?? { entities: {} };
    const entityArchetypeDefinitions = (await this.fetchJson<EntityArchetypeDefinitions>(
      entityArchetypesFile.url,
    )) ?? { archetypes: {} };

    return {
      scenes: sceneDefinitions.scenes,
      mapVisuals: sceneDefinitions.mapVisuals,
      entityVisuals: entityVisualDefinitions.entities,
      entityArchetypes: entityArchetypeDefinitions.archetypes,
      definitions: {
        sceneDefinitions: sceneDefinitionsFile.url,
        entityVisualDefinitions: entityVisualDefinitionsFile.url,
        entityArchetypes: entityArchetypesFile.url,
        itemDefinitions: itemDefinitionsFile.url,
        rules: rulesFile.url,
      },
      manifest,
      resolveFile: (fileId: string) => this.requireFile(manifest, fileId),
      resolveScene: (sceneId: string) => {
        const sceneDefinition = sceneDefinitions.scenes[sceneId];

        if (!sceneDefinition) {
          throw new Error(`No existe definicion de escena para ${sceneId}.`);
        }

        return sceneDefinition;
      },
      resolveArchetype: (archetypeId: string | null | undefined) => {
        if (!archetypeId) {
          return null;
        }

        return entityArchetypeDefinitions.archetypes[archetypeId] ?? null;
      },
      resolveEntityVisual: (
        entityType: string,
        visualId?: string | null,
        archetypeId?: string | null,
      ) => {
        const visualsByType = entityVisualDefinitions.entities[entityType];
        if (!visualsByType) {
          throw new Error(`No existen visuales registrados para el tipo ${entityType}.`);
        }

        if (visualId && visualsByType[visualId]) {
          return visualsByType[visualId];
        }

        const archetype = archetypeId
          ? entityArchetypeDefinitions.archetypes[archetypeId]
          : undefined;
        const fallbackVisualId = archetype?.defaultVisualId ?? 'default';
        const fallbackVisual = visualsByType[fallbackVisualId];
        if (fallbackVisual) {
          return fallbackVisual;
        }

        throw new Error(
          `No se encontro una visual para entityType=${entityType}, visualId=${visualId ?? 'null'} y archetypeId=${archetypeId ?? 'null'}.`,
        );
      },
    };
  }

  private requireFile(manifest: ContentManifest, fileId: string) {
    const file = manifest.files.find((entry) => entry.id === fileId);

    if (!file) {
      throw new Error(`El manifest no contiene el archivo ${fileId}.`);
    }

    return file;
  }

  private async fetchJson<TResponse>(url: string) {
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error(`No se pudo cargar el catalogo ${url}.`);
    }

    return (await response.json()) as TResponse;
  }
}
