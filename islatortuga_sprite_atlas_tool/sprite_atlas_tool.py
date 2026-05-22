#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
IslaTortuga Sprite Atlas Tool v1.2.0

Herramienta GUI/CLI para:
- Importar ZIPs o carpetas con PNGs exportados por generadores de animación.
- Recortar transparencias y normalizar frames a un tamaño fijo.
- Generar direcciones faltantes por espejo horizontal automático.
- Montar atlas PNG + JSON compatible con Phaser (load.atlas).
- Generar JS de animaciones para Phaser.
- Copiar los archivos generados directamente a las carpetas del juego.

Dependencias:
    pip install pillow
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import traceback
import zipfile
from dataclasses import dataclass, asdict, field
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple
import io

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError as exc:
    raise SystemExit(
        "Falta Pillow. Instálalo con:\n\n"
        "    pip install pillow\n"
    ) from exc


VERSION = "1.2.0"

# Pares de dirección para el espejo automático.
# Si existe la izquierda pero no la derecha (o viceversa), se genera por flip.
MIRROR_PAIRS: List[Tuple[str, str]] = [
    ("right",      "left"),
    ("up_right",   "up_left"),
    ("down_right", "down_left"),
]


# ---------------------------------------------------------------------------
# Configuración
# ---------------------------------------------------------------------------

@dataclass
class ExportConfig:
    input_path: str = ""
    output_dir: str = "export"
    asset_name: str = "player"

    frame_width: int = 32
    frame_height: int = 64

    scale_mode: str = "fit"   # fit | pad | crop
    anchor: str = "center"    # center | bottom_center

    alpha_threshold: int = 1
    inner_padding: int = 0
    atlas_padding: int = 2
    max_atlas_width: int = 2048

    export_normalized_frames: bool = True
    export_preview_sheet: bool = True
    include_rotations_as_poses: bool = False
    mirror_missing_directions: bool = True

    allow_scale_up: bool = False
    offset_x: int = 0
    offset_y: int = 0

    # Si se especifican, los archivos generados se copian aquí también.
    # game_assets_dir → player_atlas.png + player_atlas.json
    # game_src_dir    → player_animations.js
    game_assets_dir: str = ""
    game_src_dir: str = ""

    direction_aliases: Dict[str, str] = field(default_factory=lambda: {
        "south": "down", "north": "up",
        "east": "right", "west": "left",
        "south-east": "down_right", "south-west": "down_left",
        "north-east": "up_right",  "north-west": "up_left",
        "down": "down", "up": "up", "right": "right", "left": "left",
        "down_right": "down_right", "down_left": "down_left",
        "up_right": "up_right",    "up_left": "up_left",
    })

    animation_alias_rules: List[Tuple[str, str]] = field(default_factory=lambda: [
        (r"(?i).*idle.*",    "idle"),
        (r"(?i).*breath.*",  "idle"),
        (r"(?i).*walk.*",    "walk"),
        (r"(?i).*run.*",     "walk"),
        (r"(?i).*move.*",    "walk"),
        (r"(?i)^animation.*","walk"),
    ])

    animation_frame_rates: Dict[str, int] = field(default_factory=lambda: {
        "idle": 4,
        "walk": 24,
    })

    default_frame_rate: int = 6
    repeat: int = -1

    @classmethod
    def from_file(cls, path: str | Path) -> "ExportConfig":
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        cfg = cls()
        for k, v in data.items():
            if hasattr(cfg, k):
                setattr(cfg, k, v)
        cfg.animation_alias_rules = [tuple(x) for x in cfg.animation_alias_rules]
        return cfg

    def save(self, path: str | Path) -> None:
        Path(path).write_text(
            json.dumps(asdict(self), indent=2, ensure_ascii=False),
            encoding="utf-8",
        )


@dataclass
class SourceFrame:
    source_path: Path
    raw_animation: str
    animation: str
    raw_direction: str
    direction: str
    index: int
    output_name: str


@dataclass
class NormalizedFrame:
    name: str
    image: Image.Image
    source_path: Path
    raw_animation: str
    animation: str
    raw_direction: str
    direction: str
    index: int
    original_size: Tuple[int, int]
    bbox: Optional[Tuple[int, int, int, int]]
    warning: Optional[str] = None


# ---------------------------------------------------------------------------
# Core
# ---------------------------------------------------------------------------

def slugify(value: str, fallback: str = "item") -> str:
    value = value.strip().replace("\\", "/").split("/")[-1]
    value = re.sub(r"\.[A-Za-z0-9]+$", "", value)
    value = re.sub(r"-[0-9a-fA-F]{6,}$", "", value)
    value = value.lower().replace(" ", "_").replace("-", "_")
    value = re.sub(r"[^a-z0-9_]+", "_", value)
    value = re.sub(r"_+", "_", value).strip("_")
    return value or fallback


def parse_frame_index(path: Path) -> int:
    nums = re.findall(r"(\d+)", path.stem)
    return int(nums[-1]) if nums else 0


def apply_alias_rules(raw_animation: str, cfg: ExportConfig) -> str:
    for pattern, alias in cfg.animation_alias_rules:
        try:
            if re.match(pattern, raw_animation):
                return slugify(alias, "anim")
        except re.error:
            continue
    return slugify(raw_animation, "anim")


def map_direction(raw_direction: str, cfg: ExportConfig) -> str:
    key = raw_direction.lower().replace("_", "-")
    # Normaliza separadores dobles
    key = re.sub(r"_+", "-", raw_direction.lower()).strip("-")
    return cfg.direction_aliases.get(key, slugify(raw_direction, "dir"))


def log_noop(message: str) -> None:
    pass


def safe_mkdir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def extract_input(input_path: Path, work_dir: Path, log: Callable[[str], None]) -> Path:
    if input_path.is_dir():
        return input_path
    if not input_path.exists():
        raise FileNotFoundError(f"No existe el input: {input_path}")
    if input_path.suffix.lower() != ".zip":
        raise ValueError("El input debe ser una carpeta o un archivo .zip")
    extract_dir = work_dir / "extracted"
    safe_mkdir(extract_dir)
    log(f"Extrayendo ZIP: {input_path.name}")
    with zipfile.ZipFile(input_path, "r") as zf:
        zf.extractall(extract_dir)
    return extract_dir


