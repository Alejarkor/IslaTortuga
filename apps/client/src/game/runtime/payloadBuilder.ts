import type { JoinGamePayload, PlayerInputPayload } from './networkClient';

/**
 * Fábrica estática para construir los payloads que el cliente envía al servidor.
 * No serializa — la serialización ocurre en GameNetworkClient.send().
 */
export class PayloadBuilder {
  static joinGame(gameTicket: string): JoinGamePayload {
    return { gameTicket };
  }

  static playerInput(moveX: number, moveY: number, sequence?: number): PlayerInputPayload {
    return { moveX, moveY, sequence };
  }
}
