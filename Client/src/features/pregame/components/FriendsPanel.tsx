import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  fetchFriends,
  fetchIncomingRequests,
  sendFriendRequest,
  acceptFriendRequest,
  rejectFriendRequest
} from "@/api/friends.api";
import { ApiError } from "@/api/httpClient";
import { SendIcon } from "@/features/auth/PirateIcons";

type Tab = "amigos" | "solicitudes";

function errText(err: unknown): string | null {
  if (!err) return null;
  return err instanceof ApiError ? err.message : "Algo salió mal.";
}

/** Mensajes de chat de ejemplo — el chat en tiempo real llega en una fase posterior. */
const MOCK_CHAT = [
  { who: "CapitánRayo", time: "21:03", text: "¿Listos para una aventura?" },
  { who: "Tú", time: "21:03", text: "¡Siempre! ¿Sala o creamos una?" }
];

/**
 * Panel izquierdo: Amigos y Chat (el chat sigue siendo demo).
 */
export function FriendsPanel() {
  const [tab, setTab] = useState<Tab>("amigos");
  const [nick, setNick] = useState("");
  const [sentOk, setSentOk] = useState(false);
  const qc = useQueryClient();

  const friendsQuery = useQuery({
    queryKey: ["friends"],
    queryFn: ({ signal }) => fetchFriends(signal)
  });
  const requestsQuery = useQuery({
    queryKey: ["friends", "incoming"],
    queryFn: ({ signal }) => fetchIncomingRequests(signal)
  });

  const friends = friendsQuery.data?.friends ?? [];
  const requests = requestsQuery.data?.incomingRequests ?? [];

  const refreshSocial = () => {
    qc.invalidateQueries({ queryKey: ["friends"] });
  };

  const sendMut = useMutation({
    mutationFn: () => sendFriendRequest(nick.trim()),
    onSuccess: () => {
      setNick("");
      setSentOk(true);
    }
  });
  const acceptMut = useMutation({
    mutationFn: (requestId: string) => acceptFriendRequest(requestId),
    onSuccess: refreshSocial
  });
  const rejectMut = useMutation({
    mutationFn: (requestId: string) => rejectFriendRequest(requestId),
    onSuccess: refreshSocial
  });

  return (
    <div className="lobby-panel char-panel wood-frame">
      <div className="lobby-banner">Amigos y Chat</div>
      <div className="parch lobby-panel__inner">
        <div className="seg-tabs">
          <button
            className={`seg-tab ${tab === "amigos" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("amigos")}
          >
            Amigos
          </button>
          <button
            className={`seg-tab ${tab === "solicitudes" ? "seg-tab--active" : ""}`}
            onClick={() => setTab("solicitudes")}
          >
            Solicitudes
            {requests.length > 0 && <span className="seg-badge">{requests.length}</span>}
          </button>
        </div>

        {tab === "amigos" ? (
          <div className="friends">
            <div className="code-join color-subpanel">
              <input
                aria-label="Nickname del jugador"
                placeholder="Añadir amigo por nickname…"
                value={nick}
                onChange={(e) => {
                  setNick(e.target.value);
                  setSentOk(false);
                }}
              />
              <button
                className="mini-btn mini-btn--send"
                aria-label="Enviar solicitud"
                disabled={nick.trim().length === 0 || sendMut.isPending}
                onClick={() => sendMut.mutate()}
              >
                <SendIcon />
              </button>
            </div>
            {sentOk && <p className="friend-info__status">Solicitud enviada.</p>}
            {errText(sendMut.error) && <p className="form-error">{errText(sendMut.error)}</p>}

            <p className="friends-group__title">Amigos ({friends.length})</p>
            {friendsQuery.isLoading && (
              <p className="friend-info__status">Cargando amigos…</p>
            )}
            {friendsQuery.isError && (
              <p className="form-error">No se pudieron cargar tus amigos.</p>
            )}
            {!friendsQuery.isLoading && friends.length === 0 && (
              <p className="friend-info__status">Aún no tienes amigos.</p>
            )}
            {friends.map((f) => (
              <div key={f.player_id} className="friend-row">
                <span className="friend-av">
                  {f.nickname.charAt(0).toUpperCase()}
                  <span className="friend-av__dot friend-av__dot--off" />
                </span>
                <div className="friend-info">
                  <p className="friend-info__name">{f.nickname}</p>
                  <p className="friend-info__status">Desconectado</p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="friends">
            <p className="friends-group__title">Solicitudes ({requests.length})</p>
            {requestsQuery.isLoading && (
              <p className="friend-info__status">Cargando…</p>
            )}
            {!requestsQuery.isLoading && requests.length === 0 && (
              <p className="friend-info__status">Sin solicitudes pendientes.</p>
            )}
            {(errText(acceptMut.error) || errText(rejectMut.error)) && (
              <p className="form-error">
                {errText(acceptMut.error) || errText(rejectMut.error)}
              </p>
            )}
            {requests.map((r) => {
              const busy =
                (acceptMut.isPending && acceptMut.variables === r.friend_request_id) ||
                (rejectMut.isPending && rejectMut.variables === r.friend_request_id);
              return (
                <div key={r.friend_request_id} className="friend-row">
                  <span className="friend-av">
                    {r.from_nickname.charAt(0).toUpperCase()}
                  </span>
                  <div className="friend-info">
                    <p className="friend-info__name">{r.from_nickname}</p>
                    <p className="friend-info__status">Quiere ser tu amigo</p>
                  </div>
                  <button
                    className="mini-btn"
                    disabled={busy}
                    onClick={() => acceptMut.mutate(r.friend_request_id)}
                  >
                    Aceptar
                  </button>
                  <button
                    className="mini-btn"
                    disabled={busy}
                    onClick={() => rejectMut.mutate(r.friend_request_id)}
                  >
                    Rechazar
                  </button>
                </div>
              );
            })}
          </div>
        )}

        <div className="chat">
          <div className="chat-log color-subpanel color-subpanel--scroll">
            <div className="subpanel-scroll">
              {MOCK_CHAT.map((m, i) => (
                <div key={i} className="chat-msg">
                  <span className="chat-msg__who">{m.who}</span>
                  <span>{m.text}</span>
                  <span className="chat-msg__time">{m.time}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="chat-input">
            <input aria-label="Mensaje de chat" placeholder="Chat (próximamente)…" disabled />
            <button className="mini-btn mini-btn--send" aria-label="Enviar" disabled>
              <SendIcon />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
