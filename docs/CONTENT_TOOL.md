# Content Tool

Herramienta de escritorio para importar mapas exportados desde Tiled hacia `content-packs`.

## Arranque

- Script de repo:
  - `pnpm run dev:content-tool`
- Lanzador directo:
  - [open-content-tool.bat](/C:/Users/alejandro.langarica/Desktop/Personal/Proyectos/IslaTortuga/IslaTortuga/open-content-tool.bat)

## Flujo de uso

1. Selecciona un mapa `.tmj` exportado desde Tiled
2. Selecciona la carpeta `content-packs` del proyecto
3. Ajusta:
   - `Version`
   - `ContentPackId`
   - `MapId`
4. Pulsa `Analizar dependencias`
5. Si falta alguna dependencia:
   - selecciónala en la tabla
   - pulsa `Resolver dependencia seleccionada...`
6. Cuando todo esté en `OK`, pulsa `Exportar al content pack`

## Qué hace

- Lee el `.tmj`
- Detecta tilesets embebidos y tilesets externos `.tsx`
- Convierte los `.tsx` a formato inline runtime
- Resuelve las imágenes referenciadas por los tilesets
- Copia mapa e imágenes a la estructura correcta del pack
- Actualiza:
  - `manifest.json`
  - `visual-definitions.json`
  - `index.json`
- Crea archivos base en `definitions/` si faltan

## Destinos runtime

- mapa:
  - `content-packs/<version>/maps/<mapId>.tmj`
- imágenes de tileset:
  - `content-packs/<version>/tilesets/...`
- definiciones:
  - `content-packs/<version>/definitions/...`

## Limitaciones actuales

- Está pensada para mapas JSON `.tmj`
- Soporta tilesets TSX de imagen principal
- Tilesets TSX tipo collection-of-images no están soportados todavía
- La herramienta actual importa mapa y dependencias de tilesets; no reemplaza aún un pipeline completo de sprites, audio y atlases
