import { RoomRepository } from "./roomRepository";
import { RoomStateMachine } from "./roomStateMachine";
import { Room } from "./types";

/**
 * Mantiene sincronizado el estado de la sala con el ciclo de vida de la partida.
 * En esta fase su única responsabilidad es mover la sala a in_game cuando la
 * MatchInstance se ha creado. En fases posteriores aquí entrarán crashed/restoring
 * y finished.
 */
export class RoomSyncAdapter {
  constructor(private readonly rooms: RoomRepository) {}

  /** La partida se creó: la sala pasa de starting a in_game y guarda su matchId. */
  async onMatchCreated(room: Room, matchId: string): Promise<Room> {
    RoomStateMachine.assertTransition(room.state, "in_game");
    room.state = "in_game";
    room.matchId = matchId;
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }

  /** La partida terminó: la sala pasa a finished. */
  async onMatchFinished(room: Room): Promise<Room> {
    RoomStateMachine.assertTransition(room.state, "finished");
    room.state = "finished";
    room.updatedAt = new Date().toISOString();
    await this.rooms.save(room);
    return room;
  }
}
