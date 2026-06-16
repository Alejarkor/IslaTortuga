# Inventario de assets — Isla Tortuga (Login + Lobby)

Lista de assets necesarios para que el **login** y la **pantalla de pre-juego (lobby)**
tengan el aspecto de las referencias. Sigue la convención del sistema de assets del
proyecto (`assetKey` tipo `categoria/Nombre`, campo `usage`, manifests por pantalla).

## Cómo está pensado

- **Formatos**: imágenes UI en **PNG** (con transparencia donde haga falta) o **WebP**;
  modelos en **GLB**; audio en **OGG**.
- **9-slice**: los marcos, botones y campos deberían ser **9-slice** (esquinas fijas +
  bordes/centro escalables) para que aguanten cualquier tamaño sin deformarse. Lo marco
  con ✅ en la columna *9-slice*. El cliente ya está preparado para escalar estos paneles.
- **@2x**: exporta a resolución alta (1.5×–2×) para pantallas grandes; el CSS las baja.
- **usage**: etiqueta lógica que el cliente usa para resolver cada asset dentro del
  manifest (hoy la herramienta lo deja a `null`; recomiendo rellenarlo para no depender
  de heurísticas por nombre).

## Manifests sugeridos

| Manifest | targetType / targetId | Contenido |
|----------|----------------------|-----------|
| `manifest_login` | global / `login` | Fondo + panel del login (ya existe con `LoginBG`, `PanelLogin`) |
| `manifest_ui_common` | global / `uiCommon` | Marcos, botones, inputs, tabs, iconos, logo (compartidos login+lobby) |
| `manifest_lobby` | global / `lobby` | Fondo del lobby, iconos de salas, avatares, plataforma |
| `manifest_player_editor_pregame` | global / `playerEditorPregame` | 3D del personaje (ya existe) + máscara + previews de pelo + pedestal |

---

## 1. Compartidos / globales (manifest_ui_common)

| assetKey | usage | tipo | formato | tamaño aprox. | 9-slice | Uso |
|----------|-------|------|---------|---------------|:------:|-----|
| `ui/LogoIslaTortuga` | `logo` | texture | PNG (alpha) | 760×360 | – | Logo tallado (tortuga+calavera+cuerda). Login y cabecera del lobby |
| `ui/PanelFrame` | `panel_frame` | texture | PNG (alpha) | 512×512 | ✅ | Marco de madera + filo dorado de los paneles grandes |
| `ui/PanelParchment` | `panel_body` | texture | PNG/WebP | 512×512 (tileable) | ✅ | Textura de pergamino del interior de los paneles |
| `ui/RopeTrim` | `rope_trim` | texture | PNG (alpha) | 256×64 (tileable) | ✅ (horizontal) | Cuerda que bordea paneles/cabecera |
| `ui/CornerOrnament` | `corner` | texture | PNG (alpha) | 128×128 | – | Adorno metálico de esquina (brújula, ancla, remache) |
| `ui/ButtonTeal` | `btn_primary` | texture | PNG (alpha) | 256×96 | ✅ | Botón turquesa (Entrar, Unirse, Guardar). Incluir estados normal/hover/pressed |
| `ui/ButtonGold` | `btn_secondary` | texture | PNG (alpha) | 256×96 | ✅ | Botón dorado (Crear sala) |
| `ui/ButtonPlay` | `btn_play` | texture | PNG (alpha) | 420×120 | ✅ | Botón grande "JUGAR" (placa con ancla) |
| `ui/IconButton` | `icon_btn` | texture | PNG (alpha) | 96×96 | ✅ | Botón cuadrado de madera para iconos de cabecera |
| `ui/InputField` | `input` | texture | PNG (alpha) | 256×72 | ✅ | Campo de texto (madera hundida con filo dorado) |
| `ui/Dropdown` | `dropdown` | texture | PNG (alpha) | 256×64 | ✅ | Desplegable (filtros de salas) |
| `ui/TabActive` | `tab_on` | texture | PNG (alpha) | 200×64 | ✅ | Pestaña activa |
| `ui/TabInactive` | `tab_off` | texture | PNG (alpha) | 200×64 | ✅ | Pestaña inactiva |
| `ui/Badge` | `badge` | texture | PNG (alpha) | 48×48 | – | Globo de contador (Solicitudes "2") |
| `ui/ScrollThumb` | `scroll` | texture | PNG (alpha) | 24×96 | ✅ (vertical) | Barra de scroll (chat / listas) |

