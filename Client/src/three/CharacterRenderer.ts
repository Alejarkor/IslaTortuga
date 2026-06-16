import { Engine } from "@babylonjs/core/Engines/engine";
import { Scene } from "@babylonjs/core/scene";
import { ArcRotateCamera } from "@babylonjs/core/Cameras/arcRotateCamera";
import { HemisphericLight } from "@babylonjs/core/Lights/hemisphericLight";
import { DirectionalLight } from "@babylonjs/core/Lights/directionalLight";
import { Vector3 } from "@babylonjs/core/Maths/math.vector";
import { Color3, Color4 } from "@babylonjs/core/Maths/math.color";
import { SceneLoader } from "@babylonjs/core/Loading/sceneLoader";
import type { ShaderMaterial } from "@babylonjs/core/Materials/shaderMaterial";
import type { Material } from "@babylonjs/core/Materials/material";
import type { AbstractMesh } from "@babylonjs/core/Meshes/abstractMesh";
import { TransformNode } from "@babylonjs/core/Meshes/transformNode";
import { Mesh } from "@babylonjs/core/Meshes/mesh";
import type { Bone } from "@babylonjs/core/Bones/bone";
import type { AnimationGroup } from "@babylonjs/core/Animations/animationGroup";

// Registra el loader glTF/GLB (efecto secundario).
import "@babylonjs/loaders/glTF";

import { hexToColor3 } from "./colorUtils";
import { createMaskColorMaterial, setMaskTint } from "./maskColorMaterial";
import {
  buildProceduralCharacter,
  type ProceduralCharacter
} from "./proceduralCharacter";
import { CHARACTER_COLOR_SLOTS, NO_HAIR_ID } from "@/config/characterColorSlots";
import type { Appearance } from "@/domain/appearance/appearanceSchema";
import type { BodyAssets } from "@/domain/character/customizationOptions";

export type RendererErrorHandler = (
  scope: "body" | "hair",
  detail: string,
  error: unknown
) => void;

/** Patrón para identificar cada estilo de pelo (nodo Pelo1..Pelo8). */
const HAIR_NODE_REGEX = /pelo\s*_?\s*\d+/i;

/** Nombre de la animación idle a reproducir en bucle. */
const IDLE_ANIM_REGEX = /idle/i;

/** Codifica espacios u otros caracteres de la ruta sin doble-codificar. */
function safeUrl(url: string): string {
  return /%[0-9A-Fa-f]{2}/.test(url) ? url : encodeURI(url);
}

/** Aplica un color a un material PBR (albedoColor) o estándar (diffuseColor). */
function tintMaterial(material: Material, color: Color3): void {
  const anyMat = material as unknown as {
    albedoColor?: Color3;
    diffuseColor?: Color3;
  };
  if (anyMat.albedoColor) anyMat.albedoColor = color;
  else if (anyMat.diffuseColor) anyMat.diffuseColor = color;
}

/**
 * Renderizador del personaje sobre un <canvas>.
 *
 * Adaptado a los assets reales del proyecto:
 *  - Cuerpo: un GLB ("IT_Character") con sus propios materiales. Si el manifest
 *    aporta una máscara RGBA se usa el shader de máscara (5 zonas); si no, se
 *    tiñen directamente los materiales del GLB (tinte adaptativo).
 *  - Pelo: un único GLB ("Pelos") con 8 nodos (Pelo1..Pelo8). Se carga una vez
 *    y se alterna la visibilidad del estilo seleccionado.
 *  - Sin assets (manifest no publicado): maniquí procedural de respaldo (CA-07).
 */
export class CharacterRenderer {
  private readonly engine: Engine;
  private readonly scene: Scene;
  private readonly camera: ArcRotateCamera;
  private readonly onError?: RendererErrorHandler;
  private readonly onHairDiscovered?: (hairIds: string[]) => void;

  private bodyAssets: BodyAssets = {
    modelUrl: null,
    maskUrl: null,
    baseColorUrl: null
  };
  private hairPackUrl: string | null = null;

  private bodyMaskMaterial: ShaderMaterial | null = null;
  private bodyMaterials: Material[] = [];
  private procedural: ProceduralCharacter | null = null;
  private bodyRoot: AbstractMesh | TransformNode | null = null;

