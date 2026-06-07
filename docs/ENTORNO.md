# Variables de entorno

Referencia minima de configuracion para el stack actual: API Nest, servidor de juego en Unity y cliente Babylon.

## Archivos base

- [`.env`](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/.env)
- [`.env.example`](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/.env.example)

## API y portal

- `DATABASE_URL`
  PostgreSQL para `apps/server`.

- `PORT`
  Puerto HTTP de la API Nest.

- `JWT_SECRET`
  Firma de JWT de login.

- `JWT_EXPIRES_IN`
  Duracion del JWT.

- `CLIENT_URL`
  URL del cliente web.

## Tickets y sesion de juego

- `GAME_TICKET_SECRET`
  Debe coincidir entre la API Nest y cualquier host del servidor de juego que valide tickets.

## Content packs

- `CONTENT_PACKS_ROOT`
  Ruta fisica a `content-packs`.
  La usan la API y el bootstrap de Unity cuando necesitan localizar el pack activo.

- `CONTENT_PACKS_DISABLE_CACHE`
  Solo afecta al servidor C# standalone legado.
  En el flujo principal con Unity embebido, la cache se controla desde el gateway o desde el cliente.

## Cliente Babylon

- `VITE_API_URL`
  Sobrescribe la URL de la API si el cliente no usa proxy local.

- `VITE_DISABLE_CONTENT_CACHE`
  Desactiva la cache del cliente para content-packs.

## Unity embebido

El bootstrap principal dentro de Unity usa:

- `CONTENT_PACKS_ROOT`
- `GAME_TICKET_SECRET`

El host y el puerto del gateway local se configuran hoy desde el inspector de [ServerBootstrapBehaviour.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/Unity/IslaTortugaServer/Assets/Scripts/Bootstrap/ServerBootstrapBehaviour.cs), no por variable de entorno.
