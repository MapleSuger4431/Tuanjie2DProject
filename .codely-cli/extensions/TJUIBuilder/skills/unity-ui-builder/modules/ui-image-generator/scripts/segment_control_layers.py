#!/usr/bin/env python3
"""Segment a functional-control transparent PNG into visual layers (geometry producer).

Implements the script half of the v2 方案 item C: **geometry from pixels, semantics
from vision**. The old §3.1 flow asked the AI to estimate ``size_ratio`` /
``fills_parent`` — the last active "AI produces geometry" path; AI ratio estimates
drift exactly like the §8.4.5 text-label percentages did.

Pipeline:
- alpha connected-component segmentation (4-connectivity + small morphological
  close to glue antialiasing fragments);
- near blobs merged (hand-drawn controls shatter into pieces);
- per-component colour clustering (k=2): a baked track+fill/knob shows as ONE
  alpha component but TWO colour clusters. Coherent clusters split into two
  layers; clusters closer than --color-separation mark the layer
  ``indistinguishable`` — the deterministic signal for the §3.1 rule
  "fill 与 track 视觉完全相同 → 不提取 fill";
- ``size_ratio`` = layer bbox / **alpha-visible bbox** (NOT the canvas — padded
  canvases would skew ratios), ``fills_parent`` = both axis ratios >= 0.9.

Output: layers JSON (bbox / size_ratio / fills_parent / colour stats /
indistinguishable) + annotated diagnostic PNG. ``layer_count < 2`` sets
``fallback: true`` — the caller then falls back to the legacy AI-only analysis,
explicitly marked 未经脚本几何 (prompt-engineering.md §3.1).

Semantics stay with the AI: the follow-up analyse call names each numbered
layer (``layer_name`` / ``visual_description`` / visibility) and infers
invisible parts (``expanded_panel_bg``). AI geometry fields are discarded.

Usage: {PYTHON} segment_control_layers.py IMAGE [--json-output PATH]
Exit codes (三态门禁): 0 = pass (>=2 layers, script geometry authoritative),
2 = review (fallback: <2 layers), 1 = error.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import numpy as np
from PIL import Image

try:  # shared helper when run next to ui-style-reference scripts; local fallback otherwise
    from key_color_processing import json_safe as _json_safe
except ImportError:  # ui-image-generator scripts may not sit next to key_color_processing
    def _json_safe(value: Any) -> Any:
        if isinstance(value, np.ndarray):
            return [_json_safe(item) for item in value.tolist()]
        if isinstance(value, np.generic):
            return value.item()
        if isinstance(value, dict):
            return {str(k): _json_safe(v) for k, v in value.items()}
        if isinstance(value, (list, tuple)):
            return [_json_safe(v) for v in value]
        return value


def _bbox_of(mask: np.ndarray) -> Optional[Tuple[int, int, int, int]]:
    ys, xs = np.nonzero(mask)
    if ys.size == 0:
        return None
    return (int(xs.min()), int(ys.min()),
            int(xs.max()) - int(xs.min()) + 1, int(ys.max()) - int(ys.min()) + 1)


def _merge_near(blobs: List[Tuple[Tuple[int, int, int, int], np.ndarray]],
                 merge_gap: int) -> List[Tuple[Tuple[int, int, int, int], np.ndarray]]:
    """Merge blobs whose bboxes are within merge_gap (Chebyshev) of each other."""
    merged = list(blobs)
    changed = True
    while changed:
        changed = False
        out: List[Tuple[Tuple[int, int, int, int], np.ndarray]] = []
        used = [False] * len(merged)
        for i, (box_i, mask_i) in enumerate(merged):
            if used[i]:
                continue
            group = [(box_i, mask_i)]
            used[i] = True
            for j in range(i + 1, len(merged)):
                if used[j]:
                    continue
                box_j, mask_j = merged[j]
                gap_x = max(0, max(box_j[0] - (box_i[0] + box_i[2]),
                                   box_i[0] - (box_j[0] + box_j[2])))
                gap_y = max(0, max(box_j[1] - (box_i[1] + box_i[3]),
                                   box_i[1] - (box_j[1] + box_j[3])))
                if max(gap_x, gap_y) <= merge_gap:
                    group.append((box_j, mask_j))
                    used[j] = True
                    changed = True
            if len(group) == 1:
                out.append((box_i, mask_i))
            else:
                union = np.zeros_like(mask_i)
                x0 = min(b[0] for b, _ in group)
                y0 = min(b[1] for b, _ in group)
                x1 = max(b[0] + b[2] for b, _ in group)
                y1 = max(b[1] + b[3] for b, _ in group)
                canvas = np.zeros((y1 - y0, x1 - x0), dtype=bool)
                for b, m in group:
                    canvas[b[1] - y0:b[1] - y0 + m.shape[0],
                          b[0] - x0:b[0] - x0 + m.shape[1]] |= m.astype(bool)
                out.append(((x0, y0, x1 - x0, y1 - y0), canvas))
        merged = out
    return merged


def _cluster_split(rgb: np.ndarray, mask: np.ndarray,
                   color_separation: float, cluster_min_share: float
                   ) -> Tuple[List[np.ndarray], bool, Dict[str, Any]]:
    """Try to split one alpha component into two colour layers (k-means k=2).

    Returns (masks, indistinguishable, audit). When the two centroids are closer
    than color_separation the component is single-coloured → indistinguishable
    (the §3.1 "same fill and track" signal). When clusters are spatially
    incoherent (scattered pixels), no split happens.
    """
    import cv2 as cv2
    pixels = rgb[mask]
    audit: Dict[str, Any] = {"attempted": False}
    if len(pixels) < 8:
        return [mask], False, audit
    audit["attempted"] = True
    data = pixels.astype(np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 20, 1.0)
    _, labels_arr, centers = cv2.kmeans(data, 2, None, criteria, 3,
                                        cv2.KMEANS_PP_CENTERS)
    centers = centers.astype(np.float32)
    separation = float(np.linalg.norm(centers[0] - centers[1]))
    audit["centroid_separation"] = round(separation, 2)
    labels_flat = labels_arr.ravel()
    share0 = float((labels_flat == 0).sum()) / len(labels_flat)
    share1 = 1.0 - share0
    audit["cluster_shares"] = [round(share0, 3), round(share1, 3)]
    if separation < color_separation:
        return [mask], True, audit  # single colour — fill/track indistinguishable
    if min(share0, share1) < cluster_min_share:
        return [mask], False, audit  # one dominant colour — not two visual layers

    h, w = mask.shape
    label_img = np.zeros((h, w), np.uint8)
    ys, xs = np.nonzero(mask)
    label_img[ys, xs] = (labels_flat + 1).astype(np.uint8)
    layer_masks: List[np.ndarray] = []
    coherent = True
    for cluster in (1, 2):
        cluster_mask = label_img == cluster
        cluster_area = int(cluster_mask.sum())
        if cluster_area == 0:
            coherent = False
            break
        num_c, _, cstats, _ = cv2.connectedComponentsWithStats(
            cluster_mask.astype(np.uint8), connectivity=8)
        if num_c <= 1:
            coherent = False
            break
        # Coherence = the cluster's pixels live in at most a few big regions.
        # A handle drawn over a track BISECTS the track into two halves — that
        # is a legitimate single layer, so "largest component >= 70%" would be
        # wrong. Top-3 components covering >= 85% accepts the bisection while
        # rejecting spatially interleaved noise (thousands of tiny fragments).
        top = sorted((int(v) for v in cstats[1:, cv2.CC_STAT_AREA]), reverse=True)[:3]
        if sum(top) / cluster_area < 0.85:
            coherent = False  # scattered pixels of this colour — not one layer
            break
        layer_masks.append(cluster_mask)
    if not coherent or len(layer_masks) != 2:
        audit["split_rejected"] = "clusters spatially incoherent"
        return [mask], False, audit
    audit["split"] = True
    return layer_masks, False, audit


def segment_control_layers(image_path: Path,
                           alpha_threshold: int = 8,
                           merge_gap: int = 4,
                           top_n: int = 8,
                           min_layer_area: int = 24,
                           color_separation: float = 60.0,
                           cluster_min_share: float = 0.15) -> dict:
    import cv2 as cv2
    with Image.open(image_path) as opened:
        image = np.asarray(opened.convert("RGBA")).copy()
    h, w = image.shape[:2]
    rgb = image[:, :, :3]
    alpha_mask = image[:, :, 3] > alpha_threshold
    alpha_bbox = _bbox_of(alpha_mask)
    if alpha_bbox is None:
        return {
            "verdict": "review", "fallback": True, "layer_count": 0, "layers": [],
            "alpha_bbox": None, "image_size": {"width": w, "height": h},
            "metrics": {}, "thresholds": {}, "reasons": ["no opaque pixels"],
            "evidence_files": [], "audit_file": None,
        }
    bx, by, bw, bh = alpha_bbox
    basis_w, basis_h = max(1, bw), max(1, bh)

    # Small morphological close glues antialiasing fragments before 4-connectivity.
    closed = cv2.morphologyEx((alpha_mask.astype(np.uint8)) * 255,
                              cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    num, labels, stats, _ = cv2.connectedComponentsWithStats(closed, connectivity=4)
    blobs: List[Tuple[Tuple[int, int, int, int], np.ndarray]] = []
    for label in range(1, num):
        x, y, cwid, chgt, area = (int(v) for v in stats[label])
        if area < min_layer_area:
            continue
        local = (labels[y:y + chgt, x:x + cwid] == label)
        blobs.append(((x, y, cwid, chgt), local))
    blobs = _merge_near(blobs, merge_gap)

    # Per blob: colour-cluster split for baked multi-colour components.
    layer_records: List[dict] = []
    for box, local_mask in blobs:
        gx0, gy0, gw, gh = box
        global_mask = np.zeros((h, w), bool)
        global_mask[gy0:gy0 + local_mask.shape[0], gx0:gx0 + local_mask.shape[1]] = local_mask
        masks, indistinguishable, audit = _cluster_split(
            rgb, global_mask, color_separation, cluster_min_share)
        for m in masks:
            layer_bbox = _bbox_of(m)
            if layer_bbox is None:
                continue
            pixels = rgb[m]
            mean_rgb = [round(float(v), 1) for v in pixels.mean(axis=0)]
            quant = (pixels // 32) * 32
            keys, counts = np.unique(quant, axis=0, return_counts=True)
            dominant = keys[int(np.argmax(counts))]
            layer_records.append({
                "bbox": {"x": layer_bbox[0], "y": layer_bbox[1],
                         "width": layer_bbox[2], "height": layer_bbox[3]},
                "pixel_count": int(m.sum()),
                "fills_parent": bool(layer_bbox[2] / basis_w >= 0.9
                                     and layer_bbox[3] / basis_h >= 0.9),
                "size_ratio": {
                    "width": round(layer_bbox[2] / basis_w, 4),
                    "height": round(layer_bbox[3] / basis_h, 4),
                },
                "color": {
                    "mean_rgb": mean_rgb,
                    "dominant_bucket": [int(v) for v in dominant],
                },
                "indistinguishable": bool(indistinguishable),
                "split_audit": audit,
            })
    layer_records.sort(key=lambda rec: -rec["pixel_count"])
    layer_records = layer_records[:top_n]
    for index, rec in enumerate(layer_records):
        rec["layer_index"] = index + 1

    fallback = len(layer_records) < 2
    verdict = "review" if fallback else "pass"
    reasons: List[str] = []
    if fallback:
        reasons.append(
            "fewer than 2 layers segmented — fall back to the legacy AI-only "
            "analysis and mark the result 未经脚本几何 (prompt-engineering.md §3.1)")
    for rec in layer_records:
        if rec["indistinguishable"]:
            reasons.append(
                f"layer {rec['layer_index']} is single-coloured (fill/track "
                "indistinguishable) — apply the §3.1 rule: do not extract a separate fill")

    return _json_safe({
        "verdict": verdict,
        "fallback": fallback,
        "layer_count": len(layer_records),
        "layers": layer_records,
        "alpha_bbox": {"x": bx, "y": by, "width": bw, "height": bh},
        "image_size": {"width": w, "height": h},
        "metrics": {"blob_count": len(blobs)},
        "thresholds": {
            "alpha_threshold": alpha_threshold, "merge_gap": merge_gap,
            "top_n": top_n, "min_layer_area": min_layer_area,
            "color_separation": color_separation,
            "cluster_min_share": cluster_min_share,
        },
        "reasons": reasons,
        "evidence_files": [],
        "audit_file": None,
    })


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Segment a functional control transparent PNG into visual layers "
                    "(script geometry for §3.1; AI keeps semantics only).")
    parser.add_argument("image", type=Path)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--merge-gap", type=int, default=4)
    parser.add_argument("--top-n", type=int, default=8)
    parser.add_argument("--min-layer-area", type=int, default=24)
    parser.add_argument("--color-separation", type=float, default=60.0)
    parser.add_argument("--cluster-min-share", type=float, default=0.15)
    parser.add_argument("--json-output", type=Path, default=None,
                        help="layers JSON output (also receives an annotated PNG sibling)")
    args = parser.parse_args()
    try:
        result = segment_control_layers(
            args.image, alpha_threshold=args.alpha_threshold, merge_gap=args.merge_gap,
            top_n=args.top_n, min_layer_area=args.min_layer_area,
            color_separation=args.color_separation, cluster_min_share=args.cluster_min_share)
    except (OSError, ValueError) as error:
        result = {"verdict": "fail", "fallback": True, "layer_count": 0, "layers": [],
                  "metrics": {}, "thresholds": {}, "reasons": [f"error: {error}"],
                  "evidence_files": [], "audit_file": None}

    # Annotated diagnostic: pure-red numbered boxes on the original (AI-semantics
    # step re-annotates the same boxes; this file is for human review).
    if args.json_output is not None:
        try:
            import cv2 as cv2
            with Image.open(args.image) as opened:
                base = np.asarray(opened.convert("RGBA")).copy()
            for rec in result.get("layers", []):
                box = rec["bbox"]
                cv2.rectangle(base, (box["x"], box["y"]),
                              (box["x"] + box["width"] - 1, box["y"] + box["height"] - 1),
                              (255, 0, 0, 255), 2)
                cv2.putText(base, str(rec["layer_index"]),
                            (box["x"], max(10, box["y"] - 4)),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 0, 0, 255), 2)
            annotated = args.json_output.with_suffix(".annotated.png")
            Image.fromarray(base, "RGBA").save(annotated)
            result["evidence_files"] = [str(annotated)]
        except Exception:
            pass
        result["audit_file"] = str(args.json_output)
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        args.json_output.write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                    encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return {"pass": 0, "review": 2, "fail": 1}[result["verdict"]]


if __name__ == "__main__":
    sys.exit(main())
