# Isla Tortuga Protocol

Contrato base de red para `IslaTortuga.Server`.

## Objetivos

- Mensajes JSON legibles y depurables.
- Cliente agnóstico de lenguaje.
- Envoltorio uniforme para operaciones websocket.
- Tickets temporales emitidos por HTTP y consumidos una sola vez por game.

## Envelope

Todos los mensajes websocket usan esta forma:

```json
{
  "op": "player.input",
  "requestId": "3a5f59e7",
  "sentAt": 1747777200000,
  "payload": {}
}
```

## Flujo de entrada al juego

1. El usuario obtiene una cookie de sesión válida en HTTP.
2. El cliente solicita `POST /api/game/ticket`.
3. La API responde con un `gameTicket` válido durante 30 segundos.
4. El cliente abre `ws://host/ws/game`.
5. El primer mensaje es `auth.join` con el ticket.
6. El servidor valida y consume el ticket.
7. El servidor responde `auth.accepted`.
8. El cliente empieza a enviar `player.input`.

## Flujo de reconexión

1. El cliente conserva `previousSessionId`.
2. Solicita `POST /api/game/reconnect-ticket`.
3. La API genera un ticket temporal nuevo.
4. El cliente envía `auth.reconnect`.
5. El servidor reata la conexión a la sesión anterior cuando sea posible.

## Operaciones iniciales

- `auth.join`
- `auth.reconnect`
- `auth.accepted`
- `auth.rejected`
- `player.input`
- `world.snapshot`
- `ping`
- `pong`
- `error`

## Carga de mundo

El servidor consume mapas exportados por Tiled en `.tmj` con tilesets embebidos o, como mínimo, con datos suficientes para:

- layers
- object layers
- classes
- types
- custom properties
- collision shapes en tiles

La clase base actual es `TiledWorldBuilder`.
