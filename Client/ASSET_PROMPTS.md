# Prompts de generación de assets — Isla Tortuga

Lista numerada de assets con un **prompt en inglés** listo para pegar en un generador
de imágenes (los generadores rinden mejor en inglés). Cada prompt asume el **ESTILO BASE**
de abajo: cópialo como prefijo (o como style/system) antes del prompt específico.

> **3D y técnico**: los modelos `.glb` (cuerpo, pelos, pedestal) y la **máscara RGBA del
> cuerpo** NO se generan con un generador de imágenes 2D; van por Blender/Substance.
> Van marcados con ⚙️ y llevan, en su lugar, un prompt de *concept art* de referencia.

---

## ESTILO BASE (prefijo común para todos los prompts)

```
Cozy low-poly stylized pirate adventure game asset, early 1800s Caribbean mystery
theme, hand-painted painterly look, soft rounded low-poly shapes, warm golden lantern
light mixed with cool moonlit teal tones, palette of aged parchment cream, dark
varnished wood, weathered brass and gold trim, nautical rope; soft ambient shadows,
subtle grain, charming and inviting, clean game UI art, crisp edges, centered.
```

**Negative (común):**
```
no text, no lettering, no watermark, no signature, photorealistic, harsh neon,
flat vector clip-art, cluttered background, low quality, jpeg artifacts, extra borders.
```

**Convenciones de salida:**
- Elementos de UI (marcos, botones, inputs, iconos, adornos): **fondo transparente**,
  un único elemento centrado → añade al prompt: `isolated single UI element, centered,
  transparent background, PNG with alpha`.
- Fondos de pantalla: **sin transparencia**, escena completa, horizontal.
- Marcos/botones/campos: pídelos **planos y de frente** (no en perspectiva) para poder
  recortarlos en 9-slice después → `flat front view, symmetrical, even margins`.

---

## A. Fondos y marca

**1. `textures/LobbyBG` — Fondo de isla nocturna (login y lobby)**
`[ESTILO BASE] A moonlit tropical pirate cove at night seen from a wooden dock: a tall
stone lighthouse on the left casting a soft glow, a large anchored sailing galleon on
the right, calm reflective sea, palm silhouettes, full moon and drifting clouds, warm
lantern accents; cinematic wide background, depth and atmosphere, slightly desaturated
cozy mystery mood. 16:9, 2560x1440, no UI elements, empty center for panels.`

**2. `ui/LogoIslaTortuga` — Logo del juego**
`[ESTILO BASE] Ornate game logo emblem for "Isla Tortuga": a stylized sea turtle shell
crest fused with a small pirate skull on top, framed by coiled nautical rope and a
brass compass star, carved wood and gold filigree, teal patina accents; emblem only
WITHOUT any letters, isolated, transparent background, centered, 1024x768.`

**3. `textures/PanelLogin` — Panel de pergamino del login**
`[ESTILO BASE] A weathered aged parchment scroll panel with torn soft edges, framed by
thick coiled rope and dark wood corners with brass rivets and a small compass ornament
top-right; empty parchment center for a form, vertical orientation; flat front view,
isolated, transparent background, 1000x1200.`

---

## B. Marcos y superficies (9-slice)

**4. `ui/PanelFrame` — Marco de panel (madera + oro)**
`[ESTILO BASE] A rectangular ornate frame border made of dark varnished wood with a
gold-brass inner trim and nautical rope along the edges, decorative corner brackets;
hollow center (empty/transparent), flat front view, symmetrical, even thickness on all
sides for 9-slice slicing, isolated, transparent background, 512x512.`

**5. `ui/PanelParchment` — Textura de pergamino (interior)**
`[ESTILO BASE] Seamless tileable aged parchment paper texture, warm cream tones, subtle
stains and fibers, soft vignette-free even lighting; flat, top-down, seamless, 1024x1024.`

**6. `ui/RopeTrim` — Cuerda decorativa (tileable horizontal)**
`[ESTILO BASE] A horizontal length of twisted nautical rope, golden-brown hemp, evenly
coiled, seamless tileable left-to-right; flat side view, isolated, transparent
background, 512x96.`

**7. `ui/CornerOrnament` — Adorno de esquina**
`[ESTILO BASE] A single decorative metal corner ornament for a UI panel: aged brass and
gold with a tiny compass-rose and rope motif, fits a top-left corner; isolated,
transparent background, 256x256.`

