# Biblia Del Mapa

Este documento es la referencia principal para editar mapas de `IslaTortuga` en Tiled y para entender como se consumen despues desde Phaser.

Su objetivo es evitar dudas sobre:

- que archivo de mapa se usa realmente
- donde deben vivir los assets
- como organizar capas
- como definir colisiones
- como trabajar alturas
- como representar objetos interactivos
- como hacer que el jugador se dibuje por encima o por debajo de ciertas cosas

## 1. Fuente de verdad

La fuente de verdad del mapa y de los assets del juego es la carpeta:

```txt
assets/
```

En concreto, ahora mismo el mapa que consume Phaser es:

```txt
assets/maps/test_map.tmj
```

Y los tilesets/imágenes que usa ese mapa viven en:

```txt
assets/tilesets/
```

Archivos usados ahora:

- `assets/maps/test_map.tmj`
- `assets/tilesets/TX Tileset Grass.png`
- `assets/tilesets/TX Plant.png`
- `assets/tilesets/TX Props.png`

Importante:

- No usar `apps/client/public/assets` como fuente manual de edición del mapa.
- Si en algún momento existen copias ahí, deben considerarse temporales o legacy.
- El mapa que hay que editar es el de `assets/maps`.

## 2. Como Phaser carga el mapa

El archivo que carga el mapa es:

```txt
apps/client/src/phaser/scenes/WorldScene.ts
```

Ese archivo:

- carga el `.tmj`
- carga las imágenes de tileset
- crea las capas
- aplica colisiones
- crea el jugador de prueba

`WorldScene.ts` no debería editarse cada vez que cambie el dibujo del mapa.

Solo debería tocarse cuando:

- cambian nombres de capas
- cambian tilesets usados por el mapa
- cambia la lógica de colisión
- cambia la lógica de spawns
- cambia el sistema de profundidad/render
- se añaden object layers nuevas que deban leerse en runtime

## 3. Tiled: concepto correcto

En Tiled hay varias cosas distintas:

- `Tile Layer`: sirve para pintar tiles sobre una rejilla
- `Object Layer`: sirve para colocar instancias, puntos, rectángulos, polígonos, rutas o tile objects
- `Tile Collision Editor`: sirve para definir colisiones dentro de la definición de un tile del tileset

Importante:

- La colisión definida en el `Tile Collision Editor` no crea una layer nueva visible en el mapa.
- Esa colisión queda guardada dentro del tile del tileset.
- Para verla sobre el mapa en Tiled hay que activar la visualización correspondiente.

## 4. Que debe ir en Tile Layers

Usar `Tile Layer` para todo lo visual y estructural del mapa:

- suelo
- césped
- caminos
- agua
- paredes fijas
- decoración fija
- bases de estructuras
- árboles y vegetación visual
- tejados
- sombras pintadas
- overlays decorativos

Regla:

- si algo es parte del escenario y no se comporta como entidad independiente, normalmente va en tile layer

## 5. Que debe ir en Object Layers

Usar `Object Layer` para datos de gameplay e instancias del mundo:

- spawn del jugador
- spawns de NPCs
- puertas
- llaves
- cofres
- palancas
- triggers
- zonas de transición
- zonas de interacción
- colliders precisos
- props movibles
- rutas
- puntos especiales

Regla:

- si algo tiene identidad propia, estado, interacción o puede moverse/cambiar, mejor como objeto

## 6. Convención recomendada de capas

Para un mapa con diferentes alturas, interacciones y props, la estructura recomendada es usar `Group Layers` por nivel de altura.

Ejemplo:

```txt
Level_0/
Level_1/
Level_2/
```

Dentro de cada grupo:

```txt
Ground
GroundDetails
WallsLower
ObjectsStatic
AbovePlayer
CollisionObjects
Interactables
SpawnPoints
Triggers
```

### Significado de cada capa

`Ground`

- suelo principal
- tierra
- arena
- césped
- agua base si aplica

`GroundDetails`

- detalles visuales menores
- manchas
- piedras pequeñas
- decals

`WallsLower`

- partes bajas de muros
- bases de columnas
- troncos
- muebles bajos
- partes que deben quedar por debajo del jugador

`ObjectsStatic`

- props visuales fijos que no necesitan lógica propia

`AbovePlayer`

- copas de árboles
- tejados
- toldos
- elementos altos que deben verse por encima del jugador

`CollisionObjects`

- rectángulos o polígonos de colisión precisa
- especialmente útil si Arcade Physics no basta con colisión por tile completo

`Interactables`

- puertas
- llaves
- cofres
- mecanismos
- objetos movibles

`SpawnPoints`

- puntos donde aparecen jugador, NPCs o entidades

`Triggers`

- zonas invisibles
- cambios de estado
- transiciones
- scripts
- eventos

## 7. Alturas y pisos

El mapa no debe pensarse solo como una imagen plana.

Hay dos conceptos distintos:

- `level` o piso/altura
- `y` o posición vertical en pantalla

Regla mental:

- `level` decide en que piso está algo
- `y` decide el orden dentro de ese piso

Ejemplo:

- un jugador en `Level_0` no debe mezclarse igual que un objeto de `Level_1`
- dentro de `Level_0`, dos entidades pueden ordenarse por `y`

## 8. Orden de dibujo del jugador

En Phaser el orden visual se controla por:

- orden de creación
- `depth`
- y en sistemas más avanzados, por la posición `y`

### Recomendación general

No usar solo una técnica.

Usar mezcla de:

- capas separadas en Tiled
- `depth` fijo por grandes grupos
- `depth` por `y` para entidades dinámicas dentro del mismo piso

### Regla práctica

