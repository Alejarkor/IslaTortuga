# Mapa del Proyecto

```txt
IslaTortuga/
├─ apps/
│  ├─ client/
│  │  └─ src/
│  │     ├─ features/
│  │     │  ├─ auth/                # Sesion local, login y usuario actual
│  │     │  └─ game-session/        # Entrada al juego y bootstrap de partida
│  │     ├─ game/
│  │     │  ├─ bootstrap/           # start-game, runtime y arranque del juego
│  │     │  ├─ content/             # manifest, cache, catalogo y descarga
│  │     │  └─ runtime/             # Phaser, escenas y cliente websocket
│  │     └─ shared/
│  │        └─ http/                # Cliente HTTP comun del frontend
│  └─ server/
│     └─ src/
│        ├─ auth/                   # JWT, login, registro y guard
│        ├─ game-session/           # start-game y emision de game tickets
│        ├─ health/                 # healthcheck
│        └─ prisma/                 # acceso a base de datos
├─ src/
│  ├─ IslaTortuga.Server/
│  │  ├─ Api/                       # HTTP del game server
│  │  ├─ Content/                   # indice de content packs y resolucion
│  │  ├─ GameLoop/                  # tick autoritativo
│  │  ├─ Networking/                # websocket puro y protocolo
│  │  ├─ Replication/               # snapshots e interest management
│  │  ├─ Rooms/                     # rooms y jugadores de sala
│  │  ├─ Sessions/                  # game tickets y player sessions
│  │  └─ World/                     # entidades, mundo y carga desde Tiled
│  └─ IslaTortuga.Protocol/
│     ├─ examples/                  # ejemplos de mensajes JSON
│     ├─ schemas/                   # json schema del protocolo
│     └─ protocol.md                # contrato de red
├─ assets/                          # assets fuente editables
│  ├─ maps/
│  ├─ sprites/
│  ├─ tilesets/
│  ├─ audio/
│  └─ raw/
├─ content-packs/                   # assets runtime versionados y descargables
│  ├─ index.json
│  └─ v001/
│     ├─ manifest.json
│     ├─ maps/
│     ├─ tilesets/
│     ├─ sprites/
│     └─ definitions/
├─ docs/                            # documentacion tecnica y mapas del repo
├─ infra/                           # despliegue e infraestructura
└─ packages/                        # reservado para librerias compartidas
```

## Regla base

- `assets/` es la fuente editable.
- `content-packs/` es lo que consume el cliente en runtime.
- `apps/server` no simula juego.
- `src/IslaTortuga.Server` es la verdad del mundo.
