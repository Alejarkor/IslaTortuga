# Fase 1 · Salas, tickets y lanzamiento de partida

Objetivo (del roadmap): que los clientes puedan crear salas, unirse, marcar ready y
lanzar una partida. El lanzamiento instancia una **MatchInstance** (cascarón, sin
realtime) y genera un **ticket por jugador**.

Reparto acordado:
- **Backend web (Node/TS)** → salas y tickets, dentro del **GameApi** ya existente,
  con estado en **Redis** (servicio nuevo en el docker-compose).
- **Game Server (Unity/C#)** → orquestación de partidas: `MatchInstance`,
  `MatchOrchestrator` y los endpoints de control `create-match` / `stop-match`.

## Flujo completo

```
Cliente → GameApi: crear sala / unirse / ready        (estado en Redis)
                         │  cuando todos ready y hay capacidad
                         ▼
GameApi.RoomService.launch():
   1. GET  {GameServer}/capacity            ¿hay hueco?
   2. POST {GameServer}/control/create-match → matchId   (reserva capacidad)
   3. TicketService emite 1 ticket por jugador (Redis, con TTL)
   4. RoomSyncAdapter: sala -> in_game
   5. devuelve { matchId, gateway, tickets } a los clientes
```

## Backend (GameApi) — qué se añadió

```
src/
  redis.ts                    cliente Redis (ioredis) + interfaz RedisLike
  ids.ts                      generación de ids y código de sala
  rooms/
    types.ts                  Room, RoomMember, MatchConfig
    roomStateMachine.ts       transiciones válidas (waiting→ready_check→starting→in_game→finished)
    roomRepository.ts         persistencia de salas en Redis (+ índice por código)
    roomService.ts            orquesta create/join/ready/leave/launch + canLaunch()
    roomSyncAdapter.ts        mueve la sala a in_game al crear la partida
    errors.ts, routes.ts      errores de dominio y router Express
  tickets/
    types.ts                  JoinTicket
    ticketRepository.ts       tickets en Redis; consume() atómico vía GETDEL
    ticketService.ts          emite 1 ticket por jugador con TTL
  gameserver/
    controlClient.ts          GameServerControlClient (interfaz) + impl HTTP (fetch)
  testing/                    InMemoryRedis y FakeControlClient (para tests)
```

### Endpoints HTTP (montados en el GameApi, prefijo /internal)

| Método | Ruta | Cuerpo | Efecto |
|---|---|---|---|
| POST | /internal/rooms | hostPlayerId, nickname, [maxPlayers, mapId, isPrivate] | crea sala (host = master) |
| GET  | /internal/rooms/:roomId | — | estado de la sala |
| POST | /internal/rooms/:roomId/join | playerId, nickname | unirse |
| POST | /internal/rooms/:roomId/leave | playerId | salir (reasigna host / borra si vacía) |
| POST | /internal/rooms/:roomId/ready | playerId, ready(boolean) | marcar/desmarcar listo |
| POST | /internal/rooms/:roomId/launch | — | lanzar: crea partida + tickets |

### Variables de entorno nuevas

| Variable | Default | Para qué |
|---|---|---|
| REDIS_HOST / REDIS_PORT | localhost / 6379 | conexión a Redis |
| GAME_SERVER_CONTROL_URL | http://host.docker.internal:8090 | ControlApi del Game Server (Unity) |
| GS_CONTROL_TOKEN | (vacío) | token compartido opcional para create/stop-match |

## Game Server (Unity) — qué se añadió

```
Assets/Scripts/GameServer/
  match/
    MatchConfig.cs            config de partida (maxPlayers, mapId, players)
    MatchInstance.cs          partida aislada (cascarón: id, estado, jugadores)
    MatchOrchestrator.cs      crea/para partidas y reserva/libera capacidad
  control/
    JsonReader.cs             parser JSON thread-safe (hilo del HttpListener)
    ControlApi.cs             + POST /control/create-match y /control/stop-match
```

### Contratos de control

```
POST /control/create-match
  body: { "maxPlayers": 8, "mapId": "beach_map_01", "players": ["p1","p2"] }
  200 : { "ok": true, "matchId": "match_ab12cd34ef56", "gatewayHost": "localhost", "gatewayPort": 9090 }
  409 : { "ok": false, "error": "no capacity" }

POST /control/stop-match
  body: { "matchId": "match_ab12cd34ef56" }
  200 : { "ok": true }
  404 : { "ok": false, "error": "match not found" }
```

Cada create-match reserva un hueco en el `CapacityManager`, así que `/capacity` (Fase 0)
refleja en todo momento las partidas vivas. stop-match lo libera.

## Cómo probarlo

### Backend (Node) — automático
```
cd Server/GameApi
npm install
npm test          # vitest: 17 pruebas (state machine, repos, tickets, RoomService)
```
Las pruebas usan dobles en memoria (InMemoryRedis, FakeControlClient): no necesitan
Redis ni el Game Server levantados.

### Game Server (Unity) — Test Runner
`Window → General → Test Runner → EditMode → Run All`. Se añaden a las de Fase 0:
- `MatchOrchestratorTests` — capacidad, ids únicos, recuperable por id, stop.
- `MatchControlApiTests` — create-match 200 + matchId, stop-match 200/404, 409 sin
  capacidad, 400 con JSON inválido.

### End-to-end (manual, todo levantado)
1. `docker compose up` (incluye Postgres + Redis + GameApi).
2. Arranca el Game Server en Unity (Play) → ControlApi en :8090.
3. Asegúrate de que `GAME_SERVER_CONTROL_URL` apunta a ese :8090.
4. Con curl: crea sala, une dos jugadores, marca ready ambos y lanza; el GameApi
   devolverá `matchId` y un ticket por jugador, y el Game Server tendrá la
   MatchInstance viva (visible en `/capacity`).

## Definición de Hecho (DoD) — Fase 1

Una sala llena y con todos ready deriva en una MatchInstance instanciada y un ticket
por jugador, sin nada de realtime todavía. ✔️ (la conexión realtime es la Fase 2)

## Nota

Hubo que reescribir algunos ficheros porque el guardado a través de la carpeta
montada truncaba ficheros grandes; se verificó que todo el código Node typechea
(`tsc --noEmit`) y que las 17 pruebas Node pasan. El código C# se valida con el Test
Runner de Unity.
