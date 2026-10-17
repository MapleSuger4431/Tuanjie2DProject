#!/usr/bin/env python3
"""Validate a square, key-colour-padded UI-only reference image.

When ``transform.json`` contains a ``key_color`` field (background_mode =
"key_color"), padding/content checks are performed using key-colour similarity
instead of alpha transparency. Backward-compatible: if no key colour is present,
falls back to alpha-based checks.

Usage: {PYTHON} validate_ui_only_reference.py IMAGE TRANSFORM_JSON [--json-output PATH]
  [--baseline text_removed_2048.png]
Three-state gate (shared/contracts.md 三态门禁): exit 0 = pass (skip visual),
2 = review (visual arbitration with the §4 two-question prompt), 1 = fail
(mechanical failures — retry 3b). With --baseline, the over-keying direction
is checked mechanically via a size-band comparison against the 3a output.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict, List, Optional

import numpy as np
from PIL import Image

from key_color_processing import json_safe, key_color_mask


class ReferenceValidationError(ValueError):
    """Raised when a reference cannot safely enter the extraction pipeline."""


def _rect(value: Any, image_width: int, image_height: int) -> Dict[str, int]:
    if not isinstance(value, dict):
        raise ReferenceValidationError("transform.scaled_content_rect must be an object")
    try:
        rect = {key: int(value[key]) for key in ("x", "y", "width", "height")}
    except (KeyError, TypeError, ValueError) as error:
        raise ReferenceValidationError("scaled_content_rect requires integer x, y, width, height") from error
    if rect["x"] < 0 or rect["y"] < 0 or rect["width"] <= 0 or rect["height"] <= 0:
        raise ReferenceValidationError("scaled_content_rect must have non-negative origin and positive size")
    if rect["x"] + rect["width"] > image_width or rect["y"] + rect["height"] > image_height:
        raise ReferenceValidationError("scaled_content_rect falls outside the image")
    return rect


def _region_stats(image: np.ndarray, key_color: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Compute background/foreground statistics for a region.

    With *key_color*: background = pixels matching key colour, foreground = rest.
    Without (backward compat): background = alpha=0 pixels, foreground = alpha>0.
    """
    pixels = int(image.shape[0] * image.shape[1])
    if not pixels:
        return {"pixels": 0, "background_pixels": 0, "background_coverage": 0.0,
                "foreground_pixels": 0, "foreground_coverage": 0.0}
    if key_color is not None:
        fg_mask = key_color_mask(image, key_color)
        background_pixels = int((fg_mask == 0).sum())
        foreground_pixels = int((fg_mask > 0).sum())
    elif image.shape[2] >= 4:
        background_pixels = int((image[:, :, 3] == 0).sum())
        foreground_pixels = int((image[:, :, 3] > 0).sum())
    else:
        background_pixels = 0
        foreground_pixels = pixels
    return {
        "pixels": pixels,
        "background_pixels": background_pixels,
        "background_coverage": background_pixels / pixels,
        "foreground_pixels": foreground_pixels,
        "foreground_coverage": foreground_pixels / pixels,
    }


def _content_components(content_image: np.ndarray, key_color) -> tuple:
    """Connected components of the content foreground mask.

    Returns (num, labels, stats, fg_mask). fg = non-key-colour pixels (key_colour
    mode) or opaque pixels (alpha fallback), consistent with _region_stats.
    """
    import cv2 as cv2
    if key_color is not None:
        fg_mask = key_color_mask(content_image, key_color)
    elif content_image.shape[2] >= 4:
        fg_mask = ((content_image[:, :, 3] > 0).astype(np.uint8)) * 255
    else:
        h, w = content_image.shape[:2]
        fg_mask = np.full((h, w), 255, dtype=np.uint8)
    num, labels, stats, _ = cv2.connectedComponentsWithStats(fg_mask, connectivity=8)
    return num, labels, stats, fg_mask


