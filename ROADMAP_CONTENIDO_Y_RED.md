# Roadmap de Contenido, Runtime y Red

Este documento fija el plan de trabajo para llevar el proyecto a una arquitectura limpia donde:

- la API Nest gestiona identidad, sesion, portal y prejuego
- el game server C# es autoritativo y contiene la logica del mundo
- los assets se guardan como fuente editable agnostica del cliente
- los content packs son paquetes runtime descargables
- Phaser actua como cliente visual, no como formato canonico de contenido

## Objetivo final

Queremos llegar a un sistema en el que:

1. Un creador edita contenido en `assets/`
2. Una herramienta interna valida y empaqueta ese contenido
3. El resultado se publica en `content-packs/`
4. La API dice al cliente que pack y mapa necesita
5. El cliente descarga el pack, lo cachea y lo adapta a Phaser
6. El game server carga el mismo contenido logico y simula el mundo
7. El cliente solo renderiza snapshots y envia input

## Principio base

El contenido no debe modelarse pensando directamente en Phaser.

Phaser es un consumidor del contenido, no la fuente de verdad. Eso implica:

- `assets/` guarda contenido editable y semantico
- `content-packs/` guarda contenido runtime distribuible
- el cliente tiene una capa de adaptacion a Phaser
- el game server tiene su propia capa de lectura del mismo contenido

## Vision de arquitectura

```txt
assets/ fuente editable
  -> pipeline de contenido
  -> content-packs runtime versionados
  -> API devuelve manifest y metadatos
  -> cliente descarga y adapta a Phaser
  -> game server carga y simula
```

## Fase 1. Fijar el modelo canonico de assets

### Objetivo

Decidir con precision donde vive cada cosa y cual es la fuente de verdad.

### Decisiones que deben quedar cerradas

- `assets/` es siempre la fuente editable
- `content-packs/` es siempre salida generada
- nada se edita a mano dentro de `content-packs/` salvo debugging puntual
- Tiled exporta mapas fuente para el pipeline, no para Phaser directamente
- sprites, audio y definiciones de gameplay se guardan por dominio y no por motor

### Estructura objetivo recomendada

```txt
assets/
  maps/
    source/
    exported/
  tilesets/
    source/
    exported/
  sprites/
    source/
    exported/
  audio/
    source/
    exported/
  definitions/
    entities/
    items/
    visuals/
    rules/
```

### Reglas

- `source/` contiene lo editable
- `exported/` contiene la salida intermedia si una herramienta externa la necesita
- `definitions/` no depende de Phaser
- la semantica importante del juego vive en datos, no en nombres dispersos de capas

### Validacion de cierre

- Existe una unica estructura acordada para `assets/`
- Sabemos que carpetas son editables y cuales son generadas
- Esta escrito que archivos nunca se deben tocar a mano
- Existe una convención de nombres para mapas, tilesets, sprites y definiciones

## Fase 2. Definir el contrato de contenido

### Objetivo

Diseñar que informacion minima debe contener un mapa y un pack para que puedan consumirlo:

- el game server
- el cliente Phaser
- futuros clientes de otro motor o plataforma

### Lo que debe quedar definido

- nombres canonicos de layers de mapa
- object layers permitidas
- clases y propiedades soportadas
- convencion de colisiones
- convencion de spawn points
- convencion de interactuables
- convencion de entidades visuales

### Propuesta de capas canonicas

- `Ground`
- `Trunks`
- `AbovePlayer`
- `SpawnPoints`
- `CollisionObjects`
- `Interactables`
- `Triggers`

### Propuesta de objetos y semantica

- `PlayerSpawn`
- `NpcSpawn`
- `Door`
- `Key`
- `Chest`
- `Pickup`
- `Trigger`
- `Transition`

### Propuesta de propiedades base

- `id`
- `class`
- `type`
- `level`
- `Collider`
- `blocksMovement`
- `blocksVision`
- `visualId`
- `entityArchetypeId`
- `interactionId`

### Validacion de cierre

- Existe una lista canonica de layers aceptadas
- Existe una lista canonica de tipos de objeto
- Existe una lista canonica de propiedades soportadas
- Sabemos que parte es visual y que parte es logica
- Un mapa puede validarse automaticamente contra estas reglas

## Fase 3. Diseñar el content pack runtime

### Objetivo

Definir el formato de distribucion que descargara el cliente y cargara el game server.

### Estructura objetivo

```txt
content-packs/
  index.json
  v001/
    manifest.json
    maps/
    tilesets/
    atlases/
    audio/
    definitions/
```

