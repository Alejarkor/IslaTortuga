import { useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { fetchFriends, fetchIncomingRequests } from "@/api/friends.api";
import { SendIcon } from "@/features/auth/PirateIcons";

type Tab = "amigos" | "solicitudes";

/** Mensajes de chat de ejemplo (no hay backend de chat todavía). */
const MOCK_CHAT = [
  { who: "CapitánRayo", time: "21:03", text: "¿Listos para una aventura?" },
  { who: "Tú", time: "21:03", text: "¡Siempre! ¿Sala o creamos una?" },
  { who: "MarinaAzul", time: "21:04", text: "Creo una sala en 2 min, aviso." }
];

/**
 * Panel izquierdo: Amigos y Chat.
 */
export function FriendsPanel() {
  const [tab, setTab] = useState<Tab>("amigos");

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
            {requests.length > 0 && (
              <span className="seg-badge">{requests.length}</span>
            )}
          </button>
        </div>

        {tab === "amigos" ? (
          <div className="friends">
            <p className="friends-group__title">
              Amigos ({friends.length})
            </p>
            {friends.length === 0 && (
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
                <button className="mini-btn">Invitar</button>
              </div>
            ))}
          </div>
        ) : (
          <div className="friends">
            <p className="friends-group__title">
              Solicitudes ({requests.length})
            </p>
            {requests.length === 0 && (
              <p className="friend-info__status">Sin solicitudes pendientes.</p>
            )}
            {requests.map((r) => (
              <div key={r.friend_request_id} className="friend-row">
                <span className="friend-av">
                  {r.from_nickname.charAt(0).toUpperCase()}
                </span>
                <div className="friend-info">
                  <p className="friend-info__name">{r.from_nickname}</p>
                  <p className="friend-info__status">Quiere ser tu amigo</p>
                </div>
                <button className="mini-btn">Aceptar</button>
              </div>
            ))}
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
            <input placeholder="Escribe un mensaje…" />
            <button className="mini-btn mini-btn--send" aria-label="Enviar">
              <SendIcon />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
