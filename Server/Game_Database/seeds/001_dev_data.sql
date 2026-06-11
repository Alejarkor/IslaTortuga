-- ============================================================
-- Datos de desarrollo para IslaT
-- Ejecutar manualmente después de tener creadas las tablas.
-- ============================================================

-- Limpieza opcional de datos de prueba.
-- OJO: esto borra solo datos, no tablas.
TRUNCATE TABLE
    friend_relations,
    friend_requests,
    player_stats,
    player_profiles,
    users
RESTART IDENTITY CASCADE;


-- ============================================================
-- Usuarios de prueba
-- password_hash es falso. En producción debe ser bcrypt/argon2.
-- ============================================================

INSERT INTO users (
    username,
    email,
    password_hash,
    status
)
VALUES
(
    'alejarkor',
    'alejarkor@test.com',
    'fake_hash_dev_alejarkor',
    'active'
),
(
    'player_ana',
    'ana@test.com',
    'fake_hash_dev_ana',
    'active'
),
(
    'player_mario',
    'mario@test.com',
    'fake_hash_dev_mario',
    'active'
);


-- ============================================================
-- Perfiles de jugador
-- ============================================================

INSERT INTO player_profiles (
    user_id,
    nickname,
    appearance_json
)
SELECT
    user_id,
    'Alejarkor',
    '{
        "body": "default",
        "hair": "black",
        "shirt": "blue",
        "accessory": "none"
    }'::jsonb
FROM users
WHERE username = 'alejarkor';


INSERT INTO player_profiles (
    user_id,
    nickname,
    appearance_json
)
SELECT
    user_id,
    'Ana',
    '{
        "body": "default",
        "hair": "brown",
        "shirt": "green",
        "accessory": "hat"
    }'::jsonb
FROM users
WHERE username = 'player_ana';


INSERT INTO player_profiles (
    user_id,
    nickname,
    appearance_json
)
SELECT
    user_id,
    'Mario',
    '{
        "body": "default",
        "hair": "blonde",
        "shirt": "red",
        "accessory": "none"
    }'::jsonb
FROM users
WHERE username = 'player_mario';


-- ============================================================
-- Estadísticas iniciales
-- ============================================================

INSERT INTO player_stats (
    player_id,
    games_played,
    games_won,
    games_lost,
    total_play_time_seconds,
    stats_json
)
SELECT
    player_id,
    0,
    0,
    0,
    0,
    '{
        "itemsCollected": 0,
        "distanceWalked": 0,
        "timesEscaped": 0,
        "timesSabotaged": 0
    }'::jsonb
FROM player_profiles;


-- ============================================================
-- Solicitud de amistad de prueba
-- Alejarkor envía solicitud a Mario
-- ============================================================

INSERT INTO friend_requests (
    from_player_id,
    to_player_id,
    status
)
SELECT
    from_player.player_id,
    to_player.player_id,
    'pending'
FROM player_profiles AS from_player
CROSS JOIN player_profiles AS to_player
WHERE from_player.nickname = 'Alejarkor'
  AND to_player.nickname = 'Mario';


-- ============================================================
-- Amistad aceptada de prueba
-- Alejarkor y Ana son amigos
-- ============================================================

INSERT INTO friend_relations (
    player_a_id,
    player_b_id
)
SELECT
    player_a.player_id,
    player_b.player_id
FROM player_profiles AS player_a
CROSS JOIN player_profiles AS player_b
WHERE player_a.nickname = 'Alejarkor'
  AND player_b.nickname = 'Ana';