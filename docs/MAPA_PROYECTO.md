# Mapa del Proyecto

```txt
IslaTortuga/
|- apps/
|  |- client/
|  |  `- src/
|  |     |- features/              # Auth, portal y sesion de juego
|  |     |- game/
|  |     |  |- bootstrap/          # Arranque del runtime cliente
|  |     |  |- content/            # Manifest, cache y catalogo
|  |     |  `- runtime/            # Babylon, escenas 3D y red
|  |     `- shared/                # HTTP comun y utilidades
|  `- server/
|     `- src/                      # API Nest y prejuego
|- content-packs/
|  |- index.json
|  `- v001/
|     |- manifest.json
|     |- scenes/                   # Escenas exportadas desde Unity
|     |- models/
|     |- textures/
|     |- materials/
|     |- audio/
|     |- animations/
|     `- definitions/              # scene-definitions, entity visuals, reglas
|- docs/
|- src/
|  |- IslaTortuga.Protocol/        # Contrato de red y schemas
|  |- IslaTortuga.Server/          # Servidor C# standalone heredado
|  `- IslaTortuga.Server.Core/     # Nucleo C# reutilizable
`- Unity/
   `- IslaTortugaServer/
      `- Assets/Scripts/
         |- Bootstrap/             # Arranque del host autoritativo
         |- Networking/            # Gateway HTTP/WebSocket embebido
         `- ServerCore/            # Mundo, sesiones y replicacion
```

## Regla base

- `content-packs/` es el runtime que consume Babylon.
- Unity es la fuente de verdad para escenas 3D y simulacion.
- `apps/server` no simula gameplay.
- El cliente Babylon es 3D y consume escenas y visuales exportados desde Unity.
