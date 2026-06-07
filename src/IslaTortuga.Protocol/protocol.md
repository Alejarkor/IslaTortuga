# Isla Tortuga Protocol

Contrato base de red entre la API, el host autoritativo en Unity y el cliente Babylon.

## Objetivos

- mensajes JSON legibles y depurables
- cliente agnostico de implementacion
- sesiones de juego via ticket temporal
- carga de escenas por `sceneId`, no por estado visual incrustado en red

## Envelope

```json
{
  "op": "player.input",
  "requestId": "3a5f59e7",
  "sentAt": 1747777200000,
  "payload": {}
}
```

## Flujo de entrada

1. La API emite un `gameTicket`.
2. El cliente abre `ws://host/ws/game`.
3. El primer mensaje es `auth.join`.
4. Unity valida el ticket.
5. Unity responde `auth.accepted`.
6. Unity envia `scene.bootstrap`.
7. El cliente empieza a enviar `player.input`.

## Operaciones activas

- `auth.join`
- `auth.reconnect`
- `auth.accepted`
- `auth.rejected`
- `scene.bootstrap`
- `scene.change`
- `player.input`
- `world.delta`
- `ping`
- `pong`
- `error`

## Escenas

El protocolo no replica una escena 3D completa por red.

Replica:

- `sceneId`
- `sceneInstanceId`
- entidades visibles y sus cambios

El cliente usa ese `sceneId` para descargar la escena exportada adecuada desde `content-packs`.

## Coordenadas

La simulacion actual sigue usando coordenadas planas para locomocion, pero el mundo es 3D.

- `x` de red corresponde al eje `X`
- `y` de red corresponde al desplazamiento sobre `Z` en el mundo

La componente vertical se resuelve en escena, colisiones o visualizacion, no en el protocolo base actual.
