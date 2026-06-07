# AGENTS.md - Guia de trabajo para IslaTortuga

## Regla principal

Este proyecto usa una arquitectura clara:

- servidor autoritativo dentro de Unity
- cliente web 3D en Babylon
- API Nest para auth, portal y tickets
- content-packs versionados con escenas exportadas desde Unity

Si el codigo o la documentacion viejos contradicen esta direccion, prioriza el enfoque actual y corrige la incoherencia minima necesaria.

## Principios

- Unity decide estado de juego, colisiones y sesiones.
- Babylon renderiza e interpola, pero no es autoridad.
- No usar pipelines visuales heredados ajenos al flujo 3D actual.
- No introducir gameplay critico dentro de React.
- No tratar el cliente como fuente de verdad.

## Direccion visual y de contenido

- Todo el runtime es 3D.
- Las escenas cliente se describen con builder `unity-scene-export`.
- Los personajes y props deben converger hacia visuales 3D.
- Los content-packs ya no deben tener nuevas carpetas `legacy`.

## Prioridades al tocar codigo

1. Mantener coherencia entre Unity, Babylon y content-packs.
2. Separar bien API, simulacion y render.
3. Favorecer tipos claros y contratos estables.
4. Eliminar restos heredados cuando aparezcan.

## Lo que un agente no debe hacer

- Reintroducir pipelines de contenido heredados o paralelos.
- Crear visuales runtime que no formen parte del flujo 3D de Unity a Babylon.
- Mover autoridad de Unity al cliente.
- Mezclar base de datos o auth sensible dentro del host de Unity.

## Flujo recomendado de trabajo

1. Revisar primero `content-packs`, `apps/client` y `Unity/IslaTortugaServer`.
2. Confirmar como se resuelve `sceneId`.
3. Cambiar el minimo codigo posible para mantener el flujo entero funcionando.
4. Actualizar documentacion cuando cambie arquitectura o pipeline.

## Resumen mental

IslaTortuga es un juego multijugador web 3D. Unity hospeda el servidor autoritativo y Babylon actua como cliente de render y entrada. El camino activo del proyecto pasa por exportar escenas 3D desde Unity a content-packs y consumirlas desde el cliente sin dependencias heredadas ajenas a ese flujo.
