# Cómo preparar y entregar los assets de UI (para que se usen y queden bien)

Objetivo: que el lobby quede como la referencia y que las piezas **escalen sin
deformarse**. Esto es el "cómo prepararlos"; el detalle pieza a pieza (tamaños e
insets exactos) está en `ASSET_PREP_LOBBY.md`.

---

## A. Reglas generales (para TODAS las piezas)

1. **Formato:** PNG con **canal alfa** (transparencia real). Nada de fondo
   blanco/beige; el fondo debe ser **transparente**.
2. **Resolución 2×:** exporta al **doble** del tamaño en pantalla (te doy el
   tamaño base; expórtalo ×2). El CSS lo baja → nítido en pantallas grandes.
3. **Recorte ajustado al contenido**, pero con el **marco completo** (no cortes
   las esquinas decorativas).
4. **Vista frontal, plana y simétrica** en marcos, botones, inputs y banner
   (sin perspectiva ni inclinación) — es lo que permite reescalar.
5. **Misma luz y paleta** en todo: luz cálida arriba, sombra abajo; madera oscura
   barnizada + filo latón/oro + cuerda + acentos turquesa.
6. **Un asset = un PNG**, y el **nombre = assetKey**. Ej.: fichero `PanelFrame.png`
   en `server_assets/ui/` → assetKey **`ui/PanelFrame`**.

---

## B. 9-slice = la clave para que reescale (marcos, botones, inputs, banner)

Un marco/botón no se estira como una foto: se corta en **9 trozos**. Las **4
esquinas quedan fijas**, los **4 bordes** se estiran solo en su dirección y el
**centro** rellena. Así el MISMO marco sirve para un panel alto o un botón ancho
sin deformarse.

Para que el 9-slice quede bien, al diseñar la pieza:

- **Borde de grosor uniforme** en los 4 lados (las esquinas/adornos/remaches
  caben **dentro** de ese borde).
- **Centro uniforme/tileable** (madera o pergamino liso). **No** pongas en el
  centro nada que no deba estirarse (un remache o un escudo en mitad del botón
  se estiraría feo).
- **Icono fijo en un botón** (p. ej. el ancla de "JUGAR"): ponlo **pegado a un
  lado**; ese lado llevará un inset grande para que no se deforme.
- **Estados de botón** (normal / hover / pressed): dámelos como **3 piezas** (o
  una tira vertical con los 3); el script los separa.

**Los insets** (px de borde fijo en arriba/derecha/abajo/izquierda): o me los
dices, o me das las piezas y **yo los mido** y genero el `slices.json`. En
`ASSET_PREP_LOBBY.md` ya tienes mi propuesta de insets por pieza.

---

## C. Tipos de pieza (centro hueco vs relleno)

| Tipo | Centro | Ejemplos |
|------|--------|----------|
| **Marco hueco** | **transparente** (se ve lo de detrás) | `ui/PanelFrame` (deja ver el pergamino) |
| **Placa sólida** | relleno | botones, banner de título, inputs, pergamino, cabecera |
| **Icono / logo / moneda** | (no es 9-slice) | escalan uniformes; solo 2× + alfa |
| **Fondo** | sin alfa | `textures/LobbyBG` (grande, centro despejado) |

---

## D. Cómo me lo entregas (elige una)

**Opción 1 — piezas sueltas (recomendada):**
- Un PNG por pieza, nombrado con su assetKey.
- Los registras en el AssetEditor y los metes en el manifest **`uiCommon`**
  (targetType `global`, targetId `uiCommon`), `status: published`.
- Yo ya tengo el cliente cableado para consumirlos; solo ajusto insets si hace falta.

**Opción 2 — una lámina (como la primera vez):**
- Pones **todas las piezas en una sola imagen** sobre un **fondo liso uniforme**
  (gris/beige plano), separadas, **a máxima resolución**.
- Me la pasas y yo: **troceo**, quito el fondo a transparencia, separo estados,
  y genero el `slices.json` con los insets. Tú luego las registras.

> En ambos casos: tras subirlas al manifest `uiCommon` (published + current),
> recargas con Ctrl+Shift+R y el skin las aplica solo.

---

## E. Imprescindibles para clavar la referencia (resumen)

Tamaño = **base** (expórtalo ×2). "9-slice" = lleva corte escalable.

| assetKey | tamaño base | 9-slice | alfa / centro | dónde |
|----------|-------------|---------|----------------|-------|
| `textures/LobbyBG` | 2560×1440 | no | sin alfa | fondo del lobby |
| `ui/HeaderFrame` | 1920×150 | sí (T36 R48 B36 L48) | alfa / relleno | barra superior |
| `ui/LogoIslaTortuga` | 760×360 | no | alfa | logo central |
| `ui/PanelFrame` | 600×800 | sí (64 56 64 56) | alfa / **hueco** | marco de panel |
| `ui/PanelParchment` | 512×512 tileable | no | sin alfa | interior pergamino |
| `ui/TitleBanner` | 420×120 | sí (60 60 30 60) | alfa / relleno | banner de título |
| `ui/ButtonTeal_normal/_hover/_pressed` | 360×96 | sí (18 26 22 26) | alfa / relleno | botones turquesa |
| `ui/ButtonGold` | 360×96 | sí (18 26 22 26) | alfa / relleno | "Crear sala" |
| `ui/ButtonPlay` | 560×150 | sí (30 110 30 110) | alfa / relleno | "JUGAR" (ancla izq.) |
| `ui/InputField` | 360×88 | sí (18 18 18 18) | alfa / relleno | campos de texto |
| `ui/Dropdown` | 360×80 | sí (18 40 18 18) | alfa / relleno | filtros de salas |
| `ui/TabActive` / `ui/TabInactive` | 280×88 | sí (26 26 8 26) | alfa / relleno | pestañas |
| `ui/IconButton` | 96×96 | sí (22 22 22 22) | alfa / relleno | iconos cabecera |
| `ui/AvatarFrame` | 160×160 | no | alfa / hueco | aro de retrato |
| `ui/ColorPanel` | 900×280 | sí (28 28 28 28) | alfa / relleno | caja de colores |
| `ui/HairThumb` / `ui/HairThumbSelected` | 96×96 | sí (16 16 16 16) | alfa / relleno | miniatura de pelo |
| Iconos `ui/icons/*` (mail, bell, settings, logout, send, refresh, plus, players, modos) | 96×96 | no | alfa | iconos varios |

3D (van por su pipeline, no por el manifest de UI): `models/CharacterPlatform`
(pedestal), `textures/PreviewBackdrop` (fondo del visor), `textures/IT_Character_mask`
(máscara RGBA para colorear el cuerpo por zonas), `textures/hair_previews/PeloN`
(miniaturas reales de cada peinado).

---

## Resumen en una frase

PNG **transparentes a 2×**, **marcos/botones/inputs en plano y con borde uniforme**
(para 9-slice), nombre = **assetKey**, y me los das **sueltos** (los registras en
`uiCommon`) **o en una lámina** (yo troceo y saco los insets). Con eso lo enchufo
y queda como la referencia.
