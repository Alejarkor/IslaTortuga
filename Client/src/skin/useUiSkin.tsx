import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";

import { fetchUiManifest } from "@/api/assets.api";
import { ApiError } from "@/api/httpClient";
import { env } from "@/config/env";
import type { ManifestFile } from "@/types/api";
import { useUiAssetsStore } from "./uiAssetsStore";

/**
 * Mapa assetKey (del manifest de UI) -> variable CSS. El valor se inyecta como
 * url("...") en :root, y las hojas de estilo lo consumen con fallback al tema
 * CSS actual. Si el manifest no existe (404), no se inyecta nada y se mantiene
 * el aspecto de respaldo.
 */
const KEY_TO_VAR: Record<string, string> = {
  "ui/LogoIslaTortuga": "--ui-logo",
  "ui/LobbyBG": "--ui-lobby-bg",
  "ui/PanelFrame": "--ui-panel-frame",
  "ui/PanelParchment": "--ui-parch",
  "ui/HeaderFrame": "--ui-header",
  "ui/TitleBanner": "--ui-title-banner",
  "ui/MiniPanel": "--ui-minipanel",
  "ui/AvatarFrame": "--ui-avatar",
  "ui/ColorPanel": "--ui-color-panel",
  "ui/WoodFill": "--ui-wood",
  "ui/ButtonSmall": "--ui-btn-small",
  "ui/RopeTrim": "--ui-rope",
  "ui/CornerOrnament": "--ui-corner",
  "ui/ButtonTeal_normal": "--ui-btn-teal",
  "ui/ButtonTeal_hover": "--ui-btn-teal-hover",
  "ui/ButtonTeal_pressed": "--ui-btn-teal-pressed",
  "ui/ButtonGold": "--ui-btn-gold",
  "ui/ButtonPlay": "--ui-btn-play",
  "ui/IconButton": "--ui-icon-btn",
  "ui/ArrowButton": "--ui-arrow",
  "ui/CloseButton": "--ui-close",
  "ui/InputField": "--ui-input",
  "ui/Dropdown": "--ui-dropdown",
  "ui/Checkbox": "--ui-checkbox",
  "ui/Slider": "--ui-slider",
  "ui/TabActive": "--ui-tab-on",
  "ui/TabInactive": "--ui-tab-off",
  "ui/Notification": "--ui-notif",
  "ui/CoinIcon": "--ui-coin",
  "ui/GemIcon": "--ui-gem",
  "ui/EnergyIcon": "--ui-energy",
  "ui/ChestIcon": "--ui-chest",
  "ui/MapIcon": "--ui-map",
  "ui/RankBadge": "--ui-rank"
};

/** Versión numérica de un fichero (string -> número, 0 si no parsea). */
function versionNum(file: ManifestFile): number {
  const n = parseInt(String(file.version ?? ""), 10);
  return Number.isFinite(n) ? n : 0;
}

/**
 * De todos los ficheros del manifest, quédate con el de versión MÁS ALTA por
 * cada assetKey. Así, si conviven v001 y v002 del mismo asset, siempre se usa
 * el último publicado.
 */
function latestByKey(files: ManifestFile[]): ManifestFile[] {
  const best = new Map<string, ManifestFile>();
  for (const file of files) {
    if (!file.downloadUrl) continue;
    const prev = best.get(file.assetKey);
    if (!prev || versionNum(file) >= versionNum(prev)) {
      best.set(file.assetKey, file);
    }
  }
  return [...best.values()];
}

function applyVars(allFiles: ManifestFile[]) {
  const root = document.documentElement;
  const files = latestByKey(allFiles);
  const map: Record<string, string> = {};
  let applied = 0;
  for (const file of files) {
    if (!file.downloadUrl) continue;
    map[file.assetKey] = file.downloadUrl;
    const cssVar = KEY_TO_VAR[file.assetKey];
    if (cssVar) {
      root.style.setProperty(cssVar, `url("${file.downloadUrl}")`);
      applied++;
    }
  }
  if (applied > 0) root.classList.add("ui-skinned");
  useUiAssetsStore.getState().setAssets(map);

  const unmapped = files
    .map((f) => f.assetKey)
    .filter((k) => !(k in KEY_TO_VAR));
  console.info(
    `[IslaTortuga] Skin de UI: ${allFiles.length} ficheros en el manifest, ` +
      `${files.length} assetKeys únicos (última versión), ${applied} aplicados.`,
    unmapped.length ? { sin_mapear: unmapped } : ""
  );
}

/**
 * Carga el manifest de UI y aplica los assets como variables CSS.
 * Renderiza null; se monta una vez en la raíz de la app.
 */
export function UiSkin() {
  const query = useQuery({
    queryKey: ["ui", "skin"],
    queryFn: async ({ signal }) => {
      try {
        return await fetchUiManifest(signal);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          console.warn(
            `[IslaTortuga] Manifest de UI no encontrado (targetId=${env.uiManifestTargetId}). Usando tema CSS de respaldo.`
          );
          return null;
        }
        throw error;
      }
    },
    staleTime: 10 * 60_000,
    retry: false
  });

  useEffect(() => {
    if (query.data?.files) applyVars(query.data.files);
  }, [query.data]);

  return null;
}
