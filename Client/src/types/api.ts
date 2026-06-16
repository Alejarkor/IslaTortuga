/**
 * Tipos de las respuestas del WebServer.
 * Reflejan los contratos reales expuestos en Server/WebServer/src/index.ts.
 */

export type SessionPayload = {
  userId: string;
  playerId: string;
  username: string;
  nickname: string;
};

export type MeResponse = {
  ok: boolean;
  session: SessionPayload;
};

export type PlayerProfile = {
  player_id: string;
  user_id: string;
  nickname: string;
  appearance_json: unknown;
  created_at?: string;
  updated_at?: string;
};

export type ProfileResponse = {
  ok: boolean;
  profile: PlayerProfile;
};

export type PlayerStats = {
  player_id: string;
  games_played: number;
  games_won: number;
  games_lost: number;
  total_play_time_seconds: number;
  stats_json: Record<string, unknown>;
  updated_at?: string;
};

export type StatsResponse = {
  ok: boolean;
  stats: PlayerStats;
};

export type AuthResponse = {
  ok: boolean;
  user: {
    user_id: string;
    username: string;
    email: string;
    status: string;
  };
  profile: {
    player_id: string;
    user_id: string;
    nickname: string;
    appearance_json?: unknown;
  };
  error?: string;
};

/** Un fichero dentro de un manifest de assets. */
export type ManifestFile = {
  assetFileId: string;
  assetKey: string;
  assetType: string;
  version: string;
  hash: string;
  sizeBytes: number;
  mimeType: string;
  downloadUrl: string;
  required: boolean;
  loadPriority: number;
  /** Etiqueta de uso lógico: "body", "body_mask", "hair", "hair_preview"... */
  usage: string | null;
};

export type ManifestResponse = {
  ok: boolean;
  manifestId: string;
  name: string;
  version: string;
  targetType: string;
  targetId: string;
  isCurrent: boolean;
  publishedAt: string | null;
  files: ManifestFile[];
};
