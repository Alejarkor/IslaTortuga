# Fase 4 · Spawn y despawn de entidades de red

Objetivo (roadmap): instanciar y destruir entidades de red (jugadores y objetos) con
id único, respetando la separación lógica/visual: el servidor solo maneja **ids y
estado**, nunca binarios de asset (el cliente resuelve el prefab por su manifest).

## Qué se añadió (Unity / C#)

```
match/runtime/spawn/
  NetworkPrefabRegistry.cs   catálogo lógico de networkPrefabIds válidos
  SpawnSystem.cs             spawnEntity(...) y spawnPlayer(ownerId) -> OWNER + ownerId
  DespawnSystem.cs           despawn(entityId)
gateway/
  NetworkMessages.cs         SPAWN_ENTITY / DESPAWN_ENTITY (3D: x,y,z + cuaternión x,y,z,w)
  PlayerGateway.cs           al completar el handshake: spawnea al jugador, envía el
                             snapshot del mundo y difunde su alta; al salir, despawnea
                             y lo notifica
```
`NetworkRuntime` ahora expone `Spawn`, `Despawn` y `Prefabs`.

## Contratos de mensajes (3D)

```
Servidor -> Cliente (alta):
{ "type": "SPAWN_ENTITY", "payload": {
    "networkEntityId": "ent_1", "networkPrefabId": "player_default",
    "position": { "x": 0, "y": 0, "z": 0 },
    "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 },
    "authority": "owner", "ownerId": "player_7", "initialState": {} } }

Servidor -> Cliente (baja):
{ "type": "DESPAWN_ENTITY", "payload": { "networkEntityId": "ent_1" } }
```
El cliente NO recibe el binario: busca `networkPrefabId` en su manifest.

## Flujo realtime (lo que verás en el cliente de prueba)

1. El cliente conecta y completa el handshake (Fase 2).
2. El servidor **spawnea su entidad** (autoridad OWNER), le manda el **snapshot** de
   todas las entidades del mundo y **difunde** su alta al resto de jugadores.
3. Si entra otro jugador, ambos se ven aparecer; si uno se va, el resto recibe su
   `DESPAWN_ENTITY`.

## Cliente de prueba (Tools/WsTestClient/index.html)

Una página HTML suelta (sin dependencias) para validar el realtime sin montar aún el
cliente de PlayCanvas:

1. Levanta el stack (Postgres, Redis, GameApi y **Unity en Play** para el gateway 9090).
2. En Postman, lanza una partida y copia un `ticketId` de la respuesta.
3. Abre `Tools/WsTestClient/index.html` en el navegador, pon `ws://localhost:9090`,
   pega el ticket y pulsa **Conectar**. Verás `MATCH_WELCOME`, el handshake automático
   y tu entidad aparecer en el lienzo.
4. Para ver dos jugadores: lanza una partida con dos jugadores listos, abre dos
   pestañas con un ticket cada una, y os veréis spawnear (y despawnear al cerrar una).

> Los ticket caducan en ~2 min; conéctate rápido tras lanzar.

## Pruebas (Test Runner · EditMode)

- `SpawnSystemTests` — spawnEntity (id único, estado, autoridad), spawnPlayer (OWNER +
  ownerId), despawn quita del mundo, y el mensaje SPAWN lleva prefabId/ids/posición 3D
  y **ningún binario**.
- `PlayerGatewaySpawnTests` — al conectar se spawnea y se difunde; un segundo jugador
  recibe el snapshot con ambos; el primero es notificado del alta del segundo; y al
  desconectarse uno, el otro recibe su DESPAWN. (Con transporte en memoria.)

## Definición de Hecho (DoD)

Se instancian y destruyen todas las entidades de red con id único y separación
lógica/visual respetada. ✔️

## Siguiente paso (Fase 5)
Sincronización de estado: replicación (STATE_DELTA), input del cliente (PLAYER_INPUT)
y gestión de interés, con el servidor autoritativo. Ahí las entidades empezarán a
moverse en 3D.
