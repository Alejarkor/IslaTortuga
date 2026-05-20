# AGENTS.md — Guía de trabajo para Codex en IslaTortuga / El Sueño de la Tortuga

> Archivo pensado para colocarse en la raíz del repositorio como `AGENTS.md`.
> Objetivo: que Codex entienda el proyecto, respete la arquitectura, use buenas prácticas y no tome atajos peligrosos.

---

## 0. Regla principal

Este proyecto no es un prototipo cualquiera ni un juego single-player pintado en Phaser. Es un juego multijugador web, server-authoritative, de deducción social, exploración, supervivencia ligera y narrativa procedural.

Antes de modificar código:

1. Lee este archivo completo.
2. Inspecciona la estructura real del repositorio.
3. Identifica el package manager, framework y comandos existentes antes de inventarlos.
4. Haz cambios pequeños, coherentes y verificables.
5. No introduzcas dependencias nuevas salvo que estén justificadas.
6. No rompas la separación entre servidor autoritativo, cliente visual y lógica pura compartida.

Si hay conflicto entre este documento y el código real, prioriza el código real, pero deja constancia del conflicto y propón una corrección mínima.

---

## 1. Identidad del proyecto

### Nombre de trabajo

- Proyecto/plataforma: `IslaTortuga`
- Nombre del juego: `El Sueño de la Tortuga`

### Género

Juego multijugador top-down 2D pixel art, estilo aventura social, deducción e investigación. Cámara cenital/isométrica ligera según arte final, pero arquitectura 2D.

### Referencias conceptuales

- Deducción social tipo Among Us, pero menos arcade y más narrativa.
- Mundo persistente durante una partida corta.
- Exploración cooperativa con sospecha, alianzas, mentiras, expediciones y objetivos ocultos.
- Mapa principal estático, pero objetivos, pistas, objetos y dependencias procedurales.

### Premisa narrativa compacta

Varios personajes llegan a una isla antigua por motivos distintos. La isla está vinculada al sueño de una enorme tortuga mítica. Cada siete días, la tortuga entra en sueño profundo y la isla se reinicia/destruye simbólicamente. Para escapar, los jugadores deben mantener o reparar el faro y encenderlo el séptimo día, cuando un barco vendrá a buscarlos.

Hay dos grandes facciones:

- `Lúcidos`: quieren entender la isla, reparar el faro y escapar.
- `Velados`: quieren sabotear el faro, alimentar la bruma y quedar ligados al sueño de la isla.

La partida debe generar tensión social: no basta con hacer tareas; hay que hablar, investigar, sospechar, moverse juntos y decidir en quién confiar.

---

## 2. Objetivo técnico inicial

La primera gran fase del proyecto busca construir una base jugable mínima:

1. Base de datos.
2. Sistema de registro/login.
3. API backend.
4. Frontend web con login y portal/sala de espera.
5. Servidor realtime autoritativo.
6. Cliente Phaser capaz de entrar al mapa.
7. Movimiento de personaje validado por servidor.
8. Visualización de otros jugadores.
9. Chat por proximidad básico.

Nada de esta primera fase debe complicarse con sistemas avanzados de procedural, IA narrativa, crafting complejo o economía. Primero debe existir el esqueleto robusto.

---

## 3. Stack recomendado

Codex debe respetar el stack existente si el repositorio ya lo tiene. Si el repo está vacío o en fase inicial, esta es la recomendación base.

### Lenguaje principal

- TypeScript en todo lo posible.
- `strict: true` en `tsconfig`.
- Evitar `any`. Si es inevitable, justificarlo y aislarlo.

### Monorepo recomendado

Usar un monorepo con `pnpm` si no hay una decisión previa distinta.

Estructura recomendada:

```txt
/
├─ apps/
│  ├─ web/                # React + Vite + Phaser embebido
│  ├─ api/                # API HTTP: auth, users, portal, match management
│  └─ realtime/           # Servidor WebSocket autoritativo
├─ packages/
│  ├─ shared/             # Tipos, DTOs, schemas, constantes compartidas
│  ├─ game-core/          # Lógica pura de juego: movimiento, colisiones, inventario
│  └─ config/             # Config común: eslint, tsconfig, prettier
├─ prisma/                # Schema y migraciones si se usa Prisma
├─ docker-compose.yml     # Postgres/Redis/local services
├─ .env.example
├─ AGENTS.md
└─ README.md
```

### Frontend

- `Vite + React + TypeScript` para UI de login, portal, lobby, paneles y HUD.
- `Phaser 3` para renderizado del mundo 2D, cámara, sprites, animaciones, tilemaps y efectos.
- Phaser no debe ser la autoridad del juego. Phaser pinta e interpola; el servidor decide.

