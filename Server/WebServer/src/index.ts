import express, { Request, Response, NextFunction } from "express";
import cookieParser from "cookie-parser";
import jwt from "jsonwebtoken";

const app = express();

app.use(express.json());
app.use(cookieParser());

const port = Number(process.env.PORT ?? 3000);
const gameApiUrl = process.env.GAME_API_URL ?? "http://localhost:3001";
const jwtSecret = process.env.JWT_SECRET ?? "dev_secret_change_me";

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

  res.cookie("session", token, {
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    maxAge: 7 * 24 * 60 * 60 * 1000
  });

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

  res.cookie("session", token, {
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    maxAge: 7 * 24 * 60 * 60 * 1000
  });

  return res.status(apiResponse.status).json(apiResponse.data);
});

app.post("/api/auth/logout", (_req, res) => {
  res.clearCookie("session");

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

app.listen(port, () => {
  console.log(`WebServer listening on port ${port}`);
});