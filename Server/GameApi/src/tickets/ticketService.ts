import { genId } from "../ids";
import { JoinTicket } from "./types";
import { TicketRepository } from "./ticketRepository";

export interface TicketServiceOptions {
  ttlSeconds?: number;
}

/**
 * Emite y consume tickets de unión. En el lanzamiento de partida genera un ticket
 * por jugador con una caducidad corta (suficiente para que el cliente abra la
 * conexión realtime, no más).
 */
export class TicketService {
  private readonly ttlSeconds: number;

  constructor(
    private readonly repo: TicketRepository,
    options: TicketServiceOptions = {}
  ) {
    this.ttlSeconds = options.ttlSeconds ?? 120;
  }

  async issueForMatch(matchId: string, playerIds: string[]): Promise<JoinTicket[]> {
    const now = Date.now();
    const tickets: JoinTicket[] = playerIds.map((playerId) => ({
      ticketId: genId("ticket"),
      matchId,
      playerId,
      issuedAt: new Date(now).toISOString(),
      expiresAt: new Date(now + this.ttlSeconds * 1000).toISOString()
    }));

    for (const ticket of tickets) {
      await this.repo.create(ticket, this.ttlSeconds);
    }

    return tickets;
  }

  /** Consumo atómico (lo usará el Game Server en la Fase 2). */
  async consume(ticketId: string): Promise<JoinTicket | null> {
    return this.repo.consume(ticketId);
  }
}