### Backend HTTP

Opción preferida si no existe otra:

- `Fastify + TypeScript`
- Validación con `Zod` o schemas equivalentes.
- Separar rutas, servicios, repositorios y dominio.

Alternativa aceptable:

- `NestJS`, si el proyecto ya ha arrancado con Nest o se necesita una arquitectura más opinionada.

No mezclar ambos sin necesidad.

### Realtime

- WebSocket con `ws` inicialmente.
- El servidor realtime es autoritativo.
- El cliente envía intenciones/input, nunca posiciones finales como verdad.
- Snapshots periódicos del estado autorizado.
- Más adelante se puede valorar Redis Pub/Sub, NATS, Colyseus o uWebSockets.js si hay necesidad real.

### Base de datos

- PostgreSQL.
- ORM recomendado: Prisma.
- Migraciones versionadas.
- `.env.example` siempre actualizado.
- Nunca usar SQLite como sustituto silencioso si el objetivo es PostgreSQL, salvo para tests explícitos.

### Cache / sesiones / pubsub

- Redis opcional, no obligatorio en la primera iteración.
- Puede usarse después para rate limiting, sesiones, matchmaking o escalado realtime.

### Mapas

- Tiled Map Editor para crear mapas, capas visuales, capas de colisión y metadatos.
- Exportar preferentemente a JSON.
- Tile size objetivo: 32x32.
- El mapa puede ser visualmente estático, pero los objetivos/pistas/objetos cambian por partida.

### Testing

- Unit tests: Vitest.
- Tests de integración de API: Vitest + cliente HTTP interno.
- Tests E2E web: Playwright, cuando exista flujo estable.
- La lógica de `game-core` debe ser testeable sin navegador, sin Phaser y sin base de datos.

---

## 4. Filosofía de arquitectura

### Principio clave

La autoridad vive en el servidor.

El cliente:

- Dibuja.
- Interpola.
- Muestra UI.
- Envía input.
- Puede predecir localmente solo si después reconcilia con el servidor.

El servidor:

- Valida login y sesión.
- Autoriza entrada a partida.
- Controla posición real.
- Controla colisiones.
- Controla inventario.
- Controla chat por proximidad.
- Decide qué eventos ve cada jugador.
- Genera IDs y timestamps.
- Persiste mensajes/eventos importantes.

### No hacer

- No confiar en coordenadas mandadas por el cliente.
- No guardar estado crítico de partida solo en el navegador.
- No resolver inventario solo en UI.
- No dejar que Phaser determine colisiones autoritativas.
- No exponer mensajes de chat a jugadores fuera de rango.
- No meter reglas de gameplay dentro de componentes React.
- No convertir Tiled en motor de gameplay. Tiled describe mapa y metadatos; el motor es nuestro.

### Sí hacer

- Separar lógica pura en `packages/game-core`.
- Compartir tipos cliente-servidor desde `packages/shared`.
- Validar todos los mensajes de red.
- Persistir eventos relevantes en una tabla/event log.
- Diseñar sistemas por intención: `move`, `interact`, `pickup`, `drop`, `speak`, `hide`, etc.
- Mantener cada feature pequeña y testeable.

---

## 5. Convenciones de código

### TypeScript

- Activar `strict`.
- Preferir tipos explícitos en APIs públicas.
- Evitar clases gigantes.
- Usar funciones puras para lógica de dominio.
- Usar discriminated unions para mensajes realtime.
- Validar runtime con Zod o equivalente. TypeScript no valida datos de red en runtime.

Ejemplo de mensaje:

```ts
export type ClientMessage =
  | { type: 'player.input'; payload: PlayerInputPayload }
  | { type: 'chat.send'; payload: ChatSendPayload }
  | { type: 'inventory.pickup'; payload: PickupPayload };
```

### Nombres

- Entidades en inglés técnico: `User`, `Session`, `Match`, `Player`, `InventoryItem`.
- Conceptos narrativos pueden ir en español si forman parte del dominio: `Lucido`, `Velado`, `Bruma`, `Faro`.
- Mantener consistencia. No alternar `game`, `match`, `room`, `session` para lo mismo.

Recomendación:

- `Match`: una partida concreta.
- `Room/Lobby`: espera previa.
- `World`: estado vivo de la partida.
- `Player`: usuario dentro de una partida.
- `Character`: representación narrativa/jugable.

### Estilo

- Prettier.
- ESLint.
- Imports ordenados.
- Sin código muerto.
- Sin logs ruidosos en producción.
- Los comentarios deben explicar decisiones, no repetir el código.

### Errores

Usar errores de dominio claros:

```ts
throw new DomainError('PLAYER_NOT_IN_RANGE', 'Player is not close enough to interact');
```

