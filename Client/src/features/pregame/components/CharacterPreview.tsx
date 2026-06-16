import { useEffect, useRef, useState } from "react";

import { CharacterRenderer } from "@/three/CharacterRenderer";
import { useCharacterEditorStore } from "@/store/characterEditorStore";
import type { CharacterCustomization } from "@/domain/character/customizationOptions";

/**
 * Preview 3D del personaje.
 * Rotación arrastrando con ratón/dedo sobre el lienzo (sin botones).
 */
export function CharacterPreview({
  customization,
  onHairDiscovered
}: {
  customization: CharacterCustomization | undefined;
  onHairDiscovered: (hairIds: string[]) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const rendererRef = useRef<CharacterRenderer | null>(null);
  const discoveredRef = useRef(onHairDiscovered);
  discoveredRef.current = onHairDiscovered;

  const [ready, setReady] = useState(false);
  const [assetWarning, setAssetWarning] = useState<string | null>(null);

  const editing = useCharacterEditorStore((s) => s.editing);

  useEffect(() => {
    if (!canvasRef.current) return;

    const renderer = new CharacterRenderer(canvasRef.current, {
      onError: (scope) => {
        setAssetWarning(
          scope === "body"
            ? "No se pudo cargar el modelo del cuerpo; mostrando maniquí de respaldo."
            : "No se pudo cargar el pack de pelo."
        );
      },
      onHairDiscovered: (ids) => discoveredRef.current(ids)
    });
    rendererRef.current = renderer;
    setReady(true);

    const observer = new ResizeObserver(() => renderer.resize());
    observer.observe(canvasRef.current);

    return () => {
      observer.disconnect();
      renderer.dispose();
      rendererRef.current = null;
      setReady(false);
    };
  }, []);

  useEffect(() => {
    if (!ready || !customization || !rendererRef.current) return;
    setAssetWarning(null);
    rendererRef.current.setCustomization(
      customization.body,
      customization.hairPackUrl
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, customization]);

  useEffect(() => {
    if (!ready || !editing || !rendererRef.current) return;
    rendererRef.current.applyAppearance(editing);
  }, [ready, editing]);

  return (
    <div className="character-preview">
      <canvas ref={canvasRef} className="character-preview__canvas" />
      {assetWarning && (
        <p className="character-preview__warning">{assetWarning}</p>
      )}
    </div>
  );
}
