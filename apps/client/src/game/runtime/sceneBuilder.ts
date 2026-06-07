import {
  AbstractMesh,
  ArcRotateCamera,
  Color3,
  Color4,
  DirectionalLight,
  HemisphericLight,
  MeshBuilder,
  PointLight,
  Scene,
  StandardMaterial,
  Vector3,
} from '@babylonjs/core';
import type {
  PrimitiveSceneDefinition,
  SceneDefinition,
  UnitySceneExportColliderDefinition,
  UnitySceneExportLightDefinition,
  UnitySceneExportSceneData,
  UnitySceneExportSceneDefinition,
  UnitySceneExportTransitionTriggerDefinition,
} from '../content/contentTypes';
import type { GameRuntime } from '../bootstrap/gameRuntimeRegistry';
import { buildPrimitiveAssembly } from './primitiveAssembly';

export type BuiltSceneContext = {
  scene: Scene;
  sceneId: string;
  toWorldPosition(x: number, y: number): { x: number; y: number; z: number };
};

export class SceneBuilder {
  constructor(
    private readonly runtime: GameRuntime,
    private readonly canvas: HTMLCanvasElement,
  ) {}

  async build(scene: Scene, sceneId = this.runtime.startGame.sceneId): Promise<BuiltSceneContext> {
    const sceneDefinition = this.runtime.catalog.resolveScene(sceneId);

    if (sceneDefinition.builder === 'primitive-scene') {
      return this.buildPrimitiveScene(scene, sceneDefinition);
    }

    if (sceneDefinition.builder === 'unity-scene-export') {
      return this.buildUnitySceneExport(scene, sceneDefinition);
    }

    throw new Error(`El builder de escena ${stringifyBuilder(sceneDefinition)} no esta soportado.`);
  }

  private buildPrimitiveScene(scene: Scene, sceneDefinition: PrimitiveSceneDefinition): BuiltSceneContext {
    const coordinateScale = sceneDefinition.coordinateScale ?? 1;
    const worldWidth = sceneDefinition.worldWidth;
    const worldDepth = sceneDefinition.worldDepth;
    const groundColor = parseColor(sceneDefinition.groundColor, '#4e7a41');
    const skyColor = parseColor(sceneDefinition.skyColor, '#8ab8d8');
    const ambientColor = parseColor(sceneDefinition.ambientColor, '#efe9cf');

    scene.clearColor = new Color4(skyColor.r, skyColor.g, skyColor.b, 1);

    const center = new Vector3(worldWidth / 2, 0, worldDepth / 2);
    const radius = sceneDefinition.camera?.radius ?? Math.max(worldWidth, worldDepth) * 1.15;
    const camera = new ArcRotateCamera(
      'world-camera',
      sceneDefinition.camera?.alpha ?? -Math.PI / 2,
      sceneDefinition.camera?.beta ?? 1.05,
      radius,
      new Vector3(
        sceneDefinition.camera?.target?.x ?? center.x,
        sceneDefinition.camera?.target?.y ?? 0.3,
        sceneDefinition.camera?.target?.z ?? center.z,
      ),
      scene,
    );

    configureOrbitCamera(camera, this.canvas, radius);

    const sunLight = new DirectionalLight('sun-light', new Vector3(-0.35, -1, 0.2), scene);
    sunLight.intensity = 1.15;
    sunLight.diffuse = ambientColor;

    const skyLight = new HemisphericLight('sky-light', new Vector3(0.2, 1, -0.1), scene);
    skyLight.intensity = 0.65;
    skyLight.diffuse = ambientColor;
    skyLight.groundColor = groundColor.scale(0.55);

    createGround(scene, worldWidth, worldDepth, groundColor, new Vector3(worldWidth / 2, 0, worldDepth / 2));

    for (const prop of sceneDefinition.props ?? []) {
      const assembly = buildPrimitiveAssembly(scene, prop.propId, prop.parts);
      assembly.rootNode.position = new Vector3(prop.position.x, prop.position.y, prop.position.z);
      assembly.rootNode.rotation = new Vector3(
        degreesToRadians(prop.rotation?.x ?? 0),
        degreesToRadians(prop.rotation?.y ?? 0),
        degreesToRadians(prop.rotation?.z ?? 0),
      );
      assembly.rootNode.scaling = new Vector3(
        prop.scale?.x ?? 1,
        prop.scale?.y ?? 1,
        prop.scale?.z ?? 1,
      );
    }

    return {
      scene,
      sceneId: sceneDefinition.sceneId,
      toWorldPosition: (x: number, y: number) => ({
        x: x * coordinateScale,
        y: 0,
        z: worldDepth - y * coordinateScale,
      }),
    };
  }