No devolver strings sueltos inconsistentes desde cada servicio.

---

## 6. Seguridad mínima obligatoria

### Autenticación

- Passwords hasheadas con Argon2 o bcrypt.
- Nunca guardar contraseñas en texto plano.
- Nunca loguear password, tokens o refresh tokens.
- Access token de corta duración.
- Refresh token seguro, preferiblemente en cookie `httpOnly`, `secure`, `sameSite` adecuado.
- Para WebSocket, autenticar el handshake con token válido.

### OAuth Google

Puede existir login con Google, pero no debe bloquear la primera fase si se decide empezar con email/password.

Si se implementa Google OAuth:

- Separar `User` de `AuthAccount`.
- Una cuenta puede tener varios proveedores.
- No asumir que el email siempre está verificado salvo que Google lo indique.

### API

- Validar body/query/params.
- Rate limit en login/registro.
- CORS configurado explícitamente.
- No permitir origen `*` en producción con credenciales.
- Sanitizar errores devueltos al cliente.

### Realtime

- Validar todo mensaje entrante.
- Limitar frecuencia de mensajes por cliente.
- Ignorar inputs imposibles.
- Desconectar o penalizar spam.
- No enviar estado completo si el jugador no debe verlo.

### Secretos

- No commitear `.env` real.
- Mantener `.env.example` actualizado.
- No inventar claves fake en documentación salvo placeholders claros.

---

## 7. Base de datos inicial

Modelo recomendado para primera fase. Ajustar al código real si ya existe.

### Tablas principales

- `users`
  - `id`
  - `email`
  - `password_hash` nullable si solo OAuth
  - `display_name`
  - `created_at`
  - `updated_at`

- `auth_accounts`
  - `id`
  - `user_id`
  - `provider` (`local`, `google`)
  - `provider_user_id`
  - `email`
  - `created_at`

- `refresh_tokens` o `sessions`
  - `id`
  - `user_id`
  - `token_hash`
  - `expires_at`
  - `revoked_at`
  - `created_at`

- `matches`
  - `id`
  - `status` (`waiting`, `running`, `finished`)
  - `seed`
  - `current_day`
  - `created_at`
  - `started_at`
  - `finished_at`

- `match_players`
  - `id`
  - `match_id`
  - `user_id`
  - `role` (`lucido`, `velado`, nullable antes de empezar)
  - `nickname`
  - `spawn_x`
  - `spawn_y`
  - `last_x`
  - `last_y`
  - `joined_at`
  - `left_at`

- `chat_messages`
  - `id`
  - `match_id`
  - `sender_player_id`
  - `mode` (`whisper`, `normal`, `shout`, `board`, `private_note`)
  - `text`
  - `world_x`
  - `world_y`
  - `created_at`

- `chat_message_recipients`
  - `message_id`
  - `recipient_player_id`
  - `heard_level` (`clear`, `muffled`, `indicator_only`)

- `world_events`
  - `id`
  - `match_id`
  - `actor_player_id` nullable
  - `type`
  - `payload_json`
  - `world_x`
  - `world_y`
  - `created_at`

Más adelante:

- `item_definitions`
- `item_instances`
- `player_inventory_slots`
- `world_object_instances`
- `objective_templates`
- `match_objectives`
- `clue_instances`
- `evidence_traces`

### Regla de persistencia

No todo estado runtime debe persistirse en cada tick. Persistir:

- Usuarios/sesiones.
- Partidas y jugadores.
- Eventos relevantes.
- Chat que deba quedar en historial del jugador.
- Objetivos generados.
- Inventario y objetos si la partida sobrevive reinicios.

No persistir cada micro-movimiento salvo que haya una razón clara.

---

## 8. API HTTP esperada

Primera fase mínima:

```txt
GET  /health
POST /auth/register
POST /auth/login
POST /auth/logout
POST /auth/refresh
GET  /me
GET  /matches
POST /matches
POST /matches/:id/join
GET  /matches/:id
```

Principios:

- Respuestas consistentes.
- Errores con código estable.
- DTOs compartidos en `packages/shared`.
- No filtrar datos privados de otros usuarios.

Ejemplo de error:

```json
{
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "Invalid email or password"
  }
}
```

---

## 9. Protocolo realtime esperado

### Conexión

1. Cliente obtiene access token tras login.
2. Cliente abre WebSocket contra realtime server.
3. Servidor valida token.
4. Cliente solicita unirse a una partida.
5. Servidor responde con snapshot inicial permitido.

### Mensajes cliente-servidor