**8. `ui/HeaderFrame` — Placa de la barra superior**
`[ESTILO BASE] A long horizontal wooden signboard plank bound with rope and brass
brackets, weathered teal-painted wood with gold trim, empty surface; flat front view,
seamless horizontal center for 9-slice, isolated, transparent background, 1600x200.`

---

## C. Botones

**9. `ui/ButtonTeal` — Botón principal (3 estados)**
`[ESTILO BASE] A wide rounded game button plate, weathered teal-painted wood with a
polished gold-brass border and small rivets, gently beveled, soft inner glow; empty
surface (no text); produce three horizontal variants in one sheet: normal, hover
(brighter), pressed (darker, inset). Flat front view, isolated, transparent background,
768x96 each.`

**10. `ui/ButtonGold` — Botón secundario dorado**
`[ESTILO BASE] A wide rounded game button plate in polished gold-brass with darker
engraved border and rivets, warm sheen, empty surface; flat front view, isolated,
transparent background, 512x96.`

**11. `ui/ButtonPlay` — Botón grande "JUGAR"**
`[ESTILO BASE] A large prominent rounded teal wooden button plate with ornate gold frame
and a small anchor emblem on the left, premium call-to-action look, empty surface for
a label; flat front view, isolated, transparent background, 640x160.`

**12. `ui/IconButton` — Botón cuadrado para iconos**
`[ESTILO BASE] A small square wooden button tile with rounded corners, gold-brass beveled
border and rivets, empty recessed center; flat front view, isolated, transparent
background, 128x128.`

**13. `ui/ArrowButton` — Flecha (girar personaje)**
`[ESTILO BASE] An octagonal carved wooden button with a gold-brass rim and an engraved
chevron arrow pointing right, weathered, glowing slightly; flat front view, isolated,
transparent background, 128x128. (Se reflejará para la flecha izquierda.)`

---

## D. Inputs y controles

**14. `ui/InputField` — Campo de texto**
`[ESTILO BASE] A horizontal recessed input field: dark sunken wood interior with a thin
gold-brass frame, slight inner shadow, empty; flat front view, seamless horizontal
center for 9-slice, isolated, transparent background, 512x96.`

**15. `ui/Dropdown` — Desplegable**
`[ESTILO BASE] A horizontal dropdown selector plate in dark wood with gold trim and a
small brass downward chevron on the right, empty; flat front view, isolated,
transparent background, 512x96.`

**16. `ui/TabActive` + `ui/TabInactive` — Pestañas**
`[ESTILO BASE] Two pirate UI tab plates side by side: left ACTIVE (raised, lit parchment
with gold edge), right INACTIVE (darker, recessed wood); empty surfaces, flat front
view, isolated, transparent background, 256x80 each.`

**17. `ui/Badge` — Globo contador**
`[ESTILO BASE] A small round brass notification badge with a subtle teal enamel center,
slight bevel, empty; isolated, transparent background, 96x96.`

**18. `ui/XPBar` — Barra de experiencia (marco + relleno)**
`[ESTILO BASE] A horizontal progress bar: an outer brass-and-wood frame (empty track) and
a separate matching golden glowing fill strip; provide both stacked; flat front view,
seamless horizontal, isolated, transparent background, 512x48.`

**19. `ui/AvatarFrame` — Aro de retrato**
`[ESTILO BASE] A circular ornate avatar frame ring, gold-brass with rope braiding and
small rivets, hollow transparent center; flat front view, isolated, transparent
background, 256x256.`

**20. `ui/ColorSwatchFrame` — Marco de muestra de color**
`[ESTILO BASE] A tiny square color swatch frame, thin gold-brass border with soft inner
shadow, hollow transparent center; flat front view, isolated, transparent background,
64x64.`

**21. `ui/HairThumbFrame` — Marco/realce de miniatura de peinado**
`[ESTILO BASE] A small rounded square thumbnail frame in carved wood with a gold
selection glow variant; provide normal and selected (glowing gold) versions; hollow
center, flat front view, isolated, transparent background, 96x96 each.`

---

## E. Iconos

