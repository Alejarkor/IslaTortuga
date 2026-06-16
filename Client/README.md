# Isla Tortuga — Cliente de Pre-juego

Editor de personaje en la pantalla de pre-juego. Implementa la definición de
_"Editor de Personaje en Pre-Juego"_ integrándose con el backend ya existente
(`Server/WebServer` + `Server/GameApi`) **sin requerir cambios en la base de datos**.

## Stack

Vite · React · TypeScript · React Router · TanStack Query · Zustand · Zod · Babylon.js

## Puesta en marcha

```bash
cd Client
cp .env.example .env      # ajusta valores si hace falta
npm install
npm run dev               # http://localhost:5173
```

El servidor de desarrollo proxya `/api` y `/assets` hacia el `WebServer`
(`http://localhost:3000` por defecto), de modo que la cookie de sesión HttpOnly
se comparte en el mismo origen. Configurable con `VITE_WEB_SERVER_URL`.

Scripts: `npm run dev`, `npm run build`, `npm run preview`, `npm run typecheck`.

## Integración con el backend (sin tocar BD)

| Necesidad | Endpoint existente usado |
|-----------|--------------------------|
| Sesión / login / registro / logout | `/api/me`, `/api/auth/*` |
| Perfil y stats | `/api/profile`, `/api/stats` |
| Cargar apariencia | `GET /api/profile` → `appearance_json` |
| Guardar apariencia | `PATCH /api/profile/appearance` con `{ appearance }` |
| Manifest del personaje | `GET /assets/manifest?targetType&targetId` |
| Descarga de GLB/texturas | `GET /assets/files/...` |

La apariencia se persiste en la columna **`player_profiles.appearance_json`**
(jsonb), que ya existe. **No se han creado tablas nuevas.**

### Estructura guardada en `appearance_json`

```json
{
  "schema_version": 1,
  "body_asset_key": "characters/body/body_base_01",
  "hair_id": "Pelo3",
  "colors": {
    "skin": "#C98F65",
    "eyes": "#3A5F85",
    "clothes_primary": "#2E7BDC",
    "clothes_secondary": "#3A3A3A",
    "hair_color": "#211710"
  }
}
```

`hair_id` es `"none"` (sin pelo) o el nombre del nodo del estilo dentro del pack
(`Pelo1`..`Pelo8`). El cliente normaliza datos antiguos/incompletos
(`coerceAppearance`), así que perfiles existentes no rompen el editor.

## Assets reales y manifest

Los modelos se cargan desde el **manifest** creado con la herramienta de assets
(`Tools/AssetEditor`). Configuración actual (en `.env`):

- **targetType:** `global`  ·  **targetId:** `playerEditorPregame`
- **Cuerpo:** un GLB, assetKey `models/IT_Character - Rigged`
- **Pack de pelo:** un GLB, assetKey `models/Pelos`, con 8 nodos `Pelo1`..`Pelo8`

Como la herramienta deja `usage = null`, el cliente identifica cuerpo y pelo por
su `assetKey` (configurable con `VITE_CHARACTER_BODY_ASSET_KEY` /
`VITE_CHARACTER_HAIR_ASSET_KEY`). Los 8 estilos de pelo se **descubren en
tiempo de ejecución** leyendo los nodos del pack; "Sin pelo" lo añade el cliente.

> **Importante:** el endpoint `GET /assets/manifest` solo devuelve manifests con
> `status = published` y marcados como vigentes (`is_current`). El manifest
> `playerEditorPregame` está ahora en `draft`: hay que **publicarlo y marcarlo
> como current** desde la herramienta de assets para que el cliente lo cargue.
> Mientras tanto, el editor funciona con un **maniquí procedural de respaldo**.

## Modelo de color

- El **pelo** se tiñe por completo con el color del slot `hair_color` (se aplica
  al estilo seleccionado).
- El **cuerpo**:
  - Si el manifest incluye una **textura máscara RGBA** (un fichero con
    `usage = body_mask` o cuyo assetKey contenga "mask"), se usa el shader de
    máscara: 4 canales (skin/eyes/clothes_primary/clothes_secondary).
  - Si **no** hay máscara (caso actual: el GLB del cuerpo trae 2 materiales y
    sin máscara), se aplica **tinte adaptativo**: los slots de cuerpo se mapean
    a los materiales del GLB en orden. Con 2 materiales, `skin` y `eyes`
    recolorean; para las 4-5 zonas independientes hay que exportar la máscara
    RGBA y añadirla al manifest (`usage = body_mask`).

El selector permite cualquier color **excepto transparencia** (sin alfa).
La configuración de slots vive en `src/config/characterColorSlots.ts`.

## Estructura del proyecto

```
src/
├── api/            # cliente HTTP + endpoints (auth, profile, appearance, assets)
├── app/            # router + queryClient
├── config/         # env, slots de color, defaults
├── domain/         # esquemas Zod, validación, diff, catálogo desde manifest
├── store/          # estado del editor (guardado vs edición) — Zustand
├── three/          # render Babylon: máscara, pack de pelo, fallback, renderer
├── features/
│   ├── auth/       # login/registro (tema pirata) + guard de sesión
│   └── pregame/    # PreGamePage + componentes del editor + hooks
├── ui/             # componentes base (Button, Panel, Spinner)
└── styles/         # globals.css + login.css (tema pirata)
```

La pantalla de login/registro admite una imagen de fondo opcional en
`public/login-bg.jpg` (ver `public/README.txt`); si falta, usa un fondo CSS.

## Criterios de aceptación cubiertos

CA-01 carga inicial · CA-02 preview inmediato · CA-03 guardado explícito ·
CA-04 persistencia entre sesiones · CA-05 pelo inválido (validación cliente) ·
CA-06 color inválido (Zod `#RRGGBB`) · CA-07 fallback visual + log si falta un asset.