```ts
type ClientToServer =
  | { type: 'match.join'; payload: { matchId: string } }
  | { type: 'player.input'; payload: PlayerInputPayload }
  | { type: 'chat.send'; payload: ChatSendPayload }
  | { type: 'interaction.request'; payload: InteractionPayload }
  | { type: 'inventory.pickup'; payload: PickupPayload }
  | { type: 'inventory.drop'; payload: DropPayload };
```

### Mensajes servidor-cliente

```ts
type ServerToClient =
  | { type: 'world.snapshot'; payload: WorldSnapshot }
  | { type: 'world.delta'; payload: WorldDelta }
  | { type: 'chat.received'; payload: ReceivedChatMessage }
  | { type: 'interaction.result'; payload: InteractionResult }
  | { type: 'error'; payload: ServerErrorPayload };
```

### Movimiento

- Cliente manda input: dirección, flags, secuencia, timestamp cliente opcional.
- Servidor simula movimiento en ticks.
- Servidor aplica colisiones.
- Servidor manda snapshot/delta.
- Cliente interpola.

No aceptar:

```ts
{ type: 'setPosition', x: 100, y: 200 }
```

como verdad del cliente.

### Tickrate recomendado inicial

- Simulación servidor: 10-20 Hz para empezar.
- Snapshots: 10 Hz o menos, según fluidez.
- Cliente renderiza a FPS del navegador interpolando.

No optimizar prematuramente. Primero claridad y estabilidad.

---

## 10. Phaser y Tiled

### Rol de Phaser

Phaser sirve para:

- Renderizar tilemap.
- Renderizar personajes.
- Animaciones.
- Cámara.
- Luces/efectos visuales si procede.
- HUD simple del canvas si conviene.

Phaser no sirve como autoridad de:

- Posición final.
- Colisiones reales.
- Inventario.
- Chat.
- Visibilidad de información.
- Objetivos de partida.

### Rol de Tiled

Tiled sirve para:

- Capas visuales.
- Capas de colisión.
- Objetos de mapa.
- Zonas: playa, pueblo, faro, bosque, cueva, etc.
- Puntos de spawn.
- Tags de ubicación para procedural.

Ejemplo de capas:

```txt
Ground
DecorationBelow
Collision
Interactables
SpawnPoints
ProceduralSlots
DecorationAbove
```

### Metadatos útiles en Tiled

- `zone`: `beach`, `village`, `forest`, `lighthouse`, `cave`
- `slotType`: `clue`, `item`, `corpse`, `tool`, `danger`, `ritual`
- `walkable`: boolean
- `blocksSight`: boolean futuro
- `interactableType`: `well`, `board`, `bed`, `tree`, `chest`, etc.

### Colisiones

El servidor debe cargar una representación de colisiones.
Puede ser:

- Grid derivado del tilemap.
- Polígonos exportados de Tiled.
- Preprocesado a JSON compacto.

El cliente puede usar colisiones locales para suavizar, pero la corrección final es del servidor.

---

## 11. Chat por proximidad

### Objetivo

El chat no es global por defecto. Cada jugador solo conserva en su historial lo que ha presenciado o recibido.

### Modos

- `whisper`: rango corto.
- `normal`: rango medio.
- `shout`: rango largo, puede mostrar indicador direccional.
- `board`: mensaje público en tablón físico del mundo.
- `private_note`: sistema futuro para notas/cartas/mensajes privados físicos o creativos.

### Privacidad

Privacidad significa:

- El cliente solo recibe mensajes que puede o debe conocer.
- El historial local del jugador solo muestra mensajes presenciados.
- La base de datos registra destinatarios por mensaje.

No significa:

- Cifrado extremo a extremo.
- Que el servidor no pueda leer mensajes.

No prometer privacidad criptográfica si no se implementa.

### Representación UI

Preferencia actual:

- Panel de chat con mensajes en orden temporal.
- Cada mensaje indica autor, hora, modo y contenido.
- Solo aparecen mensajes presenciados.
- Encima del personaje puede aparecer bocadillo/indicador visual opcional.
- Si alguien susurra lejos: no se recibe.
- Si alguien grita lejos: puede aparecer indicador de procedencia sin texto completo.

### Persistencia

Al enviar un mensaje:

1. Servidor valida longitud, frecuencia y modo.
2. Calcula receptores según posición y rango.
3. Crea `chat_messages`.
4. Crea `chat_message_recipients`.
5. Emite solo a receptores autorizados.

---

## 12. Inventario y objetos

### Objetivo

Cada jugador puede llevar objetos, equiparlos en la mano, dejarlos, intercambiarlos y usarlos en interacciones.

### Diseño base

Separar:

- `ItemDefinition`: tipo de objeto. Ejemplo: llave oxidada, cuerda, martillo.
- `ItemInstance`: objeto concreto en una partida.
- `InventorySlot`: posición concreta donde está.