  private async buildUnitySceneExport(
    scene: Scene,
    sceneDefinition: UnitySceneExportSceneDefinition,
  ): Promise<BuiltSceneContext> {
    const sceneDataFile = this.runtime.catalog.resolveFile(sceneDefinition.sceneDataFileId);
    const sceneData = await this.fetchJson<UnitySceneExportSceneData>(sceneDataFile.url);
    const boundsWidth = Math.max(sceneData.bounds?.width ?? 30, 1);
    const boundsDepth = Math.max(sceneData.bounds?.depth ?? 30, 1);

    scene.clearColor = new Color4(0.75, 0.84, 0.93, 1);

    const radius = Math.max(boundsWidth, boundsDepth) * 1.15;
    const camera = new ArcRotateCamera(
      'world-camera',
      -Math.PI / 2,
      1.05,
      radius,
      new Vector3(0, 1.2, 0),
      scene,
    );
    configureOrbitCamera(camera, this.canvas, radius);

    if ((sceneData.lights?.length ?? 0) > 0) {
      sceneData.lights?.forEach((light, index) => {
        this.createExportedLight(scene, light, index);
      });
    } else {
      const sunLight = new DirectionalLight('sun-light', new Vector3(-0.35, -1, 0.2), scene);
      sunLight.intensity = 1.1;

      const skyLight = new HemisphericLight('sky-light', new Vector3(0.2, 1, -0.1), scene);
      skyLight.intensity = 0.6;
      skyLight.groundColor = new Color3(0.28, 0.32, 0.24);
    }

    createGround(scene, boundsWidth, boundsDepth, parseColor('#617c4d', '#617c4d'), Vector3.Zero());

    sceneData.props?.forEach((prop) => {
      createScenePlaceholderBox(
        scene,
        `prop-${prop.propId}`,
        prop.position,
        prop.rotation,
        prop.scale,
        hashColor(prop.visualAssetId ?? prop.propId),
        0.95,
      );
    });

    sceneData.colliders
      ?.filter((collider) => collider.clientCollision !== 'none')
      .forEach((collider) => {
        this.createColliderPreview(scene, collider);
      });

    sceneData.transitions?.forEach((transition) => {
      if (transition.trigger) {
        this.createTransitionPreview(scene, transition.transitionId, transition.trigger);
      }
    });

    return {
      scene,
      sceneId: sceneDefinition.sceneId,
      toWorldPosition: (x: number, y: number) => ({
        x,
        y: 0,
        z: y,
      }),
    };
  }

  private createExportedLight(scene: Scene, light: UnitySceneExportLightDefinition, index: number) {
    const lightColor = parseColor(light.color, '#FFF5E0');
    const intensity = light.intensity ?? 1;

    if (light.lightType === 'directional') {
      const rotation = light.rotation ?? {};
      const direction = directionFromEuler(rotation.x ?? 50, rotation.y ?? 330);
      const directionalLight = new DirectionalLight(`export-light-${index}`, direction, scene);
      directionalLight.diffuse = lightColor;
      directionalLight.intensity = intensity;
      return;
    }

    if (light.lightType === 'point' || light.lightType === 'spot') {
      const pointLight = new PointLight(
        `export-light-${index}`,
        toVector3(light.position),
        scene,
      );
      pointLight.diffuse = lightColor;
      pointLight.intensity = intensity;
      pointLight.range = light.range ?? 18;
      return;
    }

    const hemiLight = new HemisphericLight(
      `export-light-${index}`,
      new Vector3(0, 1, 0),
      scene,
    );
    hemiLight.diffuse = lightColor;
    hemiLight.intensity = intensity;
  }

  private createColliderPreview(scene: Scene, collider: UnitySceneExportColliderDefinition) {
    const color =
      collider.colliderKind === 'trigger'
        ? parseColor('#e7c45d', '#e7c45d')
        : parseColor('#6d88a8', '#6d88a8');

    if (collider.shape.type === 'box') {
      createScenePlaceholderBox(
        scene,
        `collider-${collider.colliderId}`,
        collider.shape.center,
        undefined,
        collider.shape.size,
        color,
        0.25,
      );
      return;
    }

    const mesh =
      collider.shape.type === 'sphere'
        ? MeshBuilder.CreateSphere(`collider-${collider.colliderId}`, {
            diameter: collider.shape.radius * 2,
            segments: 16,
          }, scene)
        : collider.shape.type === 'capsule'
          ? MeshBuilder.CreateCapsule(`collider-${collider.colliderId}`, {
              height: collider.shape.height,
              radius: collider.shape.radius,
              tessellation: 10,
            }, scene)
          : MeshBuilder.CreateCylinder(`collider-${collider.colliderId}`, {
              height: collider.shape.height,
              diameter: collider.shape.radius * 2,
              tessellation: 10,
            }, scene);

    mesh.position = toVector3(collider.shape.center);
    applyDebugMaterial(mesh, color, 0.2, scene);
  }

