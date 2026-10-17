#!/usr/bin/env python3
"""Extract text labels from text_only_2048.png.

Unlike extract_reference_components.py (designed for UI controls), this script
is optimised for text: it uses horizontal-only dilation to bridge character
gaps within a line, skips split_by_projection and morphological opening (which
erode thin strokes), then runs connectedComponents on the dilated mask.

Dilation is a grouping device only: the reported geometry (root_box /
original_image_box / relative_parent_box) is the tight bounding box of the
label's foreground pixels on the ORIGINAL (non-dilated) mask. Using the dilated
stats directly would inflate every box by up to (dilate_kernel_width - 1) px
horizontally and shift x left by roughly half that amount.

Output format matches extract_reference_components.py for downstream compatibility.
"""
import argparse
import json
import sys
from pathlib import Path

import cv2
import numpy as np

# Reuse existing key-colour utilities
sys.path.insert(0, str(Path(__file__).parent))
from key_color_processing import key_color_mask, json_safe
from extract_reference_components import percentage_rect, _load_content, _original_image_box, _diagnostic_color


def extract_text_labels(
    image_path: Path,
    transform_path: Path,
    output_dir: Path,
    min_area: int = 64,
    min_dimension: int = 12,
    dilate_kernel_width: int = 40,
) -> dict:
    image_path = image_path.resolve()
    transform_path = transform_path.resolve()
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    transform = json.loads(transform_path.read_text(encoding="utf-8"))
    key_color = transform.get("key_color")
    diag_color = _diagnostic_color(key_color)
    box_rgb = tuple(diag_color[:3])
    text_rgb = (0, 0, 0) if sum(diag_color[:3]) > 382 else (255, 255, 255)
    image, content_rect = _load_content(image_path, transform)
    h, w = image.shape[:2]

    # 1. Foreground mask (text pixels = foreground, key colour = background)
    mask = key_color_mask(image, key_color)

    # 2. Horizontal-only dilation to bridge character gaps within a line
    #    Vertical kernel = 1 so different lines never merge
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (dilate_kernel_width, 1))
    dilated = cv2.dilate(mask, kernel, iterations=1)

    # 3. Connected components on dilated mask
    num_labels, labels, stats, _ = cv2.connectedComponentsWithStats(dilated, connectivity=8)

    components = []
    comp_idx = 0
    for label in range(1, num_labels):  # skip background (0)
        dx, dy, d_width, d_height, d_area = stats[label]

        # Filter on dilated stats to keep detection behaviour stable (the
        # pipeline's --min-area/--min-dimension values were tuned against them).
        if d_area < min_area:
            continue
        if d_width < min_dimension or d_height < min_dimension:
            continue

        # Skip full-frame blob
        if dx <= 0 and dy <= 0 and dx + d_width >= w and dy + d_height >= h:
            continue

        # Geometry from the ORIGINAL mask: dilation only groups same-line
        # characters into one connected component. The dilated bbox would
        # include bridged background strips that are not text pixels.
        # Dilation is extensive, so the tight bbox is always contained in the
        # dilated bbox — scanning only that region avoids a full-image pass
        # per component.
        region = (slice(dy, dy + d_height), slice(dx, dx + d_width))
        label_pixels = (labels[region] == label) & (mask[region] > 0)
        if label_pixels.any():
            ys, xs = np.nonzero(label_pixels)
            x0, x1 = int(xs.min()), int(xs.max())
            y0, y1 = int(ys.min()), int(ys.max())
            x = x0 + dx
            y = y0 + dy
            width = x1 - x0 + 1
            height = y1 - y0 + 1
        else:
            # Defensive: dilation is extensive (every dilated component keeps
            # at least one original foreground pixel), so this should never fire.
            x, y, width, height = int(dx), int(dy), int(d_width), int(d_height)

        comp_idx += 1
        node_id = f"component-{comp_idx:03d}"

        # Bounding box in content-local coordinates
        root = percentage_rect(x, y, width, height, content_rect["width"], content_rect["height"])
        root["image_x"] = x + content_rect.get("x", 0)
        root["image_y"] = y + content_rect.get("y", 0)

        # Original image coordinates (undo scale)
        orig_box = _original_image_box(root, transform, content_rect)
        if orig_box is None:
            scale = transform.get("scale", 1.0)
            orig_box = {
                "x": round(x / scale), "y": round(y / scale),
                "width": round(width / scale), "height": round(height / scale),
            }

        relative_parent_box = {
            "x": x, "y": y, "width": width, "height": height,
            "percentage": root.get("percentage", {}),
            "percent": root.get("percent", {}),
            "image_x": x,
            "image_y": y,
        }

        # Crop from the ORIGINAL (non-dilated) mask for clean text pixels
        crop = mask[y:y + height, x:x + width]
        crop_file = f"component_{comp_idx:03d}.png"
        cv2.imwrite(str(output_dir / crop_file), crop)

        components.append({
            "node_id": node_id,
            "file": crop_file,
            "file_path": str(output_dir / crop_file),
            "root_box": root,
            "original_image_box": orig_box,
            "relative_parent_box": relative_parent_box,
            "relative_box": relative_parent_box,
            "provenance": {
                "source_image": str(image_path),
                "transform": str(transform_path),
                "source_size": transform.get("source_size", {}),
                "square_size": transform.get("square_size", 0),
                "scale": transform.get("scale", 1.0),
                "content_rect": content_rect,
                "contour_box": {"x": x, "y": y, "width": width, "height": height},
            },
            "self_reference": False,
        })

    # Save diagnostics
    cv2.imwrite(str(output_dir / "diagnostic_foreground_mask.png"), mask)
    cv2.imwrite(str(output_dir / "diagnostic_dilated_mask.png"), dilated)

    # Draw bounding boxes on the original image for annotation
    annotated = image.copy()
    for i, comp in enumerate(components):
        box = comp["root_box"]
        x, y = box["image_x"] - content_rect.get("x", 0), box["image_y"] - content_rect.get("y", 0)
        w2, h2 = box["width"], box["height"]
        cv2.rectangle(annotated, (x, y), (x + w2, y + h2), box_rgb, 2)
        cv2.putText(annotated, str(i + 1), (x, y - 5),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, text_rgb, 2)
    cv2.imwrite(str(output_dir / "diagnostic_annotated.png"), annotated)

    result = {
        "components": components,
        "original_image_size": {"width": w, "height": h},
        "content_rect": content_rect,
        "self_reference_warnings": [],
        "diagnostics": {
            "foreground_mask": str(output_dir / "diagnostic_foreground_mask.png"),
            "dilated_mask": str(output_dir / "diagnostic_dilated_mask.png"),
            "annotated": str(output_dir / "diagnostic_annotated.png"),
        },
    }

    output_json = output_dir / "components.json"
    output_json.write_text(json.dumps(json_safe(result), ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Extracted {len(components)} text labels → {output_dir}")
    return result


def main():
    parser = argparse.ArgumentParser(description="Extract text labels from text_only image (horizontal dilation, no projection splitting).")
    parser.add_argument("image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--min-area", type=int, default=64)
    parser.add_argument("--min-dimension", type=int, default=12)
    parser.add_argument("--dilate-width", type=int, default=40)
    args = parser.parse_args()
    extract_text_labels(args.image, args.transform_json, args.output_dir,
                        min_area=args.min_area, min_dimension=args.min_dimension,
                        dilate_kernel_width=args.dilate_width)


if __name__ == "__main__":
    main()
