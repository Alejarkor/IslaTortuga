import { Router, Request, Response } from "express";
import { RedisLike } from "../redis";
import {
  GameServerControlClient,
  NoCapacityError,
  ControlClientError
} from "../gameserver/controlClient";
import { TicketRepository } from "../tickets/ticketRepository";
import { TicketService } from "../tickets/ticketService";
import { RoomError } from "./errors";
import { RoomRepository } from "./roomRepository";
import { RoomService, RoomServiceOptions } from "./roomService";
import { RoomSyncAdapter } from "./roomSyncAdapter";

export interface RoomServices {
  rooms: RoomRepository;
  tickets: TicketService;
  service: RoomService;
}

/** Construye el grafo de objetos de salas/tickets a partir de Redis y el control client. */
export function buildRoomServices(
  redis: RedisLike,
  control: GameServerControlClient,
  options: RoomServiceOptions = {}
): RoomServices {
  const rooms = new RoomRepository(redis);
  const ticketRepo = new TicketRepository(redis);
  const tickets = new TicketService(ticketRepo);
  const sync = new RoomSyncAdapter(rooms);
  const service = new RoomService(rooms, tickets, control, sync, options);
  return { rooms, tickets, service };
}

function handle(fn: (req: Request, res: Response) => Promise<void>) {
  return async (req: Request, res: Response) => {
    try {
      await fn(req, res);
    } catch (err) {
      if (err instanceof RoomError) {
        return res.status(err.httpStatus).json({ ok: false, error: err.message });
      }
      if (err instanceof NoCapacityError) {
        return res.status(409).json({ ok: false, error: err.message });
      }
      if (err instanceof ControlClientError) {
        return res.status(502).json({ ok: false, error: err.message });
      }
      console.error(err);
      return res.status(500).json({ ok: false, error: "internal server error" });
    }
  };
}

export function createRoomsRouter(services: RoomServices): Router {
  const router = Router();
  const { service } = services;

  router.post(
    "/internal/rooms",
    handle(async (req, res) => {
      const { hostPlayerId, nickname, maxPlayers, mapId, isPrivate } = req.body ?? {};
      if (!hostPlayerId || !nickname) {
        res.status(400).json({ ok: false, error: "hostPlayerId y nickname son obligatorios" });
        return;
      }
      const room = await service.createRoom({ hostPlayerId, nickname, maxPlayers, mapId, isPrivate });
      res.status(201).json({ ok: true, room });
    })
  );

  router.get(
    "/internal/rooms",
    handle(async (_req, res) => {
      const rooms = await service.listJoinableRooms();
      res.json({ ok: true, rooms });
    })
  );

  router.post(
    "/internal/rooms/join-by-code",
    handle(async (req, res) => {
      const { code, playerId, nickname } = req.body ?? {};
      if (!code || !playerId || !nickname) {
        res.status(400).json({ ok: false, error: "code, playerId y nickname son obligatorios" });
        return;
      }
      const room = await service.joinByCode(code, { playerId, nickname });
      res.json({ ok: true, room });
    })
  );

  router.get(
    "/internal/rooms/:roomId",
    handle(async (req, res) => {
      const room = await service.getRoom(req.params.roomId);
      res.json({ ok: true, room });
    })
  );

  router.post(
    "/internal/rooms/:roomId/join",
    handle(async (req, res) => {
      const { playerId, nickname } = req.body ?? {};
      if (!playerId || !nickname) {
        res.status(400).json({ ok: false, error: "playerId y nickname son obligatorios" });
        return;
      }
      const room = await service.joinRoom(req.params.roomId, { playerId, nickname });
      res.json({ ok: true, room });
    })
  );

  router.post(
    "/internal/rooms/:roomId/leave",
    handle(async (req, res) => {
      const { playerId } = req.body ?? {};
      if (!playerId) {
        res.status(400).json({ ok: false, error: "playerId es obligatorio" });
        return;
      }
      const room = await service.leaveRoom(req.params.roomId, playerId);
      res.json({ ok: true, room });
    })
  );

  router.post(
    "/internal/rooms/:roomId/ready",
    handle(async (req, res) => {
      const { playerId, ready } = req.body ?? {};
      if (!playerId || typeof ready !== "boolean") {
        res.status(400).json({ ok: false, error: "playerId y ready (boolean) son obligatorios" });
        return;
      }
      const room = await service.setReady(req.params.roomId, playerId, ready);
      res.json({ ok: true, room });
    })
  );

  router.post(
    "/internal/rooms/:roomId/launch",
    handle(async (req, res) => {
      const { playerId } = req.body ?? {};
      if (!playerId) {
        res.status(400).json({ ok: false, error: "playerId (creador) es obligatorio" });
        return;
      }
      const result = await service.launch(req.params.roomId, playerId);
      res.json({
        ok: true,
        matchId: result.matchId,
        gateway: result.gateway,
        room: result.room,
        tickets: result.tickets
      });
    })
  );

  return router;
}
