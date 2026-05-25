import { FormEvent, useEffect, useState } from 'react';
import { Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { loginUser, registerUser, type UserDto } from './shared/http/apiClient';
import {
  clearAuth,
  loadCurrentUser,
  saveAuth,
} from './features/auth/authSession';
import { GameBootstrapPage } from './features/game-session/GameBootstrapPage';

function LoginPage({ onAuthSuccess }: { onAuthSuccess: (user: UserDto) => void }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const response = await loginUser({ email, password });
      saveAuth(response);
      onAuthSuccess(response.user);
      navigate('/portal');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo iniciar sesion');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="card">
        <p className="eyebrow">Isla Tortuga</p>
        <h1>Entrar</h1>
        <form onSubmit={handleSubmit} className="form">
          <label>
            Email
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" required />
          </label>
          <label>
            Contrasena
            <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" required />
          </label>
          {error && <p className="error">{error}</p>}
          <button disabled={loading}>{loading ? 'Entrando...' : 'Entrar'}</button>
        </form>
        <p className="muted">
          ¿No tienes cuenta? <Link to="/register">Crear cuenta</Link>
        </p>
      </section>
    </main>
  );
}

function RegisterPage({ onAuthSuccess }: { onAuthSuccess: (user: UserDto) => void }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [nickname, setNickname] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const response = await registerUser({ email, password, nickname });
      saveAuth(response);
      onAuthSuccess(response.user);
      navigate('/portal');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear la cuenta');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="card">
        <p className="eyebrow">Isla Tortuga</p>
        <h1>Crear cuenta</h1>
        <form onSubmit={handleSubmit} className="form">
          <label>
            Email
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" required />
          </label>
          <label>
            Nickname
            <input value={nickname} onChange={(event) => setNickname(event.target.value)} required />
          </label>
          <label>
            Contrasena
            <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" minLength={8} required />
          </label>
          {error && <p className="error">{error}</p>}
          <button disabled={loading}>{loading ? 'Creando...' : 'Crear cuenta'}</button>
        </form>
        <p className="muted">
          ¿Ya tienes cuenta? <Link to="/login">Entrar</Link>
        </p>
      </section>
    </main>
  );
}

function PortalPage({
  user,
  onLogout,
}: {
  user: UserDto;
  onLogout: () => void;
}) {
  const navigate = useNavigate();

  function handleLogout() {
    clearAuth();
    onLogout();
    navigate('/login');
  }

  return (
    <main className="portal-page">
      <section className="card portal-card">
        <p className="eyebrow">Portal</p>
        <h1>Bienvenido, {user.profile?.nickname ?? user.email}</h1>
        <p className="muted">Sesion validada contra la API. Ya puedes entrar al mapa Babylon.</p>
        <div className="actions">
          <button onClick={() => navigate('/game')}>Entrar al mundo de prueba</button>
          <button className="secondary" onClick={handleLogout}>Cerrar sesion</button>
        </div>
      </section>
    </main>
  );
}

function GamePage() {
  return <GameBootstrapPage />;
}

export function App() {
  const [user, setUser] = useState<UserDto | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);

  useEffect(() => {
    let cancelled = false;

    loadCurrentUser()
      .then((currentUser) => {
        if (!cancelled) {
          setUser(currentUser);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setCheckingSession(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  if (checkingSession) {
    return <main className="center-page">Comprobando sesion...</main>;
  }

  return (
    <Routes>
      <Route path="/" element={<Navigate to={user ? '/portal' : '/login'} replace />} />
      <Route
        path="/login"
        element={user ? <Navigate to="/portal" replace /> : <LoginPage onAuthSuccess={setUser} />}
      />
      <Route
        path="/register"
        element={user ? <Navigate to="/portal" replace /> : <RegisterPage onAuthSuccess={setUser} />}
      />
      <Route
        path="/portal"
        element={user ? <PortalPage user={user} onLogout={() => setUser(null)} /> : <Navigate to="/login" replace />}
      />
      <Route path="/game" element={user ? <GamePage /> : <Navigate to="/login" replace />} />
    </Routes>
  );
}