### Principios

- el pack es independiente del editor
- el pack esta versionado
- el pack se puede validar por hash y tamaño
- el pack separa datos logicos de recursos visuales
- el pack sirve tanto al cliente como al game server

### Contenido minimo del manifest

- `contentPackId`
- `version`
- `mapId`
- `files[]`
- `hash`
- `size`
- `type`
- `url`

### Validacion de cierre

- `index.json` describe el pack por defecto y sus versiones
- `manifest.json` enumera todos los archivos necesarios
- cada archivo tiene tipo, ruta, tamaño y hash
- cliente y servidor pueden resolver un mismo `mapId` sin rutas hardcodeadas

## Fase 4. Crear la libreria o toolkit de contenido

### Objetivo

Construir una herramienta interna para dejar de hacer esto a mano.

### Nombre recomendado

- `packages/content-toolkit`
- o `packages/content-pipeline`

### Responsabilidades del toolkit

- localizar `assets/`
- leer mapas exportados de Tiled
- validar layers, objects y propiedades
- copiar o transformar archivos al pack runtime
- generar `manifest.json`
- generar `index.json`
- calcular hashes
- avisar de errores claros

### Comandos objetivo

- `pnpm content:validate`
- `pnpm content:build-pack`
- `pnpm content:build-pack --version v002`
- `pnpm content:inspect-map`

### Reglas de diseño

- la herramienta no debe depender de Phaser
- la herramienta no debe contener logica de render
- la herramienta debe ser reutilizable por cliente y servidor
- los errores deben ser legibles por diseñadores y programadores

### Validacion de cierre

- Podemos generar un content pack entero con un comando
- Si falta una layer obligatoria, falla con mensaje claro
- Si un `PlayerSpawn` no existe, falla con mensaje claro
- Si falta un asset declarado, falla con mensaje claro
- El manifest se genera automaticamente

## Fase 5. Separar definiciones logicas de definiciones visuales

### Objetivo

Evitar que el contenido del juego quede acoplado a Phaser o a nombres de sprites.

### Modelo recomendado

- `entity-archetypes.json`
  - define que es una entidad a nivel de juego
- `item-definitions.json`
  - define items y su logica
- `rules.json`
  - reglas globales del pack o mapa
- `visual-definitions.json`
  - mapea ids visuales a recursos concretos

### Ejemplo conceptual

- el servidor conoce `tree_oak_large`
- el cliente resuelve `tree_oak_large` a `visualId`
- `visualId` decide atlas, frame, animaciones y offsets

### Beneficio

- la logica sigue viva aunque cambie el arte
- otro cliente podria representar la misma entidad de otra forma
- Phaser solo consume la parte visual adaptada

### Validacion de cierre

- Las entidades del servidor no dependen de nombres de frames Phaser
- El contenido visual se resuelve por ids semanticos
- La definicion de visuales puede cambiar sin tocar la simulacion

## Fase 6. Capa de adaptacion a Phaser

### Objetivo

Hacer explicito que Phaser necesita una traduccion, no es el formato base.

### Responsabilidades del cliente

- descargar el content pack
- cargar el catalogo
- resolver `visualId`
- traducir mapas y assets a llamadas de Phaser
- crear animaciones runtime
- cargar audio
- aplicar offsets, depth y reglas visuales

### Modulos que deberian existir o consolidarse

- `ContentDownloader`
- `AssetCache`
- `AssetCatalog`
- `PhaserAssetLoader`
- `PhaserVisualResolver`

### Reglas

- Phaser no debe asumir rutas magicas
- Phaser no debe depender de nombres duros como `Ground` fuera de una capa de adaptacion clara
- Toda dependencia de formato Phaser debe vivir del lado cliente

### Validacion de cierre

- El cliente puede arrancar un mapa solo con `manifest + definitions`
- El cliente puede renderizar entidades usando `visualId`
- La resolucion de animaciones no esta mezclada con red

## Fase 7. Carga del mundo en el game server

### Objetivo

Conseguir que el servidor lea el mismo contenido desde una perspectiva logica.

### Responsabilidades del servidor

- cargar `content-packs`
- resolver `mapId`
- leer layers y object layers relevantes
- construir colision, spawns, triggers e interactuables
- crear entidades de mundo
- ignorar informacion puramente visual que no necesite

### Importante

El servidor no necesita pensar como Phaser. Necesita pensar en:

- navegacion
- colision
- entidades
- triggers
- spawns
- reglas del mundo

### Validacion de cierre

