import { randomBytes, randomUUID } from "crypto";

/** Identificador con prefijo legible, p. ej. room_3f9a1c... */
export function genId(prefix: string): string {
  return `${prefix}_${randomUUID().replace(/-/g, "")}`;
}

/** Código corto de sala, fácil de compartir por voz/chat (sin caracteres ambiguos). */
export function genRoomCode(length = 6): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sin I, O, 0, 1
  const bytes = randomBytes(length);
  let code = "";
  for (let i = 0; i < length; i++) {
    code += alphabet[bytes[i] % alphabet.length];
  }
  return code;
}
