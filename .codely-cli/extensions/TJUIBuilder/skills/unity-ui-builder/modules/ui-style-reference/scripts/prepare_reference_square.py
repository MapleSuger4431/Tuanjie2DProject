#!/usr/bin/env python3
"""Place a reference at top-left on key-colour padding and scale it to a square.

Analyses the original image to dynamically select a key colour (prioritising
magenta, maximising contrast with image content), then pads the shorter
dimension with that key colour to produce a square canvas.

Usage: {PYTHON} prepare_reference_square.py INPUT_IMAGE OUTPUT_IMAGE TRANSFORM_JSON
Prints one JSON document describing the generated image and its coordinate transform.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from key_color_processing import json_safe, select_key_color

TARGET_SIZE = 2048


def rect_dict(x: int, y: int, width: int, height: int, total_width: int, total_height: int) -> dict:
    normalized = {"x": x / total_width, "y": y / total_height,
                  "width": width / total_width, "height": height / total_height}
    return {
        "x": x, "y": y, "width": width, "height": height,
        # `percentage` remains normalized (0..1) for compatibility.
        "percentage": normalized,
        "percent": {key: value * 100 for key, value in normalized.items()},
    }


def prepare(input_path: Path, output_path: Path, metadata_path: Path, target_size: int = TARGET_SIZE) -> dict:
    with Image.open(input_path) as opened:
        source = opened.convert("RGBA")
    source_width, source_height = source.size
    square_size = max(source_width, source_height)

    # Analyse original image to select the best key colour
    source_arr = np.asarray(source)
    key_color = select_key_color(source_arr)
    kc_rgb = (key_color["rgb"]["r"], key_color["rgb"]["g"], key_color["rgb"]["b"])

    # Pad with key colour (fully opaque)
    canvas = Image.new("RGBA", (square_size, square_size), (*kc_rgb, 255))
    canvas.alpha_composite(source, (0, 0))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    metadata_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, "PNG")
    # API will upscale to target_size; we don't resize in Python to avoid compounding blur
    scale = TARGET_SIZE / square_size
    scaled_width = round(source_width * scale)
    scaled_height = round(source_height * scale)
    metadata = {
        "input_image": str(input_path), "output_image": str(output_path), "target_size": target_size,
        "background_mode": "key_color",
        "key_color": key_color,
        "geometry_contract": {
            "percentage": "normalized 0..1 (legacy)",
            "percent": "0..100 (explicit downstream layout values)",
        },
        "source_size": {"width": source_width, "height": source_height},
        "square_size": square_size,
        "source_square_size": {"width": square_size, "height": square_size},
        "padding": {"left": 0, "top": 0, "right": square_size - source_width,
                    "bottom": square_size - source_height,
                    "percentage": {"right": (square_size - source_width) / square_size,
                                   "bottom": (square_size - source_height) / square_size},
                    "percent": {"right": (square_size - source_width) / square_size * 100,
                                "bottom": (square_size - source_height) / square_size * 100}},
        "original_content_rect": rect_dict(0, 0, source_width, source_height, square_size, square_size),
        "scaled_content_rect": rect_dict(0, 0, scaled_width, scaled_height, target_size, target_size),
        "scale": scale,
    }
    metadata_path.write_text(json.dumps(json_safe(metadata), indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(json_safe(metadata), ensure_ascii=False))
    return metadata


def main() -> None:
    parser = argparse.ArgumentParser(description="Top-left square-pad a reference image with key-colour padding.")
    parser.add_argument("input_image", type=Path)
    parser.add_argument("output_image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("--size", type=int, default=TARGET_SIZE, help="Output square size (default: 2048).")
    args = parser.parse_args()
    if args.size <= 0:
        parser.error("--size must be positive")
    prepare(args.input_image, args.output_image, args.transform_json, args.size)


if __name__ == "__main__":
    main()
