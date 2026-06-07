# Content Pipeline

## Estado

El flujo activo de contenido es 3D puro y parte de Unity.

## Pipeline vigente

1. Autorar la escena 3D en Unity.
2. Exportarla al `content-pack` con builder `unity-scene-export`.
3. Registrar la escena en `definitions/scene-definitions.json`.
4. Publicar el `manifest.json` y los ficheros de escena para el cliente Babylon.

## Artefactos esperados

- escenas exportadas en `content-packs/v001/scenes`
- definiciones de escena y visuales 3D en `content-packs/v001/definitions`
- modelos, materiales, texturas y animaciones 3D en los directorios de `assets`

## Regla de coherencia

Si aparece cualquier pipeline alternativo que no salga de Unity y no termine en escenas o visuales 3D de Babylon, debe tratarse como obsoleto y retirarse.
