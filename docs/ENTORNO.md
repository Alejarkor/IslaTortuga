# Variables de entorno

Este documento es la referencia unica para las variables de entorno del proyecto.

## Donde se definen

- Archivo local de desarrollo: [`.env`](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/.env)
- Plantilla base: [`.env.example`](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/.env.example)

## Variables activas

### Base de datos

- `DATABASE_URL`
  - La usa Prisma desde [schema.prisma](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/prisma/schema.prisma)
  - Conexion a PostgreSQL del backend Nest
  - Ejemplo:
    - `postgresql://isla:isla_password@localhost:5432/isla_tortuga`

### API Nest

- `PORT`
  - La usa [main.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/src/main.ts)
  - Puerto HTTP de la API y prejuego
  - Valor por defecto si falta: `3000`

- `JWT_SECRET`
  - La usa [auth.service.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/src/auth/auth.service.ts)
  - Clave con la que la API firma y valida los JWT de login

- `JWT_EXPIRES_IN`
  - La usa [auth.service.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/src/auth/auth.service.ts)
  - Tiempo de vida del JWT
  - Ejemplo: `7d`

- `CLIENT_URL`
  - Reservada para el backend/prejuego y configuracion de cliente
  - En local normalmente: `http://localhost:5173`

### Ticket de juego

- `GAME_TICKET_SECRET`
  - La usan:
    - [game-session.service.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/src/game-session/game-session.service.ts)
    - [GameTicketService.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server/Sessions/GameTicketService.cs)
  - Debe ser la misma en la API Nest y en el game server C#
  - Sirve para firmar y validar los `gameTicket`

### Content packs

- `CONTENT_PACKS_ROOT`
  - La usan:
    - [content-paths.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/server/src/game-session/content-paths.ts)
    - [ContentPathResolver.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server/Content/ContentPathResolver.cs)
  - Ruta fisica al directorio de `content-packs`
  - Puede ser relativa o absoluta
  - En local:
    - `./content-packs`
  - Si no existe, ambos servidores intentan localizar `content-packs` recorriendo carpetas hacia arriba

### Game server C#

- `GAME_SERVER_PORT`
  - La usa [Program.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server/Program.cs)
  - Puerto HTTP/WebSocket del servidor de juego
  - Valor por defecto si falta: `5055`

- `CONTENT_PACKS_DISABLE_CACHE`
  - La usa [Program.cs](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/src/IslaTortuga.Server/Program.cs)
  - Si vale `true`, el game server sirve `/content` con `Cache-Control: no-store`
  - Muy recomendable en desarrollo mientras el pipeline de content packs siga cambiando

### Cliente web

- `VITE_API_URL`
  - La usa [apiClient.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/client/src/shared/http/apiClient.ts)
  - Solo hace falta si quieres que el cliente apunte a una API distinta del proxy local de Vite
  - Si no se define, el cliente usa `/api`

- `VITE_DISABLE_CONTENT_CACHE`
  - La usa [assetCache.ts](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/apps/client/src/game/content/assetCache.ts)
  - Si vale `true`, el cliente no guarda `Cache API` de content packs
  - En desarrollo ahora mismo ya se desactiva cache por defecto incluso sin definir esta variable

## Regla practica

- Todo lo que deba poder cambiar entre maquinas o entornos debe estar aqui documentado
- Si se anade una variable nueva al codigo, hay que actualizar:
  - [`.env.example`](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/.env.example)
  - [ENTORNO.md](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/docs/ENTORNO.md)