### Iconos (manifest_ui_common) — PNG con alpha, ~64×64 (o atlas)

`ui/icons/Mail`, `ui/icons/Bell`, `ui/icons/Settings`, `ui/icons/Logout`,
`ui/icons/Send`, `ui/icons/Refresh`, `ui/icons/Plus`, `ui/icons/Emoji`,
`ui/icons/Search`, `ui/icons/Coin`, `ui/icons/Gem`, `ui/icons/Lock`,
`ui/icons/User`, `ui/icons/Eye`, `ui/icons/EyeOff`, `ui/icons/Wheel`.

> Alternativa: un único **atlas** `ui/IconAtlas` (PNG 512×512) + JSON de coordenadas.
> Hoy uso iconos SVG inline como placeholder; estos PNG los sustituirían.

### Tipografía (opcional)
| assetKey | tipo | formato | Uso |
|----------|------|---------|-----|
| `fonts/PirateDisplay` | font | WOFF2 | Títulos tallados (hoy uso Cinzel Decorative de Google Fonts) |
| `fonts/PirateBody` | font | WOFF2 | Texto general (hoy Cinzel) |

---

## 2. Login (manifest_login) — ya existe, se puede ampliar

| assetKey | usage | tipo | formato | tamaño | Estado |
|----------|-------|------|---------|--------|--------|
| `textures/LoginBG` | `background` | texture | PNG/WebP | 1920×1080 (o 2560×1440) | ✅ ya subido |
| `textures/PanelLogin` | `panel` | texture | PNG (alpha) | ~900×1100 | ✅ ya subido |

> El login ya consume estos dos. El resto (logo, botón, inputs, iconos) saldría de
> `manifest_ui_common`.

---

## 3. Lobby — Fondo y cabecera (manifest_lobby)

| assetKey | usage | tipo | formato | tamaño | 9-slice | Uso |
|----------|-------|------|---------|--------|:------:|-----|
| `textures/LobbyBG` | `background` | texture | PNG/WebP | 1920×1080+ | – | Fondo nocturno de isla del lobby (puede reutilizar `LoginBG`) |
| `ui/HeaderFrame` | `header_frame` | texture | PNG (alpha) | 1024×128 | ✅ (horizontal) | Placa de madera + cuerda de la barra superior |
| `ui/XPBarFrame` | `xp_frame` | texture | PNG (alpha) | 256×24 | ✅ | Marco de la barra de experiencia |
| `ui/XPBarFill` | `xp_fill` | texture | PNG | 256×24 | ✅ | Relleno dorado de la barra de XP |

### Avatares
| assetKey | usage | tipo | formato | tamaño | Uso |
|----------|-------|------|---------|--------|-----|
| `ui/AvatarFrame` | `avatar_frame` | texture | PNG (alpha) | 128×128 | Aro dorado del retrato (cabecera y amigos) |
| `avatars/Default` | `avatar_default` | texture | PNG | 256×256 | Retrato por defecto |
| `avatars/Set01..NN` | `avatar` | texture | PNG | 256×256 | Galería de retratos de jugador/amigos |

> A medio plazo el retrato podría **autogenerarse** del personaje 3D (captura), y
> entonces no harían falta retratos fijos salvo el por defecto.

---

## 4. Lobby — Amigos y Chat (manifest_lobby / ui_common)

| assetKey | usage | tipo | formato | tamaño | Uso |
|----------|-------|------|---------|--------|-----|
| `ui/StatusDot` | `status` | texture | PNG (alpha) | 32×32 | Punto de estado en línea/ausente (o se hace por CSS) |
| `ui/ChatBubble` | `chat_bubble` | texture | PNG (alpha) | 256×96 | (Opcional) fondo de mensaje de chat |

> Botón "Invitar", input del chat, iconos enviar/emoji → de `manifest_ui_common`.

---

## 5. Lobby — Personaje / editor (manifest_player_editor_pregame)

