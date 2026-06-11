-- ============================================================
-- Init básico de PostgreSQL para IslaT
-- Se ejecuta automáticamente SOLO la primera vez que se crea
-- el volumen de PostgreSQL.
-- ============================================================

-- Permite usar gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS pgcrypto;