Slots iniciales:

- `hand_left` opcional futuro.
- `hand_right` o `active_hand`.
- `backpack_0..N`.
- `body` futuro si hay ropa/equipamiento.

### Acciones

- `pickup`
- `drop`
- `equip`
- `unequip`
- `transfer`
- `use`
- `combine` futuro

Todas las acciones se validan en servidor:

- Distancia al objeto.
- Capacidad de mochila.
- Si el objeto se puede coger.
- Si el jugador está dormido/escondido/incapacitado.
- Si otro jugador lo cogió antes.

### No hacer

- No cambiar inventario solo en React.
- No permitir duplicación por doble click.
- No aceptar `itemId` sin comprobar pertenencia/ubicación.

---

## 13. Sistema de escondite

### Concepto

Existen objetos del mundo donde un jugador puede esconderse: árboles, plantas, camas, cortinas, armarios, etc.

### Estado

Un jugador escondido:

- No se renderiza normalmente a otros jugadores.
- Puede tener movilidad bloqueada.
- Puede seguir oyendo según reglas de proximidad.
- Puede ser descubierto si otro jugador inspecciona el escondite o intenta entrar.

### Entidades

- `HideSpotDefinition`
- `HideSpotInstance`
- `PlayerHiddenState`

### Acciones

- `hide.enter`
- `hide.exit`
- `hide.inspect`

### Validaciones

- Distancia al escondite.
- Capacidad del escondite.
- Estado actual del jugador.
- Si el escondite está bloqueado o revelado.

---

## 14. Sistema procedural

### Principio

El mapa puede ser estático, pero la partida debe sentirse distinta porque cambian:

- Averías del faro.
- Objetivos necesarios.
- Ubicación de objetos clave.
- Pistas.
- Dependencias.
- Pequeñas ubicaciones o estados.
- Eventos de bruma/luz.

### Enfoque recomendado: grafo de objetivos

Cada partida se genera desde una seed.

El generador selecciona un subconjunto de problemas del faro:

```txt
Faro necesita:
- Lente limpia o reemplazada.
- Combustible especial.
- Engranaje de rotación.
- Llave de sala técnica.
- Ritual de luz opcional.
```

Cada problema implica:

- Objetos requeridos.
- Pistas.
- Posibles ubicaciones.
- Dependencias.
- Riesgos.
- Oportunidades de sabotaje.

### Plantillas

Ejemplo conceptual:

```ts
type ObjectiveTemplate = {
  id: string;
  tags: string[];
  requiredItems: ItemRequirement[];
  clueTemplates: ClueTemplate[];
  placementRules: PlacementRule[];
  dependencies: ObjectiveDependency[];
};
```

### Slots procedurales

El mapa debe tener slots con tags:

```txt
slot: well_01
zone: village
supports: clue,item,corpse
risk: medium
```

Si una pieza está en un pozo, las pistas posibles pueden depender de esa ubicación:

- Cadáver cercano con nota mojada.
- Tablón del pueblo mencionando el pozo.
- Huellas húmedas.
- Cubo roto.

### Regla importante

La proceduralidad debe generar combinaciones válidas, no solo aleatorias.

Cada objetivo generado debe garantizar:

- Existe al menos una ruta resoluble.
- Hay pistas suficientes.
- No se bloquea por una dependencia circular.
- Los objetos requeridos existen.
- Las ubicaciones elegidas soportan ese tipo de objeto/pista.

### Persistencia

El resultado generado se guarda. No se regenera distinto al reiniciar el servidor.

---

## 15. Sistema forense de eventos y pistas

### Idea base

El mundo genera rastros a partir de eventos, no frases inventadas sin datos.

Ejemplos de eventos:

- Un jugador entra en una zona de playa.
- Un jugador roba una mochila.
- Un jugador duerme.
- Un jugador manipula el faro.
- Un jugador usa fuego.
- Un jugador se esconde.
- Un jugador rompe un objeto.

### Event log

Guardar eventos relevantes:

```ts
type WorldEvent = {
  id: string;
  matchId: string;
  actorPlayerId?: string;
  type: string;
  position?: Vec2;
  payload: unknown;
  createdAt: string;
};
```

### Rastros

Un sistema de rastros puede convertir eventos en evidencia:

- Arena en botas.
- Olor a humo.
- Huellas cerca de un pozo.
- Arañazos en una puerta.
- Sombra vista por un NPC o jugador.
- Mochila removida.

### Sin IA generativa en primera versión

No usar IA generativa para inventar pistas. Usar plantillas parametrizadas.

Ejemplo:

```txt
Plantilla: "Hay restos de arena cerca de {objectName}."
Condición: actor estuvo en zona beach en los últimos X minutos.
```

