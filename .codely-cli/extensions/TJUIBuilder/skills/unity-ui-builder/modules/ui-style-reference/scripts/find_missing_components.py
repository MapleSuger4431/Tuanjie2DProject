#!/usr/bin/env python3
"""Find missed components by comparing baseline vs downgraded-parameter extraction.

Implements the "框外遗漏" (missed elements) DISCOVERY half of the opencv-recovery
§2/§2.5 posterior comparison. This is the first step of the documented recovery
strategy (lower ``--min-area`` 64→32 and ``--min-dimension`` 32→16 and re-run
``extract_reference_components.py``), promoted to a detection tool:

1. Run the downgrade extraction into a separate output dir;
2. Feed both components.json manifests here. A downgrade box is a MISSED-
   ELEMENT CANDIDATE when it is NOT explained by any baseline box:
   - IoU with every baseline box < --iou-threshold, AND
   - less than --containment-threshold of its area lies inside a baseline box, AND
   - its min bbox side >= --min-candidate-side (noise-fragment filter);
   - and it is not ≥80% contained in a larger kept candidate (dedupe).
3. Baseline boxes below --tiny-max-area are flagged as "框太小" (mechanical).

Verdicts (shared/contracts.md 三态门禁 contract):
- pass (exit 0): zero candidates and zero tiny boxes → skip the diagnostic-image
  visual entirely (the remaining visual question — 框内多元素/过度合并 — is
  genuinely semantic and stays visual per the v2 plan B6).
- review (exit 2): candidates exist → visual arbitration limited to the
  candidate regions (one composite annotated call), or direct §1.1 recovery.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, List

sys.path.insert(0, str(Path(__file__).parent))
from key_color_processing import json_safe


def _box_of(component: dict):
    box = component.get("root_box") or {}
    try:
        return (int(box["x"]), int(box["y"]), int(box["width"]), int(box["height"]))
    except (KeyError, TypeError, ValueError):
        return None


def _intersection(a, b) -> int:
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0, min(ay + ah, by + bh) - max(ay, by))
    return ix * iy


def _iou(a, b) -> float:
    inter = _intersection(a, b)
    if inter == 0:
        return 0.0
    union = a[2] * a[3] + b[2] * b[3] - inter
    return inter / union if union > 0 else 0.0


def _contained_ratio(inner, outer) -> float:
    inter = _intersection(inner, outer)
    area = inner[2] * inner[3]
    return inter / area if area > 0 else 0.0


def find_missing(baseline_path: Path, downgraded_path: Path,
                 iou_threshold: float = 0.1,
                 containment_threshold: float = 0.8,
                 min_candidate_side: int = 16,
                 tiny_max_area: int = 256,
                 oversized_area_ratio: float = 0.40) -> dict:
    baseline_manifest = json.loads(Path(baseline_path).read_text(encoding="utf-8-sig"))
    downgraded_manifest = json.loads(Path(downgraded_path).read_text(encoding="utf-8-sig"))
    content_rect = baseline_manifest.get("content_rect") or {}
    try:
        content_w = int(content_rect["width"])
        content_h = int(content_rect["height"])
    except (KeyError, TypeError, ValueError):
        content_w = content_h = 0
    baseline = []
    for comp in baseline_manifest.get("components", []):
        box = _box_of(comp)
        if box:
            baseline.append((comp.get("node_id", "<unnamed>"), box))
    downgraded = []
    for comp in downgraded_manifest.get("components", []):
        box = _box_of(comp)
        if box:
            downgraded.append((comp.get("node_id", "<unnamed>"), box))

    candidates: List[Dict] = []
    for node_id, box in downgraded:
        if min(box[2], box[3]) < min_candidate_side:
            continue  # noise fragment at the degraded sensitivity
        if any(_iou(box, base_box) >= iou_threshold for _, base_box in baseline):
            continue
        if any(_contained_ratio(box, base_box) >= containment_threshold
               for _, base_box in baseline):
            continue
        candidates.append({"node_id": node_id,
                           "root_box": {"x": box[0], "y": box[1],
                                        "width": box[2], "height": box[3]},
                           "min_side": int(min(box[2], box[3])),
                           "area": int(box[2] * box[3])})

    # Dedupe: drop candidates ≥80% contained in a strictly larger kept candidate.
    deduped: List[Dict] = []
    for cand in sorted(candidates, key=lambda c: -c["area"]):
        box = (cand["root_box"]["x"], cand["root_box"]["y"],
               cand["root_box"]["width"], cand["root_box"]["height"])
        if any(_contained_ratio(box, (kept["root_box"]["x"], kept["root_box"]["y"],
                                      kept["root_box"]["width"], kept["root_box"]["height"]))
               >= containment_threshold for kept in deduped):
            continue
        deduped.append(cand)

    tiny_boxes: List[Dict] = []
    oversized_boxes: List[Dict] = []
    for node_id, box in baseline:
        if box[2] * box[3] < tiny_max_area:
            tiny_boxes.append({"node_id": node_id,
                               "root_box": {"x": box[0], "y": box[1],
                                            "width": box[2], "height": box[3]},
                               "area": int(box[2] * box[3])})
        # §1.3 over-merge trigger, mechanized: a component covering >= 40% of the
        # content area (that does not touch all four content edges — full-frame
        # contours are containers, handled by filter_contained_bboxes) is an
        # over-merge suspect → visual arbitration for 框内多元素.
        if (content_w > 0 and content_h > 0
                and box[2] * box[3] >= oversized_area_ratio * content_w * content_h
                and not (box[0] <= 0 and box[1] <= 0
                         and box[0] + box[2] >= content_w
                         and box[1] + box[3] >= content_h)):
            oversized_boxes.append({"node_id": node_id,
                                    "root_box": {"x": box[0], "y": box[1],
                                                 "width": box[2], "height": box[3]},
                                    "content_area_ratio": round(
                                        box[2] * box[3] / (content_w * content_h), 4)})

    reasons: List[str] = []
    for cand in deduped:
        reasons.append(
            f"candidate missed element {cand['root_box']} (min side {cand['min_side']}) "
            "— not explained by any baseline box; visual arbitration on the candidate "
            "region or §1.1 original-image recovery")
    for tiny in tiny_boxes:
        reasons.append(
            f"baseline box {tiny['root_box']} (area {tiny['area']} px < "
            f"{tiny_max_area}) is suspiciously small (框太小) — mechanical finding")
    for big in oversized_boxes:
        reasons.append(
            f"baseline box {big['root_box']} covers {big['content_area_ratio']:.2f} of "
            "the content area — over-merge suspect (框内多元素, §1.3); visual "
            "arbitration required")

    verdict = "pass" if not deduped and not tiny_boxes and not oversized_boxes else "review"
    return json_safe({
        "verdict": verdict,
        "metrics": {
            "baseline_components": len(baseline),
            "downgraded_components": len(downgraded),
            "candidates": len(deduped),
            "tiny_boxes": len(tiny_boxes),
            "oversized_boxes": len(oversized_boxes),
        },
        "thresholds": {
            "iou_threshold": iou_threshold,
            "containment_threshold": containment_threshold,
            "min_candidate_side": min_candidate_side,
            "tiny_max_area": tiny_max_area,
            "oversized_area_ratio": oversized_area_ratio,
        },
        "reasons": reasons,
        "candidate_boxes": deduped,
        "tiny_boxes": tiny_boxes,
        "oversized_boxes": oversized_boxes,
        "evidence_files": [],
        "audit_file": None,
    })


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Detect missed UI components via downgraded-parameter extraction diff (§2/§2.5).")
    parser.add_argument("baseline_components_json", type=Path,
                        help="components.json from the baseline extraction")
    parser.add_argument("downgraded_components_json", type=Path,
                        help="components.json from the re-run with --min-area 32 --min-dimension 16")
    parser.add_argument("--iou-threshold", type=float, default=0.1)
    parser.add_argument("--containment-threshold", type=float, default=0.8)
    parser.add_argument("--min-candidate-side", type=int, default=16)
    parser.add_argument("--tiny-max-area", type=int, default=256)
    parser.add_argument("--oversized-area-ratio", type=float, default=0.40,
                        help="baseline box covering >= this fraction of content_rect area → over-merge suspect (§1.3)")
    parser.add_argument("--json-output", type=Path, default=None,
                        help="audit output (recommended: missing-components-check.json)")
    args = parser.parse_args()
    try:
        result = find_missing(args.baseline_components_json, args.downgraded_components_json,
                              args.iou_threshold, args.containment_threshold,
                              args.min_candidate_side, args.tiny_max_area,
                              args.oversized_area_ratio)
    except (OSError, json.JSONDecodeError) as error:
        result = {"verdict": "fail", "metrics": {}, "thresholds": {},
                  "reasons": [f"error: {error}"], "candidate_boxes": [],
                  "tiny_boxes": [], "oversized_boxes": [],
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
