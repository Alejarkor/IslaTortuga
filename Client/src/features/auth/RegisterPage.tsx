import { useState, type FormEvent, type CSSProperties } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { register } from "@/api/auth.api";
import { ApiError } from "@/api/httpClient";
import { AUTH_ME_KEY } from "./useAuth";
import {
  UserIcon,
  MailIcon,
  FlagIcon,
  LockIcon,
  EyeIcon,
  EyeOffIcon,
  WheelIcon
} from "./PirateIcons";
import "@/styles/login.css";
import { useLoginAssets } from "./useLoginAssets";
import { BrandEmblem } from "@/skin/BrandEmblem";

export function RegisterPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [form, setForm] = useState({
    username: "",
    email: "",
    nickname: "",
    password: ""
  });
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

  const set =
    (key: keyof typeof form) => (e: { target: { value: string } }) =>
      setForm((prev) => ({ ...prev, [key]: e.target.value }));

  const mutation = useMutation({
    mutationFn: () => register(form),
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
        ? "No se pudo crear la cuenta."
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

        <p className="login-subtitle">Crea tu cuenta y zarpa a la aventura</p>

        {errorMessage && <p className="login-error">{errorMessage}</p>}

        <div className="login-field">
          <label className="login-field__label" htmlFor="reg-user">
            Usuario
          </label>
          <div className="login-input">
            <UserIcon className="login-input__icon" />
            <input
              id="reg-user"
              type="text"
              autoComplete="username"
              placeholder="Elige un nombre de usuario"
              value={form.username}
              onChange={set("username")}
              required
            />
          </div>
        </div>

        <div className="login-field">
          <label className="login-field__label" htmlFor="reg-email">
            Email
          </label>
          <div className="login-input">
            <MailIcon className="login-input__icon" />
            <input
              id="reg-email"
              type="email"
              autoComplete="email"
              placeholder="tu@correo.com"
              value={form.email}
              onChange={set("email")}
              required
            />
          </div>
        </div>

        <div className="login-field">
          <label className="login-field__label" htmlFor="reg-nick">
            Nickname
          </label>
          <div className="login-input">
            <FlagIcon className="login-input__icon" />
            <input
              id="reg-nick"
              type="text"
              placeholder="Tu nombre de pirata"
              value={form.nickname}
              onChange={set("nickname")}
              required
            />
          </div>
        </div>

        <div className="login-field">
          <label className="login-field__label" htmlFor="reg-pass">
            Contraseña
          </label>
          <div className="login-input">
            <LockIcon className="login-input__icon" />
            <input
              id="reg-pass"
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              placeholder="Crea una contraseña"
              value={form.password}
              onChange={set("password")}
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
          {mutation.isPending ? "Creando..." : "Crear cuenta"}
        </button>

        <p className="login-foot">
          ¿Ya tienes cuenta? <Link to="/login">Inicia sesión</Link>
        </p>
      </form>
    </div>
  );
}
