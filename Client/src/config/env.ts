/**
 * Configuración de entorno centralizada y tipada.
 * Único punto de acceso a import.meta.env, con valores por defecto.
 */
type AppEnv = {
  apiBaseUrl: string;
  characterManifestTargetType: string;
  characterManifestTargetId: string;
  bodyAssetKey: string;
  hairAssetKey: string;
  loginManifestTargetType: string;
  loginManifestTargetId: string;
  uiManifestTargetType: string;
  uiManifestTargetId: string;
};

function readEnv(key: string, fallback: string): string {
  const value = import.meta.env[key as keyof ImportMetaEnv] as
    | string
    | undefined;
  return value && value.length > 0 ? value : fallback;
}

export const env: AppEnv = {
  apiBaseUrl: readEnv("VITE_API_BASE_URL", ""),
  characterManifestTargetType: readEnv(
    "VITE_CHARACTER_MANIFEST_TARGET_TYPE",
    "global"
  ),
  characterManifestTargetId: readEnv(
    "VITE_CHARACTER_MANIFEST_TARGET_ID",
    "playerEditorPregame"
  ),
  bodyAssetKey: readEnv(
    "VITE_CHARACTER_BODY_ASSET_KEY",
    "models/IT_Character - Rigged"
  ),
  hairAssetKey: readEnv("VITE_CHARACTER_HAIR_ASSET_KEY", "models/Pelos"),
  loginManifestTargetType: readEnv("VITE_LOGIN_MANIFEST_TARGET_TYPE", "global"),
  loginManifestTargetId: readEnv("VITE_LOGIN_MANIFEST_TARGET_ID", "login"),
  uiManifestTargetType: readEnv("VITE_UI_MANIFEST_TARGET_TYPE", "global"),
  uiManifestTargetId: readEnv("VITE_UI_MANIFEST_TARGET_ID", "uiCommon")
};
