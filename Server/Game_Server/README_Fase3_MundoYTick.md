# Fase 3 · Mundo y tick (NetworkRuntime mínimo)

Objetivo (roadmap): que cada partida tenga un mundo de entidades en memoria que
avanza a ritmo fijo de tick, **aislado de las demás partidas**. Es el latido sobre el
que se montan spawn (Fase 4), replicación (Fase 5) y eventos (Fase 6). Todavía **sin
mensajes de red**: solo el mundo interno y el latido.

## Qué se añadió (Unity / C#)

```
Assets/Scripts/GameServer/match/runtime/
  world/
    NetworkEntity.cs         entidad 3D: id, prefabId, Position(Vector3), Rotation(Quaternion),
                             enum Authority { Server, Owner, Master }, ownerId, estado
    NetworkWorld.cs          contenedor de entidades por id (thread-safe): add/get/remove/all
    NetworkEntityManager.cs  genera ids únicos (ent_...) sin colisiones
  SimulationLoop.cs          bucle de tick en HILO PROPIO con Stopwatch (ritmo fijo)
  GameState.cs               world + número de tick actual
  GameRules.cs               IGameRules + NoOpGameRules (hook por tick, vacío de momento)
  NetworkRuntime.cs          compone todo y arranca/para el latido
```

Integración: `MatchInstance` posee su `NetworkRuntime`; al crear la partida
(`MatchOrchestrator`, con el `tickRate` de la config) arranca su latido, y al pararla
lo detiene. Cada partida late en su propio hilo, independiente del resto y del frame
rate de Unity.

## 3D desde el principio

La posición es **`System.Numerics.Vector3`** (X,Y,Z) y la rotación
**`System.Numerics.Quaternion`** (X,Y,Z,W). Son tipos estándar de .NET (no de
UnityEngine), así que el núcleo sigue siendo probable en headless y tendremos math 3D
real para el movimiento de la Fase 5.

> Nota para fases siguientes: los contratos `SPAWN_ENTITY` (Fase 4) y `STATE_DELTA`
> (Fase 5) se emitirán en **3D** (`x,y,z` + cuaternión `x,y,z,w`). En el roadmap salían
> en 2D porque asumía un cliente Phaser; aquí el cliente es 3D.

## Cómo corre el tick

Cada `SimulationLoop` arranca un hilo de fondo (`IsBackground`) que, con un
`Stopwatch`, invoca el callback de simulación a `tickRate` Hz (30 por defecto),
incrementa el contador de tick y, si se atrasa, reancla para no acumular deuda. El
orden futuro del tick (Fases 5-6) será `processInputs -> updateSystems -> replication`.

## Pruebas (Test Runner · EditMode)

- `NetworkWorldTests` — ids únicos sin colisión; world add/get/remove; entidad 3D
  (posición y cuaternión; rotación por defecto = identidad).
- `SimulationLoopTests` — el contador de tick incrementa; `Stop` detiene el bucle;
  tickRate inválido lanza. (Asserts de tiempo tolerantes para no salir flaky.)
- `NetworkRuntimeTests` — **dos runtimes tican de forma independiente**; una entidad
  añadida persiste entre ticks; `Stop` detiene el tick; el `GameState` refleja el tick.

## Definición de Hecho (DoD)

Cada partida late a ritmo fijo y mantiene su mundo en memoria, completamente aislado
de otras partidas. ✔️

## Nota de entorno
Usa `System.Numerics` (Vector3/Quaternion), disponible con Api Compatibility Level
**.NET Standard 2.1** (el de por defecto), igual que el resto del Game Server.

## Siguiente paso (Fase 4)
Spawn y despawn de entidades de red (NetworkPrefabRegistry, SpawnSystem, DespawnSystem)
y los contratos `SPAWN_ENTITY` / `DESPAWN_ENTITY` (en 3D), sobre este mundo que ya late.
