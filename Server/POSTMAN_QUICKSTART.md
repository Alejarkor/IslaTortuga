# Postman · Guía rápida (IslaTortuga API)

Importa `IslaTortuga.postman_collection.json` en Postman
(File → Import). Trae sus propias variables, no necesitas un Environment aparte.

## Variables (edítalas si tu setup difiere)
- `gameApiUrl`  → `http://localhost:3001`  (GameApi)
- `gameServerUrl` → `http://localhost:8090` (ControlApi del Game Server / Unity)
- `gsControlToken` → vacío (rellénalo solo si pusiste `GS_CONTROL_TOKEN`)

El resto (`p1_playerId`, `roomId`, `matchId`, `ticket1`…) **se rellenan solas**: cada
petición guarda en variables lo que necesita la siguiente.

## Qué hace falta levantado según la carpeta
| Carpeta | Necesita |
|---|---|
| 0. Salud | GameApi (y Game Server para sus 2 health/capacity) |
| 1. Auth / 2. Perfil / 3. Amigos | GameApi + **Postgres** |
| 4. Salas (1→5) | GameApi + **Redis** |
| 4. Salas · 6) Lanzar | además, **Game Server (Unity) en Play** y accesible |
| 5. Game Server control | **Game Server (Unity) en Play** |

Levantar la infra: `cd Server/Game_Database && docker compose up`
(arranca Postgres, Redis, GameApi y WebServer). Y en Unity, dale a Play para el
ControlApi en :8090.

> Importante para "6) Lanzar": el GameApi llama al Game Server usando su variable de
> entorno `GAME_SERVER_CONTROL_URL`. Si el GameApi corre en Docker y Unity en tu PC,
> debe valer `http://host.docker.internal:8090`. Si corres el GameApi en local
> (`npm run dev`), vale `http://localhost:8090`.

## Orden recomendado
Ejecuta las carpetas de arriba abajo. Para el flujo de salas, lanza en orden los
pasos 1→8; o usa el **Collection Runner** sobre la carpeta "4. Salas (flujo)".

1. **0. Salud** → confirma que todo responde.
2. **1. Auth** → registra P1 y P2 (guardan sus `playerId`).
3. **4. Salas** → crear → unir P2 → ready P1 → ready P2 (la sala pasa a `ready_check`)
   → **lanzar** (crea la partida, emite 2 tickets, sala a `in_game`) → ver sala.
4. **5. Game Server control** → crear/parar partidas directamente y ver `/capacity`.

## Equivalente en curl (mínimo del flujo de salas)
```bash
API=http://localhost:3001
# crear sala
curl -s -X POST $API/internal/rooms -H "Content-Type: application/json" \
  -d '{"hostPlayerId":"p1","nickname":"Ana","maxPlayers":4,"mapId":"beach_map_01"}'
# (toma el roomId de la respuesta)
curl -s -X POST $API/internal/rooms/<roomId>/join  -H "Content-Type: application/json" -d '{"playerId":"p2","nickname":"Beto"}'
curl -s -X POST $API/internal/rooms/<roomId>/ready -H "Content-Type: application/json" -d '{"playerId":"p1","ready":true}'
curl -s -X POST $API/internal/rooms/<roomId>/ready -H "Content-Type: application/json" -d '{"playerId":"p2","ready":true}'
curl -s -X POST $API/internal/rooms/<roomId>/launch
```
(En este ejemplo con curl se usan playerIds inventados; en Postman se usan los reales
de registro. Las salas no validan que el playerId exista en la BD.)

## Reutilizar los mismos jugadores entre pruebas
La carpeta **1. Auth** usa credenciales fijas (`jugador1` / `jugador2`, password `secret123`):

- **La primera vez:** ejecuta `Registrar P1` y `Registrar P2` (crean los usuarios; si ya existen dan 409, sin problema).
- **Cada sesión a partir de entonces:** ejecuta solo `Login P1` y `Login P2`. Recuperan el `playerId` sin crear usuarios nuevos.
- Luego ya puedes lanzar la carpeta `4. Salas` tantas veces como quieras.

> Como cambió la colección, **re-impórtala** en Postman (File → Import → Replace).
