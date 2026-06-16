import { useNavigate } from "react-router-dom";

import { useAuth } from "@/features/auth/useAuth";
import {
  MailIcon,
  BellIcon,
  SettingsIcon,
  LogoutIcon
} from "@/features/auth/PirateIcons";
import { BrandEmblem } from "@/skin/BrandEmblem";

/**
 * Cabecera del lobby: identidad + nivel/XP, logo central (imagen) y acciones.
 */
export function LobbyHeader() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();

  const nickname = session?.nickname ?? "Pirata";
  const initial = nickname.charAt(0).toUpperCase();

  const level = 12;
  const xp = 2350;
  const xpMax = 5000;
  const coins = 12450;
  const gems = 845;

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
          <p className="lobby-id__level">Corsario Nivel {level}</p>
          <div className="xp-bar">
            <div className="xp-bar__track">
              <div
                className="xp-bar__fill"
                style={{ width: `${Math.round((xp / xpMax) * 100)}%` }}
              />
            </div>
            <span className="xp-bar__text">
              {xp.toLocaleString("es")} / {xpMax.toLocaleString("es")}
            </span>
          </div>
        </div>
      </div>

      <div className="lobby-logo">
        <BrandEmblem className="lobby-logo__emblem" />
      </div>

      <div className="lobby-actions">
        <span className="currency">
          <span className="currency__coin" />
          {coins.toLocaleString("es")}
        </span>
        <span className="currency">
          <span className="currency__gem" />
          {gems.toLocaleString("es")}
        </span>
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
