import "dotenv/config";
import express, { Request, Response, NextFunction } from "express";
import bcrypt from "bcryptjs";
import { Pool } from "pg";
import { getRedis } from "./redis";
import { HttpGameServerControlClient } from "./gameserver/controlClient";
import { buildRoomServices, createRoomsRouter } from "./rooms/routes";

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

const assetAdminToken = process.env.ASSET_ADMIN_TOKEN ?? "";

function requireAssetAdmin(
  req: Request,
  res: Response,
  next: NextFunction
) {
  const token = req.header("x-admin-token");

  if (!assetAdminToken || token !== assetAdminToken) {
    return res.status(401).json({
      ok: false,
      error: "invalid admin token"
    });
  }

  next();
}

function parseLimitOffset(req: Request) {
  const limit = Math.min(Number(req.query.limit ?? 100), 500);
  const offset = Math.max(Number(req.query.offset ?? 0), 0);

  return {
    limit: Number.isFinite(limit) ? limit : 100,
    offset: Number.isFinite(offset) ? offset : 0
  };
}

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

type CreateFriendRequestBody = {
  fromPlayerId?: string;
  toPlayerId?: string;
  nickname?: string;
};

type FriendRequestActionBody = {
  playerId?: string;
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

app.get("/internal/friends/:playerId", async (req, res) => {
  try {
    const result = await pool.query(
      `
      SELECT
        p.player_id,
        p.nickname,
        p.appearance_json,
        fr.created_at AS friends_since
      FROM friend_relations fr
      JOIN player_profiles p
        ON p.player_id = CASE
          WHEN fr.player_a_id = $1 THEN fr.player_b_id
          ELSE fr.player_a_id
        END
      WHERE fr.player_a_id = $1 OR fr.player_b_id = $1
      ORDER BY p.nickname ASC
      `,
      [req.params.playerId]
    );

    return res.json({
      ok: true,
      friends: result.rows
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get("/internal/friend-requests/:playerId/incoming", async (req, res) => {
  try {
    const result = await pool.query(
      `
      SELECT
        fr.friend_request_id,
        fr.status,
        fr.created_at,
        from_profile.player_id AS from_player_id,
        from_profile.nickname AS from_nickname,
        from_profile.appearance_json AS from_appearance_json
      FROM friend_requests fr
      JOIN player_profiles from_profile
        ON from_profile.player_id = fr.from_player_id
      WHERE fr.to_player_id = $1
        AND fr.status = 'pending'
      ORDER BY fr.created_at DESC
      `,
      [req.params.playerId]
    );

    return res.json({
      ok: true,
      incomingRequests: result.rows
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get("/internal/friend-requests/:playerId/outgoing", async (req, res) => {
  try {
    const result = await pool.query(
      `
      SELECT
        fr.friend_request_id,
        fr.status,
        fr.created_at,
        to_profile.player_id AS to_player_id,
        to_profile.nickname AS to_nickname,
        to_profile.appearance_json AS to_appearance_json
      FROM friend_requests fr
      JOIN player_profiles to_profile
        ON to_profile.player_id = fr.to_player_id
      WHERE fr.from_player_id = $1
        AND fr.status = 'pending'
      ORDER BY fr.created_at DESC
      `,
      [req.params.playerId]
    );

    return res.json({
      ok: true,
      outgoingRequests: result.rows
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.post("/internal/friend-requests", async (req, res) => {
  const body = req.body as CreateFriendRequestBody;

  const fromPlayerId = body.fromPlayerId;
  let toPlayerId = body.toPlayerId;

  if (!fromPlayerId) {
    return res.status(400).json({
      ok: false,
      error: "fromPlayerId is required"
    });
  }

  try {
    if (!toPlayerId && body.nickname) {
      const playerResult = await pool.query(
        `
        SELECT player_id
        FROM player_profiles
        WHERE lower(nickname) = lower($1)
        LIMIT 1
        `,
        [body.nickname]
      );

      if (playerResult.rowCount === 0) {
        return res.status(404).json({
          ok: false,
          error: "target player not found"
        });
      }

      toPlayerId = playerResult.rows[0].player_id;
    }

    if (!toPlayerId) {
      return res.status(400).json({
        ok: false,
        error: "toPlayerId or nickname is required"
      });
    }

    if (fromPlayerId === toPlayerId) {
      return res.status(400).json({
        ok: false,
        error: "cannot send friend request to yourself"
      });
    }

    const existingRelation = await pool.query(
      `
      SELECT 1
      FROM friend_relations
      WHERE
        (player_a_id = $1::uuid AND player_b_id = $2::uuid)
        OR
        (player_a_id = $2::uuid AND player_b_id = $1::uuid)
      LIMIT 1
      `,
      [fromPlayerId, toPlayerId]
    );

    if (existingRelation.rowCount && existingRelation.rowCount > 0) {
      return res.status(409).json({
        ok: false,
        error: "players are already friends"
      });
    }

    const existingRequest = await pool.query(
      `
      SELECT friend_request_id, from_player_id, to_player_id, status, created_at
      FROM friend_requests
      WHERE status = 'pending'
        AND (
          (from_player_id = $1::uuid AND to_player_id = $2::uuid)
          OR
          (from_player_id = $2::uuid AND to_player_id = $1::uuid)
        )
      LIMIT 1
      `,
      [fromPlayerId, toPlayerId]
    );

    if (existingRequest.rowCount && existingRequest.rowCount > 0) {
      return res.status(409).json({
        ok: false,
        error: "there is already a pending friend request",
        friendRequest: existingRequest.rows[0]
      });
    }

    const insertResult = await pool.query(
      `
      INSERT INTO friend_requests (
        from_player_id,
        to_player_id,
        status
      )
      VALUES ($1, $2, 'pending')
      RETURNING friend_request_id, from_player_id, to_player_id, status, created_at, resolved_at
      `,
      [fromPlayerId, toPlayerId]
    );

    return res.status(201).json({
      ok: true,
      friendRequest: insertResult.rows[0]
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.post("/internal/friend-requests/:requestId/accept", async (req, res) => {
  const body = req.body as FriendRequestActionBody;
  const playerId = body.playerId;

  if (!playerId) {
    return res.status(400).json({
      ok: false,
      error: "playerId is required"
    });
  }

  const client = await pool.connect();

  try {
    await client.query("BEGIN");

    const requestResult = await client.query(
      `
      SELECT friend_request_id, from_player_id, to_player_id, status
      FROM friend_requests
      WHERE friend_request_id = $1
      FOR UPDATE
      `,
      [req.params.requestId]
    );

    if (requestResult.rowCount === 0) {
      await client.query("ROLLBACK");

      return res.status(404).json({
        ok: false,
        error: "friend request not found"
      });
    }

    const friendRequest = requestResult.rows[0];

    if (friendRequest.to_player_id !== playerId) {
      await client.query("ROLLBACK");

      return res.status(403).json({
        ok: false,
        error: "only the target player can accept this request"
      });
    }

    if (friendRequest.status !== "pending") {
      await client.query("ROLLBACK");

      return res.status(409).json({
        ok: false,
        error: "friend request is not pending"
      });
    }

    await client.query(
      `
      INSERT INTO friend_relations (
        player_a_id,
        player_b_id
      )
      SELECT
        LEAST($1::uuid, $2::uuid),
        GREATEST($1::uuid, $2::uuid)
      WHERE NOT EXISTS (
        SELECT 1
        FROM friend_relations
        WHERE
          (player_a_id = $1::uuid AND player_b_id = $2::uuid)
          OR
          (player_a_id = $2::uuid AND player_b_id = $1::uuid)
      )
      `,
      [friendRequest.from_player_id, friendRequest.to_player_id]
    );

    const updateResult = await client.query(
      `
      UPDATE friend_requests
      SET status = 'accepted',
          resolved_at = now()
      WHERE friend_request_id = $1
      RETURNING friend_request_id, from_player_id, to_player_id, status, created_at, resolved_at
      `,
      [req.params.requestId]
    );

    await client.query("COMMIT");

    return res.json({
      ok: true,
      friendRequest: updateResult.rows[0]
    });
  } catch (error) {
    await client.query("ROLLBACK");

    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  } finally {
    client.release();
  }
});

app.post("/internal/friend-requests/:requestId/reject", async (req, res) => {
  const body = req.body as FriendRequestActionBody;
  const playerId = body.playerId;

  if (!playerId) {
    return res.status(400).json({
      ok: false,
      error: "playerId is required"
    });
  }

  try {
    const result = await pool.query(
      `
      UPDATE friend_requests
      SET status = 'rejected',
          resolved_at = now()
      WHERE friend_request_id = $1
        AND to_player_id = $2
        AND status = 'pending'
      RETURNING friend_request_id, from_player_id, to_player_id, status, created_at, resolved_at
      `,
      [req.params.requestId, playerId]
    );

    if (result.rowCount === 0) {
      return res.status(404).json({
        ok: false,
        error: "friend request not found or not pending"
      });
    }

    return res.json({
      ok: true,
      friendRequest: result.rows[0]
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.post("/internal/friend-requests/:requestId/cancel", async (req, res) => {
  const body = req.body as FriendRequestActionBody;
  const playerId = body.playerId;

  if (!playerId) {
    return res.status(400).json({
      ok: false,
      error: "playerId is required"
    });
  }

  try {
    const result = await pool.query(
      `
      UPDATE friend_requests
      SET status = 'cancelled',
          resolved_at = now()
      WHERE friend_request_id = $1
        AND from_player_id = $2
        AND status = 'pending'
      RETURNING friend_request_id, from_player_id, to_player_id, status, created_at, resolved_at
      `,
      [req.params.requestId, playerId]
    );

    if (result.rowCount === 0) {
      return res.status(404).json({
        ok: false,
        error: "friend request not found or not pending"
      });
    }

    return res.json({
      ok: true,
      friendRequest: result.rows[0]
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

//-----------------ZONA DE API DE ASSETS------------------------------------

//Endpoints privados 

app.get("/internal/assets/manifest", async (req, res) => {
  const targetType = req.query.targetType;
  const targetId = req.query.targetId;
  const version = req.query.version;

  if (!targetType || !targetId) {
    return res.status(400).json({
      ok: false,
      error: "targetType and targetId are required"
    });
  }

  try {
    const manifestResult = await pool.query(
      `
      SELECT
        manifest_id,
        name,
        version,
        target_type,
        target_id,
        status,
        is_current,
        created_at,
        published_at
      FROM asset_manifests
      WHERE target_type = $1
        AND target_id = $2
        AND status = 'published'
        AND (
          ($3::text IS NULL AND is_current = true)
          OR
          ($3::text IS NOT NULL AND version = $3)
        )
      LIMIT 1
      `,
      [targetType, targetId, version ?? null]
    );

    if (manifestResult.rowCount === 0) {
      return res.status(404).json({
        ok: false,
        error: "manifest not found"
      });
    }

    const manifest = manifestResult.rows[0];

    const filesResult = await pool.query(
      `
      SELECT
        af.asset_file_id,
        af.asset_key,
        af.asset_type,
        af.version,
        af.hash,
        af.size_bytes,
        af.mime_type,
        af.download_url,
        mf.required,
        mf.load_priority,
        mf.usage
      FROM manifest_files mf
      JOIN asset_files af ON af.asset_file_id = mf.asset_file_id
      WHERE mf.manifest_id = $1
        AND af.status = 'published'
      ORDER BY mf.load_priority ASC, af.asset_key ASC
      `,
      [manifest.manifest_id]
    );

    return res.json({
      ok: true,
      manifestId: manifest.manifest_id,
      name: manifest.name,
      version: manifest.version,
      targetType: manifest.target_type,
      targetId: manifest.target_id,
      isCurrent: manifest.is_current,
      publishedAt: manifest.published_at,
      files: filesResult.rows.map((row) => ({
        assetFileId: row.asset_file_id,
        assetKey: row.asset_key,
        assetType: row.asset_type,
        version: row.version,
        hash: row.hash,
        sizeBytes: Number(row.size_bytes),
        mimeType: row.mime_type,
        downloadUrl: row.download_url,
        required: row.required,
        loadPriority: row.load_priority,
        usage: row.usage
      }))
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get("/internal/assets/file/:assetFileId/meta", async (req, res) => {
  try {
    const result = await pool.query(
      `
      SELECT
        asset_file_id,
        asset_key,
        asset_type,
        version,
        file_path,
        download_url,
        hash,
        size_bytes,
        mime_type,
        status,
        created_at,
        published_at
      FROM asset_files
      WHERE asset_file_id = $1
        AND status = 'published'
      `,
      [req.params.assetFileId]
    );

    if (result.rowCount === 0) {
      return res.status(404).json({
        ok: false,
        error: "asset file not found"
      });
    }

    return res.json({
      ok: true,
      assetFile: result.rows[0]
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get("/internal/admin/assets/files", requireAssetAdmin, async (req, res) => {
  const { limit, offset } = parseLimitOffset(req);

  const status = req.query.status ? String(req.query.status) : null;
  const assetType = req.query.assetType ? String(req.query.assetType) : null;
  const q = req.query.q ? String(req.query.q) : null;

  try {
    const result = await pool.query(
      `
      SELECT
        asset_file_id,
        asset_key,
        asset_type,
        version,
        file_path,
        download_url,
        hash,
        size_bytes,
        mime_type,
        status,
        created_at,
        published_at
      FROM asset_files
      WHERE ($1::text IS NULL OR status = $1)
        AND ($2::text IS NULL OR asset_type = $2)
        AND (
          $3::text IS NULL
          OR asset_file_id ILIKE '%' || $3 || '%'
          OR asset_key ILIKE '%' || $3 || '%'
          OR file_path ILIKE '%' || $3 || '%'
        )
      ORDER BY created_at DESC, asset_key ASC
      LIMIT $4 OFFSET $5
      `,
      [status, assetType, q, limit, offset]
    );

    return res.json({
      ok: true,
      files: result.rows
    });
  } catch (error) {
    console.error(error);

    return res.status(500).json({
      ok: false,
      error: "internal server error"
    });
  }
});

app.get(
  "/internal/admin/assets/files/:assetFileId",
  requireAssetAdmin,
  async (req, res) => {
    try {
      const result = await pool.query(
        `
        SELECT *
        FROM asset_files
        WHERE asset_file_id = $1
        `,
        [req.params.assetFileId]
      );

      if (result.rowCount === 0) {
        return res.status(404).json({
          ok: false,
          error: "asset file not found"
        });
      }

      return res.json({
        ok: true,
        assetFile: result.rows[0]
      });
    } catch (error) {
      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);


app.patch(
  "/internal/admin/assets/files/:assetFileId/status",
  requireAssetAdmin,
  async (req, res) => {
    const status = req.body?.status;

    if (!status) {
      return res.status(400).json({
        ok: false,
        error: "status is required"
      });
    }

    try {
      const result = await pool.query(
        `
        UPDATE asset_files
        SET status = $1::varchar,
            published_at = CASE
              WHEN $1::varchar = 'published'::varchar THEN COALESCE(published_at, now())
              ELSE published_at
            END
        WHERE asset_file_id = $2::varchar
        RETURNING *
        `,
        [status, req.params.assetFileId]
      );

      if (result.rowCount === 0) {
        return res.status(404).json({
          ok: false,
          error: "asset file not found"
        });
      }

      return res.json({
        ok: true,
        assetFile: result.rows[0]
      });
    } catch (error: any) {
      if (error?.code === "23514") {
        return res.status(400).json({
          ok: false,
          error: "invalid status"
        });
      }

      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.get(
  "/internal/admin/assets/manifests",
  requireAssetAdmin,
  async (req, res) => {
    const { limit, offset } = parseLimitOffset(req);

    const status = req.query.status ? String(req.query.status) : null;
    const targetType = req.query.targetType ? String(req.query.targetType) : null;
    const targetId = req.query.targetId ? String(req.query.targetId) : null;

    try {
      const result = await pool.query(
        `
        SELECT
          am.manifest_id,
          am.name,
          am.version,
          am.target_type,
          am.target_id,
          am.status,
          am.is_current,
          am.created_at,
          am.published_at,
          COUNT(mf.asset_file_id)::int AS file_count
        FROM asset_manifests am
        LEFT JOIN manifest_files mf ON mf.manifest_id = am.manifest_id
        WHERE ($1::text IS NULL OR am.status = $1)
          AND ($2::text IS NULL OR am.target_type = $2)
          AND ($3::text IS NULL OR am.target_id = $3)
        GROUP BY am.manifest_id
        ORDER BY am.created_at DESC
        LIMIT $4 OFFSET $5
        `,
        [status, targetType, targetId, limit, offset]
      );

      return res.json({
        ok: true,
        manifests: result.rows
      });
    } catch (error) {
      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.get(
  "/internal/admin/assets/manifests/:manifestId",
  requireAssetAdmin,
  async (req, res) => {
    try {
      const manifestResult = await pool.query(
        `
        SELECT *
        FROM asset_manifests
        WHERE manifest_id = $1
        `,
        [req.params.manifestId]
      );

      if (manifestResult.rowCount === 0) {
        return res.status(404).json({
          ok: false,
          error: "manifest not found"
        });
      }

      const filesResult = await pool.query(
        `
        SELECT
          mf.manifest_id,
          mf.asset_file_id,
          mf.required,
          mf.load_priority,
          mf.usage,
          af.asset_key,
          af.asset_type,
          af.version,
          af.file_path,
          af.download_url,
          af.hash,
          af.size_bytes,
          af.mime_type,
          af.status
        FROM manifest_files mf
        JOIN asset_files af ON af.asset_file_id = mf.asset_file_id
        WHERE mf.manifest_id = $1
        ORDER BY mf.load_priority ASC, af.asset_key ASC
        `,
        [req.params.manifestId]
      );

      return res.json({
        ok: true,
        manifest: manifestResult.rows[0],
        files: filesResult.rows
      });
    } catch (error) {
      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.put(
  "/internal/admin/assets/manifests/:manifestId",
  requireAssetAdmin,
  async (req, res) => {
    const body = req.body;

    if (!body.name || !body.version || !body.targetType || !body.targetId) {
      return res.status(400).json({
        ok: false,
        error: "name, version, targetType and targetId are required"
      });
    }

    const status = body.status ?? "draft";
    const publishedAt = status === "published" ? new Date() : null;

    try {
      const result = await pool.query(
        `
        INSERT INTO asset_manifests (
          manifest_id,
          name,
          version,
          target_type,
          target_id,
          status,
          is_current,
          published_at
        )
        VALUES (
          $1::varchar,
          $2::varchar,
          $3::varchar,
          $4::varchar,
          $5::varchar,
          $6::varchar,
          false,
          $7::timestamptz
        )
        ON CONFLICT (manifest_id)
        DO UPDATE SET
          name = EXCLUDED.name,
          version = EXCLUDED.version,
          target_type = EXCLUDED.target_type,
          target_id = EXCLUDED.target_id,
          status = EXCLUDED.status,
          published_at = CASE
            WHEN EXCLUDED.status = 'published'::varchar
              THEN COALESCE(asset_manifests.published_at, EXCLUDED.published_at, now())
            ELSE asset_manifests.published_at
          END
        RETURNING *
        `,
        [
          req.params.manifestId,
          body.name,
          body.version,
          body.targetType,
          body.targetId,
          status,
          publishedAt
        ]
      );

      return res.json({
        ok: true,
        manifest: result.rows[0]
      });
    } catch (error: any) {
      console.error(error);

      if (error?.code === "23514") {
        return res.status(400).json({
          ok: false,
          error: "invalid manifest status or target type"
        });
      }

      if (error?.code === "23505") {
        return res.status(409).json({
          ok: false,
          error: "manifest already exists or duplicated target/version"
        });
      }

      return res.status(500).json({
        ok: false,
        error: "internal server error",
        details: error?.message,
        code: error?.code
      });
    }
  }
);

app.put(
  "/internal/admin/assets/files/:assetFileId",
  requireAssetAdmin,
  async (req, res) => {
    const body = req.body;

    const requiredFields = [
      "assetKey",
      "assetType",
      "version",
      "filePath",
      "downloadUrl",
      "hash",
      "sizeBytes",
      "mimeType",
      "status"
    ];

    for (const field of requiredFields) {
      if (
        body[field] === undefined ||
        body[field] === null ||
        body[field] === ""
      ) {
        return res.status(400).json({
          ok: false,
          error: `${field} is required`
        });
      }
    }

    const publishedAt = body.status === "published" ? new Date() : null;

    try {
      const result = await pool.query(
        `
        INSERT INTO asset_files (
          asset_file_id,
          asset_key,
          asset_type,
          version,
          file_path,
          download_url,
          hash,
          size_bytes,
          mime_type,
          status,
          published_at
        )
        VALUES (
          $1::varchar,
          $2::varchar,
          $3::varchar,
          $4::varchar,
          $5::text,
          $6::text,
          $7::text,
          $8::bigint,
          $9::varchar,
          $10::varchar,
          $11::timestamptz
        )
        ON CONFLICT (asset_file_id)
        DO UPDATE SET
          asset_key = EXCLUDED.asset_key,
          asset_type = EXCLUDED.asset_type,
          version = EXCLUDED.version,
          file_path = EXCLUDED.file_path,
          download_url = EXCLUDED.download_url,
          hash = EXCLUDED.hash,
          size_bytes = EXCLUDED.size_bytes,
          mime_type = EXCLUDED.mime_type,
          status = EXCLUDED.status,
          published_at = CASE
            WHEN EXCLUDED.status = 'published'::varchar
              THEN COALESCE(asset_files.published_at, EXCLUDED.published_at, now())
            ELSE asset_files.published_at
          END
        RETURNING *
        `,
        [
          req.params.assetFileId,
          body.assetKey,
          body.assetType,
          body.version,
          body.filePath,
          body.downloadUrl,
          body.hash,
          Number(body.sizeBytes),
          body.mimeType,
          body.status,
          publishedAt
        ]
      );

      return res.json({
        ok: true,
        assetFile: result.rows[0]
      });
    } catch (error: any) {
      console.error(error);

      if (error?.code === "23514") {
        return res.status(400).json({
          ok: false,
          error: "invalid asset status or type"
        });
      }

      if (error?.code === "23505") {
        return res.status(409).json({
          ok: false,
          error: "asset key/version already exists"
        });
      }

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.put(
  "/internal/admin/assets/manifests/:manifestId/files/:assetFileId",
  requireAssetAdmin,
  async (req, res) => {
    const body = req.body;

    try {
      const result = await pool.query(
        `
        INSERT INTO manifest_files (
          manifest_id,
          asset_file_id,
          required,
          load_priority,
          usage
        )
        VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT (manifest_id, asset_file_id)
        DO UPDATE SET
          required = EXCLUDED.required,
          load_priority = EXCLUDED.load_priority,
          usage = EXCLUDED.usage
        RETURNING *
        `,
        [
          req.params.manifestId,
          req.params.assetFileId,
          body.required ?? true,
          Number(body.loadPriority ?? 100),
          body.usage ?? null
        ]
      );

      return res.json({
        ok: true,
        manifestFile: result.rows[0]
      });
    } catch (error: any) {
      if (error?.code === "23503") {
        return res.status(404).json({
          ok: false,
          error: "manifest or asset file not found"
        });
      }

      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.delete(
  "/internal/admin/assets/manifests/:manifestId/files/:assetFileId",
  requireAssetAdmin,
  async (req, res) => {
    try {
      const result = await pool.query(
        `
        DELETE FROM manifest_files
        WHERE manifest_id = $1
          AND asset_file_id = $2
        RETURNING *
        `,
        [req.params.manifestId, req.params.assetFileId]
      );

      if (result.rowCount === 0) {
        return res.status(404).json({
          ok: false,
          error: "manifest file relation not found"
        });
      }

      return res.json({
        ok: true,
        removed: result.rows[0]
      });
    } catch (error) {
      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

app.post(
  "/internal/admin/assets/manifests/:manifestId/set-current",
  requireAssetAdmin,
  async (req, res) => {
    const client = await pool.connect();

    try {
      await client.query("BEGIN");

      const manifestResult = await client.query(
        `
        SELECT *
        FROM asset_manifests
        WHERE manifest_id = $1
        FOR UPDATE
        `,
        [req.params.manifestId]
      );

      if (manifestResult.rowCount === 0) {
        await client.query("ROLLBACK");

        return res.status(404).json({
          ok: false,
          error: "manifest not found"
        });
      }

      const manifest = manifestResult.rows[0];

      await client.query(
        `
        UPDATE asset_manifests
        SET is_current = false
        WHERE target_type = $1
          AND target_id = $2
        `,
        [manifest.target_type, manifest.target_id]
      );

      const updateResult = await client.query(
        `
        UPDATE asset_manifests
        SET status = 'published',
            is_current = true,
            published_at = COALESCE(published_at, now())
        WHERE manifest_id = $1
        RETURNING *
        `,
        [req.params.manifestId]
      );

      await client.query("COMMIT");

      return res.json({
        ok: true,
        manifest: updateResult.rows[0]
      });
    } catch (error) {
      await client.query("ROLLBACK");

      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    } finally {
      client.release();
    }
  }
);

app.post(
  "/internal/admin/assets/sync-report",
  requireAssetAdmin,
  async (req, res) => {
    const files = Array.isArray(req.body?.files) ? req.body.files : [];

    if (files.length === 0) {
      return res.status(400).json({
        ok: false,
        error: "files array is required"
      });
    }

    try {
      const dbResult = await pool.query(
        `
        SELECT
          asset_file_id,
          asset_key,
          version,
          file_path,
          hash,
          size_bytes,
          status
        FROM asset_files
        WHERE status <> 'deleted'
        `
      );

      const dbById = new Map<string, any>();

      for (const row of dbResult.rows) {
        dbById.set(row.asset_file_id, row);
      }

      const reportedIds = new Set<string>();

      const registeredOk: any[] = [];
      const different: any[] = [];
      const missingInDb: any[] = [];

      for (const file of files) {
        const assetFileId = String(file.assetFileId ?? "");
        if (!assetFileId) continue;

        reportedIds.add(assetFileId);

        const dbFile = dbById.get(assetFileId);

        if (!dbFile) {
          missingInDb.push(file);
          continue;
        }

        const mismatches: string[] = [];

        if (file.hash && file.hash !== dbFile.hash) {
          mismatches.push("hash");
        }

        if (
          file.sizeBytes !== undefined &&
          Number(file.sizeBytes) !== Number(dbFile.size_bytes)
        ) {
          mismatches.push("sizeBytes");
        }

        if (file.filePath && file.filePath !== dbFile.file_path) {
          mismatches.push("filePath");
        }

        if (mismatches.length > 0) {
          different.push({
            assetFileId,
            mismatches,
            disk: file,
            database: dbFile
          });
        } else {
          registeredOk.push({
            assetFileId,
            status: dbFile.status
          });
        }
      }

      const missingOnDisk = dbResult.rows.filter(
        (row) => !reportedIds.has(row.asset_file_id)
      );

      return res.json({
        ok: true,
        summary: {
          reportedFiles: files.length,
          databaseFiles: dbResult.rows.length,
          registeredOk: registeredOk.length,
          different: different.length,
          missingInDb: missingInDb.length,
          missingOnDisk: missingOnDisk.length
        },
        registeredOk,
        different,
        missingInDb,
        missingOnDisk
      });
    } catch (error) {
      console.error(error);

      return res.status(500).json({
        ok: false,
        error: "internal server error"
      });
    }
  }
);

// --- Salas y tickets (Fase 1) ---
const controlClient = new HttpGameServerControlClient({
  baseUrl: process.env.GAME_SERVER_CONTROL_URL ?? "http://localhost:8080",
  token: process.env.GS_CONTROL_TOKEN
});
const roomServices = buildRoomServices(getRedis(), controlClient);
app.use(createRoomsRouter(roomServices));

app.listen(port, () => {
  console.log(`GameApi listening on port ${port}`);
});