def discover_pngs(root: Path) -> List[Path]:
    return sorted([p for p in root.rglob("*") if p.is_file() and p.suffix.lower() == ".png"])


def detect_frames(
    root: Path, cfg: ExportConfig, log: Callable[[str], None] = log_noop
) -> List[SourceFrame]:
    pngs = discover_pngs(root)
    frames: List[SourceFrame] = []

    for p in pngs:
        rel_parts = p.relative_to(root).parts
        lower_parts = [x.lower() for x in rel_parts]
        raw_anim = ""
        raw_dir = ""

        if "animations" in lower_parts:
            i = lower_parts.index("animations")
            if len(rel_parts) > i + 2:
                raw_anim = rel_parts[i + 1]
                raw_dir = rel_parts[i + 2]
            else:
                raw_anim = p.parent.name
                raw_dir = "default"
        elif "rotations" in lower_parts:
            if not cfg.include_rotations_as_poses:
                continue
            i = lower_parts.index("rotations")
            raw_anim = "pose"
            raw_dir = p.stem if len(rel_parts) > i + 1 else "default"
        else:
            raw_anim = p.parent.parent.name if p.parent.parent != p.parent else p.parent.name
            raw_dir = p.parent.name

        animation = apply_alias_rules(raw_anim, cfg)
        direction = map_direction(raw_dir, cfg)
        idx = parse_frame_index(p)
        name = f"{animation}_{direction}_{idx:03d}.png"

        frames.append(SourceFrame(
            source_path=p,
            raw_animation=raw_anim, animation=animation,
            raw_direction=raw_dir,  direction=direction,
            index=idx, output_name=name,
        ))

    # Resolver colisiones de nombre
    used: Dict[str, int] = {}
    for f in frames:
        if f.output_name not in used:
            used[f.output_name] = 0
            continue
        used[f.output_name] += 1
        f.output_name = f"{f.output_name[:-4]}_{used[f.output_name]}.png"

    frames.sort(key=lambda f: (f.animation, f.direction, f.index, str(f.source_path)))
    log(f"Detectados {len(frames)} frames PNG.")
    return frames


def alpha_bbox(img: Image.Image, threshold: int) -> Optional[Tuple[int, int, int, int]]:
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    alpha = img.getchannel("A")
    if threshold <= 0:
        return alpha.getbbox()
    return alpha.point(lambda a: 255 if a >= threshold else 0).getbbox()


def paste_with_clipping(canvas: Image.Image, sprite: Image.Image, x: int, y: int) -> None:
    cw, ch = canvas.size
    sw, sh = sprite.size
    left, top = max(0, x), max(0, y)
    right, bottom = min(cw, x + sw), min(ch, y + sh)
    if right <= left or bottom <= top:
        return
    part = sprite.crop((left - x, top - y, left - x + right - left, top - y + bottom - top))
    canvas.alpha_composite(part, (left, top))


