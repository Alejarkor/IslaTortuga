# Arquitectura de Servidores

## Estado actual

- `apps/server`
  API HTTP en NestJS para auth, sesion, portal y emision de `gameTicket`.

- `Unity/IslaTortugaServer`
  Servidor autoritativo embebido en Unity. Simula el mundo, mantiene sesiones de juego y expone `HTTP + WebSocket` cuando se activa el gateway local.

- `apps/client`
  Cliente web con Babylon.js. Solo renderiza, interpola y envia input.

- `content-packs`
  Entrega versionada de escenas 3D exportadas desde Unity, definiciones de visuales y assets runtime.

## Regla base

- La autoridad del juego vive en Unity.
- Babylon no decide colisiones, inventario ni transiciones finales.
- La API Nest no simula mundo.
- El contrato entre API, Unity y cliente se apoya en tickets, `sceneId` y mensajes JSON por WebSocket.

## Flujo de juego

1. El usuario inicia sesion en la API.
2. La API emite un `gameTicket`.
3. El cliente Babylon abre `ws://.../ws/game`.
4. Unity valida el ticket y crea o reata la sesion de juego.
5. Unity carga la escena exportada indicada por `sceneId`.
6. Unity simula entidades y replica `world.delta`.
7. Babylon interpola y actualiza visuales 3D.

## Flujo de contenido

1. La API devuelve `contentPackId`, `version`, `sceneId` y `manifestUrl`.
2. El cliente descarga `manifest.json`.
3. `AssetCatalog` resuelve definiciones y archivos del pack.
4. `NetworkSceneManager` carga la escena 3D por `sceneId`.
5. `NetworkEntityManager` crea visuales 3D para las entidades.

## Decisiones activas

- El runtime visual activo es 3D de extremo a extremo.
- Los personajes y props se representan con visuales 3D.
- Las escenas cliente se exportan desde Unity con builder `unity-scene-export`.
- El servidor Unity puede seguir usando prefabs y colliders reales para la simulacion.
