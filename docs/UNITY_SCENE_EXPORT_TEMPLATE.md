# Unity Scene Export Template

## Objetivo

Definir una plantilla estable para autorar escenarios en Unity y exportarlos al `content-pack` para que:

- el servidor pueda seguir simulando colisiones, triggers e interacciones en Unity
- el cliente Babylon pueda reconstruir la escena base correcta
- el `NetworkSceneManager` pueda cargar por `sceneId`
- el `NetworkEntityManager` pueda instanciar entidades dentro de esa escena

La idea base es:

- Unity es la herramienta de autoría
- el servidor usa la escena autorada como runtime autoritativo
- el exportador genera una versión cliente del escenario
- el cliente no recibe la escena por red en crudo; recibe `sceneId` y descarga sus datos del `content-pack`

## Regla Principal

El exportador no debería depender solo de nombres.

Debe usar:

- componentes de authoring como fuente principal
- capas y nombres como validación y ayuda editorial

Eso evita que un rename rompa la exportación.

## Plantilla de Jerarquía

Cada escena exportable de Unity debería tener este root:

```text
SCN_<sceneId>
  _Meta
  _Visual
  _Collision
  _Spawn
  _Transitions
  _Audio
  _Lighting
```

Ejemplo:

```text
SCN_scene.house.small
  _Meta
  _Visual
  _Collision
  _Spawn
  _Transitions
```

Convenciones:

- `SCN_...` es el root lógico exportable
- los roots que empiezan por `_` son contenedores organizativos
- no se exporta por nombre del GameObject salvo como fallback o debug

## Componentes de Authoring Recomendados

### 1. `SceneExportRoot`

Debe ir en el root `SCN_<sceneId>`.

Campos:

- `sceneId`
- `displayName`
- `exportMode`
- `coordinateScale`
- `defaultSceneInstanceKind`
- `includeLighting`
- `includeAudio`

Reglas:

- `sceneId` debe ser único en el proyecto
- formato recomendado: `scene.<zona>.<subzona>`
- ejemplo: `scene.overworld.port`, `scene.house.small`, `scene.dungeon.room_a`

### 2. `SceneColliderAuthoring`

Debe ir en colliders exportables.

Campos:

- `colliderKind`
  - `blocking`
  - `walkable_modifier`
  - `trigger`
- `shapeOverride`
  - `auto`
  - `box`
  - `sphere`
  - `capsule`
  - `mesh_approx`
- `clientCollision`
  - `none`
  - `simple`
  - `full`
- `export`

Reglas:

- para cliente Babylon exportar siempre colisión simplificada
- no exportar `MeshCollider` crudo salvo que en el futuro haya soporte real
- el servidor puede seguir usando colliders Unity completos

### 3. `SceneSpawnPointAuthoring`

Campos:

- `spawnId`
- `spawnType`
  - `player_default`
  - `player_interior_entry`
  - `npc`
  - `custom`
- `facing`
- `export`

Reglas:

- `spawnId` único dentro de la escena
- nombre recomendado del objeto: `SPAWN_<type>_<id>`

Ejemplo:

- `SPAWN_player_default_main`
- `SPAWN_player_interior_entry_door_a`

### 4. `SceneTransitionAuthoring`

Campos:

- `transitionId`
- `targetSceneId`
- `targetSpawnId`
- `instanceMode`
  - `shared`
  - `per_player`
  - `per_party`
  - `named`
- `namedInstanceId`
- `transitionShape`
- `export`

Reglas:

- esto define puertas, portales o cambios de contexto
- el nombre recomendado del objeto es `TRN_<targetSceneId>_<id>`
- el exportador debe generar tanto trigger como metadata de transición

### 5. `ScenePropAuthoring`

Campos:

- `propId`
- `visualAssetId`
- `exportMode`
  - `static_mesh`
  - `primitive_proxy`
  - `ignore`
- `staticCollisionSource`
  - `none`
  - `linked_colliders`
- `export`

Reglas:

- para props decorativos, el exportador debe recoger transform
- `visualAssetId` debe resolver el asset del cliente

### 6. `SceneAudioEmitterAuthoring`

Campos:

- `audioEventId`
- `radius`
- `loop`
- `spatial`
- `export`

### 7. `SceneLightAuthoring`

Campos:

- `lightType`
- `color`
- `intensity`
- `range`
- `export`

V1:

- opcional
- si complica demasiado, se puede dejar fuera del primer exportador

## Convención de Capas

Las capas sirven para validar y para filtros rápidos del exportador.

Capas recomendadas:

- `Scene_Visual`
- `Scene_Collision`
- `Scene_Trigger`
- `Scene_Spawn`
- `Scene_Transition`
- `Scene_Audio`
- `Scene_Ignore`

Reglas:

