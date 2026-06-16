import { Effect } from "@babylonjs/core/Materials/effect";
import { ShaderMaterial } from "@babylonjs/core/Materials/shaderMaterial";
import { Texture } from "@babylonjs/core/Materials/Textures/texture";
import { Color3 } from "@babylonjs/core/Maths/math.color";
import { Vector3 } from "@babylonjs/core/Maths/math.vector";
import type { Scene } from "@babylonjs/core/scene";

const SHADER_NAME = "characterMask";

let shadersRegistered = false;

/**
 * Registra (una sola vez) los shaders del material de máscara.
 *
 * El material tiñe la textura base del cuerpo usando una máscara RGBA: cada
 * canal (R, G, B, A) delimita una zona que se multiplica por su color de tinte.
 * Las zonas no cubiertas por ningún canal conservan el color base sin teñir.
 * Se añade iluminación difusa simple (hemisférica) para dar volumen.
 */
function registerShaders(): void {
  if (shadersRegistered) return;

  Effect.ShadersStore[`${SHADER_NAME}VertexShader`] = `
    precision highp float;
    attribute vec3 position;
    attribute vec3 normal;
    attribute vec2 uv;
    uniform mat4 world;
    uniform mat4 worldViewProjection;
    varying vec2 vUV;
    varying vec3 vNormalW;
    void main(void) {
      vec4 worldPos = world * vec4(position, 1.0);
      gl_Position = worldViewProjection * vec4(position, 1.0);
      vUV = uv;
      vNormalW = normalize(mat3(world) * normal);
    }
  `;

  Effect.ShadersStore[`${SHADER_NAME}FragmentShader`] = `
    precision highp float;
    varying vec2 vUV;
    varying vec3 vNormalW;

    uniform sampler2D baseTex;
    uniform sampler2D maskTex;
    uniform float hasBaseTex;
    uniform float hasMaskTex;

    uniform vec3 tintR;
    uniform vec3 tintG;
    uniform vec3 tintB;
    uniform vec3 tintA;
    uniform vec3 lightDir;
    uniform float ambient;

    void main(void) {
      vec3 base = vec3(1.0);
      if (hasBaseTex > 0.5) {
        base = texture2D(baseTex, vUV).rgb;
      }

      vec3 tint = vec3(1.0);
      if (hasMaskTex > 0.5) {
        vec4 m = texture2D(maskTex, vUV);
        float wBase = clamp(1.0 - (m.r + m.g + m.b + m.a), 0.0, 1.0);
        tint = wBase * vec3(1.0)
             + m.r * tintR
             + m.g * tintG
             + m.b * tintB
             + m.a * tintA;
      }

      vec3 albedo = base * tint;

      // Iluminación difusa simple + ambiente para dar volumen.
      float ndl = max(dot(normalize(vNormalW), normalize(-lightDir)), 0.0);
      float lighting = ambient + (1.0 - ambient) * ndl;

      gl_FragColor = vec4(albedo * lighting, 1.0);
    }
  `;

  shadersRegistered = true;
}

export type MaskMaterialTextures = {
  baseColorUrl: string | null;
  maskUrl: string | null;
};

/**
 * Crea el ShaderMaterial de máscara para el cuerpo.
 * Si no hay texturas, el material sigue siendo válido (base blanca teñida).
 */
export function createMaskColorMaterial(
  scene: Scene,
  textures: MaskMaterialTextures
): ShaderMaterial {
  registerShaders();

  const material = new ShaderMaterial(`${SHADER_NAME}Mat`, scene, SHADER_NAME, {
    attributes: ["position", "normal", "uv"],
    uniforms: [
      "world",
      "worldViewProjection",
      "tintR",
      "tintG",
      "tintB",
      "tintA",
      "lightDir",
      "ambient",
      "hasBaseTex",
      "hasMaskTex"
    ],
    samplers: ["baseTex", "maskTex"]
  });

  material.setVector3("lightDir", new Vector3(-0.4, -1.0, -0.6));
  material.setFloat("ambient", 0.45);
  material.setFloat("hasBaseTex", 0);
  material.setFloat("hasMaskTex", 0);

  if (textures.baseColorUrl) {
    const baseTex = new Texture(textures.baseColorUrl, scene);
    material.setTexture("baseTex", baseTex);
    material.setFloat("hasBaseTex", 1);
  }

  if (textures.maskUrl) {
    const maskTex = new Texture(textures.maskUrl, scene);
    material.setTexture("maskTex", maskTex);
    material.setFloat("hasMaskTex", 1);
  }

  // Valores por defecto neutros; se sobreescriben con setTint.
  const white = new Color3(1, 1, 1);
  material.setColor3("tintR", white);
  material.setColor3("tintG", white);
  material.setColor3("tintB", white);
  material.setColor3("tintA", white);

  return material;
}

/** Actualiza el color de tinte de un canal de la máscara en caliente. */
export function setMaskTint(
  material: ShaderMaterial,
  channel: "r" | "g" | "b" | "a",
  color: Color3
): void {
  const uniform =
    channel === "r"
      ? "tintR"
      : channel === "g"
        ? "tintG"
        : channel === "b"
          ? "tintB"
          : "tintA";
  material.setColor3(uniform, color);
}
