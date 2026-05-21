# Biblia del Mapa — IslaTortuga

Referencia principal para construir y editar mapas en Tiled y para entender cómo se consumen desde Phaser y el servidor C#. Este documento cubre la estructura de capas, colisiones, Y-sorting, animaciones de tile, luces, sonidos, eventos y la división de responsabilidades entre servidor y cliente.

---

## 1. Fuente de verdad y archivos

El mapa editable vive en:

```
assets/maps/test_map.tmj
```

Los tilesets usados actualmente:

```
assets/tilesets/TX Tileset Grass.png
assets/tilesets/TX Plant.png
assets/tilesets/TX Props.png
```

El mapa de producción (content pack) vive en:

```
content-packs/v001/maps/island_01.tmj
```

Regla: `assets/` es la fuente editable. `content-packs/` es lo que el cliente descarga en runtime. No editar directamente en `content-packs/`.

---

## 2. Concepto de capas en Tiled

Hay tres tipos de capa relevantes:

- **Tile Layer** — pintar tiles sobre una rejilla. Todo visual y estructural del escenario.
- **Object Layer** — colocar puntos, rectángulos, polígonos e instancias con propiedades y clase. Todo lo que tiene lógica, estado o necesita ser leído por el código.
- **Group Layer** — agrupar capas por piso/nivel si el mapa tiene múltiples alturas.

---

## 3. Stack completo de capas

Este es el orden de dibujado definitivo del proyecto, de abajo hacia arriba:

```
Depth   Nombre           Tipo           Propósito
──────────────────────────────────────────────────────────────────────────
0       Ground           Tile Layer     Suelo base: hierba, arena, agua, caminos
5       GroundDetail     Tile Layer     Decoración al ras del suelo: flores,
                                        charcos, manchas, sombras pintadas
8       Shadows          Tile Layer     Sombras pre-horneadas de árboles y
                                        estructuras (opcional, mejora el look)
10      Walls            Tile Layer     Troncos, muros, vallas, columnas.
                                        Tiene colisión. Se dibuja bajo el jugador.
──────── ZONA Y-SORT — depth = sprite.y (valores típicos 32..mapHeight) ────────
        Entities                        Jugadores, NPCs, items interactivos,
                                        props animados. Se ordenan entre sí
                                        automáticamente por posición Y.
──────────────────────────────────────────────────────────────────────────
9999    AbovePlayer      Tile Layer     Copas de árboles, tejados, arcos, toldos.
                                        Siempre sobre el jugador.
10000   Atmosphere       Tile Layer     Niebla, lluvia, efectos de ambiente.
                                        Siempre al frente.
──────── OBJECT LAYERS (sin visual) ────────────────────────────────────────────
—       SpawnPoints      Object Layer   Puntos de aparición de jugadores/NPCs.
—       Collisions       Object Layer   Hitboxes de colisión precisa (polígonos).
—       Triggers         Object Layer   Zonas de eventos e interacciones.
—       Lights           Object Layer   Fuentes de luz y configuración.
—       SoundZones       Object Layer   Zonas de audio ambiental.
—       Interactables    Object Layer   Puertas, cofres, llaves, palancas, NPCs.
```

Los Object Layers no se renderizan. Son datos que el cliente lee al cargar el mapa y convierte en lógica runtime.

---

## 4. Y-sorting: ordenación dinámica por posición Y

### El problema

Un árbol tiene tronco (Walls, depth fijo=10) y copa (AbovePlayer, depth=9999). El jugador debe quedar delante del tronco si su Y es mayor, y detrás si su Y es menor. Las capas de tile no pueden resolver esto: son fijas. La solución es que cada sprite de entidad use `setDepth(sprite.y)`.

### Regla

```typescript
// Constantes de depth del proyecto — un único archivo
export const DepthLayers = {
  Ground:       0,
  GroundDetail: 5,
  Shadows:      8,
  Walls:        10,
  // Entidades: depth = sprite.y
  // Para un mapa de 640px alto, Y va de 0 a 640 → siempre < AbovePlayer
  AbovePlayer:  9_999,
  Atmosphere:   10_000,
  UI:           100_000,
} as const;
```

