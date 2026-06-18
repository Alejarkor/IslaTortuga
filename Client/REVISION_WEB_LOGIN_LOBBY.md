# Revisión del cliente web · Login + Lobby pre-juego

Análisis estático del cliente React (`Client/`) centrado en autenticación y la
pantalla de pre-juego, y de su contrato con el `WebServer`. No se han modificado
ficheros; esto es solo el informe.

## Resumen ejecutivo
La base está **bien construida**: arquitectura limpia (capa `api/` tipada, react-query,
`ApiError`, sesión por cookie HttpOnly con proxy mismo-origen), login/registro funcionan
y el editor de personaje está conectado de verdad. El problema principal es que **buena
parte del lobby es maqueta sin backend**: las salas son 100% mock, el chat es mock,
varios botones de amigos no hacen nada y los datos de la cabecera (nivel, monedas) están
inventados. Y, crucialmente, **el WebServer no expone rutas de salas**, así que el lobby
no puede conectarse al backend de salas/tickets (Fase 1) aunque se quisiera.

---

## Fallos / cosas que no funcionan

**1. (Alta) Salas: 100% mock y sin camino al backend.**
`features/pregame/components/RoomsPanel.tsx` usa `MOCK_ROOMS` y ninguno de sus botones
(Crear sala, Unirse, código, "JUGAR") tiene handler. No existe `rooms.api.ts` en el
cliente. Además el `WebServer` **no tiene rutas `/api/rooms*`** (solo auth, profile,
stats, friends, assets), así que el lobby no puede llegar al GameApi de salas. Esta es la
pieza grande que falta para cerrar la Fase 1 en la web.

**2. (Alta) Acciones de amigos sin cablear.**
`FriendsPanel.tsx` muestra amigos y solicitudes (solo lectura), pero los botones
"Aceptar" e "Invitar" no tienen `onClick`, y `friends.api.ts` solo implementa
`fetchFriends` y `fetchIncomingRequests`. El WebServer **sí** ofrece accept/reject/
cancel/send y outgoing — falta el cliente. Resultado: puedes ver solicitudes pero no
aceptarlas desde la UI.

**3. (Media) Cabecera con datos inventados.**
`LobbyHeader.tsx` muestra nivel 12, XP 2350/5000, 12450 monedas y 845 gemas, todo
hardcodeado; no viene de `/api/stats`. Parece que hay progresión cuando no la hay.

**4. (Media) Chat de ejemplo.**
`MOCK_CHAT` hardcodeado y el input no envía nada (no hay backend de chat). Correcto para
la fase, pero conviene marcarlo como "próximamente" o deshabilitar el input para no
confundir.

**5. (Baja) `MatchPanel.tsx` es código muerto.**
No se importa en ningún sitio (`PreGamePage` monta Friends/Character/Rooms). Borrarlo o
integrarlo.

**6. (Baja) Presencia falsa en amigos.**
`FriendsPanel` pinta "Desconectado" y el punto gris para todos; no hay presencia real.
Cosmético pero engañoso.

---

## Seguridad

**7. (Alta en prod) Secreto JWT por defecto.**
`WebServer` usa `JWT_SECRET ?? "dev_secret_change_me"`. Si no se define en producción,
cualquiera puede forjar sesiones. Debería **fallar el arranque** si falta en prod.

**8. (Alta en prod) Cookie `secure: false` fija.**
La cookie de sesión nunca se marca `Secure`, ni con HTTPS. Hay que condicionarla por
entorno (`secure: true` en prod) y revisar `sameSite`. Para dev local está bien.

**9. (Media) Sin manejo global de 401 / caducidad.**
El token dura 7 días; si caduca a mitad de sesión, las llamadas devolverán 401 y el
usuario verá errores hasta recargar. `RequireAuth` solo comprueba al montar. Mejora: un
interceptor en `httpClient` que ante 401 invalide la sesión y redirija a `/login`.

**10. (Baja) Validación de credenciales débil.**
Solo `required` y `type=email` en cliente; sin longitud mínima ni fortaleza de
contraseña. Añadir reglas y feedback (y validarlas también en servidor).

---

## Mejoras de UX / robustez

**11. Estados de error/carga incompletos.**
Login y registro muestran bien el error (`ApiError.message`), pero `FriendsPanel` no
muestra carga ni error: si `/api/friends` falla, se ve vacío sin avisar.

**12. La apariencia bloquea todo el lobby.**
`PreGamePage` muestra error genérico y **no renderiza nada** del lobby si falla la carga
de apariencia, aunque Amigos y Salas no dependen de ella. Mejor degradar: cargar el
lobby y mostrar el fallo solo en la columna del personaje.

**13. Accesibilidad.**
Bien los botones icon-only (tienen `aria-label`). Falta: los inputs de chat y de "código
de sala" usan solo `placeholder` (no `label` asociado); y el carrusel móvil (scroll-snap
+ dots) no gestiona foco/teclado entre secciones.

**14. Menor en login.**
Mientras envía, solo se deshabilita el botón, no los inputs. Trivial.

**15. `apiBaseUrl` vacío por defecto.**
Depende del proxy de Vite (correcto en dev). Conviene documentar que en prod el cliente
se sirve tras el WebServer o se define `VITE_API_BASE_URL`.

---

## Lo que está bien (para equilibrar)
- Capa `api/` tipada y `ApiError` que conserva status y payload del backend.
- Sesión por **cookie HttpOnly + proxy mismo-origen**: correcto y resistente al robo de
  token por XSS.
- `useAuth` interpreta 401 como "no autenticado" (no como error a reintentar). Limpio.
- Editor de personaje y guardado de apariencia **sí** conectados, con patrón
  "guardado vs edición" y validación de pelo antes de enviar.
- react-query con claves coherentes y reintentos conservadores.
- Responsive pensado para móvil (scroll-snap con indicadores).

---

## Prioridad sugerida
1. **Conectar salas (cierra la Fase 1 en web):** añadir `/api/rooms*` en el WebServer
   (proxy al GameApi `/internal/rooms*`), crear `rooms.api.ts` y cablear `RoomsPanel`
   (crear / unir / ready / lanzar) usando la cookie de sesión para el `playerId`.
2. **Cablear acciones de amigos** (aceptar / enviar / rechazar) en el cliente.
3. **Seguridad por entorno:** `JWT_SECRET` obligatorio en prod, cookie `secure` según
   entorno e interceptor global de 401.
4. **Datos reales en cabecera** desde `/api/stats` (o ocultarlos hasta que existan).
5. **Limpieza:** borrar `MatchPanel`, marcar chat/presencia como "próximamente",
   degradar el fallo de apariencia.

## Ficheros clave revisados
- Auth: `src/features/auth/{LoginPage,RegisterPage,RequireAuth,useAuth}.tsx`, `src/api/{auth.api,httpClient}.ts`
- Lobby: `src/features/pregame/PreGamePage.tsx` y `components/{RoomsPanel,FriendsPanel,LobbyHeader,MatchPanel}.tsx`
- Contrato: `Server/WebServer/src/index.ts`, `src/config/env.ts`, `vite.config.ts`