def normalize_image(
    img: Image.Image, cfg: ExportConfig
) -> Tuple[Image.Image, Optional[Tuple[int, int, int, int]], Optional[str]]:
    img = img.convert("RGBA")
    fw, fh = cfg.frame_width, cfg.frame_height
    if fw <= 0 or fh <= 0:
        raise ValueError("frame_width y frame_height deben ser > 0")

    bbox = alpha_bbox(img, cfg.alpha_threshold)
    canvas = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
    warning = None

    if bbox is None:
        return canvas, None, "Frame vacío."

    visible = img.crop(bbox)
    vw, vh = visible.size
    tmax_w = max(1, fw - cfg.inner_padding * 2)
    tmax_h = max(1, fh - cfg.inner_padding * 2)
    mode = cfg.scale_mode.lower().strip()
    if mode not in {"fit", "pad", "crop"}:
        mode = "fit"

    if mode == "fit":
        scale = min(tmax_w / vw, tmax_h / vh)
        if not cfg.allow_scale_up:
            scale = min(1.0, scale)
        nw = max(1, int(round(vw * scale)))
        nh = max(1, int(round(vh * scale)))
        if (nw, nh) != visible.size:
            visible = visible.resize((nw, nh), Image.Resampling.NEAREST)
        if scale < 1.0:
            warning = f"Escalado {scale:.2f}x"
    elif mode == "crop":
        cx, cy = vw // 2, vh // 2
        l = max(0, cx - fw // 2);  t = max(0, cy - fh // 2)
        r = min(vw, l + fw);       b = min(vh, t + fh)
        l = max(0, r - fw);        t = max(0, b - fh)
        visible = visible.crop((l, t, r, b))
        if vw > fw or vh > fh:
            warning = f"Recortado {vw}x{vh}→{visible.size[0]}x{visible.size[1]}"
    elif mode == "pad":
        if vw > fw or vh > fh:
            warning = f"Contenido {vw}x{vh} no cabe en {fw}x{fh}"

    sw, sh = visible.size
    if cfg.anchor == "bottom_center":
        x = (fw - sw) // 2 + cfg.offset_x
        y = fh - sh - cfg.inner_padding + cfg.offset_y
    else:
        x = (fw - sw) // 2 + cfg.offset_x
        y = (fh - sh) // 2 + cfg.offset_y

    paste_with_clipping(canvas, visible, x, y)
    return canvas, bbox, warning


def normalize_frames(
    frames: List[SourceFrame], cfg: ExportConfig, log: Callable[[str], None] = log_noop
) -> List[NormalizedFrame]:
    out: List[NormalizedFrame] = []
    for i, f in enumerate(frames):
        with Image.open(f.source_path) as im:
            original_size = im.size
            normalized, bbox, warning = normalize_image(im, cfg)
        out.append(NormalizedFrame(
            name=f.output_name, image=normalized,
            source_path=f.source_path,
            raw_animation=f.raw_animation, animation=f.animation,
            raw_direction=f.raw_direction, direction=f.direction,
            index=f.index, original_size=original_size,
            bbox=bbox, warning=warning,
        ))
        if (i + 1) % 10 == 0:
            log(f"  Normalizados {i + 1}/{len(frames)} frames...")
    return out


def add_mirrored_frames(
    frames: List[NormalizedFrame], cfg: ExportConfig, log: Callable[[str], None] = log_noop
) -> List[NormalizedFrame]:
    """
    Para cada par de dirección (ej. right ↔ left), si una existe y la otra no,
    genera la faltante aplicando flip horizontal.
    """
    if not cfg.mirror_missing_directions:
        return frames

    # Construir índice de qué (anim, dir) tenemos
    existing_dirs: Dict[str, set] = {}
    groups: Dict[Tuple[str, str], List[NormalizedFrame]] = {}
    for fr in frames:
        existing_dirs.setdefault(fr.animation, set()).add(fr.direction)
        groups.setdefault((fr.animation, fr.direction), []).append(fr)

    new_frames: List[NormalizedFrame] = []

    for src_dir, dst_dir in MIRROR_PAIRS:
        for anim, dirs in existing_dirs.items():
            # Generar dst si tiene src pero no dst
            if src_dir in dirs and dst_dir not in dirs:
                src_list = sorted(groups[(anim, src_dir)], key=lambda f: f.index)
                log(f"  Espejo: {anim}-{dst_dir} ← {src_dir} ({len(src_list)} frames)")
                for fr in src_list:
                    mirrored = fr.image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    # Renombrar: walk_right_000.png → walk_left_000.png
                    new_name = re.sub(
                        rf"_{re.escape(src_dir)}_",
                        f"_{dst_dir}_",
                        fr.name,
                    )
                    new_frames.append(NormalizedFrame(
                        name=new_name, image=mirrored,
                        source_path=fr.source_path,
                        raw_animation=fr.raw_animation, animation=anim,
                        raw_direction=dst_dir, direction=dst_dir,
                        index=fr.index, original_size=fr.original_size,
                        bbox=fr.bbox, warning=f"Espejo de {src_dir}",
                    ))
            # También generar src si tiene dst pero no src
            elif dst_dir in dirs and src_dir not in dirs:
                dst_list = sorted(groups[(anim, dst_dir)], key=lambda f: f.index)
                log(f"  Espejo: {anim}-{src_dir} ← {dst_dir} ({len(dst_list)} frames)")
                for fr in dst_list:
                    mirrored = fr.image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    new_name = re.sub(
                        rf"_{re.escape(dst_dir)}_",
                        f"_{src_dir}_",
                        fr.name,
                    )
                    new_frames.append(NormalizedFrame(
                        name=new_name, image=mirrored,
                        source_path=fr.source_path,
                        raw_animation=fr.raw_animation, animation=anim,
                        raw_direction=src_dir, direction=src_dir,
                        index=fr.index, original_size=fr.original_size,
                        bbox=fr.bbox, warning=f"Espejo de {dst_dir}",
                    ))

    if new_frames:
        log(f"  Total frames espejo generados: {len(new_frames)}")

    # Ordenar todo junto
    all_frames = frames + new_frames
    all_frames.sort(key=lambda f: (f.animation, f.direction, f.index))
    return all_frames


def pack_atlas(
    frames: List[NormalizedFrame], cfg: ExportConfig, log: Callable[[str], None] = log_noop
) -> Tuple[Image.Image, Dict]:
    fw, fh = cfg.frame_width, cfg.frame_height
    pad = max(0, cfg.atlas_padding)
    if not frames:
        raise ValueError("No hay frames para montar el atlas.")

    cell_w = fw + pad
    max_cols = max(1, cfg.max_atlas_width // cell_w)
    cols = min(max_cols, max(1, math.ceil(math.sqrt(len(frames)))))
    rows = math.ceil(len(frames) / cols)
    atlas_w = cols * fw + max(0, cols - 1) * pad
    atlas_h = rows * fh + max(0, rows - 1) * pad
    atlas = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))

    json_frames: Dict[str, Dict] = {}
    for n, fr in enumerate(frames):
        col, row = n % cols, n // cols
        x, y = col * (fw + pad), row * (fh + pad)
        atlas.alpha_composite(fr.image, (x, y))
        json_frames[fr.name] = {
            "frame": {"x": x, "y": y, "w": fw, "h": fh},
            "rotated": False, "trimmed": False,
            "spriteSourceSize": {"x": 0, "y": 0, "w": fw, "h": fh},
            "sourceSize": {"w": fw, "h": fh},
            "pivot": {"x": 0.5, "y": 0.5},
        }

    atlas_json = {
        "frames": json_frames,
        "meta": {
            "app": f"IslaTortuga Sprite Atlas Tool v{VERSION}",
            "image": f"{cfg.asset_name}_atlas.png",
            "format": "RGBA8888",
            "size": {"w": atlas_w, "h": atlas_h},
            "scale": "1",
        },
    }
    log(f"  Atlas: {atlas_w}x{atlas_h}px, {len(frames)} frames.")
    return atlas, atlas_json


def write_normalized_frames(
    frames: List[NormalizedFrame], out_dir: Path, log: Callable[[str], None]
) -> None:
    target = out_dir / "normalized_frames"
    safe_mkdir(target)
    for fr in frames:
        folder = target / fr.animation / fr.direction
        safe_mkdir(folder)
        fr.image.save(folder / fr.name)
    log(f"  Frames → {target}")


def make_preview_sheet(
    frames: List[NormalizedFrame], cfg: ExportConfig, out_path: Path
) -> None:
    groups: Dict[Tuple[str, str], List[NormalizedFrame]] = {}
    for fr in frames:
        groups.setdefault((fr.animation, fr.direction), []).append(fr)
    for k in groups:
        groups[k].sort(key=lambda f: f.index)

    fw, fh = cfg.frame_width, cfg.frame_height
    label_w, label_h, pad = 160, 18, 4
    max_count = max(len(v) for v in groups.values())
    width = label_w + max_count * (fw + pad) + pad
    height = len(groups) * (fh + label_h + pad) + pad
    sheet = Image.new("RGBA", (width, height), (30, 30, 46, 255))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    y = pad
    for (anim, direction), gframes in sorted(groups.items()):
        mirrored = any(fr.warning and "Espejo" in (fr.warning or "") for fr in gframes)
        label_color = (180, 190, 254) if not mirrored else (166, 227, 161)
        draw.text((pad, y), f"{anim}-{direction}{'  [espejo]' if mirrored else ''}",
                  fill=label_color, font=font)
        x = label_w
        for fr in gframes:
            draw.rectangle((x, y + label_h, x + fw, y + label_h + fh), fill=(50, 50, 70))
            sheet.alpha_composite(fr.image, (x, y + label_h))
            x += fw + pad
        y += fh + label_h + pad
    sheet.save(out_path)