En cada frame, después de mover un sprite:

```typescript
sprite.setDepth(sprite.y);
```

Para mapas grandes donde `sprite.y` podría superar 9999, normalizar:

```typescript
const normalizedDepth = 100 + (sprite.y / map.heightInPixels) * 8900;
sprite.setDepth(normalizedDepth);
```

### Objetos que necesitan Y-sort pero son tiles

La técnica es **partir el objeto en dos tiles**:

- La parte baja (el tronco, la base del muro) → capa `Walls` (depth=10, por debajo del jugador siempre).
- La parte alta (la copa, el tejado) → capa `AbovePlayer` (depth=9999, por encima siempre).

Cuando el jugador pasa por delante del tronco, el tronco queda detrás (depth 10 < Y del jugador). La copa siempre queda encima (9999). Visualmente parece que el árbol envuelve al jugador correctamente.

---

## 5. Tile Layer: propiedades de tiles

En el Tile Collision Editor del tileset, cada tile puede tener estas propiedades:

| Propiedad    | Tipo   | Significado                                         |
|--------------|--------|-----------------------------------------------------|
| `Collider`   | bool   | El tile tiene colisión en Arcade Physics            |
| `Type`       | string | `"Water"`, `"Lava"`, `"Ice"`, `"Sand"` — para efectos de movimiento |
| `Damage`     | int    | Daño por segundo al pisar (lava, veneno, etc.)      |

Phaser detecta colisión así en WorldScene:

```typescript
// Por propiedad
wallsLayer.setCollisionByProperty({ Collider: true });

// Por collision group definido en Tiled
wallsLayer.setCollisionFromCollisionGroup();
```

---

## 6. Animaciones de tile

Tiled soporta animación de tiles de forma nativa. En el editor:

1. Seleccionar un tile en el tileset.
2. Abrir "Tile Animation Editor" (panel de propiedades del tile).
3. Añadir frames: arrastrar los tiles que forman la animación y definir la duración en ms de cada uno.
4. Exportar el mapa normalmente.

El `.tmj` exportado incluye los datos de animación. Phaser los reproduce **automáticamente** al crear el tilemap — no hay que escribir ningún código adicional para animar tiles.

Casos de uso típicos:

- Agua que ondula.
- Antorchas que parpadean (la llama como tile).
- Portales o runas que giran.
- Plantas o flores que se mueven.

Para sprites animados en el mundo (NPCs, items brillantes, props dinámicos) que son entidades runtime y no tiles fijos, se usa el sistema de animaciones de Phaser directamente, igual que con el jugador.

---

## 7. Object Layer: SpawnPoints

Clase `PlayerSpawn`:

```
class:    PlayerSpawn
(la posición X,Y del objeto en Tiled es el punto de spawn)
```

Clase `NpcSpawn`:

```
class:        NpcSpawn
properties:
  npcId       string   Identificador del tipo de NPC
  facing      string   "up" | "down" | "left" | "right"
  enabled     bool     Si el spawn está activo al cargar
```

Clase `EnemySpawn`:

```
class:        EnemySpawn
properties:
  enemyType   string   "crab", "skeleton", etc.
  respawnTime int      Segundos hasta reaparecer. 0 = sin respawn.
  enabled     bool
```

Clase `ItemSpawn`:

```
class:        ItemSpawn
properties:
  itemId      string   ID del item a generar
  respawnTime int
```

---

## 8. Object Layer: Collisions

Polígonos o rectángulos de colisión precisa. No llevan clase especial.

Se usan cuando la colisión por tile completo no es suficiente (troncos finos, bordes de camino, esquinas irregulares).

Phaser los lee así:

```typescript
const collisionLayer = map.getObjectLayer('Collisions');
// Crear cuerpos estáticos a partir de los objetos
this.physics.add.staticGroup(); // y luego poblar con cada shape
```

