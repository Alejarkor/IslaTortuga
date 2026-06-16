import { MeshBuilder } from "@babylonjs/core/Meshes/meshBuilder";
import { StandardMaterial } from "@babylonjs/core/Materials/standardMaterial";
import { TransformNode } from "@babylonjs/core/Meshes/transformNode";
import { Vector3 } from "@babylonjs/core/Maths/math.vector";
import { Color3 } from "@babylonjs/core/Maths/math.color";
import type { Scene } from "@babylonjs/core/scene";
import type { Mesh } from "@babylonjs/core/Meshes/mesh";

/**
 * Maniquí procedural de respaldo.
 *
 * Se usa cuando el manifest todavía no tiene el GLB del cuerpo (CA-07):
 * permite probar TODO el flujo del editor (cambiar colores y pelo en vivo,
 * guardar, recargar) sin depender de que los assets reales estén subidos.
 *
 * Cada parte se asocia a un slot de color para que el cambio de color
 * se vea inmediatamente, igual que ocurrirá con el modelo real.
 */
export type ProceduralCharacter = {
  root: TransformNode;
  /** Material por slot de color (skin, eyes, clothes_primary, ...). */
  materialsBySlot: Record<string, StandardMaterial>;
  /** Material del pelo procedural. */
  hairMaterial: StandardMaterial;
  /** Mallas del pelo procedural (se ocultan si hair_id === none). */
  hairMeshes: Mesh[];
  dispose(): void;
};

function makeMaterial(scene: Scene, name: string): StandardMaterial {
  const mat = new StandardMaterial(`proc_${name}`, scene);
  mat.diffuseColor = new Color3(0.8, 0.8, 0.8);
  mat.specularColor = new Color3(0.1, 0.1, 0.1);
  return mat;
}

export function buildProceduralCharacter(scene: Scene): ProceduralCharacter {
  const root = new TransformNode("proceduralCharacter", scene);

  const skin = makeMaterial(scene, "skin");
  const eyes = makeMaterial(scene, "eyes");
  const clothesPrimary = makeMaterial(scene, "clothesPrimary");
  const clothesSecondary = makeMaterial(scene, "clothesSecondary");
  const hairMaterial = makeMaterial(scene, "hair");

  const materialsBySlot: Record<string, StandardMaterial> = {
    skin,
    eyes,
    clothes_primary: clothesPrimary,
    clothes_secondary: clothesSecondary,
    hair_color: hairMaterial
  };

  // Torso (ropa 1)
  const torso = MeshBuilder.CreateCylinder(
    "torso",
    { height: 1.1, diameterTop: 0.62, diameterBottom: 0.78, tessellation: 24 },
    scene
  );
  torso.position = new Vector3(0, 1.15, 0);
  torso.material = clothesPrimary;
  torso.parent = root;

  // Cabeza (piel)
  const head = MeshBuilder.CreateSphere("head", { diameter: 0.62 }, scene);
  head.position = new Vector3(0, 2.0, 0);
  head.material = skin;
  head.parent = root;

  // Ojos
  const eyeL = MeshBuilder.CreateSphere("eyeL", { diameter: 0.12 }, scene);
  eyeL.position = new Vector3(-0.14, 2.05, 0.27);
  eyeL.material = eyes;
  eyeL.parent = root;

  const eyeR = MeshBuilder.CreateSphere("eyeR", { diameter: 0.12 }, scene);
  eyeR.position = new Vector3(0.14, 2.05, 0.27);
  eyeR.material = eyes;
  eyeR.parent = root;

  // Brazos (piel)
  for (const side of [-1, 1]) {
    const arm = MeshBuilder.CreateCapsule(
      `arm_${side}`,
      { height: 0.95, radius: 0.12 },
      scene
    );
    arm.position = new Vector3(side * 0.52, 1.25, 0);
    arm.material = skin;
    arm.parent = root;
  }

  // Piernas (ropa 2)
  for (const side of [-1, 1]) {
    const leg = MeshBuilder.CreateCapsule(
      `leg_${side}`,
      { height: 1.0, radius: 0.15 },
      scene
    );
    leg.position = new Vector3(side * 0.2, 0.1, 0);
    leg.material = clothesSecondary;
    leg.parent = root;
  }

  // Pelo procedural (casquete sobre la cabeza)
  const hairCap = MeshBuilder.CreateSphere(
    "hairCap",
    { diameter: 0.7, slice: 0.6 },
    scene
  );
  hairCap.position = new Vector3(0, 2.08, 0);
  hairCap.material = hairMaterial;
  hairCap.parent = root;

  return {
    root,
    materialsBySlot,
    hairMaterial,
    hairMeshes: [hairCap],
    dispose() {
      root.dispose();
      skin.dispose();
      eyes.dispose();
      clothesPrimary.dispose();
      clothesSecondary.dispose();
      hairMaterial.dispose();
    }
  };
}
