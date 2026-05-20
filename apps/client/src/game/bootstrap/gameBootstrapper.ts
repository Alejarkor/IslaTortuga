import { startDevGame } from '../../shared/http/apiClient';
import { AssetCatalogLoader } from '../content/assetCatalog';
import { ContentDownloader } from '../content/contentDownloader';
import type { GameRuntime } from './gameRuntimeRegistry';

const contentDownloader = new ContentDownloader();
const assetCatalogLoader = new AssetCatalogLoader();

export async function bootstrapGameRuntime(token: string): Promise<GameRuntime> {
  const startGame = await startDevGame(token);
  const manifest = await contentDownloader.ensureContentPack(startGame.manifestUrl);
  const catalog = await assetCatalogLoader.load(manifest);

  return {
    catalog,
    manifest,
    startGame,
  };
}

export type { GameRuntime };
