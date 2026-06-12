import path from "path";
import dotenv from "dotenv";

dotenv.config();

const toolRoot = path.resolve(__dirname, "..");

function resolveAssetsRoot(): string {
  const raw =
    process.env.ASSETS_ROOT ?? "../../Server/GameAssets/server_assets";

  return path.isAbsolute(raw) ? raw : path.resolve(toolRoot, raw);
}

export const config = {
  port: Number(process.env.PORT ?? 4100),
  assetsRoot: resolveAssetsRoot(),
  gameApiUrl: process.env.GAME_API_URL ?? "http://localhost:3001",
  adminToken: process.env.ASSET_ADMIN_TOKEN ?? "",
  publicDir: path.resolve(toolRoot, "public")
};
