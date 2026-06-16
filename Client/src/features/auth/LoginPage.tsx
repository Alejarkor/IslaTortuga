import { useState, type FormEvent, type CSSProperties } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { login } from "@/api/auth.api";
import { ApiError } from "@/api/httpClient";
import { AUTH_ME_KEY } from "./useAuth";
import {
  UserIcon,
  LockIcon,
  EyeIcon,
  EyeOffIcon,
  WheelIcon
} from "./PirateIcons";
import "@/styles/login.css";
import { useLoginAssets } from "./useLoginAssets";
import { BrandEmblem } from "@/skin/BrandEmblem";

export function LoginPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [usernameOrEmail, setUsernameOrEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  const { data: loginAssets } = useLoginAssets();
  const screenStyle: CSSProperties | undefined = loginAssets?.backgroundUrl
    ? ({ "--login-bg-image": `url("${loginAssets.backgroundUrl}")` } as unknown as CSSProperties)
    : undefined;
  const cardStyle: CSSProperties | undefined = loginAssets?.panelUrl
    ? ({ "--login-panel-image": `url("${loginAssets.panelUrl}")` } as unknown as CSSProperties)
    : undefined;
  const cardClass = loginAssets?.panelUrl
    ? "login-card login-card--image"
    : "login-card";

  const mutation = useMutation({
    mutationFn: () => login({ usernameOrEmail, password }),
    onSuccess: (data) => {
      // Volcamos la sesión en caché desde la respuesta para que la ruta
      // protegida la vea de inmediato (evita tener que recargar el login).
      queryClient.setQueryData(AUTH_ME_KEY, {
        userId: data.user.user_id,
        playerId: data.profile.player_id,
        username: data.user.username,
        nickname: data.profile.nickname
      });
      navigate("/pre-game", { replace: true });
    }
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    mutation.mutate();
  };

  const errorMessage =
    mutation.error instanceof ApiError
      ? mutation.error.message
      : mutation.error
        ? "No se pudo iniciar sesión."
        : null;

  return (
    <div className="login-screen" style={screenStyle}>
      <form className={cardClass} style={cardStyle} onSubmit={onSubmit}>
        <span className="login-card__corner login-card__corner--tl" />
        <span className="login-card__corner login-card__corner--tr" />
        <span className="login-card__corner login-card__corner--bl" />
        <span className="login-card__corner login-card__corner--br" />

        <div className="login-brand">
          <BrandEmblem className="login-brand__emblem" />
          <h1 className="login-brand__title">
            <span>ISLA</span>
            <span className="is-big">TORTUGA</span>
          </h1>
        </div>

        <div className="login-rule">
          <span>&#9670;</span>
        </div>

        <p className="login-subtitle">Inicia sesión para continuar tu aventura</p>

        {errorMessage && <p className="login-error">{errorMessage}</p>}

        <div className="login-field">
          <label className="login-field__label" htmlFor="login-user">
            Usuario o email
          </label>
          <div className="login-input">
            <UserIcon className="login-input__icon" />
            <input
              id="login-user"
              type="text"
              autoComplete="username"
              placeholder="Escribe tu usuario o email"
              value={usernameOrEmail}
              onChange={(e) => setUsernameOrEmail(e.target.value)}
              required
            />
          </div>
        </div>

        <div className="login-field">
          <label className="login-field__label" htmlFor="login-pass">
            Contraseña
          </label>
          <div className="login-input">
            <LockIcon className="login-input__icon" />
            <input
              id="login-pass"
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              placeholder="Escribe tu contraseña"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            <button
              type="button"
              className="login-input__toggle"
              onClick={() => setShowPassword((v) => !v)}
              aria-label={
                showPassword ? "Ocultar contraseña" : "Mostrar contraseña"
              }
            >
              {showPassword ? (
                <EyeOffIcon className="login-input__icon" />
              ) : (
                <EyeIcon className="login-input__icon" />
              )}
            </button>
          </div>
        </div>

        <button
          type="submit"
          className="login-submit"
          disabled={mutation.isPending}
        >
          <WheelIcon className="login-submit__icon" />
          {mutation.isPending ? "Entrando..." : "Entrar"}
        </button>

        <p className="login-foot">
          ¿No tienes cuenta? <Link to="/register">Crea una</Link>
        </p>
      </form>
    </div>
  );
}
