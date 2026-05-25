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
