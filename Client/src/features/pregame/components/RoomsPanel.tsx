import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { RefreshIcon, PlusIcon } from "@/features/auth/PirateIcons";
import { ApiError } from "@/api/httpClient";
import { useAuth } from "@/features/auth/useAuth";
import {
  fetchRooms,
  fetchRoom,
  createRoom,
  joinRoom,
  joinRoomByCode,
  leaveRoom,
  setReady,
  launchRoom
} from "@/api/rooms.api";
import type { Room, LaunchResponse, RoomResponse } from "@/types/api";

type Tab = "publicas" | "privadas";

function errText(err: unknown): string | null {
  if (!err) return null;
  return err instanceof ApiError ? err.message : "Algo salió mal. Inténtalo de nuevo.";
}

/**
 * Panel de Salas conectado al backend (GameApi vía WebServer).
 * Sin sala activa: lista de salas públicas + crear + unirse por código.
 * Con sala activa: lista de miembros, ready/leave y lanzar (solo el host).
 */
export function RoomsPanel() {
  const { session } = useAuth();
  const myId = session?.playerId;
  const qc = useQueryClient();

  const [tab, setTab] = useState<Tab>("publicas");
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [launchInfo, setLaunchInfo] = useState<LaunchResponse | null>(null);

  const roomsQuery = useQuery({
    queryKey: ["rooms"],
    queryFn: ({ signal }) => fetchRooms(signal),
    enabled: !activeRoomId,
    refetchInterval: activeRoomId ? false : 5000
  });

  const roomQuery = useQuery({
    queryKey: ["room", activeRoomId],
    queryFn: ({ signal }) => fetchRoom(activeRoomId as string, signal),
    enabled: !!activeRoomId,
    refetchInterval: 2500
  });
  const room: Room | null = roomQuery.data?.room ?? null;

  const enterRoom = (r: Room) => {
    setLaunchInfo(null);
    setActiveRoomId(r.roomId);
    qc.setQueryData(["room", r.roomId], { ok: true, room: r } satisfies RoomResponse);
  };

  const createMut = useMutation({
    mutationFn: () => createRoom({ maxPlayers: 4, mapId: "beach_map_01" }),
    onSuccess: (res) => enterRoom(res.room)
  });
  const joinMut = useMutation({
    mutationFn: (roomId: string) => joinRoom(roomId),
    onSuccess: (res) => enterRoom(res.room)
  });
  const joinCodeMut = useMutation({
    mutationFn: () => joinRoomByCode(code.trim()),
    onSuccess: (res) => {
      setCode("");
      enterRoom(res.room);
    }
  });
  const readyMut = useMutation({
    mutationFn: (ready: boolean) => setReady(activeRoomId as string, ready),
    onSuccess: (res) =>
      qc.setQueryData(["room", activeRoomId], { ok: true, room: res.room } satisfies RoomResponse)
  });
  const leaveMut = useMutation({
    mutationFn: () => leaveRoom(activeRoomId as string),
    onSuccess: () => {
      setActiveRoomId(null);
      setLaunchInfo(null);
      qc.invalidateQueries({ queryKey: ["rooms"] });
    }
  });
  const launchMut = useMutation({
    mutationFn: () => launchRoom(activeRoomId as string),
    onSuccess: (res) => {
      setLaunchInfo(res);
      qc.setQueryData(["room", activeRoomId], { ok: true, room: res.room } satisfies RoomResponse);
    }
  });

  // ---------- Vista: dentro de una sala ----------
  if (activeRoomId && room) {
    const me = room.members.find((m) => m.playerId === myId);
    const isHost = room.hostPlayerId === myId;
    const allReady = room.members.length > 0 && room.members.every((m) => m.isReady);
    const myTicket = launchInfo?.tickets.find((t) => t.playerId === myId);

    return (
      <div className="lobby-panel wood-frame">
        <div className="lobby-banner">Sala {room.code}</div>
        <div className="parch lobby-panel__inner">
          <p className="room-info__meta">
            {room.mapId} · {room.members.length}/{room.maxPlayers} · estado: {room.state}
          </p>

          <div className="rooms-list">
            {room.members.map((m) => (
              <div key={m.playerId} className="room-row">
                <span className="room-icon">{m.role === "master" ? "👑" : "🏴‍☠️"}</span>
                <div className="room-info">
                  <p className="room-info__name">
                    {m.nickname}
                    {m.playerId === myId ? " (tú)" : ""}
                  </p>
                  <p className="room-info__meta">{m.isReady ? "Listo" : "No listo"}</p>
                </div>
                <span
                  className={`friend-av__dot ${m.isReady ? "" : "friend-av__dot--off"}`}
                />
              </div>
            ))}
          </div>

          {launchInfo && (
            <p className="friend-info__status">
              ¡Partida creada! match <code>{launchInfo.matchId}</code>
              {myTicket ? <> · tu ticket: <code>{myTicket.ticketId}</code></> : null}
            </p>
          )}

          {errText(readyMut.error) && <p className="form-error">{errText(readyMut.error)}</p>}
          {errText(launchMut.error) && <p className="form-error">{errText(launchMut.error)}</p>}

          <div className="rooms-foot">
            <button
              className="big-btn big-btn--gold"
              disabled={!me || readyMut.isPending}
              onClick={() => readyMut.mutate(!me?.isReady)}
            >
              {me?.isReady ? "Marcar NO listo" : "Marcar listo"}
            </button>

            {isHost && (
              <button
                className="big-btn big-btn--play"
                disabled={!allReady || room.state === "in_game" || launchMut.isPending}
                onClick={() => launchMut.mutate()}
                title={allReady ? "Lanzar partida" : "Faltan jugadores por estar listos"}
              >
                ⚓ {launchMut.isPending ? "LANZANDO…" : "LANZAR PARTIDA"}
              </button>
            )}

            <button
              className="mini-btn"
              disabled={leaveMut.isPending}
              onClick={() => leaveMut.mutate()}
            >
              Salir de la sala
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ---------- Vista: lista de salas ----------
  const rooms = roomsQuery.data?.rooms ?? [];

  return (
    <div className="lobby-panel wood-frame">
      <div className="lobby-banner">Salas</div>
      <div className="parch lobby-panel__inner">
        <div className="seg-tabs">
          <button
            className={`seg-tab ${tab === "publicas" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("publicas")}
          >
            Salas públicas
          </button>
          <button
            className={`seg-tab ${tab === "privadas" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("privadas")}
          >
            Salas privadas
          </button>
        </div>

        <div className="rooms-filters">
          <button
            className="icon-btn"
            aria-label="Actualizar"
            title="Actualizar"
            onClick={() => roomsQuery.refetch()}
          >
            <RefreshIcon />
          </button>
        </div>

        <div className="rooms-list">
          {tab === "publicas" && roomsQuery.isLoading && (
            <p className="friend-info__status">Cargando salas…</p>
          )}
          {tab === "publicas" && roomsQuery.isError && (
            <p className="form-error">No se pudieron cargar las salas.</p>
          )}
          {tab === "publicas" && !roomsQuery.isLoading && rooms.length === 0 && (
            <p className="friend-info__status">No hay salas públicas abiertas. ¡Crea una!</p>
          )}

          {tab === "publicas" &&
            rooms.map((r) => (
              <div key={r.roomId} className="room-row">
                <span className="room-icon">☠</span>
                <div className="room-info">
                  <p className="room-info__name">Sala {r.code}</p>
                  <p className="room-info__meta">{r.mapId}</p>
                </div>
                <span className="room-count">
                  👤 {r.members.length}/{r.maxPlayers}
                </span>
                <button
                  className="mini-btn"
                  disabled={joinMut.isPending}
                  onClick={() => joinMut.mutate(r.roomId)}
                >
                  Unirse
                </button>
              </div>
            ))}

          {tab === "privadas" && (
            <p className="friend-info__status">
              Usa un código para unirte a una sala privada.
            </p>
          )}
        </div>

        {(errText(createMut.error) || errText(joinMut.error) || errText(joinCodeMut.error)) && (
          <p className="form-error">
            {errText(createMut.error) || errText(joinMut.error) || errText(joinCodeMut.error)}
          </p>
        )}

        <div className="rooms-foot">
          <button
            className="big-btn big-btn--gold"
            disabled={createMut.isPending}
            onClick={() => createMut.mutate()}
          >
            <PlusIcon /> {createMut.isPending ? "Creando…" : "Crear sala"}
          </button>
          <div className="code-join color-subpanel">
            <input
              aria-label="Código de la sala"
              placeholder="Ingresa el código de la sala…"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
            />
            <button
              className="mini-btn"
              disabled={code.trim().length === 0 || joinCodeMut.isPending}
              onClick={() => joinCodeMut.mutate()}
            >
              Unirse
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