  private createTransitionPreview(
    scene: Scene,
    transitionId: string,
    trigger: UnitySceneExportTransitionTriggerDefinition,
  ) {
    if (trigger.type === 'box' && trigger.size) {
      createScenePlaceholderBox(
        scene,
        `transition-${transitionId}`,
        trigger.center,
        undefined,
        trigger.size,
        parseColor('#7d5dd9', '#7d5dd9'),
        0.15,
      );
      return;
    }

    const radius = trigger.radius ?? 0.75;
    const mesh =
      trigger.type === 'sphere'
        ? MeshBuilder.CreateSphere(`transition-${transitionId}`, { diameter: radius * 2 }, scene)
        : MeshBuilder.CreateCylinder(
            `transition-${transitionId}`,
            {
              height: trigger.height ?? 2,
              diameter: radius * 2,
            },
            scene,
          );

    mesh.position = toVector3(trigger.center);
    applyDebugMaterial(mesh, parseColor('#7d5dd9', '#7d5dd9'), 0.15, scene);
  }

  private fetchJson<TResponse>(url: string) {
    return fetch(url).then(async (response) => {
      if (!response.ok) {
        throw new Error(`No se pudo cargar ${url}.`);
      }

      return (await response.json()) as TResponse;
    });
  }
}

function configureOrbitCamera(camera: ArcRotateCamera, canvas: HTMLCanvasElement, radius: number) {
  camera.attachControl(canvas, true);
  camera.lowerRadiusLimit = radius * 0.55;
  camera.upperRadiusLimit = radius * 1.45;
  camera.lowerBetaLimit = 0.55;
  camera.upperBetaLimit = 1.25;
  camera.wheelPrecision = 24;
  camera.panningSensibility = 0;
}

function createGround(
  scene: Scene,
  width: number,
  depth: number,
  color: Color3,
  center: Vector3,
) {
  const groundMaterial = new StandardMaterial('scene-ground-material', scene);
  groundMaterial.diffuseColor = color;
  groundMaterial.specularColor = Color3.Black();

  const ground = MeshBuilder.CreateGround(
    'scene-ground',
    {
      width,
      height: depth,
      subdivisions: Math.max(width, depth),
    },
    scene,
  );
  ground.position = center;
  ground.material = groundMaterial;
}

function createScenePlaceholderBox(
  scene: Scene,
  name: string,
  position: Partial<{ x: number; y: number; z: number }>,
  rotation: Partial<{ x: number; y: number; z: number }> | undefined,
  scale: Partial<{ x: number; y: number; z: number }> | undefined,
  color: Color3,
  alpha: number,
) {
  const mesh = MeshBuilder.CreateBox(
    name,
    {
      width: scale?.x ?? 1,
      height: scale?.y ?? 1,
      depth: scale?.z ?? 1,
    },
    scene,
  );
  mesh.position = toVector3(position);
  mesh.rotation = new Vector3(
    degreesToRadians(rotation?.x ?? 0),
    degreesToRadians(rotation?.y ?? 0),
    degreesToRadians(rotation?.z ?? 0),
  );
  applyDebugMaterial(mesh, color, alpha, scene);
}

function applyDebugMaterial(mesh: AbstractMesh, color: Color3, alpha: number, scene: Scene) {
  const material = new StandardMaterial(`${mesh.name}-material`, scene);
  material.diffuseColor = color;
  material.emissiveColor = color.scale(0.2);
  material.alpha = alpha;
  mesh.material = material;
}

function toVector3(definition?: Partial<{ x: number; y: number; z: number }>) {
  return new Vector3(definition?.x ?? 0, definition?.y ?? 0, definition?.z ?? 0);
}

function directionFromEuler(pitchDegrees: number, yawDegrees: number) {
  const pitch = degreesToRadians(pitchDegrees);
  const yaw = degreesToRadians(yawDegrees);
  const x = Math.cos(pitch) * Math.sin(yaw);
  const y = -Math.sin(pitch);
  const z = Math.cos(pitch) * Math.cos(yaw);
  return new Vector3(x, y, z).normalize();
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

function hashColor(seed: string) {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = (hash * 31 + seed.charCodeAt(index)) >>> 0;
  }

  const r = 90 + (hash & 0x5f);
  const g = 90 + ((hash >> 8) & 0x5f);
  const b = 90 + ((hash >> 16) & 0x5f);
  return Color3.FromInts(r, g, b);
}

function stringifyBuilder(sceneDefinition: SceneDefinition) {
  return sceneDefinition.builder;
}