  private hairPackRoot: AbstractMesh | null = null;
  private hairGroups = new Map<string, AbstractMesh[]>();
  private currentHairId: string = NO_HAIR_ID;

  private idleGroup: AnimationGroup | null = null;
  private animationGroups: AnimationGroup[] = [];

  // Anclaje del pelo al hueso de la cabeza (se mueve con el jugador).
  private headBone: Bone | null = null;
  private bodyAffectedMesh: AbstractMesh | null = null;
  private hairHolder: TransformNode | null = null;

  private lastAppearance: Appearance | null = null;
  private loadToken = 0;

  constructor(
    canvas: HTMLCanvasElement,
    options?: {
      onError?: RendererErrorHandler;
      onHairDiscovered?: (hairIds: string[]) => void;
    }
  ) {
    this.onError = options?.onError;
    this.onHairDiscovered = options?.onHairDiscovered;

    this.engine = new Engine(canvas, true, {
      preserveDrawingBuffer: true,
      stencil: true
    });
    this.scene = new Scene(this.engine);
    this.scene.clearColor = new Color4(0.09, 0.1, 0.13, 1);

    this.camera = new ArcRotateCamera(
      "camera",
      Math.PI / 2,
      Math.PI / 2.3,
      4.6,
      new Vector3(0, 1.1, 0),
      this.scene
    );
    this.camera.attachControl(canvas, true);
    this.camera.minZ = 0.05;
    // Sin zoom: se quita la rueda del ratón y el pellizco táctil.
    this.camera.inputs.removeByType("ArcRotateCameraMouseWheelInput");
    const pointers = this.camera.inputs.attached["pointers"] as
      | { pinchZoom?: boolean; multiTouchPanAndZoom?: boolean }
      | undefined;
    if (pointers) {
      pointers.pinchZoom = false;
      pointers.multiTouchPanAndZoom = false;
    }
    // Solo rotación sobre el eje vertical (alpha). Inclinación (beta) y
    // paneo bloqueados; el radio se fija en frameCamera (sin zoom).
    const FIXED_BETA = Math.PI / 2.35;
    this.camera.beta = FIXED_BETA;
    this.camera.lowerBetaLimit = FIXED_BETA;
    this.camera.upperBetaLimit = FIXED_BETA;
    this.camera.panningSensibility = 0;

    const hemi = new HemisphericLight("hemi", new Vector3(0, 1, 0), this.scene);
    hemi.intensity = 0.9;

    const dir = new DirectionalLight(
      "dir",
      new Vector3(-0.4, -1, -0.6),
      this.scene
    );
    dir.intensity = 0.7;

    this.engine.runRenderLoop(() => this.scene.render());
  }

  resize(): void {
    this.engine.resize();
  }

  /** Gira la cámara sobre el eje vertical (rotación horizontal). */
  rotate(deltaRadians: number): void {
    this.camera.alpha += deltaRadians;
  }

  /** Define los assets y reconstruye cuerpo + pelo. */
  setCustomization(body: BodyAssets, hairPackUrl: string | null): void {
    this.bodyAssets = body;
    this.hairPackUrl = hairPackUrl;
    void this.rebuild();
  }

  private async rebuild(): Promise<void> {
    const token = ++this.loadToken;
    this.disposeAll();

    if (this.bodyAssets.modelUrl) {
      await this.buildBodyFromGlb(this.bodyAssets.modelUrl, token);
    } else {
      this.buildProceduralBody();
    }

    if (token !== this.loadToken) return;

    if (this.hairPackUrl) {
      await this.loadHairPack(this.hairPackUrl, token);
    } else {
      // Sin pack real: solo "Sin pelo" disponible.
      this.onHairDiscovered?.([]);
    }

    if (token !== this.loadToken) return;
    if (this.lastAppearance) this.applyAppearance(this.lastAppearance);

    // El pelo ya está anclado con el esqueleto en reposo: ahora sí arrancamos
    // la animación idle, que moverá cabeza+pelo juntos.
    this.playIdle(this.animationGroups);
  }

