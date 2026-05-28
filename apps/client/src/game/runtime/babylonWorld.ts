import { Engine } from '@babylonjs/core';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { PayloadBuilder } from './payloadBuilder';
import { EntityVisualFactory } from './entityVisualFactory';
import {
  GameNetworkClient,
  type AuthAcceptedPayload,
  type SceneContextPayload,
  type WorldDeltaPayload,
} from './networkClient';
import { NetworkEntityManager } from './networkEntityManager';
import { NetworkSceneManager } from './networkSceneManager';

type RuntimeCallbacks = {
  onStatusChange: (message: string) => void;
  onError: (message: string) => void;
};

export class BabylonWorld {
  private readonly keyState: Record<string, boolean> = {};
  private readonly canvas: HTMLCanvasElement;
  private readonly runtime: GameRuntime;
  private readonly callbacks: RuntimeCallbacks;

  private engine?: Engine;
  private sceneManager?: NetworkSceneManager;
  private networkClient?: GameNetworkClient;
  private entityManager?: NetworkEntityManager;
  private pendingDeltas: WorldDeltaPayload[] = [];
  private lastFrameTime = 0;
  private lastSentInput = '0:0';

  constructor(canvas: HTMLCanvasElement, runtime: GameRuntime, callbacks: RuntimeCallbacks) {
    this.canvas = canvas;
    this.runtime = runtime;
    this.callbacks = callbacks;
  }

  async initialize() {
    this.engine = new Engine(this.canvas, true, {
      adaptToDeviceRatio: true,
      antialias: true,
    });
    this.sceneManager = new NetworkSceneManager(this.engine, this.runtime, this.canvas);
    await this.syncScene({
      sceneId: this.runtime.startGame.sceneId,
      sceneInstanceId: 'shared',
    });
    this.attachInputHandlers();
    this.connectToGameServer(
      this.runtime.startGame.webSocketUrl,
      this.runtime.startGame.gameTicket,
    );

    this.lastFrameTime = performance.now();
    this.engine.runRenderLoop(() => this.renderFrame());
    window.addEventListener('resize', this.handleResize);
  }

  dispose() {
    window.removeEventListener('resize', this.handleResize);
    window.removeEventListener('keydown', this.handleKeyDown);
    window.removeEventListener('keyup', this.handleKeyUp);

    this.networkClient?.close();
    this.networkClient = undefined;

    this.entityManager?.dispose();
    this.entityManager = undefined;
    this.pendingDeltas = [];

    this.sceneManager?.dispose();
    this.sceneManager = undefined;

    this.engine?.dispose();
    this.engine = undefined;
  }

  private attachInputHandlers() {
    window.addEventListener('keydown', this.handleKeyDown);
    window.addEventListener('keyup', this.handleKeyUp);
  }

  private connectToGameServer(webSocketUrl: string, gameTicket: string) {
    this.networkClient = new GameNetworkClient(webSocketUrl, {
      onAuthAccepted: (payload) => this.handleAuthAccepted(payload),
      onSceneBootstrap: (payload) => {
        void this.handleSceneBootstrap(payload).catch((error) => {
          this.callbacks.onError(error instanceof Error ? error.message : 'No se pudo sincronizar la escena inicial.');
        });
      },
      onSceneChange: (payload) => {
        void this.handleSceneChange(payload).catch((error) => {
          this.callbacks.onError(error instanceof Error ? error.message : 'No se pudo cambiar de escena.');
        });
      },
      onWorldDelta: (payload) => {
        void this.applyWorldDelta(payload).catch((error) => {
          this.callbacks.onError(error instanceof Error ? error.message : 'No se pudo aplicar el delta del mundo.');
        });
      },
      onError: (payload) => {
        this.callbacks.onError(`Game server error: ${payload.message}`);
      },
      onClose: () => {
        this.callbacks.onStatusChange('Conexion al game server cerrada.');
      },
    });

    this.networkClient
      .waitUntilOpen()
      .then(() => {
        this.networkClient?.sendJoin(PayloadBuilder.joinGame(gameTicket));
      })
      .catch((error) => {
        this.callbacks.onError(
          error instanceof Error ? error.message : 'No se pudo abrir el game socket.',
        );
      });
  }