### Diseño recomendado

- `WorldEvent`: hecho bruto.
- `TraceRule`: regla que transforma evento en posible rastro.
- `TraceInstance`: rastro concreto visible/investigable.
- `ClueTextTemplate`: texto parametrizado.

---

## 16. Roles, equipos y equilibrio

### Facciones

- `Lúcidos`: mayoría.
- `Velados`: minoría saboteadora.

### Distribución tentativa

- 6 jugadores: 5 lúcidos / 1 velado.
- 7 jugadores: 5 lúcidos / 2 velados.
- 8 jugadores: 6 lúcidos / 2 velados.

Esto no es definitivo. Codex no debe hardcodear estas reglas como inmutables. Deben estar en config.

### Objetivo común

El faro debe estar listo y encendido el séptimo día para que el barco pueda encontrarlos.

### Incentivos

El diseño debe favorecer:

- Expediciones en grupo.
- Sospecha razonable.
- Riesgos al ir solo.
- Información parcial.
- Recompensas por explorar.
- Peligro de sabotaje.

---

## 17. Configuración y entornos

### Variables esperadas

Mantener `.env.example` con claves como:

```env
NODE_ENV=development
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/islatortuga
JWT_ACCESS_SECRET=change-me
JWT_REFRESH_SECRET=change-me
ACCESS_TOKEN_TTL_SECONDS=900
REFRESH_TOKEN_TTL_DAYS=30
WEB_ORIGIN=http://localhost:5173
API_PORT=3000
REALTIME_PORT=3001
```

Si se usa Google OAuth:

```env
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
GOOGLE_CALLBACK_URL=http://localhost:3000/auth/google/callback
```

### Docker Compose

Para desarrollo local:

- Postgres.
- Redis opcional.

No meter servicios innecesarios.

---

## 18. Comandos esperados

Codex debe inspeccionar `package.json` antes de asumir comandos. Si no existen, proponerlos.

Comandos recomendados:

```bash
pnpm install
pnpm dev
pnpm build
pnpm test
pnpm lint
pnpm typecheck
pnpm prisma:migrate
pnpm prisma:generate
```

Antes de finalizar una tarea, intentar ejecutar al menos:

```bash
pnpm typecheck
pnpm test
pnpm lint
```

Si no existen, informar claramente y no fingir que se han ejecutado.

---

## 19. Fases de desarrollo

### Fase 1 — Base web + auth + portal

Objetivo:

- Proyecto arrancable.
- Base de datos local.
- Registro/login.
- Sesión persistente.
- Pantalla portal/lobby.

Tareas:

1. Crear estructura monorepo.
2. Configurar TypeScript, ESLint, Prettier.
3. Docker Compose con Postgres.
4. Prisma schema inicial.
5. API `/health`.
6. Auth local.
7. Frontend login/register.
8. Página `/portal` protegida.
9. Endpoint `/me`.
10. Tests mínimos de auth.

Definición de terminado:

- Un usuario puede registrarse.
- Puede iniciar sesión.
- Puede ver el portal.
- Puede cerrar sesión.
- La DB conserva el usuario.

### Fase 2 — Realtime + mapa + movimiento

Objetivo:

- Entrar en partida.
- Ver mapa Tiled.
- Mover personaje.
- Ver otros jugadores.

Tareas:

1. Servidor WebSocket autenticado.
2. Endpoint para crear/unirse a partida.
3. Phaser carga mapa.
4. Cliente conecta a realtime.
5. Servidor simula movimiento.
6. Colisiones básicas.
7. Snapshots a clientes.
8. Interpolación visual.

Definición de terminado:

- Dos navegadores pueden entrar.
- Cada jugador ve al otro.
- El movimiento inválido no se acepta.
- El servidor conserva la autoridad.

### Fase 3 — Chat por proximidad

Objetivo:

- Hablar en susurro, normal y grito.
- Cada jugador solo ve lo que escucha.

Tareas:

1. Mensaje `chat.send`.
2. Rangos por modo.
3. Persistencia mensaje + destinatarios.
4. UI historial.
5. Indicador de grito lejano opcional.
6. Rate limit básico.

Definición de terminado:

- Si estás fuera de rango, no recibes el texto.
- Si estás en rango, queda en historial.
- El servidor calcula receptores.

### Fase 4 — Objetos e inventario básico

Objetivo:

- Recoger, llevar en mano, soltar y usar objetos simples.

Tareas:

1. Definición de objetos.
2. Instancias en mundo.
3. Slots de inventario.
4. Interacciones pickup/drop/equip.
5. Render de objeto en mapa o mano.
6. Validaciones de distancia/capacidad.

### Fase 5 — Faro y objetivos procedurales mínimos

