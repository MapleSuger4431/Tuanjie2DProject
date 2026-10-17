#!/usr/bin/env python3
"""Generation atlas: pack nodes into 2048x2048 key-colour pages, split AI output back to individual PNGs.

When ``atlas-items.json`` contains a ``key_color`` field, the canvas is filled
with that key colour instead of transparent padding. During ``split --clean``,
key-colour pixels are converted to alpha=0 (transparent) before noise cleanup
and trimming. Backward-compatible: if no key colour is present, falls back to
transparent/alpha-based behaviour.

Subcommands:
  pack   – Read atlas-items.json, bin-pack into 2048x2048 key-colour pages + manifests.
  split  – Read AI output page + manifest, crop per-slot, validate, optionally clean.
  retry  – Re-pack only failed items into new pages with higher resolution.

Usage:
  {PYTHON} generation_atlas.py pack --items items.json --output-dir out/
  {PYTHON} generation_atlas.py split --page out.png --manifest out/manifest-0.json --output-dir nodes/
  {PYTHON} generation_atlas.py retry --items items.json --failed-ids failed.json --output-dir retry/ --retry-round 1
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import numpy as np
from PIL import Image

from key_color_processing import json_safe, key_color_mask

PAGE_SIZE = 2048
DEFAULT_GUTTER = 32
MIN_CONTENT_PX = 64
MAX_EXPORT_LONG_EDGE = 1024
MIN_EXPORT_SHORT_EDGE = 64
ASPECT_TOLERANCE = 0.005  # 0.5%

# Recipe-specific minimum content pixels (higher = more space per item)
RECIPE_MIN_CONTENT = {
    "direct_children_without_text": 128,
    "leaf_clean": 64,
    "leaf_plate_no_label": 64,
    "art_text": 128,
    "composite_shell": 128,
}


# ---------------------------------------------------------------------------
# Export size calculation
# ---------------------------------------------------------------------------

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

    # Cap long edge
    long_edge = max(dw, dh)
    if long_edge > MAX_EXPORT_LONG_EDGE:
        scale = MAX_EXPORT_LONG_EDGE / long_edge
        dw, dh = dw * scale, dh * scale

    # Pick a single uniform scale factor, round dominant edge, derive other from aspect
    if dw >= dh:
        w = max(1, round(dw))
        h = max(1, round(w / aspect))
    else:
        h = max(1, round(dh))
        w = max(1, round(h * aspect))

    # Check short edge
    if min(w, h) < MIN_EXPORT_SHORT_EDGE:
        if w <= h:
            w = max(MIN_EXPORT_SHORT_EDGE, w)
            h = max(1, round(w / aspect))
        else:
            h = max(MIN_EXPORT_SHORT_EDGE, h)
            w = max(1, round(h * aspect))

    return {"width": w, "height": h}


# ---------------------------------------------------------------------------
# Shelf-based bin packing
# ---------------------------------------------------------------------------

def _scale_to_fit(src_w: float, src_h: float, avail_w: float, avail_h: float) -> Tuple[float, float]:
    """Scale (src_w, src_h) to fit inside (avail_w, avail_h), preserving aspect ratio."""
    if src_w <= 0 or src_h <= 0:
        return (0.0, 0.0)
    aspect = src_w / src_h
    w = min(src_w, avail_w)
    h = w / aspect
    if h > avail_h:
        h = avail_h
        w = h * aspect
    return (w, h)


def shelf_pack(items: List[Dict[str, Any]], page_size: int, gutter: int,
               min_content_px: int, max_items_per_page: int = 0) -> List[List[Dict[str, Any]]]:
    """Deterministic shelf-based bin packing.

    Items sorted by height (descending).  Each item keeps its source aspect ratio;
    no rotation, no splitting.  Unused area left transparent (caller's job).
    Returns a list of pages; each page is a list of items with ``slot_rect``,
    ``content_rect`` and ``source_to_atlas_scale`` filled in.
    """
    sorted_items = sorted(items, key=lambda i: (-i["source_size"]["height"],
                                                i["source_size"]["width"]))
    pages: List[List[Dict[str, Any]]] = []
    current: List[Dict[str, Any]] = []
    shelf_y = gutter
    shelf_x = gutter
    shelf_h = 0

    def _flush_page() -> None:
        nonlocal current, shelf_y, shelf_x, shelf_h
        if current:
            pages.append(current)
        current = []
        shelf_y = gutter
        shelf_x = gutter
        shelf_h = 0

    for item in sorted_items:
        src_w = float(item["source_size"]["width"])
        src_h = float(item["source_size"]["height"])
        if src_w <= 0 or src_h <= 0:
            item["slot_rect"] = None
            item["content_rect"] = None
            item["source_to_atlas_scale"] = 0.0
            continue

        max_w = page_size - 2 * gutter
        max_h = page_size - 2 * gutter

        # --- attempt 1: current shelf ---
        avail_w = page_size - shelf_x - gutter
        avail_h = page_size - shelf_y - gutter
        cw, ch = _scale_to_fit(src_w, src_h, avail_w, avail_h)

        below = cw < min_content_px or ch < min_content_px
        fits_shelf = cw > 0 and ch > 0 and not below

        if not fits_shelf:
            # --- attempt 2: new shelf (full width) ---
            new_shelf_y = shelf_y + shelf_h + gutter if current else shelf_y
            avail_w2 = max_w
            avail_h2 = page_size - new_shelf_y - gutter
            if avail_h2 >= min_content_px:
                cw2, ch2 = _scale_to_fit(src_w, src_h, avail_w2, avail_h2)
                if cw2 >= min_content_px and ch2 >= min_content_px:
                    shelf_y = new_shelf_y
                    shelf_x = gutter
                    shelf_h = 0
                    cw, ch = cw2, ch2
                    fits_shelf = True

        if not fits_shelf:
            # --- attempt 3: new page ---
            _flush_page()
            cw, ch = _scale_to_fit(src_w, src_h, max_w, max_h)
            if cw < min_content_px or ch < min_content_px:
                # Even an exclusive page can't reach threshold – pack anyway, mark risk
                item["_below_threshold"] = True

        if max_items_per_page and len(current) >= max_items_per_page:
            _flush_page()

        cw, ch = max(1, round(cw)), max(1, round(ch))
        slot_x = shelf_x - gutter
        slot_y = shelf_y - gutter
        slot_w = cw + 2 * gutter
        slot_h = ch + 2 * gutter

        item["slot_rect"] = {"x": slot_x, "y": slot_y, "width": slot_w, "height": slot_h}
        item["content_rect"] = {"x": shelf_x, "y": shelf_y, "width": cw, "height": ch}
        item["source_to_atlas_scale"] = round(cw / src_w, 6) if src_w > 0 else 0.0

        shelf_x += cw + gutter
        shelf_h = max(shelf_h, ch)
        current.append(item)

    _flush_page()
    return pages


# ---------------------------------------------------------------------------
# Pack subcommand
# ---------------------------------------------------------------------------

def _file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else ""


def _composite_page(items: List[Dict[str, Any]], page_size: int,
                    key_color: Optional[Dict[str, Any]] = None) -> np.ndarray:
    """Composite source images onto a key-colour (or transparent) canvas.

    When *key_color* is provided, the canvas is filled with the key colour
    (fully opaque). Source images with alpha are composited via alpha blending,
    so their transparent areas reveal the key colour underneath.
    """
    if key_color is not None:
        kc = key_color["rgb"]
        bg = (int(kc["r"]), int(kc["g"]), int(kc["b"]), 255)
    else:
        bg = (0, 0, 0, 0)
    canvas = Image.new("RGBA", (page_size, page_size), bg)
    for item in items:
        src_path = Path(item["source_path"])
        if not src_path.is_file():
            continue
        with Image.open(src_path) as opened:
            src = opened.convert("RGBA")
        cr = item["content_rect"]
        scaled = src.resize((cr["width"], cr["height"]), Image.LANCZOS)
        canvas.alpha_composite(scaled, (cr["x"], cr["y"]))
    return np.asarray(canvas)


def cmd_pack(args: argparse.Namespace) -> None:
    spec = json.loads(Path(args.items).read_text(encoding="utf-8"))
    page_size = args.page_size if args.page_size != PAGE_SIZE else spec.get("page_size", PAGE_SIZE)
    gutter = args.gutter if args.gutter != DEFAULT_GUTTER else spec.get("gutter_px", DEFAULT_GUTTER)
    recipe = spec.get("recipe", "")
    min_content = RECIPE_MIN_CONTENT.get(recipe, MIN_CONTENT_PX)
    if args.min_content > 0:
        min_content = args.min_content

    items = spec.get("items", [])
    for item in items:
        if "source_size" not in item:
            with Image.open(item["source_path"]) as opened:
                item["source_size"] = {"width": opened.width, "height": opened.height}
        if "source_aspect_ratio" not in item:
            sw, sh = item["source_size"]["width"], item["source_size"]["height"]
            item["source_aspect_ratio"] = round(sw / sh, 6) if sh > 0 else 1.0
        item["export_size"] = calculate_export_size(
            item["source_size"], item.get("logical_size"))
        item["retry_round"] = args.retry_round
        item["status"] = "pending"

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    key_color = spec.get("key_color")
    background_mode = spec.get("background_mode", "key_color" if key_color else "transparent")

    pages = shelf_pack(items, page_size, gutter, min_content, args.max_items_per_page)

    for page_index, page_items in enumerate(pages):
        canvas = _composite_page(page_items, page_size, key_color)
        page_path = output_dir / f"input-page-{page_index}.png"
        Image.fromarray(canvas, "RGBA").save(page_path)

        manifest_items = []
        for item in page_items:
            cr = item["content_rect"]
            sr = item["slot_rect"]
            es = item["export_size"]
            src_path = Path(item["source_path"])
            manifest_items.append({
                "node_id": item["node_id"],
                "source_path": str(src_path),
                "source_size": item["source_size"],
                "source_aspect_ratio": item["source_aspect_ratio"],
                "logical_size": item.get("logical_size"),
                "export_size": es,
                "slot_rect": sr,
                "content_rect": cr,
                "source_to_atlas_scale": item["source_to_atlas_scale"],
                "atlas_to_export_scale": round(es["width"] / cr["width"], 6) if cr["width"] > 0 else 0.0,
                "source_hash": _file_hash(src_path),
                "retry_round": item["retry_round"],
                "status": item["status"],
                "below_threshold": item.get("_below_threshold", False),
            })

        manifest = {
            "schema": "ui-reference-generation-atlas/v1",
            "atlas_kind": spec.get("atlas_kind", "unknown"),
            "recipe": recipe,
            "depth": spec.get("depth"),
            "page_index": page_index,
            "page_size": {"width": page_size, "height": page_size},
            "background_mode": background_mode,
            "key_color": key_color,
            "gutter_px": gutter,
            "page_image": str(page_path),
            "items": manifest_items,
        }
        manifest_path = output_dir / f"atlas-manifest-{page_index}.json"
        manifest_path.write_text(
            json.dumps(json_safe(manifest), indent=2, ensure_ascii=False), encoding="utf-8")

    result = {"pages": len(pages), "items": len(items),
              "output_dir": str(output_dir)}
    print(json.dumps(result, ensure_ascii=False))


# ---------------------------------------------------------------------------
# Split subcommand
# ---------------------------------------------------------------------------

GUTTER_CONTAMINATION_THRESHOLD = 200  # max contaminated pixels allowed in gutter


def _check_gutter(page: np.ndarray, slot_rect: Dict[str, int],
                  content_rect: Dict[str, int],
                  key_color: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Check if the gutter area (slot minus content) is background (key colour or transparent)."""
    sx, sy = slot_rect["x"], slot_rect["y"]
    sw, sh = slot_rect["width"], slot_rect["height"]
    cx, cy = content_rect["x"], content_rect["y"]
    cw, ch = content_rect["width"], content_rect["height"]

    slot_region = page[sy:sy + sh, sx:sx + sw]
    if slot_region.shape[:2] != (sh, sw):
        return {"clean": False, "reason": "slot_out_of_bounds", "contaminated_pixels": -1}

    # Build gutter mask: True for gutter pixels (slot minus content area)
    gutter_mask = np.ones((sh, sw), dtype=bool)
    rel_cx, rel_cy = cx - sx, cy - sy
    gutter_mask[rel_cy:rel_cy + ch, rel_cx:rel_cx + cw] = False

    # Gutter is clean if gutter pixels are background (key colour or alpha=0)
    if key_color is not None:
        fg = key_color_mask(slot_region, key_color)
        gutter_visible = gutter_mask & (fg > 10)
    elif slot_region.shape[2] >= 4:
        gutter_visible = gutter_mask & (slot_region[:, :, 3] > 10)
    else:
        gutter_visible = gutter_mask
    contaminated = int(gutter_visible.sum())
    return {"clean": contaminated <= GUTTER_CONTAMINATION_THRESHOLD, "contaminated_pixels": contaminated}


def _check_content_nonempty(region: np.ndarray,
                            key_color: Optional[Dict[str, Any]] = None) -> bool:
    """Check if the content region has any visible (non-background) pixels."""
    if region.size == 0:
        return False
    if key_color is not None:
        fg = key_color_mask(region, key_color)
        visible = fg > 10
    elif region.shape[2] >= 4:
        visible = region[:, :, 3] > 10
    else:
        visible = np.ones(region.shape[:2], dtype=bool)
    return bool(visible.any())


def _check_aspect_ratio(content_region: np.ndarray, expected_aspect: float,
                        key_color: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Check if the content region's visual aspect ratio matches expected."""
    if key_color is not None:
        fg = key_color_mask(content_region, key_color)
        rows = np.any(fg > 10, axis=1)
        cols = np.any(fg > 10, axis=0)
    elif content_region.shape[2] < 4:
        return {"ok": True, "actual_aspect": expected_aspect}
    else:
        alpha = content_region[:, :, 3]
        rows = np.any(alpha > 10, axis=1)
        cols = np.any(alpha > 10, axis=0)
    if not rows.any() or not cols.any():
        return {"ok": False, "reason": "empty_content"}
    rmin, rmax = int(np.where(rows)[0][0]), int(np.where(rows)[0][-1])
    cmin, cmax = int(np.where(cols)[0][0]), int(np.where(cols)[0][-1])
    vw, vh = cmax - cmin + 1, rmax - rmin + 1
    if vw <= 0 or vh <= 0:
        return {"ok": False, "reason": "empty_content"}
    actual = vw / vh
    diff = abs(actual - expected_aspect) / max(expected_aspect, 1e-6)
    return {"ok": diff <= 0.05 + ASPECT_TOLERANCE, "actual_aspect": round(actual, 4),
            "expected_aspect": expected_aspect, "diff_pct": round(diff * 100, 2)}


def _clean_alpha(image: np.ndarray,
                 key_color: Optional[Dict[str, Any]] = None) -> np.ndarray:
    """Clean noise; convert key colour → alpha if key_color is provided.

    - With *key_color*: key-colour pixels → alpha=0, other pixels → alpha=255,
      then suppress low-alpha noise.
    - Without: clean existing alpha noise (alpha < 10 → 0).
    """
    result = image.copy()
    if key_color is not None:
        rgb = result[:, :, :3]
        mask = key_color_mask(result, key_color)
        alpha = mask.copy()
        alpha[alpha < 10] = 0
        result = np.dstack((rgb, alpha))
    else:
        low_alpha = result[:, :, 3] < 10
        result[low_alpha, 3] = 0
    return result


def cmd_split(args: argparse.Namespace) -> None:
    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    # --page takes priority over manifest's page_image
    if args.page:
        page_path = Path(args.page)
    else:
        page_path = Path(manifest["page_image"])

    with Image.open(page_path) as opened:
        page = np.asarray(opened.convert("RGBA"))

    key_color = manifest.get("key_color")
    background_mode = manifest.get("background_mode", "key_color" if key_color else "transparent")

    # --- AI unchanged output detection: compare input page vs output page ---
    input_page_path = None
    if "page_image" in manifest:
        candidate = Path(manifest["page_image"])
        # page_image points to input-page; only compare if it differs from --page
        if args.page and str(candidate.resolve()) != str(page_path.resolve()):
            input_page_path = candidate
    ai_unchanged = False
    if input_page_path is not None and input_page_path.exists():
        with Image.open(input_page_path) as inp:
            input_arr = np.asarray(inp.convert("RGBA"))
        if input_arr.shape == page.shape and np.array_equal(input_arr, page):
            ai_unchanged = True

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    results: List[Dict[str, Any]] = []
    for item in manifest["items"]:
        node_id = item["node_id"]
        sr = item["slot_rect"]
        cr = item["content_rect"]
        es = item["export_size"]

        # --- AI unchanged output: fail all items ---
        if ai_unchanged:
            results.append({"node_id": node_id, "status": "failed",
                            "reasons": ["ai_unchanged_output"]})
            continue

        # --- gutter contamination check ---
        gutter_result = _check_gutter(page, sr, cr, key_color)

        # --- crop content_rect ---
        content = page[cr["y"]:cr["y"] + cr["height"], cr["x"]:cr["x"] + cr["width"]].copy()
        if content.shape[0] != cr["height"] or content.shape[1] != cr["width"]:
            results.append({"node_id": node_id, "status": "failed",
                            "reason": "content_rect_out_of_bounds"})
            continue

        # --- non-empty check ---
        has_content = _check_content_nonempty(content, key_color)

        # --- aspect ratio check (soft warning, not a hard gate) ---
        aspect_check = _check_aspect_ratio(content, item["source_aspect_ratio"], key_color)
        aspect_warning = gutter_result["clean"] and has_content and not aspect_check["ok"]

        # --- resolution check (skip — content_rect may be scaled down by packing) ---
        res_ok = True

        # Hard failures: gutter contamination or empty content.
        # aspect_mismatch is a SOFT warning — AI may slightly adjust layout,
        # but the slot content is still valid for downstream OpenCV.
        passed = gutter_result["clean"] and has_content and res_ok

        if passed:
            if args.clean:
                cleaned = _clean_alpha(content, key_color)
                # Trim background borders to actual content bbox
                alpha = cleaned[:, :, 3]
                rows = np.any(alpha > 10, axis=1)
                cols = np.any(alpha > 10, axis=0)
                if rows.any() and cols.any():
                    rmin, rmax = int(np.where(rows)[0][0]), int(np.where(rows)[0][-1]) + 1
                    cmin, cmax = int(np.where(cols)[0][0]), int(np.where(cols)[0][-1]) + 1
                    trimmed = cleaned[rmin:rmax, cmin:cmax]
                else:
                    trimmed = cleaned
                # Scale to export_size preserving aspect ratio
                img = Image.fromarray(trimmed, "RGBA")
                if img.size != (es["width"], es["height"]):
                    resample = Image.LANCZOS if es["width"] <= cr["width"] else Image.BICUBIC
                    img = img.resize((es["width"], es["height"]), resample)
                out_path = output_dir / f"{node_id}.png"
                img.save(out_path)
            else:
                out_path = output_dir / f"{node_id}.png"
                Image.fromarray(content, "RGBA").save(out_path)

            # Auto-generate transform.json for downstream OpenCV extraction
            if args.clean:
                tw, th = img.size
            else:
                tw, th = cr["width"], cr["height"]
            transform_data = {
                "source_size": {"width": tw, "height": th},
                "scaled_content_rect": {"x": 0, "y": 0, "width": tw, "height": th},
                "background_mode": background_mode,
                "key_color": key_color,
                "square_size": max(tw, th),
            }
            (output_dir / f"{node_id}.transform.json").write_text(
                json.dumps(transform_data, indent=2, ensure_ascii=False), encoding="utf-8")

            results.append({
                "node_id": node_id, "status": "passed",
                "output": str(out_path),
                "content_size": {"width": cr["width"], "height": cr["height"]},
                "export_size": es,
                "gutter_clean": gutter_result["clean"],
                "aspect_warning": aspect_warning,
                "aspect_diff_pct": aspect_check.get("diff_pct", 0) if aspect_warning else 0,
                "transform_json": str(output_dir / f"{node_id}.transform.json"),
            })
        else:
            reasons = []
            if not gutter_result["clean"]:
                reasons.append(f"gutter_contamination({gutter_result['contaminated_pixels']}px)")
            if not has_content:
                reasons.append("empty_content")
            # aspect_mismatch is NOT a hard failure reason — slot is still usable
            if not res_ok:
                reasons.append(f"resolution_insufficient({cr['width']}x{cr['height']}<{es['width']}x{es['height']})")
            results.append({"node_id": node_id, "status": "failed", "reasons": reasons})

    summary = {
        "manifest": str(args.manifest),
        "page": str(page_path),
        "total": len(results),
        "passed": sum(1 for r in results if r["status"] == "passed"),
        "failed": sum(1 for r in results if r["status"] == "failed"),
        "ai_unchanged_output": ai_unchanged,
        "results": results,
    }
    result_path = output_dir / "split-results.json"
    result_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description="Generation atlas pack/split/retry.")
    sub = parser.add_subparsers(dest="command", required=True)

    p_pack = sub.add_parser("pack", help="Pack items into atlas pages.")
    p_pack.add_argument("--items", required=True, type=Path, help="atlas-items.json path")
    p_pack.add_argument("--output-dir", required=True, type=Path)
    p_pack.add_argument("--page-size", type=int, default=PAGE_SIZE)
    p_pack.add_argument("--gutter", type=int, default=DEFAULT_GUTTER)
    p_pack.add_argument("--retry-round", type=int, default=0)
    p_pack.add_argument("--max-items-per-page", type=int, default=0)
    p_pack.add_argument("--min-content", type=int, default=0)
    p_pack.set_defaults(func=cmd_pack)

    p_split = sub.add_parser("split", help="Split AI output page into individual PNGs.")
    p_split.add_argument("--page", type=Path, help="AI output page image (overrides manifest)")
    p_split.add_argument("--manifest", required=True, type=Path)
    p_split.add_argument("--output-dir", required=True, type=Path)
    p_split.add_argument("--clean", action="store_true", help="Clean alpha noise + scale to export_size")
    p_split.set_defaults(func=cmd_split)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
