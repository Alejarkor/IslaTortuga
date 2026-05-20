# API Server

Este proyecto NestJS ya no aloja la simulacion realtime del juego.

## Responsabilidades

- autenticacion
- sesiones HTTP
- acceso a base de datos
- portal y prejuego
- endpoints de soporte para lobby y flujo de entrada

## Fuera de este proyecto

Estas responsabilidades se movieron al servidor de juego en C#:

- game loop
- websocket autoritativo
- rooms
- replicacion de entidades
- simulacion del mundo

Servidor de juego:

- [src/IslaTortuga.Server](C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server)

## Comandos

```bash
pnpm run start:dev
pnpm run build
```