Regla: usar colisión por tile para obstáculos rectangulares simples. Usar object layer para precisión real de gameplay.

---

## 9. Object Layer: Interactables

Todo objeto con estado, interacción o que pueda cambiar debe ir aquí, no pintado como tile.

Clases y propiedades:

```
class: Door
  id            string   Identificador único (e.g. "door_lighthouse_01")
  locked        bool
  keyId         string   ID de la llave que la abre (vacío si no tiene)
  startOpen     bool
  targetDoorId  string   Puerta de destino si es transición de sala

class: Key
  id            string
  keyId         string   Qué puerta abre

class: Chest
  id            string
  locked        bool
  keyId         string
  lootTable     string   Referencia a la tabla de loot

class: Lever
  id            string
  targetId      string   ID del objeto que activa
  startOn       bool

class: Pickup
  itemId        string
  quantity      int
  respawnTime   int      0 = sin respawn

class: Npc
  npcId         string
  dialogId      string   Árbol de diálogo inicial
  facing        string

class: StairLink
  id            string
  targetLevel   int      Nivel al que lleva
  targetStairId string
```

---

## 10. Object Layer: Triggers

Zonas invisibles (rectángulos generalmente) que activan lógica cuando el jugador entra.

```
class: ZoneTrigger
  eventId       string   ID único del evento
  action        string   "dialog:npc_intro" | "teleport:map2:spawn1" |
                         "cutscene:intro" | etc.
  condition     string   "always" | "questFlag:treasure_found" | etc.
  oneShot       bool     Si solo se activa una vez por sesión

class: MapTransition
  targetMap     string   ID del mapa destino
  targetSpawn   string   ID del spawn en el mapa destino

class: DangerZone
  damage        int      Daño por segundo dentro de la zona
  damageType    string   "fire" | "poison" | "cold"
```

Implementación en Phaser:

```typescript
const triggerLayer = map.getObjectLayer('Triggers');
for (const obj of triggerLayer.objects) {
  const zone = this.add.zone(
    obj.x + obj.width / 2,
    obj.y + obj.height / 2,
    obj.width,
    obj.height
  );
  this.physics.world.enable(zone, Phaser.Physics.Arcade.STATIC_BODY);

  this.physics.add.overlap(player, zone, () => {
    this.events.emit('trigger', {
      eventId: getProperty(obj, 'eventId'),
      action:  getProperty(obj, 'action'),
    });
  });
}
```

---

## 11. Object Layer: Lights

Fuentes de luz que el cliente instancia en runtime usando el pipeline Light2D de Phaser.

```
class: PointLight
  radius        int     Radio en píxeles (e.g. 200)
  color         color   Color de la luz en hex (e.g. #ffaa44)
  intensity     float   0.0 – 2.0. Típico: 1.0 – 1.5
  flicker       bool    Si la luz parpadea
  flickerSpeed  int     ms de ciclo del parpadeo (e.g. 100)
  flickerAmount float   Variación de intensidad (e.g. 0.3)

class: AmbientLight
  color         color   Color de la luz ambiental global
  intensity     float   0.0 – 1.0
```

Implementación en Phaser:

```typescript
// En create()
this.lights.enable();
this.lights.setAmbientColor(0x111111); // oscuridad base

const lightsLayer = map.getObjectLayer('Lights');
for (const obj of lightsLayer.objects) {
  if (obj.type === 'AmbientLight') {
    const color = Phaser.Display.Color.HexStringToColor(getProperty(obj, 'color')).color;
    this.lights.setAmbientColor(color);
    continue;
  }

  if (obj.type === 'PointLight') {
    const radius    = getProperty(obj, 'radius')    ?? 150;
    const color     = getProperty(obj, 'color')     ?? '#ffffff';
    const intensity = getProperty(obj, 'intensity') ?? 1.0;
    const flicker   = getProperty(obj, 'flicker')   ?? false;
    const flickerSpeed  = getProperty(obj, 'flickerSpeed')  ?? 100;
    const flickerAmount = getProperty(obj, 'flickerAmount') ?? 0.3;

    const light = this.lights.addLight(
      obj.x, obj.y, radius,
      Phaser.Display.Color.HexStringToColor(color).color,
      intensity
    );

    if (flicker) {
      this.tweens.add({
        targets: light,
        intensity: { from: intensity - flickerAmount, to: intensity + flickerAmount },
        duration: flickerSpeed,
        yoyo: true,
        repeat: -1,
        ease: 'Sine.easeInOut',
      });
    }
  }
}

// Todo sprite que reciba luces necesita activar el pipeline:
playerSprite.setPipeline('Light2D');
```