- El servidor puede levantar un mapa solo a partir de `mapId`
- Si falta un spawn obligatorio, falla
- Si falta colision critica, avisa
- Si el pack es invalido, no arranca la sala

## Fase 8. Cerrar el flujo start-game

### Objetivo

Dejar totalmente claro el flujo entre API, content pack y game server.

### Flujo final esperado

1. usuario inicia sesion
2. API valida JWT o cookie de sesion
3. API genera `gameTicket`
4. API responde con:
   - `roomId`
   - `contentPackId`
   - `contentVersion`
   - `mapId`
   - `manifestUrl`
   - `webSocketUrl`
5. cliente asegura el content pack
6. cliente arranca Phaser
7. cliente conecta a websocket
8. game server valida y consume `gameTicket`
9. game server crea o reata la sesion
10. cliente entra al mundo

### Validacion de cierre

- `start-game` nunca depende de rutas frágiles
- `gameTicket` se firma y consume correctamente
- la API y el game server comparten secreto de ticket
- el cliente no intenta entrar al mundo sin content pack cargado

## Fase 9. Red autoritativa minima estable

### Objetivo

Consolidar un primer vertical slice de red ya serio.

### Alcance

- player spawn
- input local
- movimiento autoritativo
- snapshot del mundo
- otro jugador visible
- colision de mapa

### Regla base

- el cliente no decide posiciones finales
- el cliente predice como mejora visual, pero el servidor manda

### Validacion de cierre

- dos clientes pueden entrar con distinto usuario
- ambos reciben snapshot
- el servidor manda la posicion final
- el movimiento local reconcilia sin romperse
- la colision se resuelve en servidor

## Fase 10. Herramientas de desarrollo

### Objetivo

No depender de intuicion o depuracion manual para todo.

### Herramientas deseables

- validador de mapas
- inspector de content packs
- visualizador de manifest
- comando para reconstruir un pack
- logs de carga de contenido
- modo debug de layers, spawns y colliders

### Validacion de cierre

- podemos detectar un error de capa antes de arrancar el juego
- podemos inspeccionar un pack sin abrirlo a mano
- podemos reconstruir el pack rapido al cambiar un asset

## Orden recomendado de trabajo

Este es el orden correcto para evitar rehacer trabajo:

1. cerrar modelo canonico de `assets/`
2. cerrar contrato de contenido
3. cerrar estructura runtime de `content-packs`
4. crear toolkit de contenido
5. separar definiciones logicas y visuales
6. reforzar adaptacion cliente a Phaser
7. reforzar carga de mundo en game server
8. cerrar bien `start-game`
9. consolidar red autoritativa minima
10. construir herramientas de validacion y debug

## Checklist maestro

### Bloque A. Contenido fuente

- Existe una estructura definitiva de `assets/`
- Los mapas editables no se consumen directamente en runtime
- Hay convencion de nombres de mapas, tilesets y sprites

### Bloque B. Contrato de datos

- Layers canonicas definidas
- Object layers canonicas definidas
- Propiedades canonicas definidas
- Definiciones logicas separadas de visuales

### Bloque C. Pipeline

- Existe toolkit de contenido
- Genera pack versionado
- Genera manifest e index
- Calcula hashes
- Valida antes de empaquetar

### Bloque D. Cliente

- El cliente descarga el pack
- El cliente puede cachearlo
- El cliente adapta a Phaser
- El cliente no depende del formato editable fuente

### Bloque E. Game server

- El servidor carga el mismo pack
- El servidor construye mundo logico
- El servidor no depende de sprites o detalles visuales

### Bloque F. Integracion

- `start-game` devuelve pack y ticket
- websocket valida ticket
- snapshot usa entidades logicas
- cliente renderiza esas entidades con catalogo visual

## Criterio de exito real

Consideraremos que este objetivo esta bien logrado cuando:

- cambiar arte no rompa la logica del servidor
- cambiar reglas del mundo no obligue a rehacer el cliente
- un mapa pueda validarse antes de jugar
- un content pack pueda generarse con un comando
- el cliente pueda descargar una version nueva sin rebuild completo
- el servidor siga siendo la autoridad del mundo
- Phaser sea intercambiable como consumidor visual sin redefinir el contenido base

## Regla de trabajo para mañana

No seguir parcheando errores concretos de mapa hasta cerrar estas tres decisiones:

1. donde vive cada asset y cual es su fuente de verdad
2. que contrato de datos debe cumplir el contenido
3. que herramienta vamos a construir para convertir ese contenido en un pack runtime estable

Cuando estas tres cosas esten cerradas, el resto del sistema empezara a ordenarse mucho mejor.