  private buildProceduralBody(): void {
    this.procedural = buildProceduralCharacter(this.scene);
    this.bodyRoot = this.procedural.root;
    this.bodyMaskMaterial = null;
    this.bodyMaterials = Object.values(this.procedural.materialsBySlot);
  }

  private async buildBodyFromGlb(url: string, token: number): Promise<void> {
    try {
      const result = await SceneLoader.ImportMeshAsync(
        "",
        "",
        safeUrl(url),
        this.scene
      );
      if (token !== this.loadToken) {
        result.meshes.forEach((m) => m.dispose());
        return;
      }

      this.bodyRoot = result.meshes[0] ?? null;

      // El GLB ya trae un único personaje; se muestran todas sus mallas.
      const visibleMeshes = result.meshes.filter((m) => m.name !== "__root__");

      // Malla con esqueleto (la del cuerpo) para anclar el pelo al hueso Head.
      this.bodyAffectedMesh =
        visibleMeshes.find((m) => m.skeleton) ??
        visibleMeshes[0] ??
        result.meshes[0] ??
        null;
      const skeleton = result.skeletons[0];
      this.headBone =
        skeleton?.bones.find(
          (b) => /(^|:)head$/i.test(b.name) && !/top|end/i.test(b.name)
        ) ?? null;

      if (this.bodyAssets.maskUrl) {
        // Modo máscara RGBA: material de shader sobre las mallas visibles.
        const material = createMaskColorMaterial(this.scene, {
          baseColorUrl: this.bodyAssets.baseColorUrl,
          maskUrl: this.bodyAssets.maskUrl
        });
        visibleMeshes.forEach((m) => (m.material = material));
        this.bodyMaskMaterial = material;
        this.bodyMaterials = [];
      } else {
        // Sin máscara: conservar materiales nativos del GLB y teñirlos.
        this.bodyMaskMaterial = null;
        const mats = new Set<Material>();
        for (const mesh of visibleMeshes) {
          if (mesh.material) mats.add(mesh.material);
        }
        this.bodyMaterials = Array.from(mats).sort((a, b) =>
          a.name.localeCompare(b.name)
        );
      }

      // Guardamos las animaciones, pero el idle se arranca DESPUÉS de anclar el
      // pelo. Si lo arrancáramos ahora, el hueso Head se movería a la pose idle
      // y el pelo (autorado en pose de reposo) quedaría descolocado (en el
      // suelo). Lo dejamos parado para anclar en bind pose.
      this.animationGroups = result.animationGroups ?? [];
      this.animationGroups.forEach((g) => g.stop());

      this.frameCamera(visibleMeshes.length ? visibleMeshes : result.meshes);
    } catch (error) {
      this.onError?.("body", url, error);
      console.error("[CharacterRenderer] Error cargando cuerpo:", url, error);
      this.buildProceduralBody();
    }
  }

  /** Carga el pack de pelo y descubre los estilos (nodos Pelo1..Pelo8). */
  private async loadHairPack(url: string, token: number): Promise<void> {
    try {
      const result = await SceneLoader.ImportMeshAsync(
        "",
        "",
        safeUrl(url),
        this.scene
      );
      if (token !== this.loadToken) {
        result.meshes.forEach((m) => m.dispose());
        return;
      }

      this.hairPackRoot = result.meshes[0] ?? null;
      this.hairGroups = groupHairMeshes(result.meshes);

      // Ocultar todos los estilos hasta que se seleccione uno.
      for (const meshes of this.hairGroups.values()) {
        meshes.forEach((m) => m.setEnabled(false));
      }

      // Anclar el pelo al hueso de la cabeza para que se mueva con el jugador.
      this.attachHairToHead();

      const ids = Array.from(this.hairGroups.keys()).sort((a, b) =>
        a.localeCompare(b, undefined, { numeric: true })
      );
      this.onHairDiscovered?.(ids);
    } catch (error) {
      this.onError?.("hair", url, error);
      console.error("[CharacterRenderer] Error cargando pelo:", url, error);
      this.onHairDiscovered?.([]);
    }
  }

  /** Aplica una apariencia completa: colores + pelo seleccionado. */
  applyAppearance(appearance: Appearance): void {
    this.lastAppearance = appearance;
    this.applyHair(appearance.hair_id);
    this.applyColors(appearance);
  }

