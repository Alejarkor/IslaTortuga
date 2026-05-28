import { Color4, Engine, Scene } from '@babylonjs/core';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { SceneBuilder, type BuiltSceneContext } from './sceneBuilder';

export type SceneContextPayload = {
  sceneId: string;
  sceneInstanceId: string;
};

export type LoadedNetworkScene = {
  scene: Scene;
  sceneContext: BuiltSceneContext;
  sceneId: string;
  sceneInstanceId: string;
};

export type SceneSyncResult = LoadedNetworkScene & {
  didReload: boolean;
};

export class NetworkSceneManager {
  private readonly sceneBuilder: SceneBuilder;
  private activeScene: LoadedNetworkScene | null = null;
  private syncVersion = 0;
  private loading = false;

  constructor(
    private readonly engine: Engine,
    runtime: GameRuntime,
    private readonly canvas: HTMLCanvasElement,
  ) {
    this.sceneBuilder = new SceneBuilder(runtime, canvas);
  }

  get currentScene() {
    return this.activeScene;
  }

  get isLoading() {
    return this.loading;
  }

  async syncScene(sceneId: string, sceneInstanceId: string): Promise<SceneSyncResult> {
    const normalizedInstanceId = sceneInstanceId || 'shared';
    if (
      this.activeScene &&
      this.activeScene.sceneId === sceneId &&
      this.activeScene.sceneInstanceId === normalizedInstanceId
    ) {
      return {
        ...this.activeScene,
        didReload: false,
      };
    }

    this.syncVersion += 1;
    const currentSyncVersion = this.syncVersion;
    this.loading = true;

    const nextScene = new Scene(this.engine);
    nextScene.clearColor = new Color4(0.05, 0.08, 0.05, 1);

    try {
      const sceneContext = await this.sceneBuilder.build(nextScene, sceneId);

      if (currentSyncVersion != this.syncVersion) {
        nextScene.dispose();
        throw new Error('La carga de escena fue reemplazada por una sincronizacion mas reciente.');
      }

      const previousScene = this.activeScene;
      this.activeScene = {
        scene: nextScene,
        sceneContext,
        sceneId,
        sceneInstanceId: normalizedInstanceId,
      };

      previousScene?.scene.dispose();

      return {
        ...this.activeScene,
        didReload: true,
      };
    } finally {
      if (currentSyncVersion === this.syncVersion) {
        this.loading = false;
      }
    }
  }

  dispose() {
    this.activeScene?.scene.dispose();
    this.activeScene = null;
    this.loading = false;
  }
}