def generate_phaser_js(frames: List[NormalizedFrame], cfg: ExportConfig) -> str:
    groups: Dict[Tuple[str, str], List[NormalizedFrame]] = {}
    for fr in frames:
        groups.setdefault((fr.animation, fr.direction), []).append(fr)

    fn = f"create{''.join(p.capitalize() for p in slugify(cfg.asset_name).split('_'))}Animations"
    lines: List[str] = [
        f"// Auto-generado por IslaTortuga Sprite Atlas Tool v{VERSION}",
        "// Carga previa requerida:",
        f"// this.load.atlas('{cfg.asset_name}', '{cfg.asset_name}_atlas.png', '{cfg.asset_name}_atlas.json');",
        "",
        f"export function {fn}(scene, textureKey = '{cfg.asset_name}') {{",
    ]

    for (anim, direction), gframes in sorted(groups.items()):
        gframes.sort(key=lambda f: f.index)
        fps = cfg.animation_frame_rates.get(anim, cfg.default_frame_rate)
        key = f"{cfg.asset_name}-{anim}-{direction}"
        lines += [
            f"  if (!scene.anims.exists('{key}')) {{",
            "    scene.anims.create({",
            f"      key: '{key}',",
            "      frames: [",
            *[f"        {{ key: textureKey, frame: '{fr.name}' }}," for fr in gframes],
            "      ],",
            f"      frameRate: {fps},",
            f"      repeat: {cfg.repeat}",
            "    });",
            "  }",
            "",
        ]

    lines += ["}", "", f"export default {fn};", ""]
    return "\n".join(lines)


def copy_to_game(cfg: ExportConfig, out_dir: Path, log: Callable[[str], None]) -> None:
    """Copia los archivos generados a las carpetas del juego si están configuradas."""
    copied = 0

    if cfg.game_assets_dir.strip():
        assets_dir = Path(cfg.game_assets_dir.strip()).expanduser().resolve()
        safe_mkdir(assets_dir)
        for fname in [f"{cfg.asset_name}_atlas.png", f"{cfg.asset_name}_atlas.json"]:
            src = out_dir / fname
            if src.exists():
                dst = assets_dir / fname
                shutil.copy2(src, dst)
                log(f"  → {dst}")
                copied += 1

    if cfg.game_src_dir.strip():
        src_dir = Path(cfg.game_src_dir.strip()).expanduser().resolve()
        safe_mkdir(src_dir)
        src = out_dir / f"{cfg.asset_name}_animations.js"
        if src.exists():
            dst = src_dir / f"{cfg.asset_name}_animations.js"
            shutil.copy2(src, dst)
            log(f"  → {dst}")
            copied += 1

    if copied:
        log(f"✓ {copied} archivos copiados al juego.")
    else:
        log("  (Sin carpetas de juego configuradas; archivos solo en output.)")


