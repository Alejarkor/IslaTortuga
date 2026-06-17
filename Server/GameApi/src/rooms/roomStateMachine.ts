import { RoomState } from "./types";

/** Error de transición de estado inválida. */
export class RoomStateError extends Error {
  constructor(from: RoomState, to: RoomState) {
    super(`Transición de sala inválida: ${from} -> ${to}`);
    this.name = "RoomStateError";
  }
}

/**
 * Transiciones permitidas. El camino feliz es
 * waiting -> ready_check -> starting -> in_game -> finished. Desde los estados de
 * pre-juego se puede cancelar. Los estados finished y cancelled son terminales.
 */
const TRANSITIONS: Record<RoomState, RoomState[]> = {
  waiting: ["ready_check", "cancelled"],
  ready_check: ["waiting", "starting", "cancelled"],
  starting: ["in_game", "ready_check", "cancelled"],
  in_game: ["finished"],
  finished: [],
  cancelled: []
};

export class RoomStateMachine {
  static canTransition(from: RoomState, to: RoomState): boolean {
    return TRANSITIONS[from]?.includes(to) ?? false;
  }

  /** Lanza RoomStateError si la transición no está permitida. */
  static assertTransition(from: RoomState, to: RoomState): void {
    if (!RoomStateMachine.canTransition(from, to)) {
      throw new RoomStateError(from, to);
    }
  }

  static isTerminal(state: RoomState): boolean {
    return TRANSITIONS[state].length === 0;
  }
}