  /** Muestra el estilo de pelo seleccionado y oculta el resto. */
  applyHair(hairId: string): void {
    this.currentHairId = hairId;

    // Maniquí procedural: alternar su casquete.
    if (this.procedural) {
      const visible = hairId !== NO_HAIR_ID;
      this.procedural.hairMeshes.forEach((m) => m.setEnabled(visible));
      return;
    }

    for (const [id, meshes] of this.hairGroups.entries()) {
      const visible = id === hairId;
      meshes.forEach((m) => m.setEnabled(visible));
    }
  }

  /** Aplica los colores (preview inmediato). */
  applyColors(appearance: Appearance): void {
    for (const slot of CHARACTER_COLOR_SLOTS) {
      const hex = appearance.colors[slot.id];
      if (!hex) continue;
      const color = hexToColor3(hex);

      if (slot.target.material === "hair") {
        this.setHairColor(color);
        continue;
      }

      // Slot del cuerpo.
      if (this.bodyMaskMaterial) {
        setMaskTint(this.bodyMaskMaterial, slot.target.channel, color);
      }
    }

    // Sin máscara: tinte adaptativo de los materiales nativos del cuerpo.
    if (!this.bodyMaskMaterial && !this.procedural) {
      this.applyAdaptiveBodyTint(appearance);
    } else if (this.procedural) {
      this.applyProceduralBodyTint(appearance);
    }
  }

  /**
   * Tiñe los materiales del cuerpo SIN textura base (planos), mapeando los
   * slots de cuerpo en orden. Los materiales con textura (el pintado del
   * modelo) se respetan para no ensuciarlos. El color por zonas completo
   * llegará con la textura máscara RGBA (usage=body_mask).
   */
  private applyAdaptiveBodyTint(appearance: Appearance): void {
    const bodySlots = CHARACTER_COLOR_SLOTS.filter(
      (slot) => slot.target.material === "body"
    );
    const flatMaterials = this.bodyMaterials.filter(
      (mat) => !materialHasBaseTexture(mat)
    );
    flatMaterials.forEach((mat, index) => {
      const slot = bodySlots[index];
      if (!slot) return;
      const hex = appearance.colors[slot.id];
      if (hex) tintMaterial(mat, hexToColor3(hex));
    });
  }

  private applyProceduralBodyTint(appearance: Appearance): void {
    if (!this.procedural) return;
    for (const slot of CHARACTER_COLOR_SLOTS) {
      if (slot.target.material !== "body") continue;
      const mat = this.procedural.materialsBySlot[slot.id];
      const hex = appearance.colors[slot.id];
      if (mat && hex) mat.diffuseColor = hexToColor3(hex);
    }
  }

  private setHairColor(color: Color3): void {
    if (this.procedural) {
      this.procedural.hairMaterial.diffuseColor = color;
      return;
    }
    // Teñir el material del estilo de pelo visible.
    const meshes = this.hairGroups.get(this.currentHairId);
    if (!meshes) return;
    for (const mesh of meshes) {
      if (mesh.material) tintMaterial(mesh.material, color);
    }
  }

  /** Ajusta la cámara para encuadrar el cuerpo cargado. */
  private frameCamera(meshes: AbstractMesh[]): void {
    const root = meshes[0];
    if (!root) return;
    const { min, max } = root.getHierarchyBoundingVectors(true);
    const size = max.subtract(min);
    const radius = Math.max(size.x, size.y, size.z);

    // Apuntar hacia la cabeza/pecho alto (no al centro/pies): el personaje
    // queda algo más abajo en el encuadre y la cabeza protagoniza.
    const targetX = (min.x + max.x) / 2;
    const targetZ = (min.z + max.z) / 2;
    // Apuntar mas arriba baja al personaje dentro del visor (~30% mas abajo).
    const targetY = min.y + size.y * 1.2;
    const dist = radius * 1.9 + 0.4;
    this.camera.setTarget(new Vector3(targetX, targetY, targetZ));
    this.camera.radius = dist;
    // Radio fijo: sin zoom.
    this.camera.lowerRadiusLimit = dist;
    this.camera.upperRadiusLimit = dist;
  }

