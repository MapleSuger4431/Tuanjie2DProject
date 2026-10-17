#!/usr/bin/env python3
"""Three-state gate for Step 3c text_only images (关键色背景 + 仅普通文字).

Checks the mechanically checkable half of the 3c validation questions:
- ``background_filled``: key-colour coverage of the whole image (gross failure
  = the img2img returned a normal screenshot);
- plain-text-likeness: non-key pixels should form many SMALL thin components
  (text strokes). A LARGE uniform non-key block is mechanical evidence the
  AI returned a screenshot or left a panel/plate in → fail (direct retry).
  A large NON-uniform block (residual UI / art text) is only suspect → review.

Verdicts (shared/contracts.md 三态门禁 contract):
- pass (exit 0): coverage OK + only text-sized components → skip visual.
- review (exit 2): large non-uniform components present → visual arbitration
  with the existing 3c three-question prompt.
- fail (exit 1): coverage below threshold, or a large uniform non-key block
  (mechanically certain — direct 3c retry, no visual needed).

Usage: {PYTHON} validate_text_only.py IMAGE TRANSFORM_JSON [--json-output PATH]
  [--min-key-coverage 0.5] [--large-uniform-ratio 0.05] [--uniform-std 12]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, List

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from key_color_processing import json_safe, key_color_mask


def validate_text_only(image_path: Path, transform_path: Path,
                       min_key_coverage: float = 0.5,
                       large_uniform_ratio: float = 0.05,
                       uniform_std: float = 12.0) -> dict:
    transform = json.loads(Path(transform_path).read_text(encoding="utf-8"))
    key_color = transform.get("key_color")
    if key_color is None:
        raise ValueError("transform.json has no key_colour — cannot validate a key-colour text_only image")
    with Image.open(image_path) as opened:
        image = np.asarray(opened.convert("RGBA"))
    h, w = image.shape[:2]
    image_area = h * w

    fg_mask = key_color_mask(image, key_color)          # 255 = non-key = text pixels
    fg_count = int((fg_mask > 0).sum())
    key_coverage = 1.0 - fg_count / image_area

    thresholds: Dict[str, Any] = {
        "min_key_coverage": min_key_coverage,
        "large_uniform_ratio": large_uniform_ratio,
        "uniform_std": uniform_std,
    }

    import cv2 as cv2
    num, labels, stats, _ = cv2.connectedComponentsWithStats(fg_mask, connectivity=8)
    small_count = 0
    large_uniform: List[dict] = []
    large_nonuniform: List[dict] = []
    large_area_threshold = large_uniform_ratio * image_area
    for label in range(1, num):
        x, y, bw, bh, area = (int(v) for v in stats[label])
        if area < large_area_threshold:
            if area >= 8:  # ignore single-pixel antialiasing noise
                small_count += 1
            continue
        region = image[y:y + bh, x:x + bw, :3]
        mask_region = (labels[y:y + bh, x:x + bw] == label)
        pixels = region[mask_region]
        std = float(np.mean(np.std(pixels.astype(np.float32), axis=0)))
        record = {
            "bbox": {"x": x, "y": y, "width": bw, "height": bh},
            "area": int(area),
            "color_std": round(std, 2),
        }
        if std < uniform_std:
            large_uniform.append(record)
        else:
            large_nonuniform.append(record)

    metrics: Dict[str, Any] = {
        "image": str(image_path),
        "image_size": {"width": w, "height": h},
        "key_coverage": round(key_coverage, 6),
        "text_pixel_ratio": round(fg_count / image_area, 6),
        "small_components": small_count,
        "large_uniform_components": large_uniform,
        "large_nonuniform_components": large_nonuniform,
    }

    verdict = "pass"
    reasons: List[str] = []
    if key_coverage < min_key_coverage:
        verdict = "fail"
        reasons.append(
            f"key-colour coverage {key_coverage:.4f} < {min_key_coverage} — the "
            "image is not a key-colour text extraction (img2img likely returned "
            "a normal screenshot); mechanically certain, retry 3c directly")
    elif large_uniform:
        verdict = "fail"
        for record in large_uniform:
            reasons.append(
                f"large uniform non-key block at {record['bbox']} (area "
                f"{record['area']} px, color_std {record['color_std']}) — a solid "
                "panel/plate left in or screenshot returned; mechanically certain, "
                "retry 3c directly")
    elif large_nonuniform:
        verdict = "review"
        for record in large_nonuniform:
            reasons.append(
                f"large non-uniform non-key block at {record['bbox']} (area "
                f"{record['area']} px) — possible residual UI element or art text; "
                "visual arbitration required (may also be dense text paragraphs)")
    elif small_count == 0:
        verdict = "review"
        reasons.append("no text-sized components found — panel may simply have no "
                       "plain text, but visual arbitration should confirm")

    return json_safe({
        "verdict": verdict,
        "metrics": metrics,
        "thresholds": thresholds,
        "reasons": reasons,
        "evidence_files": [],
        "audit_file": None,
    })


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Three-state validation of a text_only key-colour image (Step 3c).")
    parser.add_argument("image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("--json-output", type=Path, default=None,
                        help="audit/verdict JSON output path (recommended: {processingDir}/text-only-validation.json)")
    parser.add_argument("--min-key-coverage", type=float, default=0.5)
    parser.add_argument("--large-uniform-ratio", type=float, default=0.05)
    parser.add_argument("--uniform-std", type=float, default=12.0)
    args = parser.parse_args()
    try:
        result = validate_text_only(args.image, args.transform_json,
                                    args.min_key_coverage,
                                    args.large_uniform_ratio, args.uniform_std)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        result = {"verdict": "fail", "metrics": {}, "thresholds": {},
                  "reasons": [f"error: {error}"], "evidence_files": [],
                  "audit_file": None}
    if args.json_output is not None:
        result["audit_file"] = str(args.json_output)
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        args.json_output.write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                    encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return {"pass": 0, "review": 2, "fail": 1}[result["verdict"]]


if __name__ == "__main__":
    sys.exit(main())
