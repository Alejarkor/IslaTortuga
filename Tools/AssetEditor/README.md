# IslaT · AssetEditor

Herramienta local para gestionar el **sistema de assets dinámicos** (manifests JSON + archivos versionados), según los documentos `definicion_sistema_assets_multijugador_manifest.docx` y `definicion_herramienta_asset_editor.docx`.

Actúa como puente entre la carpeta física `Server/GameAssets/server_assets` y PostgreSQL (vía GameApi interna). **No sube binarios**: gestiona archivos ya presentes en disco.

## Qué hace

- **Explorador**: escanea recursivamente `server_assets`, calcula SHA-256, tamaño, MIME, `file_path` y `download_url`; infiere `asset_type` (por carpeta/extensión) y `version` (patrón `_v001`). Clasifica cada archivo: **nuevo / registrado / cambiado**, con avisos de inconsistencia (sidecar vs DB, publicado con hash distinto, en DB pero no en disco…).
- **Metadatos**: edición de `asset_key`, `asset_type`, `version`, `status` y escritura de sidecars `.asset.json` versionables junto a cada asset.
- **Manifests**: crear/editar manifests, vincular/desvincular archivos con `required`, `load_priority` y `usage`, marcar manifest **current** (valida que todos sus archivos estén publicados) y previsualizar el manifest público.
- **Sincronización**: plan de operaciones pendientes con vista previa, aplicación selectiva y dry-run contra `sync-report` de GameApi.
- **Registro**: log persistente de todas las operaciones.

## Validaciones implementadas

- No se registra un archivo cuyo físico no exista.
- No se sobrescribe un asset **publicado** con contenido distinto manteniendo versión: exige nueva versión.
- Un manifest no puede marcarse current si incluye archivos sin publicar.
- Protección contra path traversal (`../`) fuera de `server_assets`.
- Avisos cuando sidecar y DB divergen, o cuando un archivo está en DB pero no en disco (o viceversa).

## Requisitos

- Node 18+ (usa `fetch` nativo).
- GameApi corriendo con `ASSET_ADMIN_TOKEN` configurado.
- Tablas de assets creadas: migración `Server/Game_Database/migrations/003_create_asset_core.sql`.

## Uso

```bash
cd Tools/AssetEditor
cp .env.example .env   # ajustar si hace falta
npm install
npm run dev
```

Abrir `http://localhost:4100`.

## Configuración (.env)

| Variable | Default | Descripción |
|---|---|---|
| `PORT` | `4100` | Puerto local de la herramienta |
| `ASSETS_ROOT` | `../../Server/GameAssets/server_assets` | Carpeta física de assets |
| `GAME_API_URL` | `http://localhost:3001` | GameApi interna |
| `ASSET_ADMIN_TOKEN` | — | Token para `x-admin-token` |

## Arquitectura

```
Tools/AssetEditor/
├── src/
│   ├── index.ts              Bootstrap Express (API local + estáticos)
│   ├── config.ts             Configuración (.env)
│   ├── types.ts              Tipos compartidos
│   ├── core/
│   │   ├── scanner.ts        Escaneo recursivo de server_assets
│   │   ├── hasher.ts         SHA-256 en streaming
│   │   ├── mime.ts           MIME por extensión
│   │   ├── inference.ts      Inferencia de asset_type, version, asset_key, asset_file_id
│   │   ├── sidecar.ts        Lectura/escritura de .asset.json
│   │   └── paths.ts          Validación anti path-traversal
│   ├── services/
│   │   ├── gameApiClient.ts  Cliente HTTP de endpoints internos admin
│   │   ├── scanService.ts    Disco + sidecar + DB → clasificación de estados
│   │   └── syncService.ts    Operaciones de sincronización con validaciones
│   └── routes/
│       └── apiRoutes.ts      API REST local consumida por la SPA
└── public/                   SPA sin build (ES modules)
    ├── index.html
    ├── css/styles.css
    └── js/  (api, state, ui, views/{browser,manifests,sync,log})
```

## Flujo de trabajo típico

1. Copia el archivo versionado a `server_assets/<carpeta>/nombre_v001.ext`.
2. Pulsa **Escanear** → aparece como *Nuevo*.
3. Revisa/ajusta `asset_key`, tipo, versión → **Registrar en DB** (crea sidecar y fila en `asset_files` como draft).
4. **Publicar** el archivo cuando esté probado.
5. En **Manifests**, vincúlalo al manifest del contexto y márcalo **current**.
6. El cliente lo recibirá en `GET /assets/manifest?targetType=...&targetId=...`.
