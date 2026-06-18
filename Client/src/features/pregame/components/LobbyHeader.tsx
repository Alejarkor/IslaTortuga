import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";

import { useAuth } from "@/features/auth/useAuth";
import { fetchStats } from "@/api/profile.api";
import {
  MailIcon,
  BellIcon,
  SettingsIcon,
  LogoutIcon
} from "@/features/auth/PirateIcons";
import { BrandEmblem } from "@/skin/BrandEmblem";

/**
 * Cabecera del lobby: identidad + estadísticas reales (partidas/victorias),
 * logo central y acciones. Sin datos inventados de economía/nivel.
 */
export function LobbyHeader() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();

  const statsQuery = useQuery({
    queryKey: ["stats"],
    queryFn: ({ signal }) => fetchStats(signal)
  });
  const stats = statsQuery.data?.stats;

  const nickname = session?.nickname ?? "Pirata";
  const initial = nickname.charAt(0).toUpperCase();

  const played = stats?.games_played ?? 0;
  const won = stats?.games_won ?? 0;
  const winRate = played > 0 ? Math.round((won / played) * 100) : 0;

  const onLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  return (
    <header className="lobby-header wood-frame">
      <div className="lobby-id">
        <div className="lobby-avatar">{initial}</div>
        <div>
          <p className="lobby-id__name">{nickname}</p>
          <p className="lobby-id__level">
            {played} partidas · {won} victorias
          </p>
          <div className="xp-bar">
            <div className="xp-bar__track">
              <div className="xp-bar__fill" style={{ width: `${winRate}%` }} />
            </div>
            <span className="xp-bar__text">{winRate}% victorias</span>
          </div>
        </div>
      </div>

      <div className="lobby-logo">
        <BrandEmblem className="lobby-logo__emblem" />
      </div>

      <div className="lobby-actions">
        <button className="icon-btn" aria-label="Mensajes" title="Mensajes">
          <MailIcon />
        </button>
        <button className="icon-btn" aria-label="Notificaciones" title="Notificaciones">
          <BellIcon />
        </button>
        <button className="icon-btn" aria-label="Ajustes" title="Ajustes">
          <SettingsIcon />
        </button>
        <button
          className="icon-btn"
          aria-label="Cerrar sesión"
          title="Cerrar sesión"
          onClick={onLogout}
        >
          <LogoutIcon />
        </button>
      </div>
    </header>
  );
}