| assetKey | usage | tipo | formato | tamaño | Uso |
|----------|-------|------|---------|--------|-----|
| `models/IT_Character - Rigged` | `body` | model | GLB | – | ✅ ya subido (malla `Character`, anim `Idle`) |
| `models/Pelos` | `hair` | model | GLB | – | ✅ ya subido (8 estilos `Pelo1`..`Pelo8`) |
| `textures/IT_Character_mask` | `body_mask` | texture | PNG (RGBA) | 2048×2048 | **Clave**: máscara de zonas para colorear piel/ojos/ropa1/ropa2 (R/G/B/A) |
| `textures/IT_Character_albedo` | `body_base` | texture | PNG | 2048×2048 | (Opcional) textura base si se separa del GLB |
| `models/CharacterPlatform` | `platform` | model | GLB | – | Pedestal/dock circular de madera bajo el personaje |
| `textures/PreviewBackdrop` | `preview_bg` | texture | PNG/WebP | 1024×1024 | Fondo del visor (mar nocturno) o skybox |
| `textures/hair_previews/Pelo1..Pelo8` | `hair_preview` | texture | PNG | 96×96 | Miniaturas reales de cada peinado (hoy uso placeholder) |
| `ui/ArrowButton` | `arrow_btn` | texture | PNG (alpha) | 96×96 | Flecha octagonal de madera (girar personaje) |
| `ui/ColorSwatchFrame` | `swatch_frame` | texture | PNG (alpha) | 48×48 | Marco de las muestras de color (opcional) |
| `ui/ColorPanel` | `color_panel` | texture | PNG (alpha) | 512×256 | Placa de fondo del panel de colores (opcional) |

> **Prioridad funcional**: `textures/IT_Character_mask`. Sin ella, el cambio de color por
> zonas del cuerpo no funciona (solo tinte plano). Con ella, los 4 canales RGBA dan
> piel/ojos/ropa1/ropa2 independientes. Para un 5º canal (vello/cejas) o tatuajes haría
> falta una segunda máscara.

---

## 6. Lobby — Salas (manifest_lobby / ui_common)

| assetKey | usage | tipo | formato | tamaño | Uso |
|----------|-------|------|---------|--------|-----|
| `ui/RoomRowFrame` | `room_row` | texture | PNG (alpha) | 320×72 | (Opcional) marco de cada fila de sala |
| `ui/icons/ModeAdventure` | `mode_icon` | texture | PNG (alpha) | 64×64 | Emblema "Aventura" (calavera) |
| `ui/icons/ModePvE` | `mode_icon` | texture | PNG (alpha) | 64×64 | Emblema "PvE" |
| `ui/icons/ModePvP` | `mode_icon` | texture | PNG (alpha) | 64×64 | Emblema "PvP" (espadas cruzadas) |
| `ui/icons/Anchor` | `mode_icon` | texture | PNG (alpha) | 64×64 | Ancla / otros modos |
| `ui/icons/Players` | `players_icon` | texture | PNG (alpha) | 48×48 | Icono de jugadores (x/x) |

> Botones "Unirse", "Crear sala", "JUGAR", desplegables y refresh → de `manifest_ui_common`.

---

## 7. Audio (opcional — manifest_lobby / ui_common)

| assetKey | usage | tipo | formato | Uso |
|----------|-------|------|---------|-----|
| `audio/LobbyAmbient` | `music` | audio | OGG | Música/ambiente del lobby (olas, gaviotas) |
| `audio/ClickButton` | `sfx` | audio | OGG | Clic de botón |
| `audio/HoverButton` | `sfx` | audio | OGG | Hover |
| `audio/SaveAppearance` | `sfx` | audio | OGG | Confirmación al guardar apariencia |

---

## Resumen de prioridades

1. **Funcional (desbloquea features):** `textures/IT_Character_mask` (color por zonas),
   `textures/hair_previews/PeloN` (miniaturas reales).
2. **Identidad visual (mayor impacto):** `ui/LogoIslaTortuga`, `textures/LobbyBG`,
   `ui/PanelFrame` + `ui/PanelParchment`, `ui/ButtonTeal`/`ButtonGold`/`ButtonPlay`,
   `ui/InputField`, `ui/HeaderFrame`.
3. **Detalle/acabado:** iconos PNG (o atlas), `models/CharacterPlatform`, avatares,
   adornos de esquina, tabs, barra XP, iconos de modos de sala.
4. **Opcional:** audio, fuentes propias, fondos de previsualización/skybox.

Todos los marcos/botones/inputs mejor en **9-slice** para que el rediseño responsive
posterior no los deforme.
