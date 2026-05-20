import type { AssetCatalog } from '../content/assetCatalog';
import type { ContentManifest } from '../content/contentTypes';
import type { StartGameResponse } from '../../shared/http/apiClient';
import type { AuthAcceptedPayload } from '../runtime/networkClient';

export type GameRuntime = {
  catalog: AssetCatalog;
  manifest: ContentManifest;
  startGame: StartGameResponse;
  authSession?: AuthAcceptedPayload | null;
};

let currentGameRuntime: GameRuntime | null = null;

export function setCurrentGameRuntime(runtime: GameRuntime | null) {
  currentGameRuntime = runtime;
}

export function getCurrentGameRuntime() {
  return currentGameRuntime;
}
