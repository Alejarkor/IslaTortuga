import express, { Request, Response, NextFunction } from "express";
import cookieParser from "cookie-parser";
import jwt from "jsonwebtoken";
import path from "path";

const app = express();

app.use(express.json());
app.use(cookieParser());

const assetsDir = process.env.ASSETS_DIR ?? "/app/server_assets";

app.use(
  "/assets/files",
  express.static(assetsDir, {
    index: false,
    dotfiles: "deny",
    fallthrough: false
  })
);

const port = Number(process.env.PORT ?? 3000);
const gameApiUrl = process.env.GAME_API_URL ?? "http://localhost:3001";
const isProd = process.env.NODE_ENV === "production";
const DEFAULT_DEV_SECRET = "dev_secret_change_me";
const jwtSecret = process.env.JWT_SECRET ?? DEFAULT_DEV_SECRET;

// En producción exigimos un secreto propio: arrancar con el de desarrollo
// permitiría a cualquiera forjar tokens de sesión válidos.
if (isProd && (!process.env.JWT_SECRET || jwtSecret === DEFAULT_DEV_SECRET)) {
  throw new Error(
    "JWT_SECRET es obligatorio en producción y no puede ser el valor por defecto de desarrollo."
  );
}

// Opciones de la cookie de sesión, centralizadas. Secure solo en producción
// (en local sobre HTTP, Secure impediría que el navegador enviara la cookie).
const sessionCookieOptions = {
  httpOnly: true as const,
  sameSite: "lax" as const,
  secure: isProd,
  maxAge: 7 * 24 * 60 * 60 * 1000,
  path: "/"
};

type SessionPayload = {
  userId: string;
  playerId: string;
  username: string;
  nickname: string;
};

type AuthenticatedRequest = Request & {
  session?: SessionPayload;
};

function createSessionToken(payload: SessionPayload): string {
  return jwt.sign(payload, jwtSecret, {
    expiresIn: "7d"
  });
}

function authMiddleware(
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
) {
  const token = req.cookies?.session;

  if (!token) {
    return res.status(401).json({
      ok: false,
      error: "not authenticated"
    });
  }

  try {
    const decoded = jwt.verify(token, jwtSecret) as SessionPayload;
    req.session = decoded;
    next();
  } catch {
    return res.status(401).json({
      ok: false,
      error: "invalid session"
    });
  }
}

async function forwardJson(
  url: string,
  options?: {
    method?: string;
    body?: unknown;
  }
) {
  const response = await fetch(url, {
    method: options?.method ?? "GET",
    headers: {
      "Content-Type": "application/json"
    },
    body: options?.body ? JSON.stringify(options.body) : undefined
  });

  const data = await response.json();

  return {
    status: response.status,
    data
  };
}

app.get("/health", async (_req, res) => {
  try {
    const apiResponse = await forwardJson(`${gameApiUrl}/internal/health`);

    return res.status(apiResponse.status).json({
      ok: apiResponse.data.ok,
      service: "web-server",
      gameApi: apiResponse.data
    });
  } catch {
    return res.status(500).json({
      ok: false,
      service: "web-server",
      gameApi: "not reachable"
    });
  }
});

