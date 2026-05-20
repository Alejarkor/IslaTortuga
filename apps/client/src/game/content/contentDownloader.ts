import { AssetCache } from './assetCache';
import type { ContentManifest } from './contentTypes';

export class ContentDownloader {
  private readonly assetCache = new AssetCache();

  async ensureContentPack(manifestUrl: string): Promise<ContentManifest> {
    const manifestResponse = await fetch(manifestUrl, { cache: 'no-cache' });

    if (!manifestResponse.ok) {
      throw new Error('No se pudo descargar el manifest del content pack.');
    }

    const manifest = (await manifestResponse.json()) as ContentManifest;
    await this.assetCache.ensureCached(manifest.version, manifest.files);
    return manifest;
  }
}
