import { RedisLike } from "../redis";
import { JoinTicket } from "./types";

const TICKET_KEY = (ticketId: string) => `ticket:${ticketId}`;

/**
 * Persistencia de tickets en Redis. Cada ticket se guarda con TTL (expiración
 * nativa de Redis) y se consume de forma atómica con GETDEL: la primera llamada
 * devuelve el ticket y lo borra; cualquier llamada posterior con el mismo id
 * devuelve null. Esto evita que un ticket se reutilice (doble conexión).
 */
export class TicketRepository {
  constructor(private readonly redis: RedisLike) {}

  async create(ticket: JoinTicket, ttlSeconds: number): Promise<void> {
    await this.redis.set(
      TICKET_KEY(ticket.ticketId),
      JSON.stringify(ticket),
      "EX",
      Math.max(1, Math.floor(ttlSeconds))
    );
  }

  /** Lee el ticket sin consumirlo (no atómico; útil para inspección/debug). */
  async peek(ticketId: string): Promise<JoinTicket | null> {
    const raw = await this.redis.get(TICKET_KEY(ticketId));
    return raw ? (JSON.parse(raw) as JoinTicket) : null;
  }

  /**
   * Consume el ticket de forma atómica. Devuelve el ticket la primera vez y null
   * en cualquier intento posterior (o si caducó / no existe).
   */
  async consume(ticketId: string): Promise<JoinTicket | null> {
    const raw = await this.redis.getdel(TICKET_KEY(ticketId));
    return raw ? (JSON.parse(raw) as JoinTicket) : null;
  }
}
