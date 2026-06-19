# Fase 5 · Sincronización de estado (input + movimiento + replicación)

Objetivo (roadmap): que las entidades se sincronicen entre jugadores con **servidor
autoritativo**: el cliente envía intención (input), el servidor decide la verdad y
replica deltas. Mundo 3D.

## Qué se añadió (Unity / C#)

```
match/runtime/
  input/InputSystem.cs       buffer de input por jugador (orden por seq); PlayerInput
  MovementSystem.cs          aplica input a entidades OWNER: ignora la posición del
                             cliente y recalcula la suya (dir normalizada * speed * dt) en X/Z
  replication/ReplicationSystem.cs   construye STATE_DELTA solo con las entidades que cambiaron
NetworkRuntime.cs            orden del tick: processInputs -> movimiento -> reglas -> replicación,
                             y un Broadcaster que el gateway conecta a las sesiones
gateway/PlayerGateway.cs     recibe PLAYER_INPUT y lo pasa al runtime; difunde los
                             STATE_DELTA a todas las sesiones; añade playerId al MATCH_WELCOME
```

## Contratos de mensajes

```
Cliente -> Servidor (intención, nunca posición):
{ "type": "PLAYER_INPUT", "payload": { "seq": 481, "moveX": 0.2, "moveZ": 1.0, "clientTime": 10233 } }

Servidor -> Cliente (verdad del tick):
{ "type": "STATE_DELTA", "payload": {
    "serverTick": 18421,
    "entities": [ { "id": "ent_7", "x": 120.4, "y": 0, "z": 88.1 } ],
    "events": [] } }
```
El `MATCH_WELCOME` ahora incluye `playerId`, para que el cliente sepa cuál es su entidad.

## Cómo está montado (autoritativo)

- El cliente manda **solo intención** (`moveX/moveZ`); nunca su posición.
- Cada tick el servidor: aplica los inputs (`MovementSystem`) recalculando la posición,
  y luego replica con `STATE_DELTA` **solo lo que cambió**.
- El input se aplica en el **tick siguiente**, no de forma instantánea (el servidor manda).

## Probarlo con el cliente de prueba

1. Stack levantado + Unity en Play. Lanza partida (con 2 jugadores listos) y copia 2 tickets.
2. Abre `Tools/WsTestClient/index.html` en dos pestañas, una con cada ticket, **Conectar**.
3. Haz **clic en el lienzo** y usa las **flechas**: tu punto (amarillo) se mueve y la otra
   pestaña lo ve moverse — porque el servidor mueve la entidad y replica el STATE_DELTA.

## Pruebas (Test Runner · EditMode)

- `InputSystemTests` — guarda el último input y respeta el orden por seq.
- `MovementSystemTests` — mueve al OWNER recalculando (ignora al cliente), no mueve
  entidades SERVER, y normaliza la diagonal.
- `ReplicationSystemTests` — el delta solo incluye entidades que cambiaron.
- `RuntimeMovementTests` — integración: con input, el owner se mueve a lo largo de
  varios ticks y se emiten STATE_DELTA.

## Definición de Hecho (DoD)

Las entidades se sincronizan entre jugadores con servidor autoritativo y deltas (de
momento, interés = todos; el filtro por distancia/AOI es el siguiente refinamiento). ✔️

## Siguiente paso (Fase 6)
Eventos bidireccionales (SOUND, ANIM, OPEN_DOOR, PICKUP_ITEM, CHAT): acciones puntuales
validadas por el servidor y entregadas según interés, sobre esta misma infraestructura.