  /** Reproduce la animación idle en bucle, deteniendo el resto. */
  private playIdle(groups: AnimationGroup[] | undefined): void {
    if (!groups || groups.length === 0) return;
    groups.forEach((g) => g.stop());
    const idle = groups.find((g) => IDLE_ANIM_REGEX.test(g.name)) ?? groups[0];
    idle.start(true, 1.0, idle.from, idle.to, false);
    this.idleGroup = idle;
  }

  /**
   * Cuelga el pack de pelo del hueso "Head" del esqueleto, compensando la
   * pose de reposo para que en reposo quede en su sitio autorado y, en
   * animación, siga el movimiento de la cabeza. Si no hay hueso, no hace nada.
   */
  private attachHairToHead(): void {
    if (!this.headBone || !this.bodyAffectedMesh || !this.hairPackRoot) {
      return;
    }
    // Anclamos con el esqueleto en pose de reposo (sin idle aún). Forzamos el
    // cálculo de matrices del cuerpo y del esqueleto para que el hueso Head
    // esté en su sitio (bind) y el pelo case con su posición autorada.
    this.bodyAffectedMesh.computeWorldMatrix(true);
    this.bodyAffectedMesh.skeleton?.prepare();
    // Holder (malla vacía) anclado al hueso de la cabeza.
    const holder = new Mesh("hairHolder", this.scene);
    holder.attachToBone(this.headBone, this.bodyAffectedMesh);
    // setParent PRESERVA la posición mundial autorada del pelo y, a la vez,
    // lo hace seguir al hueso (robusto ante la escala/rotación del rig).
    this.hairPackRoot.setParent(holder);
    this.hairHolder = holder;
  }

  private disposeHair(): void {
    if (this.hairHolder) {
      this.hairHolder.dispose(); // arrastra el pack y sus mallas
      this.hairHolder = null;
      this.hairPackRoot = null;
    } else if (this.hairPackRoot) {
      this.hairPackRoot.dispose();
      this.hairPackRoot = null;
    }
    this.hairGroups.clear();
    this.currentHairId = NO_HAIR_ID;
  }

  private disposeAll(): void {
    this.disposeHair();

    this.idleGroup?.stop();
    this.idleGroup = null;
    this.animationGroups = [];
    this.headBone = null;
    this.bodyAffectedMesh = null;

    if (this.procedural) {
      this.procedural.dispose();
      this.procedural = null;
    }
    if (this.bodyRoot) {
      this.bodyRoot.dispose();
      this.bodyRoot = null;
    }
    if (this.bodyMaskMaterial) {
      this.bodyMaskMaterial.dispose();
      this.bodyMaskMaterial = null;
    }
    this.bodyMaterials = [];
  }

  dispose(): void {
    this.disposeAll();
    this.scene.dispose();
    this.engine.dispose();
  }
}

/** Indica si un material PBR tiene textura base (albedo/diffuse). */
function materialHasBaseTexture(material: Material): boolean {
  const anyMat = material as unknown as {
    albedoTexture?: unknown;
    diffuseTexture?: unknown;
  };
  return Boolean(anyMat.albedoTexture || anyMat.diffuseTexture);
}

/**
 * Agrupa las mallas importadas del pack por estilo de pelo.
 * El id es el ancestro (o la propia malla) cuyo nombre casa con "PeloN".
 */
function groupHairMeshes(meshes: AbstractMesh[]): Map<string, AbstractMesh[]> {
  const groups = new Map<string, AbstractMesh[]>();

  for (const mesh of meshes) {
    if (!mesh.name || mesh.name === "__root__") continue;
    const id = hairIdForNode(mesh);
    if (!id) continue;
    const list = groups.get(id) ?? [];
    list.push(mesh);
    groups.set(id, list);
  }

  return groups;
}

/** Busca, subiendo por la jerarquía, el primer nombre tipo "PeloN". */
function hairIdForNode(node: AbstractMesh): string | null {
  let current: AbstractMesh | TransformNode | null = node;
  while (current) {
    const match = current.name?.match(HAIR_NODE_REGEX);
    if (match) return match[0].replace(/\s|_/g, "");
    current = current.parent as AbstractMesh | TransformNode | null;
  }
  return null;
}
