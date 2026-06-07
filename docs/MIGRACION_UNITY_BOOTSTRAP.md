# Unity Bootstrap

## Resumen

La direccion actual del proyecto es:

- servidor autoritativo dentro de Unity
- cliente web 3D en Babylon
- escenas cliente exportadas desde Unity a `content-packs`

La migracion ya no busca sustituir Babylon por Unity en el cliente. Busca consolidar a Unity como host del mundo y a Babylon como renderer 3D remoto.

## Piezas activas

- [ServerBootstrapBehaviour.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/Bootstrap/ServerBootstrapBehaviour.cs)
- [EmbeddedGameServerHost.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/ServerCore/Embedded/EmbeddedGameServerHost.cs)
- [EmbeddedServerNetworkingHost.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/Networking/EmbeddedServerNetworkingHost.cs)
- [SceneTemplateRuntime.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/ServerCore/World/SceneTemplateRuntime.cs)

## Flujo esperado

1. Unity localiza `content-packs`.
2. Resuelve la escena inicial exportada.
3. Arranca `EmbeddedGameServerHost`.
4. Expone opcionalmente `GET /content`, `GET /health` y `WS /ws/game`.
5. Simula el mundo y emite `world.delta`.
6. Babylon consume `sceneId` y renderiza la escena 3D correspondiente.

## Pendientes razonables

- Exportar visuales 3D mas ricos que los proxies actuales del cliente.
- Replicar parametros de animator o hints de locomocion desde Unity al cliente.
- Unificar o retirar el servidor C# standalone heredado en `src/IslaTortuga.Server`.
