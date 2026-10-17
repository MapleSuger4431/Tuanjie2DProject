#!/usr/bin/env python3
"""Check key-color residue in transparent PNGs.

Usage:
    python check_key_color_residue.py <image_path> <transform_json> [--edge-width 5] [--threshold 0.01]

Exit codes:
    0 = clean (no residue)
    1 = residue detected (key-color pixels found in edge region)
    2 = error (file not found, invalid image, etc.)

Output JSON: {"has_residue": bool, "residue_ratio": float, "edge_width": int, "residue_pixels": int, "checked_pixels": int, "key_color": {"r": int, "g": int, "b": int}}
"""

import argparse
import json
import sys
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print(json.dumps({"error": "Pillow/numpy not installed. Run: python scripts/setup_env.py"}))
    sys.exit(2)


def color_distance(pixel_rgb, target_rgb, tolerance=30):
    """Check if a pixel is close to the key color within tolerance."""
    dr = abs(int(pixel_rgb[0]) - target_rgb[0])
    dg = abs(int(pixel_rgb[1]) - target_rgb[1])
    db = abs(int(pixel_rgb[2]) - target_rgb[2])
    return dr <= tolerance and dg <= tolerance and db <= tolerance


def main():
    parser = argparse.ArgumentParser(description="Check key-color residue in transparent PNG.")
    parser.add_argument("image_path", help="Path to the transparent PNG to check.")
    parser.add_argument("transform_json", help="Path to transform.json containing key_color definition.")
    parser.add_argument("--edge-width", type=int, default=5, help="Width of edge region to check (pixels, default 5).")
    parser.add_argument("--threshold", type=float, default=0.01, help="Residue ratio threshold (default 0.01 = 1%%).")
    parser.add_argument("--tolerance", type=int, default=30, help="RGB tolerance per channel (default 30).")
    args = parser.parse_args()

    img_path = Path(args.image_path)
    transform_path = Path(args.transform_json)

    if not img_path.exists():
        print(json.dumps({"error": f"Image not found: {img_path}"}))
        sys.exit(2)
    if not transform_path.exists():
        print(json.dumps({"error": f"Transform JSON not found: {transform_path}"}))
        sys.exit(2)

    # Read key color from transform.json
    try:
        transform = json.loads(transform_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        print(json.dumps({"error": f"Invalid transform JSON: {e}"}))
        sys.exit(2)

    key_color = transform.get("key_color")
    if not key_color or "rgb" not in key_color:
        # No key color defined — cannot check residue, report clean
        print(json.dumps({"has_residue": False, "reason": "no key_color in transform.json"}))
        sys.exit(0)

    target_rgb = {
        "r": key_color["rgb"]["r"],
        "g": key_color["rgb"]["g"],
        "b": key_color["rgb"]["b"],
    }

    # Load image
    try:
        img = Image.open(img_path).convert("RGBA")
    except Exception as e:
        print(json.dumps({"error": f"Cannot open image: {e}"}))
        sys.exit(2)

    arr = np.array(img)
    h, w = arr.shape[:2]

    if h < args.edge_width * 2 or w < args.edge_width * 2:
        # Image too small for edge check — check entire image
        edge_mask = np.ones((h, w), dtype=bool)
    else:
        edge_mask = np.zeros((h, w), dtype=bool)
        # Top edge
        edge_mask[:args.edge_width, :] = True
        # Bottom edge
        edge_mask[-args.edge_width:, :] = True
        # Left edge
        edge_mask[:, :args.edge_width] = True
        # Right edge
        edge_mask[:, -args.edge_width:] = True

    # Extract RGB from edge region
    edge_rgb = arr[:, :, :3][edge_mask]
    edge_alpha = arr[:, :, 3][edge_mask]

    # Only check pixels that are NOT fully transparent (alpha > 0)
    # Fully transparent pixels are expected to be removed already
    # Key-color residue = pixels that have alpha > 0 AND RGB close to key color
    non_transparent = edge_alpha > 0
    if not non_transparent.any():
        # All edge pixels are transparent — clean
        print(json.dumps({
            "has_residue": False,
            "residue_ratio": 0.0,
            "edge_width": args.edge_width,
            "residue_pixels": 0,
            "checked_pixels": int(edge_mask.sum()),
            "key_color": target_rgb,
        }))
        sys.exit(0)

    # Check which non-transparent pixels match key color
    edge_rgb_non_transparent = edge_rgb[non_transparent]
    matches = np.array([
        color_distance(px, (target_rgb["r"], target_rgb["g"], target_rgb["b"]), args.tolerance)
        for px in edge_rgb_non_transparent
    ])

    residue_pixels = int(matches.sum())
    checked_pixels = int(non_transparent.sum())
    residue_ratio = residue_pixels / checked_pixels if checked_pixels > 0 else 0.0

    has_residue = residue_ratio > args.threshold

    result = {
        "has_residue": bool(has_residue),
        "residue_ratio": round(residue_ratio, 4),
        "edge_width": args.edge_width,
        "residue_pixels": residue_pixels,
        "checked_pixels": checked_pixels,
        "key_color": target_rgb,
    }
    print(json.dumps(result, ensure_ascii=False))
    sys.exit(1 if has_residue else 0)


if __name__ == "__main__":
    main()
