import { create } from "zustand";

/**
 * Mapa assetKey -> downloadUrl de los assets de UI cargados desde el manifest.
 * Lo rellena UiSkin; los componentes leen URLs concretas (p. ej. el logo) para
 * renderizar <img> cuando el asset existe, con fallback si no.
 */
type UiAssetsState = {
  assets: Record<string, string>;
  setAssets: (assets: Record<string, string>) => void;
};

export const useUiAssetsStore = create<UiAssetsState>((set) => ({
  assets: {},
  setAssets: (assets) => set({ assets })
}));

/** Devuelve la URL de un asset de UI por assetKey, o null si no está cargado. */
export function useUiAsset(assetKey: string): string | null {
  return useUiAssetsStore((s) => s.assets[assetKey] ?? null);
}
