# Registro de assets de UI en el manifest

Los 32 PNG troceados están en `Server/GameAssets/server_assets/ui/`. El AssetEditor
los detecta ahí; su **assetKey** = ruta sin extensión (p. ej. `ui/PanelFrame`).

## Pasos en el AssetEditor

1. Escanear/registrar los ficheros de `server_assets/ui/` (status **published**).
2. Crear un manifest **`manifest_ui_common`**: targetType **`global`**, targetId
   **`uiCommon`**, status **published**, y **marcarlo como current**.
3. Añadir al manifest todos los `ui/*` (el `usage` es opcional; el cliente mapea por
   assetKey). Sugerencia de `usage` en la tabla.
4. En `Client/.env`: `VITE_UI_MANIFEST_TARGET_ID=uiCommon` (ya es el valor por defecto)
   y **reiniciar** `npm run dev`.

El cliente carga ese manifest, inyecta cada asset como variable CSS y aplica el skin.
Si el manifest no existe, se mantiene el tema CSS de respaldo (no se rompe nada).

## Tabla (assetKey → uso en cliente)

| Fichero | assetKey | usage sugerido | Dónde se usa |
|---------|----------|----------------|--------------|
| LogoIslaTortuga.png | `ui/LogoIslaTortuga` | logo | Marca (pendiente swap a img) |
| LobbyBG.png | `ui/LobbyBG` | background | Fondo del lobby |
| PanelFrame.png | `ui/PanelFrame` | panel_frame | Marco de todos los paneles (9-slice) |
| PanelParchment.png | `ui/PanelParchment` | panel_body | Interior de pergamino |
| HeaderFrame.png | `ui/HeaderFrame` | header_frame | Barra superior |
| RopeTrim.png | `ui/RopeTrim` | rope | (reservado) |
| CornerOrnament.png | `ui/CornerOrnament` | corner | (reservado) |
| ButtonTeal_normal.png | `ui/ButtonTeal_normal` | btn | Botones (normal) |
| ButtonTeal_hover.png | `ui/ButtonTeal_hover` | btn | Botones (hover) |
| ButtonTeal_pressed.png | `ui/ButtonTeal_pressed` | btn | Botones (pressed) |
| ButtonGold.png | `ui/ButtonGold` | btn | Botón dorado (Crear sala) |
| ButtonPlay.png | `ui/ButtonPlay` | btn | Botón JUGAR |
| IconButton.png | `ui/IconButton` | icon_btn | Iconos de cabecera |
| ArrowButton.png | `ui/ArrowButton` | arrow | (visor personaje) |
| CloseButton.png | `ui/CloseButton` | close | (reservado) |
| InputField.png | `ui/InputField` | input | Campos de texto |
| Dropdown.png | `ui/Dropdown` | dropdown | Filtros de salas |
| Checkbox.png | `ui/Checkbox` | checkbox | (reservado) |
| Slider.png | `ui/Slider` | slider | (reservado) |
| TabActive.png | `ui/TabActive` | tab_on | Pestañas activas |
| TabInactive.png | `ui/TabInactive` | tab_off | Pestañas inactivas |
| Notification.png | `ui/Notification` | notif | (reservado) |
| CoinIcon.png | `ui/CoinIcon` | icon | Monedas (cabecera) |
| GemIcon.png | `ui/GemIcon` | icon | Gemas (cabecera) |
| EnergyIcon.png | `ui/EnergyIcon` | icon | (reservado) |
| ChestIcon.png | `ui/ChestIcon` | icon | (modo PvE de sala) |
| MapIcon.png | `ui/MapIcon` | icon | (reservado) |
| RankBadge.png | `ui/RankBadge` | icon | (reservado) |
| ButtonTeal.png | — | — | (compuesto; no registrar, usar los _normal/_hover/_pressed) |
| RadioOn/RadioOff.png | `ui/RadioOn` / `ui/RadioOff` | radio | (reservado) |

> Los insets de 9-slice están fijados a mano en `src/styles/skin.css`
> (p. ej. PanelFrame 32/27). Si al verlo algún marco/botón se ve estirado o con el
> borde mal, se ajustan ahí en 1 línea por asset.