Objetivo:

- Generar una partida con objetivos del faro variables.

Tareas:

1. Seed por partida.
2. Plantillas de objetivo.
3. Slots procedurales en mapa.
4. Generador de grafo válido.
5. Persistencia del resultado.
6. UI básica de progreso conocido.

---

## 20. Buenas prácticas específicas para Codex

### Antes de codificar

Codex debe:

1. Leer `AGENTS.md`.
2. Leer `README.md` si existe.
3. Leer `package.json` raíz y de apps/packages.
4. Revisar estructura real.
5. Revisar tests existentes.
6. Identificar patrones actuales antes de añadir nuevos.

### Durante la tarea

- Cambiar el mínimo necesario.
- No reescribir media arquitectura para una tarea pequeña.
- Mantener nombres coherentes.
- Crear tests para lógica nueva.
- Actualizar tipos compartidos si cambia el contrato cliente-servidor.
- Actualizar `.env.example` si cambia configuración.
- Actualizar README si cambia cómo arrancar el proyecto.

### Al terminar

Codex debe resumir:

- Qué ha cambiado.
- Archivos principales tocados.
- Tests/comandos ejecutados.
- Qué no pudo verificar.
- Riesgos o siguientes pasos.

### No hacer sin permiso explícito

- Borrar grandes carpetas.
- Cambiar de framework.
- Migrar de Phaser a Unity.
- Cambiar PostgreSQL por otra DB.
- Meter Firebase/Supabase como sustituto de arquitectura propia.
- Añadir pagos, analytics o servicios externos.
- Añadir IA generativa al runtime del juego.
- Cambiar el lore base.
- Hacer commits o push si el usuario no lo pide.

---

## 21. Anti-errores frecuentes

### Error: tratar Phaser como Unity

Phaser no tiene un ecosistema tipo Unity con prefabs, colliders y player controller listos de la misma forma. Hay que programar sistemas y usar Tiled para datos de mapa. No diseñar como si hubiera GameObjects autoritativos.

### Error: cliente autoritativo

No dejar que el cliente diga dónde está. El cliente envía input; el servidor decide posición.

### Error: chat global accidental

No emitir mensajes a todos por comodidad. El chat por proximidad es una mecánica central.

### Error: procedural aleatorio sin validez

No colocar objetos/pistas al azar sin garantizar que el objetivo se puede resolver.

### Error: mezclar lógica con UI

React no debe contener reglas de inventario o colisiones. Phaser no debe contener reglas de roles. La lógica debe ir en servicios o `game-core`.

### Error: persistir demasiado

No guardar cada frame/tick en Postgres. Usar memoria runtime y persistir eventos relevantes.

### Error: falta de validación runtime

Los mensajes de red deben validarse. TypeScript no protege de JSON malicioso.

### Error: dependencias grandes por comodidad

No añadir librerías pesadas para resolver algo simple sin justificar.

---

## 22. Contratos de dominio sugeridos

### Vector

```ts
export type Vec2 = {
  x: number;
  y: number;
};
```

### Player runtime

```ts
export type PlayerState = {
  id: string;
  userId: string;
  matchId: string;
  nickname: string;
  position: Vec2;
  velocity: Vec2;
  facing: 'up' | 'down' | 'left' | 'right';
  hidden?: boolean;
  activeItemId?: string;
};
```

### Input

```ts
export type PlayerInputPayload = {
  seq: number;
  moveX: number;
  moveY: number;
  sprint?: boolean;
  interact?: boolean;
};
```

### Chat

```ts
export type ChatMode = 'whisper' | 'normal' | 'shout';

export type ChatSendPayload = {
  mode: ChatMode;
  text: string;
};
```

### Snapshot

```ts
export type WorldSnapshot = {
  serverTime: number;
  matchId: string;
  selfPlayerId: string;
  players: PlayerState[];
};
```

Estos contratos son orientativos. No duplicarlos si ya existen equivalentes.

---

## 23. UX inicial

### Pantallas

1. Login.
2. Registro.
3. Portal.
4. Lobby/espera.
5. Juego.

### Portal

Debe mostrar:

- Usuario logueado.
- Crear partida.
- Unirse a partida.
- Lista simple de partidas disponibles si existe backend para ello.

### Juego

Debe mostrar:

- Canvas Phaser.
- HUD mínimo.
- Chat.
- Estado de conexión.
- Botón/salida para volver al portal.

No buscar una estética final en fase 1. Priorizar flujo completo.

---

## 24. Arte y assets

### Estilo

- Pixel art top-down.
- Tile base: 32x32.
- Mantener consistencia de resolución.
- Evitar mezclar assets con escalas incompatibles sin procesar.

### Organización

