# Game Server · Fase 0 — Bootstrap del host (Unity / C#)

Implementación de la **Fase 0** del roadmap multijugador: el proceso del Game Server
arranca, se mantiene vivo, registra logs y métricas, sabe responder si puede aceptar
partidas y se apaga limpiamente. Todavía **sin sockets de jugador ni simulación**.

> Nota de stack: el roadmap describe firmas en TypeScript/Node como *supuesto*. Aquí
> el Game Server se implementa en **C# sobre Unity** (servidor dedicado headless),
> manteniendo la misma arquitectura, nombres de clase y orden de implementación.

## Dónde está el código

```
Assets/Scripts/
  Shared/                         IslaTortuga.Shared      (contratos futuros; hoy ProtocolInfo)
  GameServer/                     IslaTortuga.GameServer
    host/
      ServerConfig.cs             configuración + validación
      ServerConfigException.cs
      IServerLogger.cs            abstracción de logging (sin dependencia de UnityEngine)
      ConsoleServerLogger.cs      logger por defecto (consola → Player.log)
      MetricsRegistry.cs          contadores y gauges thread-safe
      GameServerHost.cs           ata arranque y apagado (POCO, probable headless)
      GameServerBootstrap.cs      MonoBehaviour: entrypoint dentro de Unity
    control/
      CapacityManager.cs          ¿puede aceptar otra partida?
      ControlApi.cs               HttpListener con /health y /capacity
      Json.cs                     mini-serializador JSON sin dependencias
  Tests/EditMode/                 IslaTortuga.GameServer.Tests (NUnit)
```

El núcleo (todo salvo `GameServerBootstrap`) **no depende de UnityEngine**, para que
sea probable en modo headless y reutilizable.

## Cómo arrancarlo en Unity

1. Crea un GameObject vacío en la escena de arranque del servidor y añádele el
   componente **`GameServerBootstrap`**.
2. Play (o build dedicada con `-batchmode -nographics`).
3. Comprueba los endpoints:

```
curl http://localhost:8090/health
curl http://localhost:8090/capacity
```

`/health` → `{"status":"ok","service":"game-server","uptimeSeconds":...}`
`/capacity` → `{"ok":true,"activeMatches":0,"maxMatches":50,"availableSlots":50,...}`

## Configuración (variables de entorno, con caída a defaults)

| Variable | Default | Significado |
|---|---|---|
| `GS_CONTROL_HOST` | `localhost` | Host de la ControlApi |
| `GS_CONTROL_PORT` | `8090` | Puerto HTTP de control |
| `GS_GATEWAY_PORT` | `9090` | Puerto realtime (se usa desde la Fase 2) |
| `GS_TICK_RATE` | `30` | Ticks por segundo (Fase 3) |
| `GS_MAX_MATCHES` | `50` | Partidas simultáneas en el host |
| `GS_MAX_PLAYERS_PER_MATCH` | `8` | Jugadores por partida |

Una configuración inválida (puerto fuera de rango, `tickRate <= 0`, etc.) lanza
`ServerConfigException` y aborta el arranque de forma ruidosa y temprana.

## Pruebas

Window → General → **Test Runner** → pestaña **EditMode** → **Run All**.

Cubren el DoD de la fase:

- **Unitarias**: `ServerConfig` rechaza configuración inválida; `CapacityManager.CanAcceptMatch()` refleja los límites configurados.
- **Integración**: `GET /health` responde 200 `ok`; `GET /capacity` devuelve la carga real; el apagado ordenado (`ShutdownGracefullyAsync`) libera el puerto sin dejar recursos abiertos (otro host puede volver a enlazarlo).

## Definición de Hecho (DoD) — Fase 0

El binario arranca, responde a `/health` y `/capacity`, escribe logs legibles y se
apaga limpiamente. ✔️

## Notas / posibles ajustes en tu entorno

- **`System.Net.Http`**: si el Test Runner se queja de que no encuentra `HttpClient`,
  pon *Project Settings → Player → Api Compatibility Level* en **.NET Standard 2.1**
  (o **.NET Framework**). Las pruebas de integración usan `HttpClient`.
- **Test Framework**: requiere el paquete *Test Framework* (com.unity.test-framework),
  incluido por defecto en proyectos recientes.
- **Binding en producción (Linux dedicado)**: para escuchar en todas las interfaces,
  arranca con `GS_CONTROL_HOST=*` o `+`. En Windows eso puede requerir permisos de
  administrador o una reserva de URL (`netsh http add urlacl`); en dev usa `localhost`.

## Siguiente paso (Fase 1)

Salas, tickets y lanzamiento de partida. Según lo acordado, se implementan **ampliando
el `GameApi`** existente (Node/TS) e incorporando **Redis** para salas y tickets, más
el `ControlApi` de `create-match`/`stop-match` en el Game Server y su cliente.
