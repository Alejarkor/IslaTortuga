/**
 * Ticket de unión a partida. Es temporal y de un solo uso: lo emite el backend al
 * lanzar la partida y lo consume el Game Server (Fase 2) cuando el cliente abre la
 * conexión realtime.
 */
export interface JoinTicket {
  ticketId: string;
  matchId: string;
  playerId: string;
  issuedAt: string; // ISO-8601
  expiresAt: string; // ISO-8601
}
