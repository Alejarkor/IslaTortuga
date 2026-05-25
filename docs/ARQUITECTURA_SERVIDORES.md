# Arquitectura de Servidores

## Separacion actual

- `apps/server`
  API HTTP en NestJS para auth, sesion, portal y prejuego.

- `src/IslaTortuga.Server`
  Servidor de juego autoritativo en C# con WebSocket puro y mensajes JSON.

- `src/IslaTortuga.Protocol`
  Contrato de red agnostico de cliente.

- `content-packs`
  Entrega versionada de mapas, sprites, tilesets, audio y definiciones.

## Flujo previsto

1. El usuario inicia sesion en la API.
2. La API valida la sesion HTTP y genera un `gameTicket`.
3. El cliente abre websocket contra el game server.
4. El game server consume y destruye el ticket.
5. El game server crea o reata la `PlayerSession`.
6. El game server simula el mundo y replica snapshots.

## Flujo de contenido

1. La API devuelve `contentPackId`, `contentVersion`, `mapId` y `manifestUrl`.
2. El cliente descarga el `manifest.json`.
3. `ContentDownloader` asegura en cache los archivos del pack.
4. `AssetCatalog` carga las definiciones visuales.
5. Solo entonces arranca Babylon.

## Regla de organizacion

- Nada de simulacion de juego en `apps/server`.
- Nada de acceso directo a base de datos en el game server salvo que se disene expresamente.
- El punto de acoplamiento entre ambos lados debe ser el protocolo y el flujo de tickets/sesion.