**22. `ui/IconAtlas` — Set de iconos de interfaz (atlas)**
Genera cada icono con el MISMO estilo (mejor en una sola lámina/atlas para coherencia).
`[ESTILO BASE] A set of cohesive pirate-themed UI glyph icons, engraved gold-brass on a
neutral disc, simple readable silhouettes, consistent line weight, isolated on
transparent background, 64x64 each: 1) envelope/letter, 2) bell, 3) gear/cog,
4) door with exit arrow (logout), 5) message-in-a-bottle or paper bird (send),
6) circular arrows (refresh), 7) plus sign, 8) smiling emoji face, 9) magnifying glass,
10) padlock, 11) sailor bust (user), 12) eye, 13) eye crossed-out, 14) ship steering
wheel. Arrange in a grid, evenly spaced.`

**23. `ui/icons/Coin` — Moneda**
`[ESTILO BASE] A single shiny gold doubloon coin with an engraved turtle/skull motif,
slight 3/4 tilt, warm highlight; isolated, transparent background, 128x128.`

**24. `ui/icons/Gem` — Gema**
`[ESTILO BASE] A single faceted teal-blue gemstone with soft inner glow, cut like a
diamond, premium currency icon; isolated, transparent background, 128x128.`

**25. `ui/icons/Players` — Icono de jugadores**
`[ESTILO BASE] A small engraved brass icon of two pirate sailor busts side by side,
simple readable silhouette; isolated, transparent background, 64x64.`

**26. `ui/icons/Modes` — Emblemas de modo de sala**
`[ESTILO BASE] A set of round wooden-and-brass emblem badges, consistent style, isolated
on transparent background, 96x96 each: 1) ADVENTURE = pirate skull, 2) PvE = treasure
chest, 3) PvP = crossed cutlass swords, 4) anchor, 5) waving pirate flag. Simple, bold,
readable.`

---

## F. Personaje y escena del visor

**27. ⚙️ `textures/IT_Character_mask` — Máscara RGBA del cuerpo (NO IA)**
No usar generador de imágenes. Es una textura técnica: pinta en Blender/Substance, sobre
el UV del cuerpo, zonas planas por canal — R=piel, G=ojos, B=ropa 1, A=ropa 2 (sin
degradados, colores puros 255 por canal). Exportar PNG RGBA 2048².

**28. `textures/hair_previews/Pelo1..Pelo8` — Miniaturas de peinado (×8)**
Idealmente capturas reales del 3D. Si las generas: `[ESTILO BASE] A small portrait icon
of a single low-poly pirate hairstyle floating on a neutral dark teal disc, front 3/4
view, hair only (no face), soft studio light; one distinct style per image (short messy,
medium, long, ponytail, bandana-short, curly, braided, bald cap); isolated, 128x128.`

**29. ⚙️ `models/CharacterPlatform` — Pedestal (concept para 3D)**
Concept para modelar después: `[ESTILO BASE] A small round wooden dock platform / pirate
stage, weathered planks bound with rope, brass rim, a coiled rope and a tiny lantern at
the edge, low-poly stylized, 3/4 view turntable concept, neutral background, 1024x1024.`

**30. `textures/PreviewBackdrop` — Fondo del visor del personaje**
`[ESTILO BASE] A soft blurred moonlit harbor backdrop for a character turntable: distant
ship and lighthouse bokeh, teal night gradient, vignette, low detail so a character
reads in front; square 1024x1024, no UI.`

---

## G. Avatares

**31. `avatars/Default` (+ set) — Retratos de jugador**
`[ESTILO BASE] A friendly low-poly pirate character head-and-shoulders portrait inside a
circular vignette, warm lantern light, cozy mystery mood, distinct face; generate a small
varied set (different ages, hats, bandanas, skin tones) for player/friend avatars;
isolated round portrait, transparent corners, 512x512.`

---

## Orden sugerido de generación

1. **Identidad**: #1 fondo, #2 logo, #3 panel login.
2. **Marco base reutilizable**: #4 PanelFrame, #5 parchment, #9 ButtonTeal, #14 InputField,
   #8 HeaderFrame, #22 set de iconos.
3. **Lobby**: #10–#13, #16–#19, #23–#26, #11 botón JUGAR.
4. **Personaje**: #28 miniaturas, #30 fondo del visor, #20/#21 marcos; y por la vía 3D
   #27 máscara y #29 pedestal.
5. **Avatares**: #31.