- `Scene_Collision` para colliders de bloqueo
- `Scene_Trigger` para triggers genéricos
- `Scene_Spawn` para spawn points
- `Scene_Transition` para puertas/cambios de escena
- `Scene_Ignore` excluye del export aunque exista en escena

## Convención de Nombres

Los nombres no deberían ser la fuente de verdad, pero sí una guía visual.

Prefijos recomendados:

- `SCN_` root exportable
- `ENV_` geometría ambiental grande
- `PROP_` props estáticos
- `COL_` collider bloqueante
- `TRG_` trigger genérico
- `SPAWN_` punto de spawn
- `TRN_` transición entre escenas
- `AUD_` emisor de audio
- `LGT_` luz exportable

Ejemplos:

- `COL_wall_north_01`
- `TRN_scene.house.small_frontdoor`
- `SPAWN_player_default_main`
- `PROP_tree_oak_03`

## Qué Debe Exportar la Tool

### 1. Registro de escena

Actualizar `definitions/scene-definitions.json` con una entrada:

```json
{
  "sceneId": "scene.house.small",
  "builder": "unity-scene-export",
  "sceneDataFileId": "scene.scene.house.small"
}
```

### 2. Archivo de escena exportada

Ejemplo de salida:

```json
{
  "sceneId": "scene.house.small",
  "coordinateScale": 1,
  "bounds": { "width": 12, "depth": 10 },
  "spawnPoints": [],
  "transitions": [],
  "colliders": [],
  "props": [],
  "audioEmitters": [],
  "lights": []
}
```

### 3. Manifest

Añadir al `manifest.json` el archivo exportado:

- `scene.scene.house.small`

Tipo recomendado:

- `scene`

## Colisiones: Qué Exportar y Qué No

### Sí exportar

- `BoxCollider`
- `SphereCollider`
- `CapsuleCollider`
- colliders compuestos simples

### No exportar en V1

- `MeshCollider` arbitrario como geometría cliente
- física exacta de Unity

### Por qué sí hay que exportarlas

Porque el cliente las necesita para:

- predicción local básica
- evitar atravesar paredes visualmente
- mejorar el movimiento percibido

### Por qué no hace falta exportarlas perfectas

Porque:

- la autoridad sigue en el servidor
- el cliente solo necesita una aproximación robusta
- si hay divergencia, el server corrige

## Política Recomendada Para Colliders

V1:

- el servidor usa colliders Unity reales
- el exportador genera una versión simplificada cliente

Más adelante, si queréis máxima paridad:

- una misma capa de authoring puede alimentar tanto servidor como export cliente

## Instancias y Transiciones

El exportador no crea instancias.
Solo exporta la metadata para que el servidor sepa cómo crearlas.

Ejemplo:

```json
{
  "transitionId": "front_door",
  "targetSceneId": "scene.house.small",
  "targetSpawnId": "player_interior_entry_main",
  "instanceMode": "shared"
}
```

O:

```json
{
  "transitionId": "private_room",
  "targetSceneId": "scene.house.private",
  "targetSpawnId": "player_interior_entry_main",
  "instanceMode": "per_player"
}
```

## Ámbito Recomendado Para El Primer Exportador

V1 debería soportar solo:

- un `SceneExportRoot`
- spawn points
- transitions
- colliders simples
- props con transform y `visualAssetId`

No intentaría meter aún:

- navmesh
- audio complejo
- luces completas
- materiales avanzados
- LODs
- mesh baking

## Validaciones Que Debe Hacer La Tool

- falta `SceneExportRoot`
- `sceneId` vacío o duplicado
- `targetSceneId` vacío en transición
- `targetSpawnId` inexistente
- colliders en capas no permitidas
- `visualAssetId` vacío en prop exportable
- más de un spawn `player_default` con mismo id

## Propuesta de Estructura de Código Para La Tool

En Unity:

```text
Assets/Scripts/Editor/SceneExport/
  SceneExportWindow.cs
  SceneExportService.cs
  SceneExportValidator.cs
  SceneManifestWriter.cs
  SceneDefinitionWriter.cs
  Authoring/
    SceneExportRoot.cs
    SceneColliderAuthoring.cs
    SceneSpawnPointAuthoring.cs
    SceneTransitionAuthoring.cs
    ScenePropAuthoring.cs
```

## Decisiones Recomendadas

### 1. La semántica vive en componentes

No en nombres.

### 2. Las capas son auxiliares

Sirven para validar y organizar.

### 3. El cliente exporta colisión simplificada

No física exacta.

### 4. La transición exporta intención

No crea la instancia por sí misma.

### 5. La escena cliente es un asset más del content-pack

No un estado replicado por red.

## Siguiente Paso

Si esta plantilla te encaja, el siguiente bloque es implementar:

1. componentes de authoring
2. ventana `Export Scene To Content Pack`
3. serialización `unity-scene-export`
4. actualización automática de `scene-definitions.json` y `manifest.json`
