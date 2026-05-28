import {
  Color3,
  MeshBuilder,
  Scene,
  StandardMaterial,
  TransformNode,
  Vector3,
  type AbstractMesh,
} from '@babylonjs/core';
import type { PrimitivePartDefinition, Vector3Definition } from '../content/contentTypes';

export type PrimitiveAssembly = {
  rootNode: TransformNode;
  meshes: AbstractMesh[];
  dispose(): void;
};

export function buildPrimitiveAssembly(
  scene: Scene,
  name: string,
  parts: PrimitivePartDefinition[],
): PrimitiveAssembly {
  const rootNode = new TransformNode(`${name}-root`, scene);
  const meshes: AbstractMesh[] = [];
  const materials: StandardMaterial[] = [];

  parts.forEach((part, index) => {
    const mesh = createPrimitiveMesh(scene, `${name}-part-${index}`, part);
    mesh.parent = rootNode;
    mesh.position = toVector3(part.position);
    mesh.rotation = toRadiansVector3(part.rotation);
    mesh.scaling = toScaleVector3(part.scale);
    meshes.push(mesh);

    if (mesh.material instanceof StandardMaterial) {
      materials.push(mesh.material);
    }
  });

  return {
    rootNode,
    meshes,
    dispose: () => {
      rootNode.dispose(false);
      meshes.forEach((mesh) => mesh.dispose(false, true));
      materials.forEach((material) => material.dispose(true, true));
    },
  };
}

function createPrimitiveMesh(scene: Scene, name: string, part: PrimitivePartDefinition) {
  const dimensions = resolveDimensions(part);

  const mesh =
    part.shape === 'sphere'
      ? MeshBuilder.CreateSphere(
          name,
          {
            diameter: dimensions.x,
            segments: 18,
          },
          scene,
        )
      : part.shape === 'capsule'
        ? MeshBuilder.CreateCapsule(
            name,
            {
              height: dimensions.y,
              radius: dimensions.x * 0.5,
              tessellation: 12,
              capSubdivisions: 6,
            },
            scene,
          )
        : part.shape === 'cylinder'
          ? MeshBuilder.CreateCylinder(
              name,
              {
                height: dimensions.y,
                diameter: dimensions.x,
                tessellation: 12,
              },
              scene,
            )
          : MeshBuilder.CreateBox(
              name,
              {
                width: dimensions.x,
                height: dimensions.y,
                depth: dimensions.z,
              },
              scene,
            );

  const material = new StandardMaterial(`${name}-material`, scene);
  material.diffuseColor = parseColor(part.color, '#b7c09d');
  material.emissiveColor = parseColor(part.emissiveColor, '#000000');
  material.alpha = part.alpha ?? 1;
  mesh.material = material;

  return mesh;
}

function resolveDimensions(part: PrimitivePartDefinition) {
  const x = part.dimensions?.x ?? 1;
  const y = part.dimensions?.y ?? x;
  const z = part.dimensions?.z ?? x;

  return { x, y, z };
}

function toVector3(definition?: Partial<Vector3Definition>) {
  return new Vector3(definition?.x ?? 0, definition?.y ?? 0, definition?.z ?? 0);
}

function toScaleVector3(definition?: Partial<Vector3Definition>) {
  return new Vector3(definition?.x ?? 1, definition?.y ?? 1, definition?.z ?? 1);
}

function toRadiansVector3(definition?: Partial<Vector3Definition>) {
  return new Vector3(
    degreesToRadians(definition?.x ?? 0),
    degreesToRadians(definition?.y ?? 0),
    degreesToRadians(definition?.z ?? 0),
  );
}

function degreesToRadians(value: number) {
  return (value * Math.PI) / 180;
}

function parseColor(color: string | undefined, fallback: string) {
  return Color3.FromHexString(normalizeHex(color ?? fallback));
}

function normalizeHex(value: string) {
  if (!value.startsWith('#')) {
    return `#${value}`;
  }

  return value;
}
