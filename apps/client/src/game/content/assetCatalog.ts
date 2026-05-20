import type {
  AssetCatalogDefinitionFiles,
  ContentManifest,
  ContentFileEntry,
  VisualDefinitions,
} from './contentTypes';

export type AssetCatalog = VisualDefinitions & {
  definitions: AssetCatalogDefinitionFiles;
  manifest: ContentManifest;
  resolveFile(fileId: string): ContentFileEntry;
};

export class AssetCatalogLoader {
  async load(manifest: ContentManifest): Promise<AssetCatalog> {
    const visualDefinitionsFile = this.requireFile(manifest, 'definitions.visuals');
    const entityArchetypesFile = this.requireFile(manifest, 'definitions.entityArchetypes');
    const itemDefinitionsFile = this.requireFile(manifest, 'definitions.itemDefinitions');
    const rulesFile = this.requireFile(manifest, 'definitions.rules');

    const visualDefinitions = (await this.fetchJson<VisualDefinitions>(
      visualDefinitionsFile.url,
    )) ?? { maps: {}, players: { default: null as never } };

    return {
      ...visualDefinitions,
      definitions: {
        visualDefinitions: visualDefinitionsFile.url,
        entityArchetypes: entityArchetypesFile.url,
        itemDefinitions: itemDefinitionsFile.url,
        rules: rulesFile.url,
      },
      manifest,
      resolveFile: (fileId: string) => this.requireFile(manifest, fileId),
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
