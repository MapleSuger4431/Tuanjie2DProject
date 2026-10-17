#!/usr/bin/env python3
"""Convert key-colour background to transparent (alpha=0) in PNG images.

This script performs chroma-key removal: pixels similar to the specified key
colour (in HSV space) are set to alpha=0, while all other pixels retain their
original RGB values with alpha=255. Low-alpha noise is suppressed.

Usage:
  {PYTHON} make_key_color_transparent.py INPUT OUTPUT --key-color-r 255 --key-color-g 0 --key-color-b 255
  {PYTHON} make_key_color_transparent.py INPUT OUTPUT --key-color-hex ff00ff
  {PYTHON} make_key_color_transparent.py INPUT OUTPUT --transform-json transform.json
  {PYTHON} make_key_color_transparent.py INPUT OUTPUT --key-color-json '{"rgb":{"r":255,"g":0,"b":255}}'

Optional:
  --hue-tolerance 12    HSV hue tolerance (OpenCV 0-179 scale)
  --sat-tolerance 80    Saturation tolerance (0-255)
  --val-tolerance 80    Value tolerance (0-255)
  --trim                Trim transparent padding after conversion
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict, Optional, Tuple

import numpy as np
from PIL import Image

from key_color_processing import json_safe, key_color_mask, rgba_from_alpha

# ---------------------------------------------------------------------------
# Export size calculation (migrated from generation_atlas.py)
# ---------------------------------------------------------------------------

MAX_EXPORT_LONG_EDGE = 1024
MIN_EXPORT_SHORT_EDGE = 64


def calculate_export_size(source_size: Dict[str, int],
                          logical_size: Optional[Dict[str, float]] = None) -> Dict[str, int]:
    """Compute the final PNG pixel dimensions from logical or source size.

    Target ~2x logical; long edge <= 1024; short edge >= 64 if possible.
    Aspect ratio is always preserved from source.
    """
    sw, sh = source_size["width"], source_size["height"]
    aspect = sw / sh if sh > 0 else 1.0

    if logical_size and logical_size.get("width", 0) > 0 and logical_size.get("height", 0) > 0:
        dw = 2.0 * logical_size["width"]
        dh = 2.0 * logical_size["height"]
    else:
        dw, dh = float(sw), float(sh)

    long_edge = max(dw, dh)
    if long_edge > MAX_EXPORT_LONG_EDGE:
        scale = MAX_EXPORT_LONG_EDGE / long_edge
        dw, dh = dw * scale, dh * scale

    if dw >= dh:
        w = max(1, round(dw))
        h = max(1, round(w / aspect))
    else:
        h = max(1, round(dh))
        w = max(1, round(h * aspect))

    if min(w, h) < MIN_EXPORT_SHORT_EDGE:
        if w <= h:
            w = max(MIN_EXPORT_SHORT_EDGE, w)
            h = max(1, round(w / aspect))
        else:
            h = max(MIN_EXPORT_SHORT_EDGE, h)
            w = max(1, round(h * aspect))

    return {"width": w, "height": h}


def _parse_export_size(raw: Optional[str]) -> Optional[Dict[str, int]]:
    """Parse --export-size 'WxH' into a dict."""
    if not raw:
        return None
    parts = raw.lower().split("x")
    if len(parts) != 2:
        raise ValueError(f"Invalid --export-size format: {raw!r}, expected WxH")
    return {"width": int(parts[0]), "height": int(parts[1])}


def _pad_to_square(image: np.ndarray,
                   key_color: Optional[Dict[str, Any]]) -> np.ndarray:
    """Pad image to a square with key colour (or transparent if no key colour)."""
    h, w = image.shape[:2]
    side = max(h, w)
    if h == side and w == side:
        return image
    if key_color is not None:
        kc = key_color["rgb"]
        bg = (int(kc["r"]), int(kc["g"]), int(kc["b"]), 255)
    else:
        bg = (0, 0, 0, 0)
    canvas = Image.new("RGBA", (side, side), bg)
    canvas.paste(Image.fromarray(image, "RGBA"), (0, 0))
    return np.asarray(canvas)


def _parse_key_color(args: argparse.Namespace) -> Optional[Dict[str, Any]]:
    """Build a key_color dict from CLI arguments."""
    if args.transform_json:
        transform = json.loads(Path(args.transform_json).read_text(encoding="utf-8"))
        kc = transform.get("key_color")
        if kc:
            return kc
    if args.key_color_json:
        return json.loads(args.key_color_json)
    if args.key_color_hex:
        hex_str = args.key_color_hex.lstrip("#")
        r, g, b = int(hex_str[0:2], 16), int(hex_str[2:4], 16), int(hex_str[4:6], 16)
        return {"rgb": {"r": r, "g": g, "b": b}}
    if args.key_color_r is not None and args.key_color_g is not None and args.key_color_b is not None:
        return {"rgb": {"r": args.key_color_r, "g": args.key_color_g, "b": args.key_color_b}}
    return None


def _alpha_bounds(alpha: np.ndarray) -> Optional[Dict[str, int]]:
    rows, columns = np.where(alpha > 0)
    if not len(rows):
        return None
    left, right = int(columns.min()), int(columns.max()) + 1
    top, bottom = int(rows.min()), int(rows.max()) + 1
    return {"x": left, "y": top, "width": right - left, "height": bottom - top}


def _despill_key_color(rgb: np.ndarray, alpha: np.ndarray,
                       kc_rgb: Tuple[int, int, int]) -> Tuple[np.ndarray, np.ndarray]:
    """Remove key-colour spill from semi-transparent edge pixels.

    Anti-aliased edges between the key-colour background and content create
    blended pixels that fall outside HSV tolerance. This step uses Euclidean
    distance to the key colour (kc_dist, normalized 0..1) to detect and
    suppress contamination — colour-agnostic, works for any key colour.

    Returns corrected (rgb, alpha). RGB is never modified.
    """
    r, g, b = rgb[:, :, 0].astype(np.int16), rgb[:, :, 1].astype(np.int16), rgb[:, :, 2].astype(np.int16)
    kc_r, kc_g, kc_b = int(kc_rgb[0]), int(kc_rgb[1]), int(kc_rgb[2])

    # Distance from pure key colour (normalized 0..1)
    kc_dist = np.sqrt(
        ((r - kc_r) / 255.0) ** 2 +
        ((g - kc_g) / 255.0) ** 2 +
        ((b - kc_b) / 255.0) ** 2
    )

    # Pixels very close to key colour: make fully transparent
    near_key = kc_dist < 0.35
    alpha = np.where(near_key, 0, alpha).astype(np.uint8)

    # Edge halo: pixels moderately close to key colour (anti-aliased blends).
    # Heavier contamination (closer to key colour) → larger alpha reduction.
    remaining = (alpha > 0) & (kc_dist >= 0.35) & (kc_dist < 0.55)
    heavy = remaining & (kc_dist < 0.45)
    alpha = np.where(heavy, np.maximum(0, alpha - 180), alpha)
    medium = remaining & (kc_dist >= 0.45)
    alpha = np.where(medium, np.maximum(0, alpha - 100), alpha)

    return rgb, alpha


def make_transparent(input_path: Path, output_path: Path,
                     key_color: Optional[Dict[str, Any]] = None,
                     hue_tolerance: int = 20,
                     sat_tolerance: int = 100,
                     val_tolerance: int = 100,
                     trim: bool = False,
                     erode_alpha: bool = True,
                     export_size: Optional[Dict[str, int]] = None,
                     pad_to_square: bool = False) -> Dict[str, Any]:
    """Convert key-colour pixels to transparent (alpha=0) with alpha-only despill.

    Uses moderate HSV tolerance (default 20/100/100) plus an alpha-only despill step
    that removes key-colour spill from anti-aliased edge pixels WITHOUT
    modifying RGB content. An optional 1px alpha erosion removes residual edge
    halo.

    The tightened hue tolerance (20) prevents neighbouring colours in HSV space
    from being misclassified as key colour — e.g. for magenta (hue 150) this
    safely excludes blue/cyan content (hue 110-120).

    If *key_color* is None, falls back to alpha-based noise cleaning (backward compat).

    If *pad_to_square* is True, pads the input to a square with key colour before
    processing (useful for preparing img2img inputs or restoring square outputs).

    If *export_size* is provided, scales the final (post-trim) image to the given
    dimensions using LANCZOS (downscale) or BICUBIC (upscale).
    """
    input_path = input_path.resolve()
    output_path = output_path.resolve()
    with Image.open(input_path) as opened:
        source = np.asarray(opened.convert("RGBA"))

    if pad_to_square:
        source = _pad_to_square(source, key_color)

    if key_color is not None:
        mask = key_color_mask(source, key_color,
                              hue_tolerance=hue_tolerance,
                              sat_tolerance=sat_tolerance,
                              val_tolerance=val_tolerance)
        rgb = source[:, :, :3].copy()
        alpha = mask.copy()
        alpha[alpha < 10] = 0

        # Despill: remove key-colour contamination from edge pixels
        kc = key_color.get("rgb", {})
        kc_rgb = (int(kc.get("r", 255)), int(kc.get("g", 0)), int(kc.get("b", 255)))
        rgb, alpha = _despill_key_color(rgb, alpha, kc_rgb)

        # Alpha erosion: shrink visible region by 1px to remove residual
        # edge halo (anti-aliased key-colour bleed that survived HSV + despill).
        if erode_alpha:
            try:
                from scipy import ndimage
                eroded = ndimage.binary_erosion(alpha > 0, iterations=1)
                alpha = np.where(eroded, alpha, 0).astype(np.uint8)

                # Spatial edge cleanup: remove residual key-colour halo from
                # edge pixels that blend key colour and bright content (white,
                # yellow) — these slip through HSV + despill because their hue
                # shifts away from the key colour, but they still carry
                # contamination.
                # Target only the 2px zone adjacent to transparent regions, so
                # interior content is never affected.
                transparent_mask = alpha == 0
                edge_zone = ndimage.binary_dilation(
                    transparent_mask, iterations=2) & (alpha > 0)

                r_ch = rgb[:, :, 0].astype(np.int16)
                g_ch = rgb[:, :, 1].astype(np.int16)
                b_ch = rgb[:, :, 2].astype(np.int16)

                # Distance to key colour — detects contamination regardless of hue
                edge_kc_dist = np.sqrt(
                    ((r_ch - kc_rgb[0]) / 255.0) ** 2 +
                    ((g_ch - kc_rgb[1]) / 255.0) ** 2 +
                    ((b_ch - kc_rgb[2]) / 255.0) ** 2
                )
                is_key_tinted = edge_kc_dist < 0.45

                # Semi-transparent key-colour pixels → fully transparent
                semi_trans = (alpha > 0) & (alpha < 255)
                alpha = np.where(semi_trans & is_key_tinted, 0, alpha).astype(np.uint8)

                # Opaque key-colour pixels in 2px edge zone → fully transparent
                edge_key = edge_zone & is_key_tinted & (alpha == 255)
                alpha = np.where(edge_key, 0, alpha).astype(np.uint8)
            except ImportError:
                pass  # scipy not available — skip erosion

        # Zero RGB of fully transparent pixels. Alpha=0 pixels still retain
        # their original key-colour RGB values, which AI vision models read
        # regardless of alpha — causing false "key-colour residue" reports.
        fully_transparent = alpha == 0
        rgb[fully_transparent] = 0

        result = np.dstack((rgb, alpha))
    else:
        result = rgba_from_alpha(source)

    bounds = _alpha_bounds(result[:, :, 3])
    if trim and bounds is not None:
        x, y, width, height = (bounds[key] for key in ("x", "y", "width", "height"))
        written = result[y:y + height, x:x + width]
    else:
        written = result

    # Scale to export_size if requested
    scaled = False
    if export_size is not None:
        img = Image.fromarray(written, "RGBA")
        ew, eh = export_size["width"], export_size["height"]
        if img.size != (ew, eh):
            resample = Image.LANCZOS if ew <= written.shape[1] else Image.BICUBIC
            img = img.resize((ew, eh), resample)
            written = np.asarray(img)
            scaled = True

    output_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(written, "RGBA").save(output_path)
    return {
        "input": str(input_path),
        "output": str(output_path),
        "key_color": key_color,
        "input_dimensions": {"width": int(source.shape[1]), "height": int(source.shape[0])},
        "output_dimensions": {"width": int(written.shape[1]), "height": int(written.shape[0])},
        "alpha_bounding_rect": bounds,
        "trimmed": bool(trim and bounds is not None),
        "scaled_to_export_size": scaled,
        "export_size": export_size if scaled else None,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert key-colour background to transparent (alpha=0).")
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    # Key colour specification (mutually exclusive, first match wins)
    parser.add_argument("--key-color-r", type=int, help="Key colour red channel (0-255).")
    parser.add_argument("--key-color-g", type=int, help="Key colour green channel (0-255).")
    parser.add_argument("--key-color-b", type=int, help="Key colour blue channel (0-255).")
    parser.add_argument("--key-color-hex", type=str, help="Key colour as hex string (e.g. ff00ff).")
    parser.add_argument("--key-color-json", type=str, help="Key colour as JSON string.")
    parser.add_argument("--transform-json", type=Path, help="Read key_color from transform.json.")
    # Tolerance overrides
    parser.add_argument("--hue-tolerance", type=int, default=20, help="HSV hue tolerance (default: 20).")
    parser.add_argument("--sat-tolerance", type=int, default=100, help="Saturation tolerance (default: 100).")
    parser.add_argument("--val-tolerance", type=int, default=100, help="Value tolerance (default: 100).")
    # Deprecated args (ignored, kept for backward compatibility)
    parser.add_argument("--color", help="[Deprecated] Use --key-color-r/g/b or --key-color-hex instead.")
    # Options
    parser.add_argument("--trim", action="store_true", help="Trim transparent padding (changes dimensions).")
    parser.add_argument("--export-size", type=str, default=None,
                        help="Scale final image to WxH after trim (e.g. 512x384). Uses LANCZOS downscale, BICUBIC upscale.")
    parser.add_argument("--pad-to-square", action="store_true",
                        help="Pad input image to a square with key colour before processing.")
    args = parser.parse_args()

    key_color = _parse_key_color(args)
    export_size = _parse_export_size(args.export_size)
    result = make_transparent(args.input, args.output, key_color,
                              hue_tolerance=args.hue_tolerance,
                              sat_tolerance=args.sat_tolerance,
                              val_tolerance=args.val_tolerance,
                              trim=args.trim,
                              export_size=export_size,
                              pad_to_square=args.pad_to_square)
    print(json.dumps(json_safe(result), ensure_ascii=False))


if __name__ == "__main__":
    main()
