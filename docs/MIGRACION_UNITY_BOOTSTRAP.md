# Migracion del Servidor a Unity

## Resumen

El repositorio ya contiene un proyecto Unity real en [Unity/IslaTortugaServer](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer). El servidor autoritativo de juego se ha empezado a migrar ahi como runtime embebido.

Para meter el servidor dentro de Unity sin romperlo, la migracion debe hacerse en dos capas:

1. Extraer el nucleo de simulacion a una libreria embebible.
2. Crear en Unity una escena `Bootstrap` que instancie ese runtime y conecte transporte, contenido y ciclo de tick.

La libreria base extraida en este repo es:

- [IslaTortuga.Server.Core.csproj](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server.Core/IslaTortuga.Server.Core.csproj)

Y su integracion actual en Unity vive en:

- [Assets/Scripts/ServerCore](</C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/ServerCore>)
- [Assets/Scripts/Bootstrap/ServerBootstrapBehaviour.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/Bootstrap/ServerBootstrapBehaviour.cs)
- [Assets/Scenes/Bootstrap.unity](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scenes/Bootstrap.unity)

## Que entra en Unity

- `GameTicketService`
- `SessionManager`
- `GameRoomManager`
- `GameWorld`
- `PlayerEntity`
- `TiledWorldBuilder`
- `SnapshotBuilder`
- `EmbeddedGameServerHost`

Todo eso ya vive desacoplado del host web y, dentro de Unity, arranca con un `MonoBehaviour` que resuelve `content-packs`, localiza el mapa y ejecuta el tick en `FixedUpdate`.

## Que no conviene meter en el cliente Unity

- `JWT_SECRET`
- `DATABASE_URL`
- Prisma/PostgreSQL
- Auth HTTP publica
- secretos de firma productivos

Si Unity corre en maquinas de jugadores, meter ahi la base de datos o las claves de auth expone el sistema entero. Lo razonable es:

- mover a Unity el servidor autoritativo de simulacion
- mantener fuera la identidad, cuentas y persistencia sensible

## Escena Bootstrap objetivo

La escena `Bootstrap` deberia crear un objeto persistente con responsabilidades muy separadas:

1. Resolver rutas de contenido
2. Cargar el mapa inicial
3. Crear `EmbeddedGameServerHost`
4. Arrancar el tick en `FixedUpdate`
5. Exponer snapshots al cliente local o al transporte de red que toque
6. Mantener viva la escena con `DontDestroyOnLoad`

## Integracion aplicada en Unity

La escena [Bootstrap.unity](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scenes/Bootstrap.unity) ya contiene un objeto `Server Bootstrap` con [ServerBootstrapBehaviour.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/Bootstrap/ServerBootstrapBehaviour.cs).

Ese componente:

1. Marca el objeto como persistente con `DontDestroyOnLoad`
2. Busca `content-packs` en el repo o en `StreamingAssets`
3. Localiza el mapa `.tmj`
4. Crea `EmbeddedGameServerHost`
5. Ejecuta el tick del servidor en `FixedUpdate`
6. Muestra un overlay simple con estado del bootstrap

Ejemplo orientativo del mismo patron:

```csharp
using IslaTortuga.Server.Core.Embedded;
using UnityEngine;

public sealed class ServerBootstrapBehaviour : MonoBehaviour
{
    private EmbeddedGameServerHost _server = null!;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        _server = new EmbeddedGameServerHost(new EmbeddedGameServerHostOptions
        {
            DefaultMapPath = System.IO.Path.Combine(Application.streamingAssetsPath, "content-packs", "v001", "maps", "island_01.tmj"),
            TickDeltaSeconds = Time.fixedDeltaTime,
            TicketSecret = "dev_game_ticket_secret_change_me",
        });
    }

    private void FixedUpdate()
    {
        var snapshots = _server.Tick();
        // Aqui se reenvian a cliente local, bots o transporte interno.
    }
}
```

## Cambios pendientes para completar la migracion

1. Decidir donde viviran definitivamente `content-packs` dentro de Unity:
   - `StreamingAssets`
   - `Addressables`
   - pipeline propio de importacion
2. Elegir el transporte dentro de Unity:
   - solo local/in-process
   - WebSocket interno
   - Unity Transport / Netcode adapter
3. Sustituir el cliente web Babylon por cliente Unity.
4. Reutilizar o reemplazar la API Nest para login y persistencia.
5. Eliminar la duplicacion temporal entre `src/IslaTortuga.Server.Core` y `Assets/Scripts/ServerCore` empaquetando el core como package o ensamblado estable para Unity.

## Arquitectura recomendada tras la migracion

- `Unity`
  - escena `Bootstrap`
  - cliente visual
  - runtime autoritativo embebido
- `Servicio externo opcional`
  - auth
  - cuentas
  - persistencia
  - matchmaking

## Siguiente paso recomendado

Abrir la escena [Bootstrap.unity](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scenes/Bootstrap.unity) en Unity y comprobar que el overlay reporta el mapa cargado y el tick creciendo. A partir de ahi, el siguiente bloque natural es conectar el cliente Unity a este runtime embebido en lugar del cliente Babylon web.