app.post("/api/auth/register", async (req, res) => {
  const apiResponse = await forwardJson(`${gameApiUrl}/internal/auth/register`, {
    method: "POST",
    body: req.body
  });

  if (!apiResponse.data.ok) {
    return res.status(apiResponse.status).json(apiResponse.data);
  }

  const token = createSessionToken({
    userId: apiResponse.data.user.user_id,
    playerId: apiResponse.data.profile.player_id,
    username: apiResponse.data.user.username,
    nickname: apiResponse.data.profile.nickname
  });

  res.cookie("session", token, sessionCookieOptions);

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.post("/api/auth/login", async (req, res) => {
  const apiResponse = await forwardJson(`${gameApiUrl}/internal/auth/login`, {
    method: "POST",
    body: req.body
  });

  if (!apiResponse.data.ok) {
    return res.status(apiResponse.status).json(apiResponse.data);
  }

  const token = createSessionToken({
    userId: apiResponse.data.user.user_id,
    playerId: apiResponse.data.profile.player_id,
    username: apiResponse.data.user.username,
    nickname: apiResponse.data.profile.nickname
  });

  res.cookie("session", token, sessionCookieOptions);

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.post("/api/auth/logout", (_req, res) => {
  res.clearCookie("session", { path: "/" });

  return res.json({
    ok: true
  });
});

app.get("/api/me", authMiddleware, async (req: AuthenticatedRequest, res) => {
  return res.json({
    ok: true,
    session: req.session
  });
});

app.get("/api/profile", authMiddleware, async (req: AuthenticatedRequest, res) => {
  const playerId = req.session!.playerId;

  const apiResponse = await forwardJson(
    `${gameApiUrl}/internal/profiles/${playerId}`
  );

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.get("/api/stats", authMiddleware, async (req: AuthenticatedRequest, res) => {
  const playerId = req.session!.playerId;

  const apiResponse = await forwardJson(
    `${gameApiUrl}/internal/stats/${playerId}`
  );

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.patch(
  "/api/profile/appearance",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/profiles/${playerId}/appearance`,
      {
        method: "PATCH",
        body: req.body
      }
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.get("/api/friends", authMiddleware, async (req: AuthenticatedRequest, res) => {
  const playerId = req.session!.playerId;

  const apiResponse = await forwardJson(
    `${gameApiUrl}/internal/friends/${playerId}`
  );

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.get(
  "/api/friends/requests/incoming",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests/${playerId}/incoming`
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.get(
  "/api/friends/requests/outgoing",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests/${playerId}/outgoing`
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.post(
  "/api/friends/requests",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const fromPlayerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests`,
      {
        method: "POST",
        body: {
          fromPlayerId,
          toPlayerId: req.body?.toPlayerId,
          nickname: req.body?.nickname
        }
      }
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.post(
  "/api/friends/requests/:requestId/accept",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests/${req.params.requestId}/accept`,
      {
        method: "POST",
        body: {
          playerId
        }
      }
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.post(
  "/api/friends/requests/:requestId/reject",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests/${req.params.requestId}/reject`,
      {
        method: "POST",
        body: {
          playerId
        }
      }
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

app.post(
  "/api/friends/requests/:requestId/cancel",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const playerId = req.session!.playerId;

    const apiResponse = await forwardJson(
      `${gameApiUrl}/internal/friend-requests/${req.params.requestId}/cancel`,
      {
        method: "POST",
        body: {
          playerId
        }
      }
    );

    return res.status(apiResponse.status).json(apiResponse.data);
  }
);

//------------------------------------ ENDPOINT DE ASSETS ------------------------------------

app.get("/assets/manifest", async (req, res) => {
  const targetType = req.query.targetType;
  const targetId = req.query.targetId;

  if (!targetType || !targetId) {
    return res.status(400).json({
      ok: false,
      error: "targetType and targetId are required"
    });
  }

  const apiResponse = await forwardJson(
    `${gameApiUrl}/internal/assets/manifest?targetType=${encodeURIComponent(
      String(targetType)
    )}&targetId=${encodeURIComponent(String(targetId))}`
  );

  return res.status(apiResponse.status).json(apiResponse.data);
});


//------------------------------------ SALAS (proxy autenticado) ------------------------------------
// El playerId/nickname SIEMPRE salen de la sesión verificada (JWT), nunca del
// cuerpo que envía el cliente: así un usuario no puede actuar como otro.

app.get("/api/rooms", authMiddleware, async (_req: AuthenticatedRequest, res) => {
  const r = await forwardJson(`${gameApiUrl}/internal/rooms`);
  return res.status(r.status).json(r.data);
});

app.post("/api/rooms", authMiddleware, async (req: AuthenticatedRequest, res) => {
  const r = await forwardJson(`${gameApiUrl}/internal/rooms`, {
    method: "POST",
    body: {
      hostPlayerId: req.session!.playerId,
      nickname: req.session!.nickname,
      maxPlayers: req.body?.maxPlayers,
      mapId: req.body?.mapId,
      isPrivate: req.body?.isPrivate
    }
  });
  return res.status(r.status).json(r.data);
});

app.post(
  "/api/rooms/join-by-code",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(`${gameApiUrl}/internal/rooms/join-by-code`, {
      method: "POST",
      body: {
        code: req.body?.code,
        playerId: req.session!.playerId,
        nickname: req.session!.nickname
      }
    });
    return res.status(r.status).json(r.data);
  }
);

app.get(
  "/api/rooms/:roomId",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(
      `${gameApiUrl}/internal/rooms/${req.params.roomId}`
    );
    return res.status(r.status).json(r.data);
  }
);

app.post(
  "/api/rooms/:roomId/join",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(
      `${gameApiUrl}/internal/rooms/${req.params.roomId}/join`,
      {
        method: "POST",
        body: {
          playerId: req.session!.playerId,
          nickname: req.session!.nickname
        }
      }
    );
    return res.status(r.status).json(r.data);
  }
);

app.post(
  "/api/rooms/:roomId/leave",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(
      `${gameApiUrl}/internal/rooms/${req.params.roomId}/leave`,
      { method: "POST", body: { playerId: req.session!.playerId } }
    );
    return res.status(r.status).json(r.data);
  }
);

app.post(
  "/api/rooms/:roomId/ready",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(
      `${gameApiUrl}/internal/rooms/${req.params.roomId}/ready`,
      {
        method: "POST",
        body: { playerId: req.session!.playerId, ready: req.body?.ready }
      }
    );
    return res.status(r.status).json(r.data);
  }
);

app.post(
  "/api/rooms/:roomId/launch",
  authMiddleware,
  async (req: AuthenticatedRequest, res) => {
    const r = await forwardJson(
      `${gameApiUrl}/internal/rooms/${req.params.roomId}/launch`,
      { method: "POST", body: { playerId: req.session!.playerId } }
    );
    return res.status(r.status).json(r.data);
  }
);


app.listen(port, () => {
  console.log(`WebServer listening on port ${port}`);
});