def export_all(cfg: ExportConfig, log: Callable[[str], None] = log_noop) -> Path:
    input_path = Path(cfg.input_path).expanduser().resolve()
    out_dir = Path(cfg.output_dir).expanduser().resolve()
    safe_mkdir(out_dir)
    cfg.save(out_dir / "export_config.json")

    with tempfile.TemporaryDirectory(prefix="islatortuga_sprite_tool_") as td:
        work = Path(td)
        root = extract_input(input_path, work, log)
        source_frames = detect_frames(root, cfg, log)
        if not source_frames:
            raise ValueError("No se detectaron frames PNG útiles.")

        log("Normalizando frames...")
        normalized = normalize_frames(source_frames, cfg, log)

        if cfg.mirror_missing_directions:
            log("Generando espejos de direcciones faltantes...")
            normalized = add_mirrored_frames(normalized, cfg, log)

        log("Montando atlas...")
        atlas, atlas_json = pack_atlas(normalized, cfg, log)

        # Guardar archivos principales
        buf = io.BytesIO()
        atlas.save(buf, format="PNG")
        (out_dir / f"{cfg.asset_name}_atlas.png").write_bytes(buf.getvalue())
        (out_dir / f"{cfg.asset_name}_atlas.json").write_text(
            json.dumps(atlas_json, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        (out_dir / f"{cfg.asset_name}_animations.js").write_text(
            generate_phaser_js(normalized, cfg), encoding="utf-8"
        )

        if cfg.export_normalized_frames:
            write_normalized_frames(normalized, out_dir, log)
        if cfg.export_preview_sheet:
            make_preview_sheet(normalized, cfg, out_dir / "preview_sheet.png")

        warnings = [f"{fr.name}: {fr.warning}" for fr in normalized if fr.warning and "Espejo" not in fr.warning]
        avisos_section = ("## Avisos\n" + "\n".join(f"- {w}" for w in warnings[:30])) if warnings else ""
        readme = (
            f"# Export — {cfg.asset_name}\n"
            f"Frames: {len(normalized)} | Tamaño: {cfg.frame_width}x{cfg.frame_height} | Modo: {cfg.scale_mode}\n\n"
            f"## Uso en Phaser\n"
            f"```js\n"
            f"this.load.atlas('{cfg.asset_name}', '{cfg.asset_name}_atlas.png', '{cfg.asset_name}_atlas.json');\n"
            f"```\n"
            f"```js\n"
            f"import createAnimations from './{cfg.asset_name}_animations.js';\n"
            f"createAnimations(this, '{cfg.asset_name}');\n"
            f"```\n"
            f"{avisos_section}\n"
        )
        (out_dir / "README.md").write_text(readme, encoding="utf-8")

        # Copiar al juego si está configurado
        log("Copiando al juego...")
        copy_to_game(cfg, out_dir, log)

    log(f"✓ Export finalizado: {out_dir}")
    return out_dir


# ---------------------------------------------------------------------------
# GUI
# ---------------------------------------------------------------------------

_C = {
    "bg":      "#1e1e2e", "surface": "#313244", "surf2": "#45475a",
    "border":  "#585b70", "text":    "#cdd6f4", "muted": "#a6adc8",
    "accent":  "#89b4fa", "green":   "#a6e3a1", "yellow":"#f9e2af",
    "red":     "#f38ba8",
}


def _open_folder(path: str) -> None:
    try:
        if sys.platform == "win32":
            os.startfile(path)
        elif sys.platform == "darwin":
            subprocess.Popen(["open", path])
        else:
            subprocess.Popen(["xdg-open", path])
    except Exception:
        pass


def run_gui() -> None:
    import tkinter as tk
    from tkinter import filedialog, ttk

    def _theme(root: tk.Tk) -> None:
        root.configure(bg=_C["bg"])
        s = ttk.Style(root)
        s.theme_use("clam")
        base = dict(background=_C["bg"], foreground=_C["text"],
                    fieldbackground=_C["surface"], troughcolor=_C["surface"],
                    bordercolor=_C["border"], darkcolor=_C["surface"],
                    lightcolor=_C["surf2"], relief="flat", font=("Segoe UI", 10))
        s.configure(".", **base)
        s.configure("TFrame",     background=_C["bg"])
        s.configure("TLabel",     background=_C["bg"], foreground=_C["text"])
        s.configure("TLabelframe", background=_C["bg"], foreground=_C["accent"],
                    bordercolor=_C["border"], relief="solid")
        s.configure("TLabelframe.Label", background=_C["bg"], foreground=_C["accent"],
                    font=("Segoe UI", 9, "bold"))
        s.configure("TEntry",   fieldbackground=_C["surface"], foreground=_C["text"],
                    insertcolor=_C["text"], bordercolor=_C["border"])
        s.configure("TSpinbox", fieldbackground=_C["surface"], foreground=_C["text"],
                    buttonbackground=_C["surf2"], arrowcolor=_C["text"])
        s.configure("TCombobox", fieldbackground=_C["surface"], foreground=_C["text"],
                    arrowcolor=_C["text"])
        s.map("TCombobox",
              fieldbackground=[("readonly", _C["surface"])],
              selectbackground=[("readonly", _C["surf2"])],
              selectforeground=[("readonly", _C["text"])])
        s.configure("TCheckbutton", background=_C["bg"], foreground=_C["text"],
                    indicatorcolor=_C["surface"])
        s.map("TCheckbutton",
              indicatorcolor=[("selected", _C["accent"])],
              foreground=[("active", _C["accent"])])
        s.configure("TButton", background=_C["surf2"], foreground=_C["text"],
                    bordercolor=_C["border"], padding=(10, 5))
        s.map("TButton", background=[("active", _C["border"])])
        s.configure("Primary.TButton", background=_C["accent"], foreground=_C["bg"],
                    font=("Segoe UI", 10, "bold"), padding=(16, 8))
        s.map("Primary.TButton", background=[("active", "#74c7ec")])
        s.configure("Success.TButton", background=_C["green"], foreground=_C["bg"],
                    font=("Segoe UI", 9, "bold"), padding=(10, 5))
        s.map("Success.TButton", background=[("active", "#94d3a2")])
        s.configure("TProgressbar", troughcolor=_C["surface"],
                    background=_C["accent"], thickness=6)
        s.configure("TScrollbar", background=_C["surf2"],
                    troughcolor=_C["surface"], arrowcolor=_C["muted"])
        s.configure("TSeparator", background=_C["border"])

    class App(tk.Tk):
        def __init__(self) -> None:
            super().__init__()
            self.title(f"IslaTortuga Sprite Atlas Tool  v{VERSION}")
            self.geometry("1100x740")
            self.minsize(900, 600)
            self.cfg = ExportConfig()
            self._last_output: Optional[str] = None
            _theme(self)
            self._build()
            self.bind("<Control-e>", lambda _: self._export())
            self.bind("<Control-s>", lambda _: self._save_cfg())

        # ── Layout ──────────────────────────────────────────────────────────

        def _build(self) -> None:
            hdr = tk.Frame(self, bg=_C["surface"], height=46)
            hdr.pack(fill="x")
            hdr.pack_propagate(False)
            tk.Label(hdr, text="  IslaTortuga Sprite Atlas Tool",
                     bg=_C["surface"], fg=_C["accent"],
                     font=("Segoe UI", 13, "bold")).pack(side="left", padx=8, pady=10)
            tk.Label(hdr, text=f"v{VERSION}",
                     bg=_C["surface"], fg=_C["muted"],
                     font=("Segoe UI", 9)).pack(side="left", pady=14)

            body = ttk.Frame(self)
            body.pack(fill="both", expand=True, padx=12, pady=8)

            left = ttk.Frame(body)
            left.pack(side="left", fill="y", padx=(0, 8))

            right = ttk.Frame(body)
            right.pack(side="left", fill="both", expand=True)

            self._left(left)
            self._right(right)
            self._statusbar()

        def _left(self, p: ttk.Frame) -> None:
            # ── IO ──
            io = ttk.LabelFrame(p, text="Entrada / Salida", padding=10)
            io.pack(fill="x", pady=(0, 8))
            self.v_input  = tk.StringVar(value=self.cfg.input_path)
            self.v_output = tk.StringVar(value=self.cfg.output_dir)
            self.v_asset  = tk.StringVar(value=self.cfg.asset_name)
            self._prow(io, 0, "ZIP / carpeta",  self.v_input,  self._pick_input)
            self._prow(io, 1, "Output",          self.v_output, self._pick_output)
            ttk.Label(io, text="Asset name").grid(row=2, column=0, sticky="w", pady=3)
            ttk.Entry(io, textvariable=self.v_asset).grid(
                row=2, column=1, columnspan=2, sticky="ew", padx=(6, 0))
            io.columnconfigure(1, weight=1)

            # ── Juego ──
            gm = ttk.LabelFrame(p, text="Copiar al juego (opcional)", padding=10)
            gm.pack(fill="x", pady=(0, 8))
            self.v_gass = tk.StringVar(value=self.cfg.game_assets_dir)
            self.v_gsrc = tk.StringVar(value=self.cfg.game_src_dir)
            self._prow(gm, 0, "assets/ del juego", self.v_gass,
                       lambda: self._pick_dir(self.v_gass, "Carpeta assets del juego"))
            self._prow(gm, 1, "src/ del juego",    self.v_gsrc,
                       lambda: self._pick_dir(self.v_gsrc, "Carpeta src del juego"))
            gm.columnconfigure(1, weight=1)
            tk.Label(gm, text="PNG+JSON → assets/    JS → src/",
                     bg=_C["bg"], fg=_C["muted"],
                     font=("Segoe UI", 8)).grid(row=2, column=0, columnspan=3, sticky="w", pady=(2, 0))

            # ── Normalización ──
            nrm = ttk.LabelFrame(p, text="Normalización", padding=10)
            nrm.pack(fill="x", pady=(0, 8))
            self.v_fw   = tk.IntVar(value=self.cfg.frame_width)
            self.v_fh   = tk.IntVar(value=self.cfg.frame_height)
            self.v_mode = tk.StringVar(value=self.cfg.scale_mode)
            self.v_anch = tk.StringVar(value=self.cfg.anchor)
            self.v_alph = tk.IntVar(value=self.cfg.alpha_threshold)
            self.v_ipad = tk.IntVar(value=self.cfg.inner_padding)
            self.v_apad = tk.IntVar(value=self.cfg.atlas_padding)
            self.v_maxw = tk.IntVar(value=self.cfg.max_atlas_width)
            self.v_offx = tk.IntVar(value=self.cfg.offset_x)
            self.v_offy = tk.IntVar(value=self.cfg.offset_y)
            self.v_scup = tk.BooleanVar(value=self.cfg.allow_scale_up)
            self.v_xfrm = tk.BooleanVar(value=self.cfg.export_normalized_frames)
            self.v_xprv = tk.BooleanVar(value=self.cfg.export_preview_sheet)
            self.v_rots = tk.BooleanVar(value=self.cfg.include_rotations_as_poses)
            self.v_mirr = tk.BooleanVar(value=self.cfg.mirror_missing_directions)

            r = 0
            self._s2(nrm, r, "Frame W", self.v_fw, 1, 512, "Frame H", self.v_fh, 1, 512); r += 1
            self._c2(nrm, r, "Modo", self.v_mode, ["fit","pad","crop"],
                              "Anchor", self.v_anch, ["center","bottom_center"]); r += 1
            self._s2(nrm, r, "Alpha thr.", self.v_alph, 0, 255,
                              "Inner pad", self.v_ipad, 0, 64); r += 1
            self._s2(nrm, r, "Atlas pad", self.v_apad, 0, 64,
                              "Max atlas W", self.v_maxw, 64, 8192); r += 1
            self._s2(nrm, r, "Offset X", self.v_offx, -256, 256,
                              "Offset Y", self.v_offy, -256, 256); r += 1

            chk = ttk.Frame(nrm)
            chk.grid(row=r, column=0, columnspan=4, sticky="w", pady=(6, 0))
            ttk.Checkbutton(chk, text="Escalar ↑",      variable=self.v_scup).grid(row=0, column=0, sticky="w")
            ttk.Checkbutton(chk, text="Export frames",  variable=self.v_xfrm).grid(row=0, column=1, sticky="w", padx=10)
            ttk.Checkbutton(chk, text="Preview sheet",  variable=self.v_xprv).grid(row=1, column=0, sticky="w")
            ttk.Checkbutton(chk, text="Rotations",      variable=self.v_rots).grid(row=1, column=1, sticky="w", padx=10)
            ttk.Checkbutton(chk, text="↔ Espejo direcciones faltantes",
                            variable=self.v_mirr).grid(row=2, column=0, columnspan=2, sticky="w", pady=(4, 0))

            # ── Botones ──
            btns = ttk.Frame(p)
            btns.pack(fill="x", pady=(0, 6))
            ttk.Button(btns, text="Escanear",      command=self._scan).pack(side="left")
            ttk.Button(btns, text="Cargar config", command=self._load_cfg).pack(side="left", padx=6)
            ttk.Button(btns, text="Guardar config",command=self._save_cfg).pack(side="left")

            self._btn_export = ttk.Button(
                p, text="▶  Exportar atlas  (Ctrl+E)",
                style="Primary.TButton", command=self._export,
            )
            self._btn_export.pack(fill="x", pady=(4, 0))

        def _right(self, p: ttk.Frame) -> None:
            sb = ttk.LabelFrame(p, text="Animaciones detectadas", padding=8)
            sb.pack(fill="x", pady=(0, 8))
            self._txt_sum = tk.Text(sb, height=5, wrap="word",
                                    bg=_C["surface"], fg=_C["muted"],
                                    insertbackground=_C["text"], relief="flat",
                                    font=("Consolas", 9), state="disabled")
            self._txt_sum.pack(fill="x")

            lb = ttk.LabelFrame(p, text="Log", padding=8)
            lb.pack(fill="both", expand=True)
            self._txt_log = tk.Text(lb, wrap="word",
                                    bg=_C["surface"], fg=_C["text"],
                                    insertbackground=_C["text"], relief="flat",
                                    font=("Consolas", 9), state="disabled")
            sc = ttk.Scrollbar(lb, command=self._txt_log.yview)
            self._txt_log.configure(yscrollcommand=sc.set)
            sc.pack(side="right", fill="y")
            self._txt_log.pack(fill="both", expand=True)
            for tag, fg in [("ok","#a6e3a1"),("warn","#f9e2af"),
                             ("err","#f38ba8"),("muted","#a6adc8"),("info","#cdd6f4")]:
                self._txt_log.tag_configure(tag, foreground=fg)
            self._log("Listo. Configura las rutas y exporta.", "muted")

        def _statusbar(self) -> None:
            bar = tk.Frame(self, bg=_C["surface"], height=34)
            bar.pack(fill="x", side="bottom")
            bar.pack_propagate(False)
            self._bar = ttk.Progressbar(bar, mode="determinate", length=200)
            self._bar.pack(side="left", padx=10, pady=8)
            self._var_st = tk.StringVar(value="")
            tk.Label(bar, textvariable=self._var_st, bg=_C["surface"],
                     fg=_C["muted"], font=("Segoe UI", 9)).pack(side="left", padx=4)
            self._btn_open = ttk.Button(bar, text="📂  Abrir carpeta",
                                        style="Success.TButton",
                                        command=lambda: _open_folder(self._last_output or ""),
                                        state="disabled")
            self._btn_open.pack(side="right", padx=10, pady=4)

        # ── Widget helpers ───────────────────────────────────────────────────

        def _prow(self, p, row, label, var, cmd):
            ttk.Label(p, text=label).grid(row=row, column=0, sticky="w", pady=3)
            ttk.Entry(p, textvariable=var).grid(row=row, column=1, sticky="ew", padx=(6, 4), pady=3)
            ttk.Button(p, text="…", width=3, command=cmd).grid(row=row, column=2, pady=3)

        def _s2(self, p, row, l1, v1, mn1, mx1, l2, v2, mn2, mx2):
            ttk.Label(p, text=l1).grid(row=row, column=0, sticky="w", pady=3)
            ttk.Spinbox(p, textvariable=v1, from_=mn1, to=mx1, width=8).grid(
                row=row, column=1, sticky="w", padx=(4, 14), pady=3)
            ttk.Label(p, text=l2).grid(row=row, column=2, sticky="w", pady=3)
            ttk.Spinbox(p, textvariable=v2, from_=mn2, to=mx2, width=8).grid(
                row=row, column=3, sticky="w", padx=4, pady=3)

        def _c2(self, p, row, l1, v1, vls1, l2, v2, vls2):
            ttk.Label(p, text=l1).grid(row=row, column=0, sticky="w", pady=3)
            ttk.Combobox(p, textvariable=v1, values=vls1, state="readonly", width=9).grid(
                row=row, column=1, sticky="w", padx=(4, 14), pady=3)
            ttk.Label(p, text=l2).grid(row=row, column=2, sticky="w", pady=3)
            ttk.Combobox(p, textvariable=v2, values=vls2, state="readonly", width=14).grid(
                row=row, column=3, sticky="w", padx=4, pady=3)

        # ── Log ─────────────────────────────────────────────────────────────

        def _log(self, msg: str, tag: str = "info") -> None:
            def _do():
                self._txt_log.configure(state="normal")
                self._txt_log.insert("end", msg + "\n", tag)
                self._txt_log.see("end")
                self._txt_log.configure(state="disabled")
            self.after(0, _do)

        def log(self, msg: str) -> None:
            tag = ("ok" if msg.startswith("✓") else
                   "warn" if msg.startswith("⚠") else
                   "err" if msg.startswith("✗") else "info")
            self._log(msg, tag)

        def _st(self, t: str) -> None:
            self.after(0, lambda: self._var_st.set(t))

        def _prog(self, v: float) -> None:
            self.after(0, lambda: self._bar.configure(value=v))

        def _set_sum(self, lines: List[str]) -> None:
            def _do():
                self._txt_sum.configure(state="normal")
                self._txt_sum.delete("1.0", "end")
                self._txt_sum.insert("end", "\n".join(lines))
                self._txt_sum.configure(state="disabled")
            self.after(0, _do)

        # ── Config ──────────────────────────────────────────────────────────

        def _gather(self) -> ExportConfig:
            c = self.cfg
            c.input_path  = self.v_input.get().strip()
            c.output_dir  = self.v_output.get().strip()
            c.asset_name  = slugify(self.v_asset.get().strip(), "player")
            c.frame_width = int(self.v_fw.get())
            c.frame_height = int(self.v_fh.get())
            c.scale_mode  = self.v_mode.get()
            c.anchor      = self.v_anch.get()
            c.alpha_threshold = int(self.v_alph.get())
            c.inner_padding   = int(self.v_ipad.get())
            c.atlas_padding   = int(self.v_apad.get())
            c.max_atlas_width = int(self.v_maxw.get())
            c.offset_x = int(self.v_offx.get())
            c.offset_y = int(self.v_offy.get())
            c.allow_scale_up = bool(self.v_scup.get())
            c.export_normalized_frames = bool(self.v_xfrm.get())
            c.export_preview_sheet     = bool(self.v_xprv.get())
            c.include_rotations_as_poses = bool(self.v_rots.get())
            c.mirror_missing_directions  = bool(self.v_mirr.get())
            c.game_assets_dir = self.v_gass.get().strip()
            c.game_src_dir    = self.v_gsrc.get().strip()
            return c

        def _push(self) -> None:
            c = self.cfg
            self.v_input.set(c.input_path); self.v_output.set(c.output_dir)
            self.v_asset.set(c.asset_name)
            self.v_fw.set(c.frame_width);   self.v_fh.set(c.frame_height)
            self.v_mode.set(c.scale_mode);  self.v_anch.set(c.anchor)
            self.v_alph.set(c.alpha_threshold); self.v_ipad.set(c.inner_padding)
            self.v_apad.set(c.atlas_padding);   self.v_maxw.set(c.max_atlas_width)
            self.v_offx.set(c.offset_x);    self.v_offy.set(c.offset_y)
            self.v_scup.set(c.allow_scale_up)
            self.v_xfrm.set(c.export_normalized_frames)
            self.v_xprv.set(c.export_preview_sheet)
            self.v_rots.set(c.include_rotations_as_poses)
            self.v_mirr.set(c.mirror_missing_directions)
            self.v_gass.set(c.game_assets_dir)
            self.v_gsrc.set(c.game_src_dir)

        # ── Acciones ────────────────────────────────────────────────────────

        def _pick_input(self) -> None:
            path = filedialog.askopenfilename(
                title="Selecciona ZIP de entrada",
                filetypes=[("ZIP", "*.zip"), ("Todos", "*.*")])
            if path:
                self.v_input.set(path); return
            folder = filedialog.askdirectory(title="O selecciona carpeta de entrada")
            if folder:
                self.v_input.set(folder)

        def _pick_output(self) -> None:
            path = filedialog.askdirectory(title="Carpeta de output")
            if path:
                self.v_output.set(path)

        def _pick_dir(self, var: tk.StringVar, title: str) -> None:
            path = filedialog.askdirectory(title=title)
            if path:
                var.set(path)

        def _load_cfg(self) -> None:
            path = filedialog.askopenfilename(title="Cargar config",
                                              filetypes=[("JSON","*.json"),("Todos","*.*")])
            if not path: return
            try:
                self.cfg = ExportConfig.from_file(path)
                self._push()
                self._log(f"Config cargada: {path}", "ok")
            except Exception as e:
                self._log(f"✗ {e}", "err")

        def _save_cfg(self, _=None) -> None:
            self._gather()
            path = filedialog.asksaveasfilename(title="Guardar config",
                                                defaultextension=".json",
                                                filetypes=[("JSON","*.json")])
            if not path: return
            try:
                self.cfg.save(path)
                self._log(f"Config guardada: {path}", "ok")
            except Exception as e:
                self._log(f"✗ {e}", "err")

        def _scan(self) -> None:
            cfg = self._gather()
            if not cfg.input_path:
                self._log("⚠ Selecciona un input primero.", "warn"); return

            def _work():
                try:
                    with tempfile.TemporaryDirectory(prefix="islatortuga_scan_") as td:
                        root = extract_input(Path(cfg.input_path).expanduser().resolve(),
                                             Path(td), self.log)
                        frames = detect_frames(root, cfg, self.log)
                    groups: Dict[Tuple[str,str], int] = {}
                    for f in frames:
                        groups[(f.animation, f.direction)] = groups.get((f.animation, f.direction), 0) + 1
                    summary = [f"  {a}-{d}: {c} frames" for (a, d), c in sorted(groups.items())]
                    # Mostrar qué espejos se generarían
                    if cfg.mirror_missing_directions:
                        dirs_by_anim: Dict[str, set] = {}
                        for a, d in groups:
                            dirs_by_anim.setdefault(a, set()).add(d)
                        for src, dst in MIRROR_PAIRS:
                            for anim, dirs in dirs_by_anim.items():
                                if src in dirs and dst not in dirs:
                                    summary.append(f"  {anim}-{dst}: (espejo de {src})")
                                elif dst in dirs and src not in dirs:
                                    summary.append(f"  {anim}-{src}: (espejo de {dst})")
                    self._set_sum(summary or ["  Sin animaciones."])
                    self._log(f"✓ {len(frames)} frames detectados.", "ok")
                except Exception:
                    self._log(traceback.format_exc(), "err")
            threading.Thread(target=_work, daemon=True).start()

        def _export(self, _=None) -> None:
            cfg = self._gather()
            if not cfg.input_path:
                self._log("⚠ Selecciona un input.", "warn"); return
            if not cfg.output_dir:
                self._log("⚠ Selecciona output.", "warn"); return

            self._btn_export.configure(state="disabled")
            self._btn_open.configure(state="disabled")
            self._bar.configure(value=0)
            self._st("Exportando...")

            def _work():
                steps = [0]
                def _lp(msg: str) -> None:
                    self.log(msg); steps[0] += 1
                    self._prog(min(92, steps[0] * 6))
                try:
                    out = export_all(cfg, _lp)
                    self._last_output = str(out)
                    self._prog(100)
                    self._st(f"✓ {out.name}")
                    self.after(0, lambda: self._btn_open.configure(state="normal"))
                except Exception:
                    self._log(traceback.format_exc(), "err")
                    self._st("Error.")
                finally:
                    self.after(0, lambda: self._btn_export.configure(state="normal"))
            threading.Thread(target=_work, daemon=True).start()

    App().mainloop()


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def run_cli(argv=None):
    parser = argparse.ArgumentParser(description="Normaliza sprites y genera atlas Phaser.")
    parser.add_argument("--gui",               action="store_true")
    parser.add_argument("--config",            help="Config JSON.")
    parser.add_argument("--input",             help="ZIP o carpeta de entrada.")
    parser.add_argument("--output",            help="Carpeta de salida.")
    parser.add_argument("--asset",             default=None)
    parser.add_argument("--frame-width",       type=int, default=None)
    parser.add_argument("--frame-height",      type=int, default=None)
    parser.add_argument("--mode",              choices=["fit","pad","crop"], default=None)
    parser.add_argument("--anchor",            choices=["center","bottom_center"], default=None)
    parser.add_argument("--include-rotations", action="store_true")
    parser.add_argument("--no-mirror",         action="store_true")
    parser.add_argument("--game-assets-dir",   default=None)
    parser.add_argument("--game-src-dir",      default=None)
    args = parser.parse_args(argv)

    if args.gui:
        run_gui(); return 0

    cfg = ExportConfig.from_file(args.config) if args.config else ExportConfig()
    if args.input:            cfg.input_path  = args.input
    if args.output:           cfg.output_dir  = args.output
    if args.asset:            cfg.asset_name  = slugify(args.asset, "player")
    if args.frame_width:      cfg.frame_width = args.frame_width
    if args.frame_height:     cfg.frame_height = args.frame_height
    if args.mode:             cfg.scale_mode  = args.mode
    if args.anchor:           cfg.anchor      = args.anchor
    if args.include_rotations: cfg.include_rotations_as_poses = True
    if args.no_mirror:        cfg.mirror_missing_directions = False
    if args.game_assets_dir:  cfg.game_assets_dir = args.game_assets_dir
    if args.game_src_dir:     cfg.game_src_dir    = args.game_src_dir

    if not cfg.input_path:
        print("Falta --input o usa --gui", flush=True); return 2

    export_all(cfg, lambda msg: print(msg, flush=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(run_cli())
