import { RoomState } from "./types";

/** Error de transición de estado inválida. */
export class RoomStateError extends Error {
  constructor(from: RoomState, to: RoomState) {
    super(`Transición de sala inválida: ${from} -> ${to}`);
    this.name = "RoomStateError";
  }
}

/**
 * Transiciones permitidas. El creador puede lanzar desde waiting (o ready_check),
 * sin requerir que todos estén "ready". El camino es
 * waiting/ready_check -> starting -> in_game -> finished, con cancelación en pre-juego.
 */
const TRANSITIONS: Record<RoomState, RoomState[]> = {
  waiting: ["ready_check", "starting", "cancelled"],
  ready_check: ["waiting", "starting", "cancelled"],
  starting: ["in_game", "ready_check", "waiting", "cancelled"],
  in_game: ["finished"],
  finished: [],
  cancelled: []
};

export class RoomStateMachine {
  static canTransition(from: RoomState, to: RoomState): boolean {
    return TRANSITIONS[from]?.includes(to) ?? false;
  }

  static assertTransition(from: RoomState, to: RoomState): void {
    if (!RoomStateMachine.canTransition(from, to)) {
      throw new RoomStateError(from, to);
    }
  }

  static isTerminal(state: RoomState): boolean {
    return TRANSITIONS[state].length === 0;
  }
}
