# Cómo lanzar y probar todo (Fases 0–1)

Guía única de arranque end-to-end en Windows/PowerShell. Al final pruebas con la
colección de Postman (`IslaTortuga.postman_collection.json`).

## Componentes y puertos
| Componente | Puerto host | Cómo se levanta |
|---|---|---|
| Postgres | 5432 | Docker (Game_Database) |
| Adminer (UI de BD) | 8080 | Docker (Game_Database) |
| Redis | 6379 | Docker (Game_Database) |
| GameApi (REST: auth, salas, tickets…) | 3001 | local (`npm run dev`) o Docker |
| WebServer | 3000 | Docker (no hace falta para estas pruebas) |
| **Game Server · ControlApi (Unity)** | **8090** | Unity en Play |

> El Game Server usa **8090** a propósito, para no chocar con Adminer (8080) ni con
> otras herramientas web que suelen ocupar el 8080.

---

## MODO A — recomendado (permite probar TODO, incluido "lanzar")

Aquí el GameApi corre en local y el Game Server en Unity, ambos en el host: así el
GameApi llega al Game Server por `localhost:8090` sin líos de red entre Docker y el host.

### 1) Infra: Postgres + Redis (Docker)
```powershell
cd Server\Game_Database
docker compose up -d postgres redis
```

### 2) (solo la primera vez) Cargar el esquema de la BD
```powershell
# desde Server\Game_Database
Get-Content -Raw .\migrations\000_current_schema.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
# arreglo necesario (bug de esquema: friend_requests.resolved_at):
Get-Content -Raw .\migrations\004_fix_friend_requests_resolved_at.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
```

Para que el **personaje, los pelos y los assets de la interfaz** se vean, hay que
sembrar el registro de assets (tablas de assets + manifests):
```powershell
# tablas de assets (idempotente):
Get-Content -Raw .\migrations\003_create_asset_core.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
# registro de assets (ficheros + manifests, reconstruido desde los sidecars):
Get-Content -Raw .\seeds\002_assets_seed.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
# (opcional) tablas de assets:
Get-Content -Raw .\migrations\003_create_asset_core.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
# (opcional) datos de ejemplo:
Get-Content .\seeds\001_dev_data.sql | docker compose exec -T postgres psql -U admin -d islaT_DB
```

### 3) GameApi en local
```powershell
cd ..\GameApi
npm install
npm run dev
```
Lee `GameApi\.env` (ya creado), que apunta a `localhost` para Postgres/Redis y a
`http://localhost:8090` para el Game Server. Debe quedar escuchando en el puerto 3001.

Comprobación:
```powershell
curl http://localhost:3001/internal/health   # -> { ok:true, database:"connected" }
```

### 4) Game Server (Unity)
1. Abre el proyecto Unity en `Server\Game_Server\Unity3D\IT`.
2. En la escena, un GameObject con el componente **GameServerBootstrap** (ver
   `README_Fase0_GameServer.md`).
3. **Play**.

Comprobación:
```powershell
curl http://localhost:8090/health
curl http://localhost:8090/capacity
```

### 5) Probar con Postman
1. Importa `Server\IslaTortuga.postman_collection.json`.
2. Ejecuta las carpetas en orden (o usa el Collection Runner). Ver
   `POSTMAN_QUICKSTART.md`. El flujo de salas crea partida, emite 2 tickets y deja
   la sala en `in_game`.

### Parar todo
```powershell
# Ctrl+C en la ventana del GameApi y Stop en Unity
cd Server\Game_Database
docker compose down          # añade -v para borrar también los datos
```

---

## MODO B — todo en Docker (auth + salas, sin el paso "lanzar")
```powershell
cd Server\Game_Database
docker compose up -d --build      # postgres, adminer, redis, game-api, web-server
# luego carga el esquema igual que en el Modo A, paso 2
```
Sirve para las carpetas 0–4 (salud, auth, perfil, amigos, salas hasta "ready").
El paso **6) Lanzar** NO funciona tal cual en este modo: el GameApi (contenedor)
tendría que alcanzar al Game Server (Unity, en tu host) en `host.docker.internal:8090`,
y además Unity tendría que escuchar en todas las interfaces (`GS_CONTROL_HOST=*`, que en
Windows suele pedir permisos de administrador) en vez de solo `localhost`. Para probar
"lanzar", usa el Modo A.

---

## Checklist de verificación rápida
| Compruebo | Comando | Esperado |
|---|---|---|
| BD viva | `curl http://localhost:3001/internal/health` | `database:"connected"` |
| Redis | `docker compose exec redis redis-cli ping` | `PONG` |
| Game Server | `curl http://localhost:8090/health` | `status:"ok"` |
| Flujo salas | Postman carpeta "4. Salas" | sala `in_game` + 2 tickets |

## Orden resumido
1. `docker compose up -d postgres redis`  (Game_Database)
2. (1ª vez) cargar `000_current_schema.sql`
3. `cd GameApi && npm install && npm run dev`
4. Unity → Play  (ControlApi en 8090)
5. Postman → ejecutar la colección