### Nota sobre el sistema de luces

El pipeline Light2D de Phaser funciona con normal maps para efecto de volumen 3D completo. Sin normal maps, los sprites solo reciben el tinte de color, lo cual ya da buena atmósfera para pixel art. Para empezar, el tinte de color es suficiente y no requiere generar normal maps.

---

## 12. Object Layer: SoundZones

Zonas rectangulares donde se reproduce audio ambiental. El volumen se ajusta progresivamente según la distancia del jugador al borde de la zona.

```
class: AmbientSound
  soundKey      string  Clave del asset de audio (e.g. "wind", "waves", "fire")
  volume        float   Volumen máximo: 0.0 – 1.0
  fadeDistance  int     Píxeles desde el borde donde empieza el fade (e.g. 100)
  loop          bool    Si el sonido hace loop

class: PointSound
  soundKey      string
  radius        int     Radio de audición en píxeles
  volume        float
  loop          bool
```

Implementación en Phaser:

```typescript
const soundLayer = map.getObjectLayer('SoundZones');
const soundZones = soundLayer.objects.map(obj => ({
  bounds:       new Phaser.Geom.Rectangle(obj.x, obj.y, obj.width, obj.height),
  soundKey:     getProperty(obj, 'soundKey'),
  maxVolume:    getProperty(obj, 'volume') ?? 1.0,
  fadeDistance: getProperty(obj, 'fadeDistance') ?? 100,
  sound:        this.sound.add(getProperty(obj, 'soundKey'), { loop: true, volume: 0 }),
}));

// En update()
for (const zone of this.soundZones) {
  const inside = zone.bounds.contains(player.x, player.y);
  const targetVolume = inside ? zone.maxVolume : 0;
  zone.sound.setVolume(Phaser.Math.Linear(zone.sound.volume, targetVolume, 0.05));
  if (zone.sound.volume > 0.01 && !zone.sound.isPlaying) zone.sound.play();
  if (zone.sound.volume <= 0.01 && zone.sound.isPlaying) zone.sound.pause();
}
```

---

## 13. Qué gestiona el servidor y qué gestiona el cliente

Esta separación es fija y no debe mezclarse:

| Sistema                     | Servidor C# | Cliente Phaser |
|-----------------------------|:-----------:|:--------------:|
| Posición de entidades       | ✓           |                |
| Colisiones de movimiento    | ✓           |                |
| Spawn points de jugadores   | ✓           |                |
| Estado de interactables     | ✓           |                |
| Game loop (20 TPS)          | ✓           |                |
| Animaciones de tile         |             | ✓              |
| Luces (Light2D)             |             | ✓              |
| Sonidos y zonas de audio    |             | ✓              |
| Triggers visuales           |             | ✓              |
| Y-sorting de sprites        |             | ✓              |
| Interpolación de red        |             | ✓              |
| Animaciones de sprite       |             | ✓              |

El servidor lee del mapa: dimensiones, capas de tile para colisión y SpawnPoints.
El cliente lee del mapa: todo lo demás (Lights, SoundZones, Triggers, Interactables, todas las capas visuales).

---

## 14. Orden de dibujo dentro del cliente (WorldScene)

