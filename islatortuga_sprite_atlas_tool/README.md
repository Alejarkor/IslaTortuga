# IslaTortuga Sprite Atlas Tool

Herramienta para convertir exports de generadores/herramientas de pixel-art en assets usables en Phaser.

Hace esto:

1. Importa un `.zip` o una carpeta con PNGs.
2. Detecta animaciones y direcciones.
3. Recorta el área visible de cada frame usando transparencia.
4. Normaliza cada sprite a un tamaño fijo configurable, por ejemplo `32x64`.
5. Genera:
   - `player_atlas.png`
   - `player_atlas.json`
   - `player_animations.js`
   - `preview_sheet.png`
   - `normalized_frames/`
   - `export_config.json`

## Instalación

```bash
python -m venv .venv
```

### Windows

```bash
.venv\Scripts\activate
pip install -r requirements.txt
python sprite_atlas_tool.py --gui
```

### Linux / macOS

```bash
source .venv/bin/activate
pip install -r requirements.txt
python sprite_atlas_tool.py --gui
```

## Uso GUI

Ejecuta:

```bash
python sprite_atlas_tool.py --gui
```

En la interfaz:

- Selecciona el ZIP/carpeta de entrada.
- Selecciona carpeta de salida.
- Elige el nombre del asset, por ejemplo `player`.
- Configura:
  - `Frame width`: `32`
  - `Frame height`: `64`
  - `Modo`: normalmente `fit`
  - `Anchor`: `center` o `bottom_center`

Pulsa **Escanear input** para revisar qué animaciones detecta.

Pulsa **Exportar atlas**.

## Uso CLI

Ejemplo para tu caso:

```bash
python sprite_atlas_tool.py ^
  --input Pixel_art_character_sprite_32x32.zip ^
  --output export_player ^
  --asset player ^
  --frame-width 32 ^
  --frame-height 64 ^
  --mode fit ^
  --anchor center
```

En Linux/macOS:

```bash
python sprite_atlas_tool.py \
  --input Pixel_art_character_sprite_32x32.zip \
  --output export_player \
  --asset player \
  --frame-width 32 \
  --frame-height 64 \
  --mode fit \
  --anchor center
```

## Modos de normalización

### fit

Recorta la transparencia, escala el sprite si hace falta para que quepa entero, y lo centra.

Recomendado para personajes generados por IA.

### pad

Recorta la transparencia y pega el sprite centrado sin escalar.

Si no cabe, se recorta al pegar.

### crop

Recorta alrededor del centro del contenido visible hasta el tamaño final.

Puede cortar partes del sprite si el contenido es mayor que el frame final.

## Anchor

### center

Centra el sprite dentro del frame.

### bottom_center

Centra horizontalmente y apoya el sprite abajo.

Suele ser útil para personajes porque mantiene los pies más estables.

## Phaser

Carga el atlas:

```js
this.load.atlas(
  'player',
  'assets/player/player_atlas.png',
  'assets/player/player_atlas.json'
);
```

Importa el JS generado:

```js
import createPlayerAnimations from './player_animations.js';

createPlayerAnimations(this, 'player');
```

Usa animaciones:

```js
player.play('player-idle-down');
player.play('player-walk-down');
```

## Estructura compatible

Funciona especialmente bien con exports tipo:

```txt
animations/
  animation-373f82de/
    south/
      frame_000.png
      frame_001.png
  Breathing_Idle-0703b623/
    south/
      frame_000.png
      frame_001.png
rotations/
  south.png
  north.png
```

Por defecto ignora `rotations/` porque normalmente son poses estáticas. Puedes activar `include_rotations_as_poses`.

## Recomendación para IslaTortuga

Para personajes:

```txt
frame_width: 32
frame_height: 64
mode: fit
anchor: bottom_center
```

Para objetos pequeños:

```txt
frame_width: 32
frame_height: 32
mode: pad
anchor: center
```

Para árboles/props grandes:

```txt
frame_width: 64
frame_height: 96
mode: fit
anchor: bottom_center
```