  private handleAuthAccepted(payload: AuthAcceptedPayload) {
    this.runtime.authSession = payload;
    this.entityManager?.setLocalEntityId(payload.playerEntityId);
    this.callbacks.onStatusChange(`Conectado a ${payload.roomId}. Esperando deltas...`);
  }

  private async handleSceneBootstrap(payload: SceneContextPayload) {
    await this.syncScene(payload);
    this.callbacks.onStatusChange(
      `Escena sincronizada: ${payload.sceneId} (${payload.sceneInstanceId}).`,
    );
  }

  private async handleSceneChange(payload: SceneContextPayload) {
    await this.syncScene(payload);
    this.callbacks.onStatusChange(
      `Cambio de escena a ${payload.sceneId} (${payload.sceneInstanceId}).`,
    );
  }

  private async applyWorldDelta(delta: WorldDeltaPayload) {
    if (!this.entityManager || this.sceneManager?.isLoading) {
      this.pendingDeltas.push(delta);
      return;
    }

    await this.entityManager.applyWorldDelta(delta);
    this.callbacks.onStatusChange(`Delta ${delta.serverTick} recibido desde ${delta.roomId}.`);
  }

  private renderFrame() {
    const scene = this.sceneManager?.currentScene?.scene;
    if (!scene) {
      return;
    }

    const now = performance.now();
    const deltaSeconds = Math.min((now - this.lastFrameTime) / 1000, 0.05);
    this.lastFrameTime = now;

    this.updateLocalInput(deltaSeconds);
    this.entityManager?.interpolateRemoteEntities(deltaSeconds);
    scene.render();
  }

  private updateLocalInput(deltaSeconds: number) {
    const moveX =
      (this.isKeyDown('ArrowRight') || this.isKeyDown('KeyD') ? 1 : 0) -
      (this.isKeyDown('ArrowLeft') || this.isKeyDown('KeyA') ? 1 : 0);
    const moveY =
      (this.isKeyDown('ArrowDown') || this.isKeyDown('KeyS') ? 1 : 0) -
      (this.isKeyDown('ArrowUp') || this.isKeyDown('KeyW') ? 1 : 0);

    const inputSignature = `${moveX}:${moveY}`;
    if (this.networkClient && inputSignature !== this.lastSentInput) {
      this.networkClient.sendPlayerInput(PayloadBuilder.playerInput(moveX, moveY));
      this.lastSentInput = inputSignature;
    }

    this.entityManager?.updateLocalInputPrediction(deltaSeconds, moveX, moveY);
  }

  private isKeyDown(code: string) {
    return this.keyState[code] === true;
  }

  private readonly handleResize = () => {
    this.engine?.resize();
  };

  private readonly handleKeyDown = (event: KeyboardEvent) => {
    this.keyState[event.code] = true;
  };

  private readonly handleKeyUp = (event: KeyboardEvent) => {
    this.keyState[event.code] = false;
  };

  private async syncScene(payload: SceneContextPayload) {
    if (!this.sceneManager) {
      throw new Error('El gestor de escenas de red no esta disponible.');
    }

    const sceneSync = await this.sceneManager.syncScene(payload.sceneId, payload.sceneInstanceId);

    if (sceneSync.didReload || !this.entityManager) {
      this.entityManager?.dispose();
      const visualFactory = new EntityVisualFactory(this.runtime, sceneSync.scene, sceneSync.sceneContext);
      this.entityManager = new NetworkEntityManager(sceneSync.sceneContext, visualFactory);

      if (this.runtime.authSession?.playerEntityId) {
        this.entityManager.setLocalEntityId(this.runtime.authSession.playerEntityId);
      }
    }

    if (this.pendingDeltas.length > 0) {
      const queuedDeltas = this.pendingDeltas;
      this.pendingDeltas = [];

      for (const delta of queuedDeltas) {
        await this.entityManager.applyWorldDelta(delta);
      }
    }
  }
}
