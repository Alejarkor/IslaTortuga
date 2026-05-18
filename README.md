# Isla Tortuga

Isla Tortuga es un juego web multijugador 2D top-down pixel art de misterio, exploración, cooperación y sabotaje social.

Los jugadores despiertan en una isla misteriosa y deben colaborar para reparar y mantener encendido el faro antes del séptimo día. Algunos jugadores son Velados: saboteadores ocultos que intentan impedir la huida y arrastrar al grupo al sueño profundo de la isla.

## Stack previsto

- Cliente: Vite + React + Phaser + TypeScript
- Servidor: NestJS + Socket.IO + TypeScript
- Base de datos: PostgreSQL + Prisma
- Tiempo real: WebSocket mediante Socket.IO
- Mapas: Tiled Map Editor
- Infraestructura local: Docker Compose

## Estructura

```txt
apps/
  client/        # Cliente web React + Phaser
  server/        # Backend NestJS
packages/
  shared/        # Tipos compartidos cliente/servidor
assets/          # Assets fuente y exportados
docs/            # Documentación técnica y fases
infra/           # Configuración de despliegue e infraestructura
```

## Filosofía técnica

```txt
Cliente = experiencia visual y jugable
Servidor = verdad del mundo
Base de datos = memoria persistente
```

## Primera fase

La primera fase se centra en:

- PostgreSQL en Docker
- API backend con NestJS
- Registro/login con JWT
- Portal inicial tras login
- Cliente React
- Integración Phaser
- Mapa básico 2D
- Movimiento local del personaje con colisiones
