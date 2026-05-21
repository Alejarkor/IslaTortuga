import type { ContentFileEntry } from './contentTypes';

const CACHE_PREFIX = 'isla-tortuga-content';
const DISABLE_CONTENT_CACHE = import.meta.env.DEV || import.meta.env.VITE_DISABLE_CONTENT_CACHE === 'true';

export class AssetCache {
  async ensureCached(manifestVersion: string, files: ContentFileEntry[]) {
    if (DISABLE_CONTENT_CACHE) {
      await this.clearManagedCaches();
      await Promise.all(files.map((file) => this.fetchNoCache(file)));
      return;
    }

    if (typeof caches === 'undefined') {
      await Promise.all(files.map((file) => this.fetchNoCache(file)));
      return;
    }

    const cache = await caches.open(`${CACHE_PREFIX}-${manifestVersion}`);

    for (const file of files) {
      const cachedResponse = await cache.match(file.url);
      if (cachedResponse) {
        continue;
      }

      const response = await this.fetchNoCache(file);
      await cache.put(file.url, response.clone());
    }
  }

  private async fetchNoCache(file: ContentFileEntry) {
    const response = await fetch(file.url, { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`No se pudo descargar el asset ${file.id}.`);
    }

    return response;
  }

  private async clearManagedCaches() {
    if (typeof caches === 'undefined') {
      return;
    }

    const keys = await caches.keys();
    await Promise.all(
      keys
        .filter((key) => key.startsWith(CACHE_PREFIX))
        .map((key) => caches.delete(key)),
    );
  }
}
