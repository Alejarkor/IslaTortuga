import { useUiAsset } from "./uiAssetsStore";
import { TurtleEmblem } from "@/features/auth/PirateIcons";

/**
 * Emblema de marca: usa el logo real (ui/LogoIslaTortuga) si el manifest de UI
 * lo aporta; si no, cae al emblema SVG de respaldo.
 */
export function BrandEmblem({ className }: { className?: string }) {
  const logo = useUiAsset("ui/LogoIslaTortuga");
  if (logo) {
    return <img className={className} src={logo} alt="Isla Tortuga" />;
  }
  return <TurtleEmblem className={className} />;
}
