import { describe, it, expect } from "vitest";
import { InMemoryRedis } from "../testing/inMemoryRedis";
import { TicketRepository } from "./ticketRepository";
import { JoinTicket } from "./types";

function makeTicket(id: string): JoinTicket {
  const now = Date.now();
  return {
    ticketId: id,
    matchId: "match_1",
    playerId: "player_1",
    issuedAt: new Date(now).toISOString(),
    expiresAt: new Date(now + 120000).toISOString()
  };
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe("TicketRepository", () => {
  it("consume() es atómico: la segunda llamada con el mismo ticket devuelve null", async () => {
    const redis = new InMemoryRedis();
    const repo = new TicketRepository(redis);
    await repo.create(makeTicket("ticket_a"), 120);

    const first = await repo.consume("ticket_a");
    const second = await repo.consume("ticket_a");

    expect(first?.ticketId).toBe("ticket_a");
    expect(second).toBeNull();
  });

  it("guarda el ticket con TTL", async () => {
    const redis = new InMemoryRedis();
    const repo = new TicketRepository(redis);
    await repo.create(makeTicket("ticket_b"), 90);

    const ttl = redis.ttlSeconds("ticket:ticket_b");
    expect(ttl).not.toBeNull();
    expect(ttl!).toBeGreaterThan(80);
    expect(ttl!).toBeLessThanOrEqual(90);
  });

  it("un ticket caducado no se puede consumir", async () => {
    const redis = new InMemoryRedis();
    const repo = new TicketRepository(redis);
    // Forzamos una expiración muy corta a través del propio Redis fake.
    await redis.set("ticket:ticket_c", JSON.stringify(makeTicket("ticket_c")), "PX", 10);
    await sleep(30);

    expect(await repo.consume("ticket_c")).toBeNull();
  });

  it("consumir un ticket inexistente devuelve null", async () => {
    const repo = new TicketRepository(new InMemoryRedis());
    expect(await repo.consume("no-existe")).toBeNull();
  });
});
