export type NetworkEnvelope<TPayload = unknown> = {
  op: string;
  requestId?: string | null;
  sentAt?: number | null;
  payload: TPayload;
};

export type JoinGamePayload = {
  gameTicket: string;
};

export type AuthAcceptedPayload = {
  sessionId: string;
  userId: string;
  displayName: string;
  roomId: string;
  playerEntityId: string;
};

export type SceneContextPayload = {
  sceneId: string;
  sceneInstanceId: string;
};

export type ErrorPayload = {
  code: string;
  message: string;
  retryable?: boolean;
};

export type EntitySpawnPayload = {
  entityId: string;
  entityType: string;
  archetypeId?: string | null;
  visualId?: string | null;
  x: number;
  y: number;
  facing: 'up' | 'down' | 'left' | 'right' | string;
  displayName?: string | null;
};

export type EntityUpdatePayload = {
  entityId: string;
  x: number;
  y: number;
  facing?: 'up' | 'down' | 'left' | 'right' | string | null;
};

export type EntityDespawnPayload = {
  entityId: string;
};

export type WorldDeltaPayload = {
  serverTick: number;
  roomId: string;
  spawns: EntitySpawnPayload[];
  updates: EntityUpdatePayload[];
  despawns: EntityDespawnPayload[];
};

export type PlayerInputPayload = {
  moveX: number;
  moveY: number;
  sequence?: number;
};

type EventHandlers = {
  onAuthAccepted?: (payload: AuthAcceptedPayload) => void;
  onSceneBootstrap?: (payload: SceneContextPayload) => void;
  onSceneChange?: (payload: SceneContextPayload) => void;
  onWorldDelta?: (payload: WorldDeltaPayload) => void;
  onError?: (payload: ErrorPayload) => void;
  onClose?: () => void;
};

export class GameNetworkClient {
  private readonly socket: WebSocket;

  constructor(webSocketUrl: string, handlers: EventHandlers) {
    this.socket = new WebSocket(resolveWebSocketUrl(webSocketUrl));

    this.socket.addEventListener('message', (event) => {
      const envelope = JSON.parse(event.data as string) as NetworkEnvelope;

      if (envelope.op === 'auth.accepted') {
        handlers.onAuthAccepted?.(envelope.payload as AuthAcceptedPayload);
        return;
      }

      if (envelope.op === 'scene.bootstrap') {
        handlers.onSceneBootstrap?.(envelope.payload as SceneContextPayload);
        return;
      }

      if (envelope.op === 'scene.change') {
        handlers.onSceneChange?.(envelope.payload as SceneContextPayload);
        return;
      }

      if (envelope.op === 'world.delta') {
        handlers.onWorldDelta?.(envelope.payload as WorldDeltaPayload);
        return;
      }

      if (envelope.op === 'auth.rejected' || envelope.op === 'error') {
        handlers.onError?.(envelope.payload as ErrorPayload);
      }
    });

    this.socket.addEventListener('close', () => {
      handlers.onClose?.();
    });
  }

  waitUntilOpen() {
    if (this.socket.readyState === WebSocket.OPEN) {
      return Promise.resolve();
    }

    return new Promise<void>((resolve, reject) => {
      this.socket.addEventListener('open', () => resolve(), { once: true });
      this.socket.addEventListener('error', () => reject(new Error('No se pudo abrir el WebSocket.')), {
        once: true,
      });
    });
  }

  sendJoin(payload: JoinGamePayload) {
    this.send('auth.join', payload);
  }

  sendPlayerInput(payload: PlayerInputPayload) {
    this.send('player.input', payload);
  }

  close() {
    this.socket.close();
  }

  private send<TPayload>(op: string, payload: TPayload) {
    const envelope: NetworkEnvelope<TPayload> = {
      op,
      sentAt: Date.now(),
      payload,
    };

    this.socket.send(JSON.stringify(envelope));
  }
}

function resolveWebSocketUrl(webSocketUrl: string) {
  if (webSocketUrl.startsWith('ws://') || webSocketUrl.startsWith('wss://')) {
    return webSocketUrl;
  }

  if (typeof window === 'undefined') {
    return `ws://localhost:5055${webSocketUrl}`;
  }

  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${protocol}//${window.location.host}${webSocketUrl}`;
}
