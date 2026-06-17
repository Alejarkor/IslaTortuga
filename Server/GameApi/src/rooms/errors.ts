/** Error de dominio de salas, con un código HTTP sugerido para las rutas. */
export class RoomError extends Error {
  constructor(message: string, readonly httpStatus = 400) {
    super(message);
    this.name = "RoomError";
  }
}

export class RoomNotFoundError extends RoomError {
  constructor() {
    super("Sala no encontrada", 404);
    this.name = "RoomNotFoundError";
  }
}

export class RoomFullError extends RoomError {
  constructor() {
    super("La sala está llena", 409);
    this.name = "RoomFullError";
  }
}

export class AlreadyMemberError extends RoomError {
  constructor() {
    super("El jugador ya está en la sala", 409);
    this.name = "AlreadyMemberError";
  }
}

export class NotMemberError extends RoomError {
  constructor() {
    super("El jugador no está en la sala", 404);
    this.name = "NotMemberError";
  }
}

export class CannotLaunchError extends RoomError {
  constructor(reason: string) {
    super(`No se puede lanzar la partida: ${reason}`, 409);
    this.name = "CannotLaunchError";
  }
}