- `Ground` y `GroundDetails` por debajo
- `WallsLower` y elementos bajos debajo del jugador
- jugador en medio
- `AbovePlayer` por encima del jugador

Conceptualmente:

```txt
Ground -> BelowPlayer -> Player -> AbovePlayer
```

### Regla futura para entidades

Cuando haya NPCs, props dinámicos y objetos móviles:

- usar `depth` basado en `y` dentro del mismo `level`
- seguir manteniendo capas tipo `AbovePlayer` para copas, techos y overlays

## 9. Colisiones: regla del proyecto

Hay dos tipos de colisión importantes:

### 9.1. Colisión simple por tile

Sirve para:

- paredes sólidas
- bloques completos
- obstáculos simples

Se puede definir con:

- `Collider`
- `collider`
- `collides`
- `type` o clase como `Solid` / `SolidTile`

### 9.2. Colisión precisa

Sirve para:

- troncos finos
- bordes concretos
- obstáculos no rectangulares
- zonas exactas de bloqueo

Para eso, mejor usar:

- `CollisionObjects` en object layer

Regla recomendada:

- usar colisión por tile para casos simples
- usar object layer para precisión real de gameplay

## 10. Importante sobre Tile Collision Editor

Si un tile del tileset tiene una forma de colisión definida en el editor de colisiones:

- esa forma pertenece al tile
- no aparece como capa independiente del mapa
- no sustituye una object layer

Esto es útil para etiquetar tiles sólidos, pero no siempre es la mejor solución para gameplay preciso si se usa Arcade Physics.

## 11. Interactuables

Todo lo que cambie de estado o se pueda usar debería ir como objeto en `Interactables`.

Ejemplos de tipos/clases:

- `Door`
- `Key`
- `Chest`
- `Lever`
- `Pickup`
- `HideSpot`
- `MoveableProp`
- `StairLink`

Propiedades recomendadas según caso:

- `id`
- `locked`
- `keyId`
- `startOpen`
- `moveable`
- `blocksMovement`
- `blocksVision`
- `level`
- `targetLevel`
- `targetDoorId`
- `lootTable`
- `interactionType`

Regla:

- una puerta no debería ser solo un tile pintado si va a abrirse/cerrarse
- una llave no debería ser solo decoración en tile layer
- los objetos con estado deben modelarse como objetos

## 12. Spawns

Los spawns no deben hardcodearse en código si van a depender del mapa.

Usar `SpawnPoints` como object layer.

Tipos útiles:

- `PlayerSpawn`
- `NpcSpawn`
- `EnemySpawn`
- `ItemSpawn`

Propiedades útiles:

- `id`
- `facing`
- `level`
- `group`
- `enabled`

## 13. Triggers

Usar la layer `Triggers` para zonas invisibles del mapa.

Ejemplos:

- entrar en una casa
- activar una cinemática
- cambiar de piso
- detectar zona segura/peligrosa
- activar bruma
- cambiar música

Tipos útiles:

- `AreaTrigger`
- `LevelTransition`
- `CutsceneTrigger`
- `AudioZone`
- `DangerZone`

## 14. Puertas, llaves y relaciones

Las relaciones entre objetos deben definirse por ids.

Ejemplo:

- una `Door` puede tener `id = door_lighthouse_01`
- una `Key` puede tener `keyId = door_lighthouse_01`

Eso permite que el código haga match entre ambos sin depender del nombre visual.

## 15. Animaciones

Separar dos clases de animación:

### 15.1. Animación ambiente

- agua
- fuego
- luces
- hojas

Esto puede vivir como animación de tile del tileset.

### 15.2. Animación de gameplay

- puerta abriéndose
- cofre abriéndose
- palanca activándose
- objeto moviéndose

Esto debería gestionarse como estado de objeto runtime, no como simple decoración.

## 16. Regla de trabajo diaria

Cuando se quiera editar el mapa:

1. editar `assets/maps/test_map.tmj`
2. usar los tilesets de `assets/tilesets`
3. mantener la convención de capas
4. no crear duplicados del mapa en otras carpetas
5. si cambia la estructura del mapa, entonces revisar `WorldScene.ts`

## 17. Cuando hay que tocar WorldScene.ts

Solo tocar `WorldScene.ts` si:

- cambian nombres de layers
- cambia qué object layers deben leerse
- cambian tilesets que hay que cargar
- cambia el sistema de colisión
- cambia el sistema de profundidad
- se empieza a leer spawn del jugador desde `SpawnPoints`

No tocar `WorldScene.ts` solo por recolocar tiles o decorar el mapa.

## 18. Convención mínima obligatoria

Para mantener el proyecto sano:

- `assets/` es la fuente de verdad
- el mapa runtime debe salir de `assets/maps`
- capas visuales en tile layers
- gameplay e instancias en object layers
- `AbovePlayer` para overlays altos
- `CollisionObjects` para colisión precisa
- `SpawnPoints` para spawns
- `Triggers` para lógica espacial

## 19. Recomendación técnica para el futuro inmediato

El siguiente paso lógico de evolución del mapa es:

1. dejar de hardcodear el spawn del jugador
2. crear `SpawnPoints`
3. crear una layer `AbovePlayer`
4. separar mejor colisión visual de colisión precisa
5. introducir `Interactables` con objetos tipo `Door`, `Key`, `Chest`

## 20. Resumen corto

Si una cosa:

- se pinta y no vive por sí sola: tile layer
- tiene identidad, estado o interacción: object layer
- debe quedar por encima del jugador: `AbovePlayer`
- necesita colisión precisa: `CollisionObjects`
- define aparición: `SpawnPoints`
- activa lógica: `Triggers`

Este documento debe considerarse la biblia de edición del mapa hasta que la arquitectura del juego obligue a refinarla.
