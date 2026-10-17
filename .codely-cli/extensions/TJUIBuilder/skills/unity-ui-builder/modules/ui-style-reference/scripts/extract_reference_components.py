#!/usr/bin/env python3
"""Extract UI components from a key-colour-background reference image.

When ``transform.json`` contains a ``key_color`` field, foreground detection
uses key-colour similarity (HSV-based) instead of alpha channel. Component
crops are converted to transparent PNGs (key colour → alpha=0) for downstream
use. Backward-compatible: if no key colour is present, falls back to alpha-based
detection.

Usage: {PYTHON} extract_reference_components.py IMAGE TRANSFORM_JSON OUTPUT_DIR
Optional --parent-content-rect X Y WIDTH HEIGHT makes emitted component coordinates parent-relative.
Prints a JSON manifest and writes transparent PNG components plus diagnostic masks.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

import cv2
import numpy as np
from PIL import Image

from key_color_processing import (filter_component_contours,
                                  filter_contained_bboxes,
                                  detect_adjacent_components,
                                  foreground_mask, json_safe,
                                  rgba_from_alpha,
                                  split_by_projection)


def _diagnostic_color(key_color: Optional[dict]) -> tuple[int, int, int, int]:
    """Return an RGBA colour that contrasts with the key colour for diagnostic overlays."""
    if key_color and "rgb" in key_color:
        rgb = key_color["rgb"]
        if isinstance(rgb, dict):
            r, g, b = rgb["r"], rgb["g"], rgb["b"]
        else:
            r, g, b = rgb[:3]
        return (255 - r, 255 - g, 255 - b, 255)
    return (0, 255, 0, 255)


def percentage_rect(x: int, y: int, width: int, height: int, basis_width: int, basis_height: int) -> dict:
    normalized = {"x": x / basis_width, "y": y / basis_height,
                  "width": width / basis_width, "height": height / basis_height}
    return {"x": x, "y": y, "width": width, "height": height,
            # `percentage` is retained for legacy consumers and remains normalized (0..1).
            "percentage": normalized,
            # `percent` is the unambiguous 0..100 geometry contract for new consumers.
            "percent": {key: value * 100 for key, value in normalized.items()}}


def _load_content(image_path: Path, transform: dict) -> Tuple[np.ndarray, Dict[str, int]]:
    with Image.open(image_path) as opened:
        image = np.asarray(opened.convert("RGBA"))
    rect = transform.get("scaled_content_rect")
    if rect is None:
        # No content rect specified — use the entire image as content (e.g. isolation images)
        h, w = image.shape[:2]
        return image.copy(), {"x": 0, "y": 0, "width": w, "height": h}
    x, y, width, height = (int(rect[key]) for key in ("x", "y", "width", "height"))
    if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > image.shape[1] or y + height > image.shape[0]:
        raise ValueError("scaled_content_rect falls outside the reference image")
    return image[y:y + height, x:x + width].copy(), {"x": x, "y": y, "width": width, "height": height}


def _original_image_box(root_box: Dict[str, int], transform: dict, content_rect: Dict[str, int]) -> Optional[dict]:
    """Map content-local geometry to original-image pixels when source metadata exists.

    Legacy manifests can omit source metadata. New uploads always receive it from
    prepare_reference_square.py. Edges are rounded before deriving width/height so
    neighbouring boxes retain shared boundaries after the 2048-to-source mapping.
    """
    source = transform.get("source_size")
    if source is None:
        return None
    if not isinstance(source, dict):
        raise ValueError("transform.source_size must be an object")
    try:
        source_width, source_height = (int(source[key]) for key in ("width", "height"))
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError("transform.source_size requires positive width and height") from error
    if source_width <= 0 or source_height <= 0:
        raise ValueError("transform.source_size requires positive width and height")

    square_size = transform.get("square_size")
    scale = transform.get("scale")
    if square_size is not None and int(square_size) <= 0:
        raise ValueError("transform.square_size must be positive")
    if scale is not None and float(scale) <= 0:
        raise ValueError("transform.scale must be positive")

    def map_edge(value: int, content_size: int, source_size: int) -> int:
        mapped = round(value * source_size / content_size)
        return max(0, min(source_size, mapped))

    left = map_edge(root_box["x"], content_rect["width"], source_width)
    top = map_edge(root_box["y"], content_rect["height"], source_height)
    right = map_edge(root_box["x"] + root_box["width"], content_rect["width"], source_width)
    bottom = map_edge(root_box["y"] + root_box["height"], content_rect["height"], source_height)
    return percentage_rect(left, top, max(0, right - left), max(0, bottom - top), source_width, source_height)


def _expand_bbox(orig_mask: np.ndarray, all_boxes: List[tuple],
                  self_idx: int, x: int, y: int, w: int, h: int) -> tuple[int, int, int, int]:
    """Expand bounding rect using original mask, stopping at other components.

    Expands into rows/columns that have foreground in orig_mask within the
    current perpendicular range.  Stops when the row/column overlaps another
    component's bbox or reaches the image edge.

    Processing order is area-ascending (smallest first) so that small components
    claim their surrounding space before larger neighbours expand into it.
    This makes the result dependent on component ordering — it is a deliberate
    design choice to protect small UI elements from being absorbed by neighbours.
    """
    mh, mw = orig_mask.shape
    # Collect other components' bboxes for collision detection
    others = [b for i, b in enumerate(all_boxes) if i != self_idx]

    def _row_hits_others(ry: int) -> bool:
        for ox, oy, ow, oh in others:
            if ry >= oy and ry < oy + oh and not (x + w <= ox or ox + ow <= x):
                return True
        return False

    def _col_hits_others(cx: int) -> bool:
        for ox, oy, ow, oh in others:
            if cx >= ox and cx < ox + ow and not (y + h <= oy or oy + oh <= y):
                return True
        return False

    # Expand top
    while y > 0 and not _row_hits_others(y - 1) and orig_mask[y - 1, x:x + w].any():
        y -= 1; h += 1
    # Expand bottom
    while y + h < mh and not _row_hits_others(y + h) and orig_mask[y + h, x:x + w].any():
        h += 1
    # Expand left
    while x > 0 and not _col_hits_others(x - 1) and orig_mask[y:y + h, x - 1].any():
        x -= 1; w += 1
    # Expand right
    while x + w < mw and not _col_hits_others(x + w) and orig_mask[y:y + h, x + w].any():
        w += 1
    # Second pass: top/bottom may need re-expansion after left/right changed w
    while y > 0 and not _row_hits_others(y - 1) and orig_mask[y - 1, x:x + w].any():
        y -= 1; h += 1
    while y + h < mh and not _row_hits_others(y + h) and orig_mask[y + h, x:x + w].any():
        h += 1
    return x, y, w, h


def extract(image_path: Path, transform_path: Path, output_dir: Path,
            parent_rect: Optional[Sequence[int]] = None, min_area: int = 64,
            min_dimension: int = 32, crop_padding: int = 4, border_clear_size: int = 0) -> dict:
    image_path = image_path.resolve()
    transform_path = transform_path.resolve()
    output_dir = output_dir.resolve()
    transform = json.loads(transform_path.read_text(encoding="utf-8"))
    key_color = transform.get("key_color")
    diag_color = _diagnostic_color(key_color)
    image, content_rect = _load_content(image_path, transform)
    mask = foreground_mask(image, key_color)
    orig_mask = mask.copy()
    if border_clear_size > 0:
        mask[:border_clear_size, :] = 0
        mask[-border_clear_size:, :] = 0
        mask[:, :border_clear_size] = 0
        mask[:, -border_clear_size:] = 0
        orig_mask[:border_clear_size, :] = 0
        orig_mask[-border_clear_size:, :] = 0
        orig_mask[:, :border_clear_size] = 0
        orig_mask[:, -border_clear_size:] = 0
    mask = split_by_projection(mask)
    contours, comp_labels, label_ids = filter_component_contours(mask, min_area=min_area, min_dimension=min_dimension)
    output_dir.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(output_dir / "diagnostic_foreground_mask.png"), mask)
    diagnostics = image.copy()

    # Pre-compute all bounding rects to detect overlaps for safe padding
    all_boxes = []
    for contour in contours:
        bx, by, bw, bh = cv2.boundingRect(contour)
        all_boxes.append((bx, by, bw, bh))

    # Expand bboxes using orig_mask, stopping at other components' bboxes.
    # Process smallest first so small components claim their space before
    # larger neighbours expand into it.
    expand_order = sorted(range(len(all_boxes)), key=lambda i: all_boxes[i][2] * all_boxes[i][3])
    expanded_boxes = list(all_boxes)
    for i in expand_order:
        bx, by, bw, bh = all_boxes[i]
        nx, ny, nw, nh = _expand_bbox(orig_mask, expanded_boxes, i, bx, by, bw, bh)
        expanded_boxes[i] = (nx, ny, nw, nh)

    # Determine containment: a large box that entirely contains a smaller box
    # (small box area > 10% of large box) is discarded — it is likely a border
    # fragment or background blob, not a real UI component.  The inner boxes are
    # kept as independent root-level components.  Full-frame contours (touching
    # all four canvas edges) are never discarded.
    bbox_items = [{"idx": i, "bbox": {"x": b[0], "y": b[1], "width": b[2], "height": b[3]}}
                  for i, b in enumerate(expanded_boxes)]
    # Bug fix: use actual image dimensions for touches_all_edges check instead of
    # content_rect from transform.json.  In recursive layers, the slot image is
    # resized by generation_atlas.py split, but transform.json still records the
    # original isolation_input dimensions.  This mismatch causes a blob covering
    # the entire actual image to not be detected as full-frame, so it becomes a
    # container and swallows all inner children.
    img_h, img_w = image.shape[:2]
    kept_items = filter_contained_bboxes(bbox_items, img_w, img_h)
    kept_indices = {item["idx"] for item in kept_items}
    # Map parent index -> list of child contour indices that were filtered into it
    child_indices_by_parent: Dict[int, List[int]] = {i: [] for i in kept_indices}
    for item in bbox_items:
        if item["idx"] in kept_indices:
            continue
        cx2, cy2, cw2, ch2 = expanded_boxes[item["idx"]]
        # Find the smallest containing parent
        best_parent = -1
        best_area = float("inf")
        for parent_idx in kept_indices:
            px, py, pw, ph = expanded_boxes[parent_idx]
            if (cx2 >= px and cy2 >= py and cx2 + cw2 <= px + pw and cy2 + ch2 <= py + ph):
                area = pw * ph
                if area < best_area:
                    best_area = area
                    best_parent = parent_idx
        if best_parent >= 0:
            child_indices_by_parent[best_parent].append(item["idx"])

    def _overlaps_any(box_a: tuple[int, int, int, int], exclude_idx: int) -> bool:
        ax, ay, aw, ah = box_a
        for j, (bx, by, bw, bh) in enumerate(expanded_boxes):
            if j == exclude_idx:
                continue
            if not (ax + aw <= bx or bx + bw <= ax or ay + ah <= by or by + bh <= ay):
                return True
        return False

    components: List[dict] = []
    parent = None
    if parent_rect is not None:
        px, py, pw, ph = map(int, parent_rect)
        if pw <= 0 or ph <= 0 or px < 0 or py < 0 or px + pw > content_rect["width"] or py + ph > content_rect["height"]:
            raise ValueError("parent content rect must be within the scaled content rect")
        parent = {"x": px, "y": py, "width": pw, "height": ph}
    for index, contour in enumerate(contours):
        if index not in kept_indices:
            continue
        x, y, width, height = expanded_boxes[index]
        # Expand crop by crop_padding if it does not overlap with adjacent components
        pad = crop_padding
        if pad > 0 and _overlaps_any((x - pad, y - pad, width + 2 * pad, height + 2 * pad), index):
            pad = 0
        cx = max(0, x - pad)
        cy = max(0, y - pad)
        cw = min(content_rect["width"] - cx, width + 2 * pad)
        ch = min(content_rect["height"] - cy, height + 2 * pad)
        root = percentage_rect(cx, cy, cw, ch, content_rect["width"], content_rect["height"])
        root["image_x"] = cx + content_rect["x"]
        root["image_y"] = cy + content_rect["y"]
        original_image_box = _original_image_box(root, transform, content_rect)
        if parent is not None:
            within_parent = cx >= parent["x"] and cy >= parent["y"] and cx + cw <= parent["x"] + parent["width"] and cy + ch <= parent["y"] + parent["height"]
            if not within_parent:
                continue
            relative = percentage_rect(cx - parent["x"], cy - parent["y"], cw, ch, parent["width"], parent["height"])
        else:
            relative = root.copy()
        # Transparent pixels are preserved; contour masking limits the result
        # to the selected connected component plus any filtered-out children
        # whose contours were merged into this parent.
        component_rgba = rgba_from_alpha(image[cy:cy + ch, cx:cx + cw], key_color)
        # Build a per-component mask using the connected-component label.
        # This prevents adjacent components' foreground pixels from leaking
        # into this component's alpha channel when their pixels happen to fall
        # within this component's bounding box.
        #
        # The label mask comes from the morphologically cleaned mask (after
        # projection splitting), so it may have lost pixels that were cut by
        # projection valleys.  We dilate the label mask slightly and AND with
        # the original foreground mask (orig_mask, before any cutting) to
        # recover those lost pixels while still excluding other components.
        _label_kernel = np.ones((5, 5), np.uint8)
        this_label = label_ids[index]
        this_label_mask = (comp_labels == this_label).astype(np.uint8) * 255
        this_dilated = cv2.dilate(this_label_mask, _label_kernel, iterations=1)
        component_full_mask = cv2.bitwise_and(orig_mask, this_dilated)
        # Add child component label masks (filtered-out components assigned to
        # this parent by filter_contained_bboxes).
        for child_idx in child_indices_by_parent.get(index, []):
            child_label = label_ids[child_idx]
            child_label_mask = (comp_labels == child_label).astype(np.uint8) * 255
            child_dilated = cv2.dilate(child_label_mask, _label_kernel, iterations=1)
            child_full_mask = cv2.bitwise_and(orig_mask, child_dilated)
            component_full_mask = cv2.bitwise_or(component_full_mask, child_full_mask)
        component_mask = component_full_mask[cy:cy + ch, cx:cx + cw].copy()
        component_rgba[:, :, 3] = np.minimum(component_rgba[:, :, 3], component_mask)
        filename = f"component_{len(components) + 1:03d}.png"
        component_path = output_dir / filename
        Image.fromarray(component_rgba, "RGBA").save(component_path)
        cv2.rectangle(diagnostics, (cx, cy), (cx + cw - 1, cy + ch - 1), diag_color, 2)
        node_id = f"component-{len(components) + 1:03d}"
        components.append({
            "node_id": node_id,
            "file": filename,
            "file_path": str(component_path),
            "root_box": root,
            "original_image_box": original_image_box,
            "relative_parent_box": relative,
            "relative_box": relative,  # legacy alias
            "provenance": {
                "source_image": str(image_path),
                "transform": str(transform_path),
                "source_size": transform.get("source_size"),
                "square_size": transform.get("square_size"),
                "scale": transform.get("scale"),
                "content_rect": content_rect,
                "contour_box": {"x": cx, "y": cy, "width": cw, "height": ch},
                "original_mapping": "rounded content-local edges clamped to source-image bounds",
                "geometry_contract": {
                    "percentage": "normalized 0..1 (legacy)",
                    "percent": "0..100 (explicit downstream layout values)",
                },
            },
        })
    Image.fromarray(diagnostics, "RGBA").save(output_dir / "diagnostic_root_boxes.png")
    manifest_path = output_dir / "components.json"

    # --- Self-reference detection: flag components covering >=90% of parent ---
    self_reference_warnings = []
    parent_area = content_rect["width"] * content_rect["height"]
    for comp in components:
        rb = comp["root_box"]
        comp_area = rb["width"] * rb["height"]
        if parent_area > 0 and comp_area / parent_area >= 0.90:
            comp["self_reference"] = True
            self_reference_warnings.append(
                f"{comp['node_id']}: covers {100*comp_area/parent_area:.1f}% of parent "
                f"(>=90% threshold) — likely AI failed to remove background; "
                f"do NOT mark recursion_done, enter retry flow"
            )
        else:
            comp["self_reference"] = False

    manifest = {
        "schema": "ui-reference-components/v3",
        "geometry_contract": {
            "percentage": "normalized 0..1 (legacy)",
            "percent": "0..100 (explicit downstream layout values)",
        },
        "image": str(image_path), "transform": str(transform_path), "output_dir": str(output_dir),
        "manifest_path": str(manifest_path), "content_rect": content_rect,
        "parent_content_rect": parent, "component_count": len(components),
        "components": components,
        "adjacent_pairs": detect_adjacent_components(
            [{"node_id": c["node_id"], "bbox": c["root_box"]} for c in components]),
        "diagnostics": {"foreground_mask": str(output_dir / "diagnostic_foreground_mask.png"),
                        "root_boxes": str(output_dir / "diagnostic_root_boxes.png")},
        "self_reference_warnings": self_reference_warnings,
    }
    manifest_path.write_text(json.dumps(json_safe(manifest), indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(json_safe(manifest), ensure_ascii=False))
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract components from a UI reference image (key colour or alpha).")
    parser.add_argument("image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--parent-content-rect", type=int, nargs=4, metavar=("X", "Y", "WIDTH", "HEIGHT"))
    parser.add_argument("--min-area", type=int, default=64)
    parser.add_argument("--min-dimension", type=int, default=32)
    parser.add_argument("--border-clear-size", type=int, default=0)
    args = parser.parse_args()
    if args.min_area < 1:
        parser.error("--min-area must be at least 1")
    if args.min_dimension < 1:
        parser.error("--min-dimension must be at least 1")
    extract(args.image, args.transform_json, args.output_dir, args.parent_content_rect, args.min_area, args.min_dimension, border_clear_size=args.border_clear_size)


if __name__ == "__main__":
    main()
