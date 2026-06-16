# Cómo preparar los assets para que el lobby quede EXACTO a la referencia

Análisis de la imagen de referencia y especificación de cada asset: tamaño,
transparencia, **líneas de corte 9-slice**, y a qué clase/elemento del cliente
mapea. La clave para que escale bien y se reutilice: los marcos, banners,
botones e inputs deben ser **piezas vacías, transparentes y en 9-slice**, no
recortes del mockup (el mockup tiene contenido "horneado" y no se puede reutilizar).

## Método general (para que cuadre)

- **Resolución**: exporta a **2×** (el CSS las baja). PNG con alfa.
- **Vista frontal y simétrica** en marcos/botones/inputs (sin perspectiva), para
  poder cortarlos en 9-slice sin deformar.
- **9-slice**: te indico los **insets** (px del borde fijo) sobre el tamaño que
  doy. Centro/bordes se estiran; esquinas fijas. El cliente ya consume `slices`.
- **Misma luz y paleta** en todas las piezas (luz cálida arriba, sombra abajo;
  madera oscura barnizada + filo latón/oro + cuerda + acentos turquesa).
- **Fondo transparente** salvo en los fondos de pantalla.

---

## 1. Fondo (a pantalla completa)

| asset | tamaño | alfa | uso |
|-------|--------|------|-----|
| `textures/LobbyBG` | 2560×1440 | no | Escena nocturna (faro, barco, muelle). `background-size: cover`. Deja el **centro/tercios despejados** (sin detalle fuerte) para que los paneles encima se lean. |

> Ya tienes `ui/LobbyBG`; si quieres clavar la referencia, re-expórtalo a esa resolución y con esa composición (faro a la izq., barco a la dcha., luna arriba-centro).

---

## 2. Cabecera (barra superior)

La barra es **una placa horizontal de madera+cuerda** a todo el ancho, con
contenido a izquierda/centro/derecha. Prepárala por piezas:

| asset | tamaño | 9-slice (T R B L) | uso |
|-------|--------|-------------------|-----|
| `ui/HeaderFrame` | 1920×150 (2×) | 36 48 36 48 | Placa de fondo de la cabecera (madera+cuerda, vacía). Estira en horizontal. |
| `ui/LogoIslaTortuga` | 760×360 | – | Emblema tallado (tortuga+calavera) **con** el texto "ISLA TORTUGA", o solo emblema si prefieres el texto por fuente. Va centrado, **sobresaliendo** por encima de la barra. |
| `ui/AvatarFrame` | 160×160 | – | Aro dorado del retrato (con hueco transparente). |
| `avatars/Default` | 256×256 | – | Retrato dentro del aro (cuadrado, esquinas transparentes). |
| `ui/XPBarFrame` + `ui/XPBarFill` | 320×28 | 10 10 10 10 | Marco + relleno dorado de la barra de XP. |
| `ui/CoinIcon`, `ui/GemIcon` | 96×96 | – | Iconos de moneda/gema (ya los tienes). Van en una **píldora** oscura con borde latón → ver `ui/Pill`. |
| `ui/Pill` | 200×64 | 24 24 24 24 | Cápsula oscura con filo latón para "12.450" / "845". |
| `ui/IconButton` | 96×96 | 22 22 22 22 | Botón cuadrado de madera para los 4 iconos (mail, campana, ajustes, salir). |

---

## 3. Marco de panel + banner de título (los 3 paneles)

En la referencia cada panel tiene un **marco ornamentado** y un **banner de
título** (placa tallada turquesa) que **se monta sobre el borde superior**.
Sepáralos para reutilizar el mismo marco en los 3 paneles:

| asset | tamaño | 9-slice (T R B L) | uso |
|-------|--------|-------------------|-----|
| `ui/PanelFrame` | 600×800 (2×) | 64 56 64 56 | Marco madera+cuerda **hueco** (centro transparente). Se estira a cualquier alto/ancho. Esquinas con remaches/adornos dentro del inset. |
| `ui/PanelParchment` | 512×512 (tileable) | – | Pergamino interior (se ve por el hueco del marco). |
| `ui/TitleBanner` | 420×120 | 60 60 30 60 | Placa tallada turquesa para el título (estira en horizontal). Se coloca **centrada y solapando** el borde superior del marco (margen negativo). |

> Importante para que cuadre: el banner **NO** va dentro del pergamino, va
> superpuesto sobre el marco (como en la referencia). En el cliente lo coloco con
> `position: relative; margin-top: -28px` sobre el panel.

---

## 4. Botones

