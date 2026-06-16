import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";

import { fetchProfile, fetchStats } from "@/api/profile.api";
import { useAuth } from "@/features/auth/useAuth";
import { Panel } from "@/ui/Panel";
import { Button } from "@/ui/Button";
import { Spinner } from "@/ui/Spinner";

/** Panel izquierdo: perfil, stats y sesión. */
export function ProfileStatsPanel() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();

  const profileQuery = useQuery({
    queryKey: ["profile"],
    queryFn: ({ signal }) => fetchProfile(signal)
  });

  const statsQuery = useQuery({
    queryKey: ["stats"],
    queryFn: ({ signal }) => fetchStats(signal)
  });

  const stats = statsQuery.data?.stats;

  const onLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  return (
    <Panel title="Perfil" className="panel--profile">
      <div className="profile-head">
        <div className="profile-avatar">
          {(session?.nickname ?? "?").charAt(0).toUpperCase()}
        </div>
        <div>
          <p className="profile-nickname">
            {profileQuery.data?.profile.nickname ?? session?.nickname ?? "—"}
          </p>
          <p className="profile-username">@{session?.username ?? ""}</p>
        </div>
      </div>

      <div className="stats-grid">
        {statsQuery.isLoading ? (
          <Spinner label="Cargando stats…" />
        ) : stats ? (
          <>
            <Stat label="Partidas" value={stats.games_played} />
            <Stat label="Ganadas" value={stats.games_won} />
            <Stat label="Perdidas" value={stats.games_lost} />
          </>
        ) : (
          <p className="muted">Sin estadísticas todavía.</p>
        )}
      </div>

      <Button variant="ghost" onClick={onLogout}>
        Cerrar sesión
      </Button>
    </Panel>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="stat">
      <span className="stat__value">{value}</span>
      <span className="stat__label">{label}</span>
    </div>
  );
}