```typescript
// Tile layers con depth fijo
groundLayer.setDepth(DepthLayers.Ground);           // 0
groundDetailLayer.setDepth(DepthLayers.GroundDetail); // 5
shadowsLayer.setDepth(DepthLayers.Shadows);         // 8
wallsLayer.setDepth(DepthLayers.Walls);             // 10

// Entidades (jugador y remotos): depth dinámico
player.setDepth(player.y);          // se actualiza en update()
remoteSprite.setDepth(remoteSprite.y); // en interpolateNetworkPlayers()

// Tile layers que siempre van por encima
abovePlayerLayer.setDepth(DepthLayers.AbovePlayer); // 9999
atmosphereLayer.setDepth(DepthLayers.Atmosphere);   // 10000
```

---

## 15. Relaciones entre objetos

Las relaciones entre objetos se definen por IDs, nunca por posición en el mapa ni por nombre visual.

Ejemplo:

- Una `Door` tiene `id = "door_lighthouse_01"`.
- Una `Key` tiene `keyId = "door_lighthouse_01"`.
- El código hace match entre ambos sin depender de coordenadas.

Ejemplo de trigger que abre una puerta:

- Un `Lever` tiene `targetId = "door_lighthouse_01"`.
- Al activar la palanca, el sistema busca el objeto con ese ID y cambia su estado.

---

## 16. Dos tipos de animación de gameplay

### Animación de ambiente (pasiva)

Agua, fuego, hojas, luces parpadeantes que forman parte del escenario y no responden a acciones del jugador. Deben ir como **animación de tile** en el tileset. Phaser las reproduce automáticamente, no requieren código.

### Animación de gameplay (reactiva)

Puerta abriéndose, cofre abriéndose, palanca activándose, objeto desapareciendo al recogerse. Estas deben gestionarse como **estado de objeto runtime** — el servidor cambia el estado, el cliente recibe la actualización y reproduce la animación correspondiente. No son decoración, son feedback de mecánica.

---

## 17. Cuándo tocar WorldScene.ts

`WorldScene.ts` solo debe modificarse cuando:

- Cambian los nombres de capas en Tiled.
- Se añaden nuevos object layers que deben leerse en runtime (Lights, SoundZones, Triggers, etc.).
- Cambia qué tilesets se cargan.
- Cambia el sistema de colisión (nueva capa, nueva estrategia).
- Cambia el sistema de profundidad (nuevos valores de depth, nuevo método de Y-sort).
- Se añaden nuevos sistemas (luces, sonidos, triggers) por primera vez.

No tocar `WorldScene.ts` solo por recolocar tiles o decorar el mapa.

---

## 18. Regla de trabajo diaria

1. Editar `assets/maps/test_map.tmj` en Tiled.
2. Usar los tilesets de `assets/tilesets/`.
3. Mantener la convención de capas de este documento.
4. No duplicar el archivo de mapa en otras carpetas.
5. Si se cambia el nombre de una capa, actualizar `WorldScene.ts`.
6. Los object layers nuevos (Lights, SoundZones, etc.) requieren código nuevo en `WorldScene.ts` para ser leídos — no son automáticos.

---

## 19. Resumen de reglas

| Si una cosa...                                    | Va en...           |
|---------------------------------------------------|--------------------|
| Se pinta y no tiene estado propio                 | Tile Layer         |
| Tiene identidad, estado o interacción             | Object Layer       |
| Siempre debe quedar sobre el jugador              | `AbovePlayer`      |
| Necesita colisión precisa no rectangular          | `Collisions`       |
| Define dónde aparece algo                         | `SpawnPoints`      |
| Activa lógica al entrar en una zona               | `Triggers`         |
| Es una fuente de luz                              | `Lights`           |
| Es sonido ambiental por zona                      | `SoundZones`       |
| Puede interactuarse (cofre, puerta, NPC...)       | `Interactables`    |
| Es una animación ambiental pasiva                 | Tile animado       |
| Es una animación reactiva a gameplay              | Estado de entidad  |
