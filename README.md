# Isla Tortuga

Isla Tortuga es un juego web multijugador 2D top-down pixel art de misterio, exploracion, cooperacion y sabotaje social.

## Stack actual

- Cliente: Vite + React + Phaser + TypeScript
- API y prejuego: NestJS + TypeScript
- Servidor de juego autoritativo: C# + WebSocket + JSON
- Base de datos: PostgreSQL + Prisma
- Mapas: Tiled Map Editor
- Infraestructura local: Docker Compose

## Estructura

```txt
apps/
  client/                  # Cliente web React + Phaser
  server/                  # API, auth, sesiones y prejuego en NestJS
src/
  IslaTortuga.Server/      # Servidor de juego C# autoritativo
  IslaTortuga.Protocol/    # Protocolo JSON, schemas y ejemplos
content-packs/             # Paquetes runtime versionados servidos por HTTP
packages/
  shared/                  # Espacio reservado para codigo compartido si hiciera falta
assets/                    # Assets fuente del proyecto
docs/                      # Documentacion tecnica
infra/                     # Infraestructura y despliegue
```

## Reparto de responsabilidades

```txt
Cliente = experiencia visual y jugable
API = identidad, sesion, portal y prejuego
Game Server = verdad del mundo y simulacion
Base de datos = memoria persistente
```

## Estado actual

- `apps/server` mantiene autenticacion, acceso a base de datos y portal/prejuego.
- `src/IslaTortuga.Server` contiene el nuevo esqueleto del servidor de juego puro en C#.
- `src/IslaTortuga.Protocol` define el contrato JSON de red.
- `assets/` contiene los recursos fuente.
- `content-packs/` contiene los paquetes versionados que el cliente descarga antes de entrar al juego.

## Comandos utiles

```bash
pnpm run dev:client
pnpm run dev:api
pnpm run dev:game-server
pnpm run dev:content-tool
pnpm run build:api
pnpm run build:game-server
```

## Entorno

La referencia unica de variables de entorno esta en [docs/ENTORNO.md](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/docs/ENTORNO.md).

## Herramientas

- Importador GUI de mapas y tilesets a content packs:
  - [docs/CONTENT_TOOL.md](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/docs/CONTENT_TOOL.md)
