# Unity Scene Export Template

## Objetivo

Definir un formato estable para exportar escenas 3D desde Unity al `content-pack` que consume Babylon.

La escena exportada debe permitir:

- cargar por `sceneId`
- conocer bounds y spawn points
- reconstruir proxies visuales y de colision en Babylon
- conservar transiciones y luces basicas

## Builder activo

El catalogo de escenas usa:

```json
{
  "sceneId": "scene.test.plain",
  "builder": "unity-scene-export",
  "sceneDataFileId": "scene.scene.test.plain"
}
```

## Archivo de escena exportada

Ejemplo minimo:

```json
{
  "sceneId": "scene.test.plain",
  "displayName": "Test Plain",
  "builder": "unity-scene-export",
  "coordinateScale": 1,
  "bounds": { "width": 30, "depth": 30 },
  "spawnPoints": [],
  "transitions": [],
  "colliders": [],
  "props": [],
  "audioEmitters": [],
  "lights": []
}
```

## Semantica recomendada

- `bounds`
  extension horizontal de la escena en el plano XZ.

- `spawnPoints`
  puntos de entrada para jugador o NPC.

- `transitions`
  metadata para puertas o cambios de escena.

- `colliders`
  version simplificada para cliente.

- `props`
  referencias visuales exportables para Babylon.

- `lights`
  luces basicas que el cliente puede reconstruir.

## Regla de authoring

- Unity es la fuente de verdad.
- El export no debe depender de nombres como unica semantica.
- Los componentes de authoring deben marcar spawn, transition, collider y prop de forma explicita.

## Alcance del formato

Este formato describe escenas 3D, puntos de spawn, colision simplificada, props, transiciones y luces que Babylon puede reconstruir desde datos exportados por Unity.
