#!/usr/bin/env python3
"""Three-state gate for recursive img2img OUTPUT PAGES (recursive-extraction.md §8.3).

The per-slot rect is a KNOWN input: generation_atlas.py pack persisted it in
``atlas-manifest-{P}.json`` (``items[].slot_rect`` / ``content_rect`` /
``source_path`` / ``source_to_atlas_scale``). This validator replaces the
unconditional per-page ``analyze_multimedia`` with per-slot mechanical checks:

Per slot (slot rect expanded by a tolerance to absorb img2img content shift):
- key-colour coverage: background removed → high. Grossly low without other
  evidence → review (dense slot content can legitimately lower it).
- content islands: mid-size non-key connected components — buttons/icons kept.
- large uniform non-key block: the plate was NOT removed → **mechanically
  certain fail** → that slot goes straight to §8.6 single-slot retry.
- content retention: output non-key mass vs the input ``isolation_input.png``
  non-key mass (scaled by the manifest ratio). img2img legitimately removes
  text/decoration (small share); losing buttons/icons is a large share —
  this closes the "AI dropped a button" blind spot that even the old visual
  Q4 (output-page-only, no input reference) could not see. Low retention → review.

Page verdict: any slot fail → fail (exit 1, those slots retry via §8.6);
any slot review → review (exit 2, visual arbitration limited to flagged slots);
all pass → pass (exit 0, skip the page-level visual entirely).

Usage: {PYTHON} validate_output_page.py PAGE_IMAGE MANIFEST_JSON [--json-output PATH]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from key_color_processing import json_safe, key_color_mask


def _slot_rect_value(item: dict, key: str) -> Optional[Dict[str, int]]:
    rect = item.get(key)
    if not isinstance(rect, dict):
        return None
    try:
        return {"x": int(rect["x"]), "y": int(rect["y"]),
                "width": int(rect["width"]), "height": int(rect["height"])}
    except (KeyError, TypeError, ValueError):
        return None


def _expanded(rect: Dict[str, int], tolerance: float,
              page_w: int, page_h: int) -> Dict[int, int]:
    pad_x = int(round(rect["width"] * tolerance))
    pad_y = int(round(rect["height"] * tolerance))
    x0 = max(0, rect["x"] - pad_x)
    y0 = max(0, rect["y"] - pad_y)
    x1 = min(page_w, rect["x"] + rect["width"] + pad_x)
    y1 = min(page_h, rect["y"] + rect["height"] + pad_y)
    return {"x": x0, "y": y0, "width": max(0, x1 - x0), "height": max(0, y1 - y0)}


def _uniform_large_blocks(fg_mask: np.ndarray, region_rgb: np.ndarray,
                          slot_area: int, uniform_area_ratio: float,
                          uniform_std: float) -> List[dict]:
    """Large solid non-key blocks inside the slot — plate-not-removed evidence."""
    import cv2 as cv2
    num, labels, stats, _ = cv2.connectedComponentsWithStats(fg_mask, connectivity=8)
    blocks: List[dict] = []
    area_threshold = uniform_area_ratio * slot_area
    for label in range(1, num):
        x, y, bw, bh, area = (int(v) for v in stats[label])
        if bw * bh < area_threshold:
            continue
        region = region_rgb[y:y + bh, x:x + bw]
        mask_region = (labels[y:y + bh, x:x + bw] == label)
        pixels = region[mask_region]
        if pixels.size == 0:
            continue
        std = float(np.mean(np.std(pixels.astype(np.float32), axis=0)))
        if std < uniform_std:
            blocks.append({"bbox": {"x": x, "y": y, "width": bw, "height": bh},
                           "area": int(area), "color_std": round(std, 2)})
    return blocks


def validate_output_page(page_path: Path, manifest_path: Path,
                         transform_path: Optional[Path] = None,
                         tolerance: float = 0.075,
                         uniform_area_ratio: float = 0.55,
                         uniform_std: float = 14.0,
                         min_key_coverage: float = 0.10,
                         min_island_side: int = 8,
                         retention_min: float = 0.10) -> dict:
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    key_color = manifest.get("key_color")
    if key_color is None and transform_path is not None:
        key_color = json.loads(Path(transform_path).read_text(encoding="utf-8-sig")).get("key_color")
    if key_color is None:
        raise ValueError("no key_colour in manifest and no --transform fallback")

    with Image.open(page_path) as opened:
        page = np.asarray(opened.convert("RGBA"))
    page_h, page_w = page.shape[:2]
    page_rgb = page[:, :, :3]

    thresholds: Dict[str, Any] = {
        "tolerance": tolerance,
        "uniform_area_ratio": uniform_area_ratio,
        "uniform_std": uniform_std,
        "min_key_coverage": min_key_coverage,
        "min_island_side": min_island_side,
        "retention_min": retention_min,
    }

    slot_results: List[dict] = []
    page_verdict = "pass"
    items = manifest.get("items", [])
    for item in items:
        node_id = item.get("node_id", "<unnamed>")
        slot_rect = _slot_rect_value(item, "slot_rect")
        if slot_rect is None or slot_rect["width"] <= 0 or slot_rect["height"] <= 0:
            slot_results.append({"node_id": node_id, "verdict": "review",
                                 "reasons": ["slot_rect missing/invalid in manifest"]})
            if page_verdict == "pass":
                page_verdict = "review"
            continue
        exp = _expanded(slot_rect, tolerance, page_w, page_h)
        slot_area = slot_rect["width"] * slot_rect["height"]
        region_rgb = page_rgb[exp["y"]:exp["y"] + exp["height"], exp["x"]:exp["x"] + exp["width"]]
        region_full = page[exp["y"]:exp["y"] + exp["height"], exp["x"]:exp["x"] + exp["width"]]
        fg_mask = key_color_mask(region_full, key_color)      # 255 = non-key content
        fg_count = int((fg_mask > 0).sum())
        region_pixels = int(fg_mask.size)
        key_coverage = 1.0 - (fg_count / region_pixels if region_pixels else 0.0)

        import cv2 as cv2
        num, labels, stats, _ = cv2.connectedComponentsWithStats(fg_mask, connectivity=8)
        islands = 0
        for label in range(1, num):
            x, y, bw, bh, _area = (int(v) for v in stats[label])
            if max(bw, bh) >= min_island_side:
                islands += 1

        uniform_blocks = _uniform_large_blocks(fg_mask, region_rgb, slot_area,
                                               uniform_area_ratio, uniform_std)

        # Content retention vs the input isolation image (known input).
        retention = None
        source_path = item.get("source_path")
        scale = item.get("source_to_atlas_scale")
        if source_path and Path(source_path).exists():
            with Image.open(source_path) as opened:
                source_img = np.asarray(opened.convert("RGBA"))
            src_fg = key_color_mask(source_img, key_color)
            src_fg_count = int((src_fg > 0).sum())
            if scale is None:
                source_size = item.get("source_size") or {}
                content_rect = _slot_rect_value(item, "content_rect")
                if source_size and content_rect:
                    sx = float(source_size.get("width", 0)) or float("nan")
                    scale = (content_rect["width"] / sx) if sx else None
            if src_fg_count > 0 and scale:
                expected = src_fg_count * float(scale) ** 2
                actual = fg_count
                retention = actual / expected if expected > 0 else None

        reasons: List[str] = []
        verdict = "pass"
        if uniform_blocks:
            verdict = "fail"
            for block in uniform_blocks:
                reasons.append(
                    f"large uniform non-key block at slot-local {block['bbox']} "
                    f"(bbox area {block['bbox']['width'] * block['bbox']['height']} px, "
                    f"color_std {block['color_std']}) — plate NOT removed; mechanically "
                    "certain, send this slot to §8.6 single-slot retry")
        else:
            if key_coverage < min_key_coverage:
                verdict = "review"
                reasons.append(
                    f"key-colour coverage {key_coverage:.4f} < {min_key_coverage} without "
                    "a uniform-block signature — dense slot content is possible; visual "
                    "arbitration required")
            # Content retention vs the input isolation image. The input contains
            # plate + children, so a clean removal legitimately lowers retention —
            # only a near-zero band is a usable mechanical signal (everything lost).
            if retention is not None and retention < retention_min:
                verdict = "review"
                reasons.append(
                    f"content retention {retention:.3f} < {retention_min} vs input "
                    "isolation image — possible total content loss (children dropped); "
                    "visual arbitration required")
            if islands == 0 and key_coverage > min_key_coverage:
                verdict = "review"
                reasons.append("no content islands detected inside the slot — slot may "
                                "be empty; visual arbitration required")
        slot_results.append({
            "node_id": node_id,
            "verdict": verdict,
            "slot_rect": slot_rect,
            "metrics": {
                "key_coverage": round(key_coverage, 6),
                "content_islands": islands,
                "uniform_blocks": uniform_blocks,
                "content_retention": None if retention is None else round(retention, 4),
            },
            "reasons": reasons,
        })
        if verdict == "fail" and page_verdict != "fail":
            page_verdict = "fail"
        elif verdict == "review" and page_verdict == "pass":
            page_verdict = "review"

    return json_safe({
        "verdict": page_verdict,
        "metrics": {"page": str(page_path), "slots": len(slot_results)},
        "thresholds": thresholds,
        "reasons": [f"{s['node_id']}: {reason}"
                    for s in slot_results for reason in s["reasons"]],
        "slots": slot_results,
        "evidence_files": [],
        "audit_file": None,
    })


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Three-state validation of a recursive img2img output page (§8.3).")
    parser.add_argument("page_image", type=Path)
    parser.add_argument("manifest_json", type=Path,
                        help="atlas-manifest-{P}.json written by generation_atlas.py pack")
    parser.add_argument("--transform", type=Path, default=None,
                        help="transform.json fallback when the manifest lacks key_colour")
    parser.add_argument("--tolerance", type=float, default=0.075,
                        help="slot rect expansion on each side to absorb img2img shift (default 0.075)")
    parser.add_argument("--uniform-area-ratio", type=float, default=0.55,
                        help="bbox area ratio of a non-key block (vs slot area) that counts as plate-not-removed (default 0.55 — high to avoid false fails on single large child elements)")
    parser.add_argument("--uniform-std", type=float, default=14.0)
    parser.add_argument("--min-key-coverage", type=float, default=0.10)
    parser.add_argument("--min-island-side", type=int, default=8)
    parser.add_argument("--retention-min", type=float, default=0.10,
                        help="content retention vs input below this → possible total content loss → review (input contains plate, so clean removal legitimately lowers retention; only near-zero is a signal)")
    parser.add_argument("--json-output", type=Path, default=None,
                        help="audit output (recommended: output-page-validation-{P}.json)")
    args = parser.parse_args()
    try:
        result = validate_output_page(
            args.page_image, args.manifest_json, args.transform,
            tolerance=args.tolerance, uniform_area_ratio=args.uniform_area_ratio,
            uniform_std=args.uniform_std, min_key_coverage=args.min_key_coverage,
            min_island_side=args.min_island_side, retention_min=args.retention_min)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        result = {"verdict": "fail", "metrics": {}, "thresholds": {},
                  "reasons": [f"error: {error}"], "slots": [],
                  "evidence_files": [], "audit_file": None}
    if args.json_output is not None:
        result["audit_file"] = str(args.json_output)
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        args.json_output.write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                    encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return {"pass": 0, "review": 2, "fail": 1}[result["verdict"]]


if __name__ == "__main__":
    sys.exit(main())
