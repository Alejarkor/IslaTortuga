import { MatchConfig } from "../rooms/types";

export interface CapacityInfo {
  canAcceptMatch: boolean;
  availableSlots: number;
  maxMatches: number;
  activeMatches: number;
}

export interface CreatedMatch {
  matchId: string;
  gatewayHost: string;
  gatewayPort: number;
}

/** El Game Server no tiene capacidad para una partida más. */
export class NoCapacityError extends Error {
  constructor() {
    super("El Game Server no tiene capacidad para otra partida.");
    this.name = "NoCapacityError";
  }
}

/** Error genérico hablando con el ControlApi del Game Server. */
export class ControlClientError extends Error {
  constructor(message: string, readonly status?: number) {
    super(message);
    this.name = "ControlClientError";
  }
}

/**
 * Cliente del plano de control del Game Server (Unity). El backend lo usa para
 * preguntar capacidad y para pedir crear/parar partidas. Es una interfaz para poder
 * inyectar un doble en los tests sin un Game Server real.
 */
export interface GameServerControlClient {
  getCapacity(): Promise<CapacityInfo>;
  createMatch(config: MatchConfig): Promise<CreatedMatch>;
  stopMatch(matchId: string): Promise<void>;
}

export interface HttpControlClientOptions {
  baseUrl: string;
  token?: string;
  timeoutMs?: number;
}

/** Implementación real sobre HTTP (fetch) contra el ControlApi del Game Server. */
export class HttpGameServerControlClient implements GameServerControlClient {
  private readonly baseUrl: string;
  private readonly token?: string;
  private readonly timeoutMs: number;

  constructor(options: HttpControlClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, "");
    this.token = options.token && options.token.length > 0 ? options.token : undefined;
    this.timeoutMs = options.timeoutMs ?? 5000;
  }

  private headers(): Record<string, string> {
    const h: Record<string, string> = { "content-type": "application/json" };
    if (this.token) {
      h["x-control-token"] = this.token;
    }
    return h;
  }

  private async request(path: string, init: RequestInit): Promise<Response> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      return await fetch(`${this.baseUrl}${path}`, { ...init, signal: controller.signal });
    } catch (err) {
      throw new ControlClientError(
        `No se pudo contactar con el Game Server en ${this.baseUrl}${path}: ${(err as Error).message}`
      );
    } finally {
      clearTimeout(timer);
    }
  }

  async getCapacity(): Promise<CapacityInfo> {
    const res = await this.request("/capacity", { method: "GET" });
    if (!res.ok) {
      throw new ControlClientError("Fallo consultando capacidad", res.status);
    }
    const body: any = await res.json();
    return {
      canAcceptMatch: Boolean(body.canAcceptMatch),
      availableSlots: Number(body.availableSlots ?? 0),
      maxMatches: Number(body.maxMatches ?? 0),
      activeMatches: Number(body.activeMatches ?? 0)
    };
  }

  async createMatch(config: MatchConfig): Promise<CreatedMatch> {
    const res = await this.request("/control/create-match", {
      method: "POST",
      headers: this.headers(),
      body: JSON.stringify(config)
    });

    if (res.status === 409) {
      throw new NoCapacityError();
    }
    if (!res.ok) {
      throw new ControlClientError("Fallo creando partida", res.status);
    }

    const body: any = await res.json();
    if (!body.matchId) {
      throw new ControlClientError("El Game Server no devolvió matchId");
    }
    return {
      matchId: String(body.matchId),
      gatewayHost: String(body.gatewayHost ?? ""),
      gatewayPort: Number(body.gatewayPort ?? 0)
    };
  }

  async stopMatch(matchId: string): Promise<void> {
    const res = await this.request("/control/stop-match", {
      method: "POST",
      headers: this.headers(),
      body: JSON.stringify({ matchId })
    });
    // 404 = la partida ya no existe; lo tratamos como éxito idempotente.
    if (!res.ok && res.status !== 404) {
      throw new ControlClientError("Fallo deteniendo partida", res.status);
    }
  }
}
