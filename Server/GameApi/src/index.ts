import express from "express";
import bcrypt from "bcryptjs";
import { Pool } from "pg";

const app = express();
app.use(express.json());

const port = Number(process.env.PORT ?? 3001);

const pool = new Pool({
  host: process.env.POSTGRES_HOST ?? "localhost",
  port: Number(process.env.POSTGRES_PORT ?? 5432),
  database: process.env.POSTGRES_DB,
  user: process.env.POSTGRES_USER,
  password: process.env.POSTGRES_PASSWORD
});

type RegisterBody = {
  username?: string;
  email?: string;
  password?: string;
  nickname?: string;  
};

type LoginBody = {
  usernameOrEmail?: string;
  password?: string;
};

app.get("/internal/health", async (_req, res) => {
  try {
    await pool.query("SELECT 1");
    res.json({
      ok: true,
      service: "game-api",
      database: "connected"
    });
  } catch (error) {
    res.status(500).json({
      ok: false,
      service: "game-api",
      database: "error"
    });
  }
});

app.post("/internal/auth/register", async (req, res) => {
  const body = req.body as RegisterBody;

  if (!body.username || !body.email || !body.password || !body.nickname) {
    return res.status(400).json({
      ok: false,
      error: "username, email, password and nickname are required"
    });
  }

  const client = await pool.connect();

  try {
    await client.query("BEGIN");

    const passwordHash = await bcrypt.hash(body.password, 12);

    const userResult = await client.query(
      `
      INSERT INTO users (username, email, password_hash)
      VALUES ($1, $2, $3)
      RETURNING user_id, username, email, status, created_at, last_login_at
      `,
      [body.username, body.email, passwordHash]
    );

    const user = userResult.rows[0];

    const profileResult = await client.query(
        `
        INSERT INTO player_profiles (user_id, nickname)
        VALUES ($1, $2)
        RETURNING player_id, user_id, nickname, appearance_json, created_at
        `,
        [user.user_id, body.nickname]
    );

    const profile = profileResult.rows[0];

    const statsResult = await client.query(
      `
      INSERT INTO player_stats (player_id)
      VALUES ($1)
      RETURNING player_id, games_played, games_won, games_lost, total_play_time_seconds, stats_json, updated_at
      `,
      [profile.player_id]
    );

    const stats = statsResult.rows[0];

    await client.query("COMMIT");

    return res.status(201).json({
      ok: true,
      user,
      profile,
      stats
    });
  } catch (error: any) {
    await client.query("ROLLBACK");

    if (error?.code === "23505") {
      return res.status(409).json({
        ok: false,
        error: "username, email or nickname already exists"
      });
    }

    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  } finally {
    client.release();
  }
});

app.post("/internal/auth/login", async (req, res) => {
  const body = req.body as LoginBody;

  if (!body.usernameOrEmail || !body.password) {
    return res.status(400).json({
      ok: false,
      error: "usernameOrEmail and password are required"
    });
  }

  try {
    const result = await pool.query(
      `
      SELECT
        u.user_id,
        u.username,
        u.email,
        u.password_hash,
        u.status,
        p.player_id,
        p.nickname,
        p.appearance_json
      FROM users u
      JOIN player_profiles p ON p.user_id = u.user_id
      WHERE u.username = $1 OR u.email = $1
      LIMIT 1
      `,
      [body.usernameOrEmail]
    );

    if (result.rowCount === 0) {
      return res.status(401).json({
        ok: false,
        error: "invalid credentials"
      });
    }

    const row = result.rows[0];

    if (row.status !== "active") {
      return res.status(403).json({
        ok: false,
        error: "user is not active"
      });
    }

    const passwordOk = await bcrypt.compare(body.password, row.password_hash);

    if (!passwordOk) {
      return res.status(401).json({
        ok: false,
        error: "invalid credentials"
      });
    }

    await pool.query(
      `
      UPDATE users
      SET last_login_at = now(), updated_at = now()
      WHERE user_id = $1
      `,
      [row.user_id]
    );

    return res.json({
      ok: true,
      user: {
        user_id: row.user_id,
        username: row.username,
        email: row.email,
        status: row.status
      },
      profile: {
        player_id: row.player_id,
        user_id: row.user_id,
        nickname: row.nickname,
        appearance_json: row.appearance_json
      }
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get("/internal/users/:userId", async (req, res) => {
  const result = await pool.query(
    `
    SELECT user_id, username, email, status, created_at, updated_at, last_login_at
    FROM users
    WHERE user_id = $1
    `,
    [req.params.userId]
  );

  if (result.rowCount === 0) {
    return res.status(404).json({
      ok: false,
      error: "user not found"
    });
  }

  return res.json({
    ok: true,
    user: result.rows[0]
  });
});

app.get("/internal/profiles/:playerId", async (req, res) => {
  const result = await pool.query(
    `
    SELECT player_id, user_id, nickname, appearance_json, created_at, updated_at
    FROM player_profiles
    WHERE player_id = $1
    `,
    [req.params.playerId]
  );

  if (result.rowCount === 0) {
    return res.status(404).json({
      ok: false,
      error: "profile not found"
    });
  }

  return res.json({
    ok: true,
    profile: result.rows[0]
  });
});

app.get("/internal/stats/:playerId", async (req, res) => {
  const result = await pool.query(
    `
    SELECT player_id, games_played, games_won, games_lost, total_play_time_seconds, stats_json, updated_at
    FROM player_stats
    WHERE player_id = $1
    `,
    [req.params.playerId]
  );

  if (result.rowCount === 0) {
    return res.status(404).json({
      ok: false,
      error: "stats not found"
    });
  }

  return res.json({
    ok: true,
    stats: result.rows[0]
  });
});

app.patch("/internal/profiles/:playerId/appearance", async (req, res) => {
  const appearance = req.body?.appearance;

  if (
    !appearance ||
    typeof appearance !== "object" ||
    Array.isArray(appearance)
  ) {
    return res.status(400).json({
      ok: false,
      error: "appearance must be an object"
    });
  }

  try {
    const result = await pool.query(
      `
      UPDATE player_profiles
      SET appearance_json = $1::jsonb,
          updated_at = now()
      WHERE player_id = $2
      RETURNING player_id, user_id, nickname, appearance_json, created_at, updated_at
      `,
      [JSON.stringify(appearance), req.params.playerId]
    );

    if (result.rowCount === 0) {
      return res.status(404).json({
        ok: false,
        error: "profile not found"
      });
    }

    return res.json({
      ok: true,
      profile: result.rows[0]
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.listen(port, () => {
  console.log(`GameApi listening on port ${port}`);
});