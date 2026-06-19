# Fase 2 · Conexión realtime y handshake

Objetivo (roadmap): que el cliente abra una conexión realtime, presente su ticket,
sea validado y quede ligado a su MatchInstance, completando el handshake de assets.
Todos los jugadores hablan con **el mismo endpoint** (PlayerGateway); la separación
por partida la da el **matchId del ticket**, ya en software.

## Flujo

```
Cliente --- WebSocket ws://host:9090/?ticket=XYZ --->  PlayerGateway (Unity)
                                                         │ 1. valida+consume ticket
                                                         │    (POST GameApi /internal/tickets/consume)
                                                         │ 2. localiza MatchInstance por matchId
                                                         │ 3. crea PlayerSession y la liga a la partida
   <--- MATCH_WELCOME { matchId, mapId, requiredManifestVersion, requiredAssetPackIds } ---
   --- CLIENT_READY_FOR_SNAPSHOT --->                    4. sesión -> Connected
```

## Qué se añadió (Unity / C#)

```
Assets/Scripts/GameServer/
  gateway/
    MessageCodec.cs           encode/decode de { type, payload } (sobre Json/JsonReader)
    ITransport.cs             abstracción de conexión (testeable con dobles)
    WebSocketTransport.cs     transporte sobre System.Net.WebSockets (HttpListener)
    TicketValidator.cs        ITicketValidator + HttpTicketValidator (consume vía GameApi)
    PlayerSession.cs          sesión: identidad, partida, transporte, estado
    PlayerSessionManager.cs   registro de sesiones por id de socket (thread-safe)
    PlayerGateway.cs          endpoint WS: valida, liga a la partida y hace el handshake
  match/
    MatchInstance.cs          + addPlayer / removePlayer (jugadores conectados)
  host/
    ServerConfig.cs           + GameApiUrl (GS_GAME_API_URL)
    GameServerHost.cs         arranca/para también el PlayerGateway
```

Backend (GameApi): nuevo `POST /internal/tickets/consume { ticketId }` → consumo
**atómico** del ticket (reutiliza el de la Fase 1); devuelve `{ matchId, playerId }`
o 404 si es inválido/ya usado.

## Contratos de mensajes

```
Servidor -> Cliente:
{ "type": "MATCH_WELCOME", "payload": {
    "matchId": "match_ab12cd34ef56", "mapId": "beach_map_01",
    "requiredManifestVersion": "1.0.0", "requiredAssetPackIds": ["base"] } }

Cliente -> Servidor:
{ "type": "CLIENT_READY_FOR_SNAPSHOT" }
```
El ticket se presenta en la query de la URL del WebSocket: `?ticket=...`.

## Cómo se prueba

### Unity (Test Runner · EditMode)
- `MessageCodecTests` — round-trip de { type, payload }.
- `PlayerSessionManagerTests` — alta/baja y resolución por id de socket.
- `PlayerGatewayTests` (integración, con `ClientWebSocket` + validador falso):
  - ticket válido → recibe `MATCH_WELCOME`, queda ligado, y tras `CLIENT_READY_FOR_SNAPSHOT` la sesión pasa a `Connected`;
  - ticket inválido → el socket se cierra;
  - desconexión → el jugador se quita de la partida.

> Requiere soporte de WebSocket en HttpListener (Windows / .NET moderno, que es el
> caso en el editor de Unity en Windows). Si tu build dedicada headless en Linux no lo
> soportara, se cambiaría WebSocketTransport a una implementación sobre TcpListener.

### End-to-end (con todo levantado)
1. Infra + GameApi + Game Server (Unity en Play) como en `COMO_LANZAR_Y_PROBAR.md`.
2. En Postman, lanza una partida (carpeta Salas) → obtienes `matchId` y un `ticket` por jugador.
3. Conecta un cliente WebSocket a `ws://localhost:9090/?ticket=<ticketId>`; recibirás
   `MATCH_WELCOME` y, al responder `CLIENT_READY_FOR_SNAPSHOT`, quedarás `connected`.

El Game Server valida el ticket contra el GameApi usando `GS_GAME_API_URL`
(por defecto `http://localhost:3001`).

## Definición de Hecho (DoD)

N clientes con sus tickets quedan ligados a su MatchInstance por el matchId del
ticket, todos sobre el mismo puerto del PlayerGateway (9090). ✔️

## Siguiente paso (Fase 3)
Mundo y tick: NetworkRuntime mínimo (NetworkWorld, NetworkEntity, SimulationLoop) que
hace latir cada partida a ritmo fijo, aislada de las demás.
