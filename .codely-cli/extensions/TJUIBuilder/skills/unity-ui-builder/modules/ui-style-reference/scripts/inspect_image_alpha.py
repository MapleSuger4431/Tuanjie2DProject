#!/usr/bin/env python3
"""Inspect the alpha channel of an image and report transparency statistics.

Used on final transparent PNGs (after key-colour → alpha conversion) to verify
alpha channel presence and content bounding box.

Usage: {PYTHON} inspect_image_alpha.py IMAGE_PATH [--json]
Prints JSON with transparent ratio, content bounding box, and corner alpha values.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def inspect(image_path: Path) -> dict:
    with Image.open(image_path) as opened:
        img = opened.convert("RGBA")
    arr = np.array(img)
    alpha = arr[:, :, 3]
    total = alpha.size
    fully_transparent = int((alpha == 0).sum())
    fully_opaque = int((alpha == 255).sum())
    semi = int(((alpha > 0) & (alpha < 255)).sum())

    rows = np.any(alpha > 10, axis=1)
    cols = np.any(alpha > 10, axis=0)
    if rows.any():
        rmin, rmax = int(np.where(rows)[0][0]), int(np.where(rows)[0][-1])
        cmin, cmax = int(np.where(cols)[0][0]), int(np.where(cols)[0][-1])
        content_box = {"x": cmin, "y": rmin, "width": cmax - cmin + 1, "height": rmax - rmin + 1}
    else:
        content_box = None

    h, w = arr.shape[:2]
    corners = {
        "top_left": int(arr[0, 0, 3]),
        "top_right": int(arr[0, w - 1, 3]),
        "bottom_left": int(arr[h - 1, 0, 3]),
        "bottom_right": int(arr[h - 1, w - 1, 3]),
    }

    transparent_ratio = fully_transparent / total if total else 0.0
    return {
        "image": str(image_path),
        "size": {"width": w, "height": h},
        "alpha": {
            "fully_transparent": fully_transparent,
            "fully_opaque": fully_opaque,
            "semi_transparent": semi,
            "transparent_ratio": round(transparent_ratio, 4),
        },
        "content_box": content_box,
        "corners": corners,
        "has_transparency": transparent_ratio > 0.01,
        "warning": transparent_ratio < 0.5 if content_box else True,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Inspect image alpha channel transparency.")
    parser.add_argument("image", type=Path)
    parser.add_argument("--json", action="store_true", help="Output as formatted JSON")
    args = parser.parse_args()
    result = inspect(args.image)
    print(json.dumps(result, indent=2, ensure_ascii=False) if args.json else json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