```txt
apps/web/public/assets/
├─ maps/
├─ tilesets/
├─ sprites/
│  ├─ characters/
│  ├─ objects/
│  └─ effects/
└─ audio/
```

### Licencias

No añadir assets sin anotar licencia y procedencia.

Crear si procede:

```txt
ASSETS_LICENSES.md
```

Debe incluir:

- Nombre del asset pack.
- Autor.
- URL/fuente.
- Licencia.
- Si permite uso comercial.
- Si requiere atribución.

---

## 25. Rendimiento y escalabilidad

Primera fase: no sobrediseñar.

Pero sí respetar bases:

- No mandar snapshots enormes si no hace falta.
- No recalcular pathfinding global cada frame.
- No consultar Postgres en cada tick por jugador.
- Mantener estado runtime en memoria del servidor realtime.
- Persistir eventos por lotes si aumenta carga.
- Separar API y realtime aunque puedan correr juntos en desarrollo.

Futuro:

- Interest management por zonas.
- Redis/NATS para escalar varias instancias.
- Matchmaking dedicado.
- Observabilidad con logs estructurados.

---

## 26. Logs y observabilidad

Usar logs claros:

- Inicio de servidor.
- Conexiones/desconexiones.
- Errores de auth.
- Errores de validación resumidos.
- Inicio/fin de partida.

No loguear:

- Passwords.
- Tokens.
- Mensajes privados completos en producción salvo necesidad legal/técnica clara.
- Datos sensibles innecesarios.

Formato recomendado:

```ts
logger.info({ matchId, playerId }, 'player joined match');
```

---

## 27. Documentación mínima

Mantener:

- `README.md`: cómo arrancar el proyecto para humanos.
- `AGENTS.md`: instrucciones para agentes.
- `.env.example`: variables necesarias.
- `docs/architecture.md`: cuando la arquitectura crezca.
- `docs/protocol.md`: cuando el protocolo realtime tenga muchas variantes.
- `docs/game-design.md`: cuando las reglas de juego se estabilicen.

No meter todo en README.

---

## 28. Criterios de calidad por PR/tarea

Una tarea está bien cerrada si:

- Compila.
- Pasa typecheck.
- Pasa tests relevantes.
- No rompe comandos existentes.
- Tiene validación de entrada si recibe datos externos.
- No introduce secretos.
- No cambia contratos sin actualizar cliente/servidor.
- No añade deuda innecesaria.
- Incluye explicación breve de decisiones.

---

## 29. Decisiones abiertas

No hardcodear como definitivas:

- Número exacto de jugadores por partida.
- Ratio final Lúcidos/Velados.
- Duración real de cada día.
- Si habrá voz o solo texto inicialmente.
- Si habrá crafting complejo.
- Si habrá progresión meta entre partidas.
- Cómo se resuelve exactamente la victoria narrativa.
- Nivel de persistencia del mundo entre reconexiones.

Diseñar con configuración para permitir cambios.

---

## 30. Prioridad actual

Si el usuario pide avanzar sin especificar, priorizar en este orden:

1. Arranque limpio del proyecto.
2. Base de datos.
3. Auth.
4. Portal.
5. Realtime autenticado.
6. Mapa Phaser/Tiled.
7. Movimiento server-authoritative.
8. Chat por proximidad.
9. Inventario básico.
10. Objetivos procedurales.

No saltar a sistemas avanzados si la base no existe.

---

## 31. Respuesta esperada de Codex al trabajar

Cuando Codex complete una tarea, responder con formato:

```txt
Hecho:
- ...

Archivos tocados:
- ...

Validación:
- pnpm typecheck: OK / no existe / falló por ...
- pnpm test: OK / no existe / falló por ...
- pnpm lint: OK / no existe / falló por ...

Notas:
- ...

Siguiente paso recomendado:
- ...
```

No ocultar fallos. No decir que algo está probado si no se ejecutó.

---

## 32. Resumen mental del proyecto para Codex

Este proyecto quiere construir un juego multijugador web de deducción social llamado `El Sueño de la Tortuga`. El jugador entra con cuenta, accede a un portal, se une a una partida y explora una isla 2D top-down pixel art. La partida gira en torno a reparar y encender un faro antes del séptimo día, mientras una minoría de jugadores sabotea desde dentro. La arquitectura debe ser seria: PostgreSQL, API, auth, servidor realtime autoritativo, Phaser como cliente visual, Tiled como editor de mapa, lógica pura separada y validación estricta.

La prioridad no es hacer una demo bonita rápidamente. La prioridad es construir una base sólida para que después puedan crecer sistemas como chat por proximidad, inventario, pistas forenses, escondites, proceduralidad narrativa y roles ocultos sin tener que rehacerlo todo.