| asset | tamaño | 9-slice (T R B L) | uso |
|-------|--------|-------------------|-----|
| `ui/ButtonTeal_normal/_hover/_pressed` | 360×96 | 18 26 22 26 | Botones turquesa (Invitar, Unirse, Guardar). Centro plano que estira; remaches en las esquinas. **Sin texto.** |
| `ui/ButtonGold` | 360×96 | 18 26 22 26 | Botón dorado (Crear sala). |
| `ui/ButtonPlay` | 560×150 | 30 110 30 110 | Botón grande "JUGAR" con ancla a la izquierda. El inset izq. **grande** (110) protege el ancla; el centro estira. |
| `ui/ArrowButton` | 110×110 | – | Flecha octogonal (girar personaje). Se refleja para la izquierda. |

> Pide **3 variantes** de ButtonTeal (normal/hover/pressed) en una tira vertical;
> el script de troceado ya las separa.

---

## 5. Pestañas, inputs y desplegables

| asset | tamaño | 9-slice (T R B L) | uso |
|-------|--------|-------------------|-----|
| `ui/TabActive` / `ui/TabInactive` | 280×88 | 26 26 8 26 | Pestañas (Amigos/Solicitudes, Salas púb./priv.). La activa "encaja" sobre la barra. |
| `ui/InputField` | 360×88 | 18 18 18 18 | Campo de texto (chat, código). Interior hundido. |
| `ui/Dropdown` | 360×80 | 18 40 18 18 | Desplegable (Todos los modos/mapas) con chevron a la derecha (inset dcho. mayor). |

---

## 6. Editor de personaje (panel central)

| asset | tamaño | 9-slice | uso |
|-------|--------|---------|-----|
| `ui/ColorPanel` | 900×280 | 28 28 28 28 | Sub-placa de pergamino que enmarca las columnas de color (la cajita de la referencia). |
| `ui/SwatchFrame` | 56×56 | – | (Opcional) marquito de cada muestra de color. |
| `ui/HairThumb` / `ui/HairThumbSelected` | 96×96 | 16 16 16 16 | Marco de miniatura de peinado (normal y seleccionado con brillo dorado). |
| `models/CharacterPlatform` | GLB | – | Pedestal/dock circular de madera bajo el personaje (modelo 3D, no imagen). |
| `textures/PreviewBackdrop` | 1024×1024 | no | Mar nocturno borroso de fondo del visor (o skybox). |
| `textures/hair_previews/PeloN` | 96×96 ×8 | – | Miniaturas reales de cada peinado (captura del 3D). |

---

## 7. Salas e iconos de modo

| asset | tamaño | uso |
|-------|--------|-----|
| `ui/RoomRow` | 600×96, 9-slice 14 14 14 14 | (Opcional) marco de cada fila de sala. |
| `ui/icons/ModeAdventure` (calavera), `ModePvE` (cofre), `ModePvP` (espadas), `ModeAnchor`, `Players` | 96×96 | Emblemas de modo y contador de jugadores. |

---

## 8. Iconos de UI (cabecera/chat)

`ui/icons/Mail, Bell, Settings, Logout, Send, Emoji, Refresh, Plus, Search` —
96×96 cada uno, relieve latón sobre disco. Mejor en **un atlas** para coherencia.
(Hoy van como SVG; estos PNG los sustituirían si los quieres pintados.)

---

## Proporciones del layout (para que la distribución sea idéntica)

Aunque cambies arte, estas medidas hacen que la **estructura** sea la de la
referencia. Las aplico en el CSS:

- **Cabecera**: alto ~88–96px; logo centrado sobresaliendo ~20px por arriba.
- **Columnas** (de los gaps incluidos): izquierda **≈ 24%**, centro **≈ 40%**,
  derecha **≈ 28%**, gaps ~1.5%. (Hoy uso `320px / 1fr / 360px`; lo paso a estos
  porcentajes para clavar la referencia.)
- **Banner de título** solapando el borde superior del panel (margin-top ≈ -28px).
- **Botón JUGAR** ocupa todo el ancho del panel derecho, alto ~64px.
- **Preview del personaje**: cuadrado, ocupa la mitad superior del panel central.

---

## Recomendación (la vía más exacta)

1. **Genera/extrae las piezas VACÍAS** de la tabla (marco, banner, botones,
   inputs, cabecera) en el estilo de la referencia, transparentes y a 2×. No
   intentes recortar los paneles del mockup (traen texto/contenido horneado).
2. Súbelas con su `assetKey` al manifest `uiCommon` (como ya hiciste con la
   primera lámina). Si me las pasas en una lámina, las **troceo yo** con el
   script y te genero los `slices.json` con los insets de arriba.
3. Yo ajusto el CSS para usar el `TitleBanner`, el `ColorPanel`, las píldoras y
   las proporciones por porcentaje → distribución idéntica.

> Si me pasas la **referencia a máxima resolución**, puedo extraer de ahí el
> **fondo** y, como guía de color/medidas, calcar los insets exactos; pero los
> marcos/botones reutilizables conviene tenerlos como piezas limpias aparte.
