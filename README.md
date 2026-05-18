# \# Isla Tortuga

# 

# Isla Tortuga es un juego web multijugador 2D top-down pixel art de misterio, exploración, cooperación y sabotaje social.

# 

# Los jugadores despiertan en una isla misteriosa y deben colaborar para reparar y mantener encendido el faro antes del séptimo día. Algunos jugadores son Velados: saboteadores ocultos que intentan impedir la huida y arrastrar al grupo al sueño profundo de la isla.

# 

# \## Stack previsto

# 

# \- Cliente: Vite + React + Phaser + TypeScript

# \- Servidor: NestJS + Socket.IO + TypeScript

# \- Base de datos: PostgreSQL + Prisma

# \- Tiempo real: WebSocket mediante Socket.IO

# \- Mapas: Tiled Map Editor

# \- Infraestructura local: Docker Compose

# 

# \## Estructura

# 

# ```txt

# apps/

# &#x20; client/        # Cliente web React + Phaser

# &#x20; server/        # Backend NestJS

# packages/

# &#x20; shared/        # Tipos compartidos cliente/servidor

# assets/          # Assets fuente y exportados

# docs/            # Documentación técnica y fases

# infra/           # Configuración de despliegue e infraestructura