> Consejo: fija una **semilla** y reusa el mismo ESTILO BASE en todos para que el set
> quede coherente. Genera primero #4/#5/#9 (definen el "lenguaje" del marco) y úsalos como
> referencia de estilo para el resto.

---

## H. Iconos pintados que faltan (estilo de la lámina) — manifest_ui_common

La lámina actual no incluye los iconos de cabecera/chat/salas. Genéralos con el MISMO
estilo pintado (relieve dorado/latón sobre disco de madera) que el resto, fondo
transparente, 96×96, vista frontal. Mejor en una sola lámina/atlas para coherencia.

**32. `ui/icons/Mail`** `[ESTILO BASE] engraved brass envelope / sealed letter glyph on a
round wooden disc with gold rim, isolated, transparent background, 96x96.`

**33. `ui/icons/Bell`** `[ESTILO BASE] ship's brass bell glyph on a round wooden disc with
gold rim, isolated, transparent background, 96x96.`

**34. `ui/icons/Settings`** `[ESTILO BASE] brass ship-wheel / cog glyph on a round wooden
disc with gold rim, isolated, transparent background, 96x96.`

**35. `ui/icons/Logout`** `[ESTILO BASE] brass door-with-exit-arrow glyph on a round
wooden disc with gold rim, isolated, transparent background, 96x96.`

**36. `ui/icons/Send`** `[ESTILO BASE] brass paper-boat / message-in-a-bottle glyph on a
round wooden disc with gold rim, isolated, transparent background, 96x96.`

**37. `ui/icons/Emoji`** `[ESTILO BASE] brass smiling face glyph on a round wooden disc
with gold rim, isolated, transparent background, 96x96.`

**38. `ui/icons/Search`** `[ESTILO BASE] brass spyglass / magnifying glass glyph on a
round wooden disc with gold rim, isolated, transparent background, 96x96.`

**39. `ui/icons/Refresh`** `[ESTILO BASE] brass circular arrows (refresh) glyph on a round
wooden disc with gold rim, isolated, transparent background, 96x96.`

**40. `ui/icons/Plus`** `[ESTILO BASE] brass plus sign glyph on a round wooden disc with
gold rim, isolated, transparent background, 96x96.`

**41. `ui/icons/Players`** `[ESTILO BASE] brass two-sailor-busts glyph on a round wooden
disc with gold rim, isolated, transparent background, 96x96.`

**42. `ui/icons/ModePvP`** `[ESTILO BASE] crossed cutlass swords emblem on a round
wood-and-brass badge, isolated, transparent background, 96x96.`

**43. `ui/icons/ModeAdventure`** `[ESTILO BASE] pirate skull emblem on a round
wood-and-brass badge, isolated, transparent background, 96x96.`

**44. `ui/icons/ModeAnchor`** `[ESTILO BASE] anchor emblem on a round wood-and-brass
badge, isolated, transparent background, 96x96.`

---

## I. Avatares / retratos — manifest_lobby

**45. `ui/AvatarFrame`** `[ESTILO BASE] circular ornate avatar frame ring, aged gold-brass
with rope braiding and rivets, hollow transparent center, flat front view, isolated,
transparent background, 256x256.`

**46. `avatars/Default`** `[ESTILO BASE] friendly low-poly pirate character bust portrait
inside a soft circular vignette, warm lantern light, cozy mystery mood, neutral
expression, generic face; round portrait, transparent corners, 512x512.`

**47. `avatars/Set` (×6–8)** `[ESTILO BASE] a varied set of low-poly pirate character bust
portraits, different skin tones, ages, hats/bandanas/eyepatches, consistent cozy style
and lighting; each a round portrait with transparent corners, 512x512.`

---

## J. Extras opcionales — manifest_ui_common

**48. `ui/ScrollThumb`** `[ESTILO BASE] a vertical brass-and-rope scrollbar thumb,
rounded, isolated, transparent background, 32x96, seamless vertical center for 9-slice.`

**49. `ui/Divider`** `[ESTILO BASE] a horizontal ornamental divider: thin rope with a
central brass compass-rose medallion, isolated, transparent background, 512x48.`

**50. `ui/Spinner`** `[ESTILO BASE] a ship's wheel / brass compass loading spinner,
single centered element designed to rotate, isolated, transparent background, 128x128.`
