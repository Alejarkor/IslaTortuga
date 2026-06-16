/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_WEB_SERVER_URL?: string;
  readonly VITE_CHARACTER_MANIFEST_TARGET_TYPE?: string;
  readonly VITE_CHARACTER_MANIFEST_TARGET_ID?: string;
  readonly VITE_CHARACTER_BODY_ASSET_KEY?: string;
  readonly VITE_CHARACTER_HAIR_ASSET_KEY?: string;
  readonly VITE_LOGIN_MANIFEST_TARGET_TYPE?: string;
  readonly VITE_LOGIN_MANIFEST_TARGET_ID?: string;
  readonly VITE_UI_MANIFEST_TARGET_TYPE?: string;
  readonly VITE_UI_MANIFEST_TARGET_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