def validate(image_path: Path, transform_path: Path, target_size: int = 2048,
              min_padding_background_coverage: float = 0.98,
              max_content_background_coverage: float | None = None,
              min_content_foreground_coverage: float = 0.15,
              baseline: Path | None = None,
              max_largest_component_ratio: float = 0.95,
              large_uniform_ratio: float = 0.03,
              uniform_std: float = 12.0,
              baseline_min_side: int = 16,
              baseline_max_area_ratio: float = 0.30,
              baseline_retention_min: float = 0.35) -> Dict[str, Any]:
    """Return three-state diagnostics (shared/contracts.md 三态门禁 contract).

    verdict = fail   → mechanical failures (size/rect/padding/foreground/largest-contour)
    verdict = review → suspected under-keying (large uniform non-key block = plate
                        residue) or over-keying (baseline band component lost)
    verdict = pass    → skip visual; audit JSON records every metric for replay.

    The over-keying direction uses a SIZE-BAND filter against the 3a output
    (text_removed_2048.png) when --baseline is given: only components whose
    bbox LONG side >= baseline_min_side AND area <= baseline_max_area_ratio of
    the content area enter the comparison (v2 方案 B5: 长边 >= 16px — thin bars
    are UI elements too). Large blocks (character art / scene plates — whatever
    they are) NEVER enter the denominator: they legitimately disappear between
    3a and 3b, so no character-art identification is needed.
    """
    if target_size <= 0:
        raise ReferenceValidationError("target_size must be positive")
    if not 0 <= min_padding_background_coverage <= 1 or (max_content_background_coverage is not None and not 0 <= max_content_background_coverage <= 1) or not 0 <= min_content_foreground_coverage <= 1:
        raise ReferenceValidationError("coverage thresholds must be between 0 and 1")
    transform = json.loads(transform_path.read_text(encoding="utf-8"))
    key_color = transform.get("key_color")
    with Image.open(image_path) as opened:
        image = np.asarray(opened.convert("RGBA"))
    height, width = image.shape[:2]
    failures: List[str] = []
    review_reasons: List[str] = []
    if (width, height) != (target_size, target_size):
        failures.append(f"image must be exactly {target_size}x{target_size}, got {width}x{height}")
    try:
        content = _rect(transform.get("scaled_content_rect"), width, height)
    except ReferenceValidationError as error:
        failures.append(str(error))
        content = None

    bg_label = "key_color" if key_color else "transparent"
    diagnostics: Dict[str, Any] = {
        "valid": False, "verdict": None, "image": str(image_path), "transform": str(transform_path),
        "image_size": {"width": width, "height": height}, "target_size": target_size,
        "background_mode": transform.get("background_mode", "unknown"),
        "key_color": key_color,
        "thresholds": {
            "min_padding_background_coverage": min_padding_background_coverage,
            "max_content_background_coverage": max_content_background_coverage,
            "min_content_foreground_coverage": min_content_foreground_coverage,
            "max_largest_component_ratio": max_largest_component_ratio,
            "large_uniform_ratio": large_uniform_ratio,
            "uniform_std": uniform_std,
            "baseline_min_side": baseline_min_side,
            "baseline_max_area_ratio": baseline_max_area_ratio,
            "baseline_retention_min": baseline_retention_min,
        }, "diagnostics": [], "failures": failures, "review_reasons": review_reasons,
        "evidence_files": [], "audit_file": None,
    }
    if content is None:
        diagnostics["verdict"] = "fail"
        return json_safe(diagnostics)
    x, y, content_width, content_height = (content[key] for key in ("x", "y", "width", "height"))
    content_image = image[y:y + content_height, x:x + content_width]
    content_area = content_width * content_height
    diagnostics["scaled_content_rect"] = content
    diagnostics["content"] = _region_stats(content_image, key_color)
    content_fg_coverage = diagnostics["content"]["foreground_coverage"]
    content_bg_coverage = diagnostics["content"]["background_coverage"]
    if content_fg_coverage < min_content_foreground_coverage:
        failures.append(f"content foreground coverage {content_fg_coverage:.6f} is below minimum {min_content_foreground_coverage}; insufficient UI foreground for mask separation")
    if max_content_background_coverage is not None and content_bg_coverage > max_content_background_coverage:
        failures.append(f"content {bg_label} coverage exceeds maximum; reference contains insufficient UI foreground")
    elif max_content_background_coverage is None and content_bg_coverage > 0.95:
        diagnostics["diagnostics"].append(
            f"content {bg_label} coverage is high; sparse HUD layout accepted without an explicit threshold")
    padding_regions: Dict[str, Dict[str, Any]] = {}
    # Right is restricted to content rows; bottom spans full width, so regions never overlap.
    if x + content_width < width:
        padding_regions["right"] = _region_stats(image[y:y + content_height, x + content_width:width], key_color)
    if y + content_height < height:
        padding_regions["bottom"] = _region_stats(image[y + content_height:height, :], key_color)
    for name, stats in padding_regions.items():
        if stats["background_coverage"] < min_padding_background_coverage:
            failures.append(f"{name} padding {bg_label} coverage {stats['background_coverage']:.6f} is below minimum")
    diagnostics["padding_regions"] = padding_regions

    component_metrics: Dict[str, Any] = {}
    if content_fg_coverage > 0:
        num, labels, stats, fg_mask = _content_components(content_image, key_color)
        largest_ratio = 0.0
        uniform_blocks: List[dict] = []
        uniform_area_threshold = large_uniform_ratio * content_area
        for label in range(1, num):
            bx, by, bw, bh, area = (int(v) for v in stats[label])
            if area > largest_ratio * content_area:
                largest_ratio = area / content_area
            if area >= uniform_area_threshold:
                region_rgb = content_image[by:by + bh, bx:bx + bw, :3]
                mask_region = (labels[by:by + bh, bx:bx + bw] == label)
                pixels = region_rgb[mask_region]
                if pixels.size == 0:
                    continue
                std = float(np.mean(np.std(pixels.astype(np.float32), axis=0)))
                if std < uniform_std:
                    uniform_blocks.append({
                        "bbox": {"x": bx, "y": by, "width": bw, "height": bh},
                        "area": area, "color_std": round(std, 2),
                    })
        component_metrics["largest_component_ratio"] = round(largest_ratio, 6)
        component_metrics["large_uniform_components"] = uniform_blocks
        if largest_ratio > max_largest_component_ratio:
            failures.append(
                f"largest foreground component covers {largest_ratio:.4f} of the content "
                f"area (> {max_largest_component_ratio}) — background likely not removed; "
                "one giant contour would swallow all components downstream")
        for block in uniform_blocks:
            review_reasons.append(
                f"large uniform non-key block at content-local {block['bbox']} (area "
                f"{block['area']} px, color_std {block['color_std']}) — possible plate "
                "residue (关键色化不足); visual arbitration required")
        diagnostics["component_metrics"] = component_metrics

        # Over-keying direction: size-band comparison against the 3a baseline.
        if baseline is not None and Path(baseline).exists():
            with Image.open(baseline) as opened:
                baseline_image_full = np.asarray(opened.convert("RGBA"))
            baseline_content = baseline_image_full[y:y + content_height, x:x + content_width]
            b_num, b_labels, b_stats, b_fg_mask = _content_components(baseline_content, key_color)
            suspects: List[dict] = []
            for label in range(1, b_num):
                bx, by, bw, bh, area = (int(v) for v in b_stats[label])
                # v2 方案 B5 规格：长边 >= baseline_min_side（细长元素如分隔条也
                # 是 UI 元素，其丢失必须可检）；面积 <= 上限（大块不进分母）。
                if max(bw, bh) < baseline_min_side:
                    continue
                if area > baseline_max_area_ratio * content_area:
                    continue  # large block (art/scene) — never enters the denominator
                baseline_fg_count = int((b_fg_mask[by:by + bh, bx:bx + bw] > 0).sum())
                ui_fg_count = int((fg_mask[by:by + bh, bx:bx + bw] > 0).sum())
                if baseline_fg_count == 0:
                    continue
                retention = ui_fg_count / baseline_fg_count
                if retention < baseline_retention_min:
                    suspects.append({
                        "bbox": {"x": bx, "y": by, "width": bw, "height": bh},
                        "retention": round(retention, 4),
                    })
            diagnostics["over_removal_suspects"] = suspects
            for suspect in suspects:
                review_reasons.append(
                    f"band component at content-local {suspect['bbox']} retained only "
                    f"{suspect['retention']:.2f} of its 3a pixels in ui_only (< "
                    f"{baseline_retention_min}) — possible over-keying (UI element lost); "
                    "visual arbitration required")

    verdict = "fail" if failures else ("review" if review_reasons else "pass")
    diagnostics["verdict"] = verdict
    diagnostics["valid"] = verdict == "pass"
    return json_safe(diagnostics)


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate a generated UI-only reference and transform metadata (three-state).")
    parser.add_argument("image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--min-padding-background-coverage", type=float, default=0.98,
                        help="Minimum background (key colour or transparent) coverage in padding regions.")
    parser.add_argument("--max-content-background-coverage", type=float,
                        help="Reject content above this background coverage; omitted values are diagnostic only.")
    parser.add_argument("--min-content-foreground-coverage", type=float, default=0.15,
                        help="Reject content below this foreground coverage; ensures enough UI foreground (default: 0.15).")
    parser.add_argument("--baseline", type=Path, default=None,
                        help="3a output (text_removed_2048.png) — enables the over-keying size-band comparison")
    parser.add_argument("--max-largest-component-ratio", type=float, default=0.95,
                        help="Largest foreground component may cover at most this fraction of the content area (doc'd gate)")
    parser.add_argument("--large-uniform-ratio", type=float, default=0.03,
                        help="Non-key component area ratio treated as large for plate-residue suspicion (default 0.03)")
    parser.add_argument("--uniform-std", type=float, default=12.0)
    parser.add_argument("--baseline-min-side", type=int, default=16,
                        help="Baseline band components need bbox min side >= this (default 16)")
    parser.add_argument("--baseline-max-area-ratio", type=float, default=0.30,
                        help="Baseline band components need area <= this fraction of content area (default 0.30) — larger blocks never enter the comparison")
    parser.add_argument("--baseline-retention-min", type=float, default=0.35,
                        help="Baseline band component retention in ui_only below this → over-keying suspicion (default 0.35)")
    # Backward-compatible aliases
    parser.add_argument("--min-padding-transparent-coverage", type=float, default=None,
                        help="[Deprecated alias] Use --min-padding-background-coverage instead.")
    parser.add_argument("--max-content-transparent-coverage", type=float, default=None,
                        help="[Deprecated alias] Use --max-content-background-coverage instead.")
    parser.add_argument("--json-output", type=Path)
    args = parser.parse_args()

    # Resolve deprecated aliases
    min_pad = args.min_padding_background_coverage
    if args.min_padding_transparent_coverage is not None:
        min_pad = args.min_padding_transparent_coverage
    max_content = args.max_content_background_coverage
    if args.max_content_transparent_coverage is not None:
        max_content = args.max_content_transparent_coverage

    try:
        diagnostics = validate(args.image, args.transform_json, args.size, min_pad,
                               max_content, args.min_content_foreground_coverage,
                               baseline=args.baseline,
                               max_largest_component_ratio=args.max_largest_component_ratio,
                               large_uniform_ratio=args.large_uniform_ratio,
                               uniform_std=args.uniform_std,
                               baseline_min_side=args.baseline_min_side,
                               baseline_max_area_ratio=args.baseline_max_area_ratio,
                               baseline_retention_min=args.baseline_retention_min)
    except (OSError, json.JSONDecodeError, ReferenceValidationError) as error:
        diagnostics = {"valid": False, "verdict": "fail", "image": str(args.image),
                       "transform": str(args.transform_json), "failures": [str(error)],
                       "review_reasons": [], "reasons": [str(error)]}
    if args.json_output:
        diagnostics["audit_file"] = str(args.json_output)
    text = json.dumps(diagnostics, ensure_ascii=False, indent=2)
    print(text)
    if args.json_output:
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        args.json_output.write_text(text + "\n", encoding="utf-8")
    raise SystemExit({"pass": 0, "review": 2, "fail": 1}[diagnostics.get("verdict", "fail")])


if __name__ == "__main__":
    main()
