-- ============================================================
-- 003_create_asset_core.sql
-- Sistema de assets dinámicos: tablas nucleares
-- asset_files, asset_manifests, manifest_files
-- Según "definicion_sistema_assets_multijugador_manifest.docx"
-- y "definicion_herramienta_asset_editor.docx" (is_current)
-- ============================================================

CREATE TABLE IF NOT EXISTS asset_files (
    asset_file_id   VARCHAR(120) PRIMARY KEY,
    asset_key       VARCHAR(200) NOT NULL,
    asset_type      VARCHAR(30)  NOT NULL
        CHECK (asset_type IN ('map', 'texture', 'model', 'audio', 'shader',
                              'material', 'sprite', 'animation', 'data')),
    version         VARCHAR(40)  NOT NULL,
    file_path       TEXT         NOT NULL,
    download_url    TEXT         NOT NULL,
    hash            TEXT         NOT NULL,
    size_bytes      BIGINT       NOT NULL CHECK (size_bytes >= 0),
    mime_type       VARCHAR(120) NOT NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'published', 'deprecated', 'deleted')),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    published_at    TIMESTAMPTZ,
    UNIQUE (asset_key, version)
);

CREATE TABLE IF NOT EXISTS asset_manifests (
    manifest_id     VARCHAR(120) PRIMARY KEY,
    name            VARCHAR(160) NOT NULL,
    version         VARCHAR(40)  NOT NULL,
    target_type     VARCHAR(40)  NOT NULL
        CHECK (target_type IN ('global', 'scenario', 'scenario_set',
                               'game_mode', 'event')),
    target_id       VARCHAR(120) NOT NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'published', 'deprecated')),
    is_current      BOOLEAN      NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    published_at    TIMESTAMPTZ,
    UNIQUE (target_type, target_id, version)
);

CREATE TABLE IF NOT EXISTS manifest_files (
    manifest_id     VARCHAR(120) NOT NULL
        REFERENCES asset_manifests(manifest_id) ON DELETE CASCADE,
    asset_file_id   VARCHAR(120) NOT NULL
        REFERENCES asset_files(asset_file_id) ON DELETE RESTRICT,
    required        BOOLEAN      NOT NULL DEFAULT true,
    load_priority   INTEGER      NOT NULL DEFAULT 100,
    usage           VARCHAR(80),
    PRIMARY KEY (manifest_id, asset_file_id)
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_asset_files_status
    ON asset_files(status);

CREATE INDEX IF NOT EXISTS idx_asset_files_asset_key
    ON asset_files(asset_key);

CREATE INDEX IF NOT EXISTS idx_asset_manifests_target
    ON asset_manifests(target_type, target_id, status);

CREATE INDEX IF NOT EXISTS idx_manifest_files_priority
    ON manifest_files(manifest_id, load_priority);

-- Solo puede existir un manifest vigente y publicado por target
CREATE UNIQUE INDEX IF NOT EXISTS asset_manifests_one_current_per_target
    ON asset_manifests (target_type, target_id)
    WHERE is_current = true AND status = 'published';
