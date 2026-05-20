import type { ContentFileEntry } from './contentTypes';

const CACHE_PREFIX = 'isla-tortuga-content';

export class AssetCache {
  async ensureCached(manifestVersion: string, files: ContentFileEntry[]) {
    if (typeof caches === 'undefined') {
      await Promise.all(files.map((file) => fetch(file.url)));
      return;
    }

    const cache = await caches.open(`${CACHE_PREFIX}-${manifestVersion}`);

    for (const file of files) {
      const cachedResponse = await cache.match(file.url);
      if (cachedResponse) {
        continue;
      }

      const response = await fetch(file.url, { cache: 'reload' });
      if (!response.ok) {
        throw new Error(`No se pudo descargar el asset ${file.id}.`);
      }

      await cache.put(file.url, response.clone());
    }
  }
}
