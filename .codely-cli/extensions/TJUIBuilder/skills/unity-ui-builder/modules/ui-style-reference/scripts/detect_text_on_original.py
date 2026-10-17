#!/usr/bin/env python3
"""Detect plain-text label geometry on the ORIGINAL reference image via colour channels.

Serves the two pipeline paths that have no ``text_only_2048.png``:
- Stage 02 Step 5.5-fallback (root level): default crop = ``original_content_rect``
- references/recursive-extraction.md §8.4.5 (parent level): crop = the parent's
  ``original_image_box`` in source_square coordinates (the source sits top-left in
  ``source_square.png`` at ORIGINAL resolution — never multiply by ``scale``).

Geometry contract (identical to extract_text_labels.py):
- horizontal dilation bridges same-line character gaps for GROUPING only;
- reported boxes are tight bboxes of foreground pixels on the ORIGINAL
  (non-dilated) mask;
- equidistant-row validation: row centres deviating from the fitted spacing
  pattern by more than half a row height are rejected and honestly reported as
  「未检出」 — they are never back-filled with AI estimates.

Detection pipeline: colour-channel text masks (bright / gold / dark,
auto-selected by row-structure scoring) → key-colour exclusion → vertical
structural-line suppression → row-band anchoring → equidistant validation →
per-band blob extraction with solid-run (icon-column) trimming.

Output ``components.json`` matches the extract_text_labels.py schema
(``root_box`` / ``original_image_box`` / ``relative_parent_box`` already mapped
to their coordinate spaces) plus a ``detection_audit`` block for human review.

Usage: {PYTHON} detect_text_on_original.py IMAGE TRANSFORM_JSON OUTPUT_DIR
  [--mode root|parent] [--crop-rect X Y W H]
  [--parent-root-box X Y W H] [--parent-original-box X Y W H]
  [--channels auto|bright|gold|dark] [--known-rows N]
Exit code 0 on success (zero detections is a legal result), 1 on bad input.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

import cv2
import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from key_color_processing import key_color_mask, json_safe
from extract_reference_components import percentage_rect, _diagnostic_color

CHANNELS = ("bright", "gold", "dark")


# ---------------------------------------------------------------------------
# Low-level mask helpers
# ---------------------------------------------------------------------------

def _channel_mask(rgb: np.ndarray, channel: str, bright_threshold: int,
                  gold_r: int, gold_g: int, dark_threshold: int) -> np.ndarray:
    """Return a 0/255 text-candidate mask for one colour channel (RGB-order input)."""
    if channel == "bright":
        gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        return ((gray > bright_threshold).astype(np.uint8)) * 255
    if channel == "gold":
        return (((rgb[:, :, 0] > gold_r) & (rgb[:, :, 1] > gold_g)).astype(np.uint8)) * 255
    if channel == "dark":
        gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        return ((gray < dark_threshold).astype(np.uint8)) * 255
    raise ValueError(f"unknown channel: {channel}")


def _suppress_vertical_lines(mask: np.ndarray, min_ratio: float, min_fill: float,
                             max_width: int) -> Tuple[np.ndarray, int]:
    """Zero out thin, nearly-continuous vertical runs (dividers / rail lines).

    A vertical divider crossing several text rows would merge them into one
    oversized band and the whole group would be rejected.  Only THIN runs
    (width <= max_width) that are almost gap-free over >= min_ratio of the
    crop height are suppressed; wide structures (progress-bar tracks, plates)
    are left alone.
    """
    h, w = mask.shape
    if h == 0 or w == 0:
        return mask, 0
    counts = (mask > 0).sum(axis=0)
    candidate = np.zeros(w, dtype=bool)
    for x in range(w):
        count = int(counts[x])
        if count < min_ratio * h:
            continue
        ys = np.flatnonzero(mask[:, x] > 0)
        if ys.size == 0:
            continue
        extent = int(ys[-1]) - int(ys[0]) + 1
        if extent > 0 and count / extent >= min_fill:
            candidate[x] = True
    result = mask.copy()
    removed = 0
    x = 0
    while x < w:
        if candidate[x]:
            x0 = x
            while x < w and candidate[x]:
                x += 1
            if x - x0 <= max_width:
                result[:, x0:x] = 0
                removed += x - x0
        else:
            x += 1
    return result, removed


def _row_bands(dilated: np.ndarray, merge_gap: int) -> List[Tuple[int, int]]:
    """Maximal vertical activity runs of the dilated mask, merging gaps <= merge_gap.

    Returns inclusive (y0, y1) pairs.  Dilation is horizontal-only, so distinct
    text lines never merge vertically here.
    """
    active = dilated.any(axis=1)
    h = active.shape[0]
    runs: List[List[int]] = []
    i = 0
    while i < h:
        if active[i]:
            j = i
            while j < h and active[j]:
                j += 1
            runs.append([i, j - 1])
            i = j
        else:
            i += 1
    merged: List[List[int]] = []
    for run in runs:
        if merged and run[0] - merged[-1][1] - 1 <= merge_gap:
            merged[-1][1] = run[1]
        else:
            merged.append(run)
    return [(a, b) for a, b in merged]


def _dist_to_lattice(value: float, anchor: float, gap: float) -> float:
    remainder = (value - anchor) % gap
    return min(remainder, gap - remainder)


def _lattice_filter(centers: List[float], min_gap: float, tol: float
                    ) -> Tuple[List[int], Optional[float], str]:
    """Equidistant-row validation (§8.4.5): keep centres that fit one spacing lattice.

    Every plausible spacing is derived from a centre pair and every centre is
    tried as lattice phase.  Lattices are ranked by (core, covered, -mean
    residual, -gap) where "core" counts centres within tol/2 of a lattice
    point — the core term prevents a dense, sloppy lattice from "explaining"
    every row and hiding a true equidistant pattern plus stray fragments.
    Rows off the winning lattice by more than ``tol`` (half a row height) are
    rejected: they are detection errors, not text.
    """
    n = len(centers)
    if n < 3:
        return list(range(n)), None, "skipped_lt3_bands"
    if min_gap <= 0:
        return list(range(n)), None, "nonpositive_min_gap"

    best_key: Optional[Tuple[int, int, float, float]] = None
    best_accepted: List[int] = list(range(n))
    best_gap: Optional[float] = None
    for i in range(n):
        for j in range(i + 1, n):
            gap = (centers[j] - centers[i]) / (j - i)
            if gap < min_gap or gap > centers[-1] - centers[0] + tol:
                continue
            for anchor in centers:
                residuals = [_dist_to_lattice(c, anchor, gap) for c in centers]
                covered = [idx for idx, r in enumerate(residuals) if r <= tol]
                if not covered:
                    continue
                core = sum(1 for idx in covered if residuals[idx] <= tol / 2)
                mean_resid = sum(residuals[idx] for idx in covered) / len(covered)
                key = (core, len(covered), -mean_resid, -gap)
                if best_key is None or key > best_key:
                    best_key = key
                    best_accepted = covered
                    best_gap = gap
    if best_key is None:
        return list(range(n)), None, "no_valid_spacing"
    return best_accepted, best_gap, "lattice_validated"


# ---------------------------------------------------------------------------
# Core detection
# ---------------------------------------------------------------------------

def detect_text_on_original(
    image_path: Path,
    transform_path: Path,
    output_dir: Path,
    mode: str = "root",
    crop_rect: Optional[Sequence[int]] = None,
    parent_root_box: Optional[Sequence[int]] = None,
    parent_original_box: Optional[Sequence[int]] = None,
    channels: str = "auto",
    bright_threshold: int = 135,
    gold_r: int = 150,
    gold_g: int = 110,
    dark_threshold: int = 115,
    known_rows: Optional[int] = None,
    min_area: int = 100,
    min_dimension: int = 16,
    dilate_width: int = 50,
    band_cap: int = 0,
    merge_gap: int = 4,
    solid_fill_ratio: float = 0.8,
    solid_run_ratio: float = 1.5,
    suppress_min_ratio: float = 0.35,
    suppress_min_fill: float = 0.95,
    suppress_max_width: int = 16,
) -> dict:
    image_path = Path(image_path).resolve()
    transform_path = Path(transform_path).resolve()
    output_dir = Path(output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    if mode not in ("root", "parent"):
        raise ValueError("mode must be 'root' or 'parent'")
    if channels != "auto" and channels not in CHANNELS:
        raise ValueError(f"channels must be auto or one of {CHANNELS}")
    if mode == "parent":
        if crop_rect is None or parent_root_box is None or parent_original_box is None:
            raise ValueError("--mode parent requires --crop-rect, --parent-root-box "
                             "and --parent-original-box")
        for name, box in (("--parent-root-box", parent_root_box),
                          ("--parent-original-box", parent_original_box)):
            if any(int(v) <= 0 for v in box[2:]):
                raise ValueError(f"{name} width/height must be positive")

    transform = json.loads(transform_path.read_text(encoding="utf-8"))
    key_color = transform.get("key_color")
    with Image.open(image_path) as opened:
        image = np.asarray(opened.convert("RGBA")).copy()
    img_h, img_w = image.shape[:2]

    # --- Crop region (source_square / original-image coordinates) ---
    if crop_rect is None:
        rect = transform.get("original_content_rect")
        if rect is None:
            crop_x, crop_y, crop_w, crop_h = 0, 0, img_w, img_h
        else:
            crop_x, crop_y = int(rect["x"]), int(rect["y"])
            crop_w, crop_h = int(rect["width"]), int(rect["height"])
    else:
        crop_x, crop_y, crop_w, crop_h = (int(v) for v in crop_rect)
    if crop_w <= 0 or crop_h <= 0:
        raise ValueError("crop rect must be positive")
    crop_x = max(0, min(crop_x, img_w - 1))
    crop_y = max(0, min(crop_y, img_h - 1))
    crop_w = min(crop_w, img_w - crop_x)
    crop_h = min(crop_h, img_h - crop_y)

    crop = image[crop_y:crop_y + crop_h, crop_x:crop_x + crop_w]
    rgb = crop[:, :, :3]
    if crop.shape[2] >= 4:
        opaque = (crop[:, :, 3] > 0).astype(np.uint8) * 255
    else:
        opaque = np.full(rgb.shape[:2], 255, dtype=np.uint8)
    key_exclusion = key_color_mask(crop, key_color) if key_color else None

    if band_cap <= 0:
        band_cap = max(40, crop_h // 6)

    # --- Per-channel masks: colour channel ∩ non-key ∩ opaque, minus rail lines ---
    evaluated = list(CHANNELS) if channels == "auto" else [channels]
    channel_masks: Dict[str, np.ndarray] = {}
    channel_audit: List[dict] = []
    for ch in evaluated:
        raw = _channel_mask(rgb, ch, bright_threshold, gold_r, gold_g, dark_threshold)
        if key_exclusion is not None:
            raw = cv2.bitwise_and(raw, key_exclusion)
        raw = cv2.bitwise_and(raw, opaque)
        raw, suppressed_cols = _suppress_vertical_lines(
            raw, suppress_min_ratio, suppress_min_fill, suppress_max_width)
        channel_masks[ch] = raw
        channel_audit.append({
            "channel": ch,
            "mask_pixels": int((raw > 0).sum()),
            "suppressed_columns": int(suppressed_cols),
        })

    # --- Row-band structure per channel + auto channel selection ---
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (dilate_width, 1))
    dilated_by_channel: Dict[str, np.ndarray] = {}
    bands_by_channel: Dict[str, List[Tuple[int, int]]] = {}
    score_by_channel: Dict[str, tuple] = {}
    for ch, raw in channel_masks.items():
        dilated = cv2.dilate(raw, kernel, iterations=1)
        bands = _row_bands(dilated, merge_gap)
        plausible = [b for b in bands if (b[1] - b[0] + 1) <= band_cap]
        dilated_by_channel[ch] = dilated
        bands_by_channel[ch] = bands
        if plausible:
            avg_h = sum(b[1] - b[0] + 1 for b in plausible) / len(plausible)
            score_by_channel[ch] = (len(plausible), -avg_h)
        else:
            score_by_channel[ch] = (0, 0.0)

    if channels == "auto":
        best = max(CHANNELS, key=lambda ch: score_by_channel[ch])
        selected: Optional[str] = best if score_by_channel[best][0] > 0 else None
    else:
        selected = channels

    raw_mask = channel_masks.get(selected) if selected else None
    dilated = dilated_by_channel.get(selected) if selected else None
    bands = bands_by_channel.get(selected, []) if selected else []

    band_records: List[dict] = []
    accepted_bands: List[Tuple[int, int]] = []
    blob_records: List[dict] = []
    trims: List[dict] = []
    spacing: Optional[float] = None
    validation_note = "zero_detect"
    zero_detect = selected is None

    if selected is not None and raw_mask is not None and dilated is not None:
        plausible = [b for b in bands if (b[1] - b[0] + 1) <= band_cap]
        for band in bands:
            if (band[1] - band[0] + 1) > band_cap:
                band_records.append({
                    "y0": band[0], "y1": band[1],
                    "height": band[1] - band[0] + 1,
                    "accepted": False, "reject_reason": "band_too_tall",
                })
        if not plausible:
            zero_detect = True
        else:
            plausible_heights = [b[1] - b[0] + 1 for b in plausible]
            median_height = float(np.median(plausible_heights))
            centers = [(b[0] + b[1]) / 2.0 for b in plausible]
            accepted_idx, spacing, validation_note = _lattice_filter(
                centers, 1.5 * max(median_height, 1.0), 0.5 * max(median_height, 1.0))
            for idx, band in enumerate(plausible):
                accepted = idx in accepted_idx
                band_records.append({
                    "y0": band[0], "y1": band[1],
                    "height": band[1] - band[0] + 1,
                    "center": centers[idx],
                    "accepted": accepted,
                    "reject_reason": None if accepted else "off_lattice",
                })
                if accepted:
                    accepted_bands.append(band)

            # --- Blob extraction inside accepted bands ---
            num_labels, labels, stats, _ = cv2.connectedComponentsWithStats(
                dilated, connectivity=8)
            for label in range(1, num_labels):
                dx, dy, d_width, d_height, _ = stats[label]
                band = next((b for b in accepted_bands
                             if dy >= b[0] and dy + d_height - 1 <= b[1]), None)
                if band is None:
                    continue
                region = (slice(dy, dy + d_height), slice(dx, dx + d_width))
                label_pixels = (labels[region] == label) & (raw_mask[region] > 0)
                if not label_pixels.any():
                    continue
                ys, xs = np.nonzero(label_pixels)
                x0r, x1r = int(xs.min()), int(xs.max())
                y0r, y1r = int(ys.min()), int(ys.max())
                tx, ty = x0r + dx, y0r + dy
                tw, th = x1r - x0r + 1, y1r - y0r + 1

                # Solid-run trimming (icon column / badge glued to the line by
                # dilation): leading/trailing column runs that are almost solid
                # across the blob's tight height and wide enough are treated as
                # non-text and trimmed.  Real text has intra-word gaps and never
                # fills a column solidly.
                col_counts = label_pixels.sum(axis=0)
                fill = col_counts / max(1, th)
                lead = 0
                while lead < tw and fill[x0r + lead] >= solid_fill_ratio:
                    lead += 1
                tail = 0
                while tail < tw - lead and fill[x1r - tail] >= solid_fill_ratio:
                    tail += 1
                remaining_width = tw - lead - tail
                lead_ok = lead >= solid_run_ratio * th and remaining_width >= min_dimension
                tail_ok = tail >= solid_run_ratio * th and remaining_width >= min_dimension
                trim = None
                if lead_ok or tail_ok:
                    if lead_ok and tail_ok:
                        side, cols = "both", lead + tail
                    elif lead_ok:
                        side, cols = "leading", lead
                    else:
                        side, cols = "trailing", tail
                    # Only the side(s) that qualified is cut; a sub-threshold
                    # trailing run (e.g. a solid last word) must stay in the box.
                    lead_cut = lead if lead_ok else 0
                    tail_cut = tail if tail_ok else 0
                    remaining = label_pixels[:, x0r + lead_cut: x1r - tail_cut + 1]
                    ys2, xs2 = np.nonzero(remaining)
                    if ys2.size == 0:
                        continue  # pure icon / badge — no text pixels left
                    tx = int(xs2.min()) + x0r + lead_cut + dx
                    ty = int(ys2.min()) + dy
                    tw = int(xs2.max()) - int(xs2.min()) + 1
                    th = int(ys2.max()) - int(ys2.min()) + 1
                    trim = {"side": side, "columns": int(cols)}

                # Size filters apply to the TIGHT bbox (raw mask), not the dilated blob
                if tw * th < min_area:
                    continue
                if tw < min_dimension or th < min_dimension:
                    continue
                blob_records.append({
                    "box": (tx, ty, tw, th),
                    "band": band,
                    "trim": trim,
                })

    blob_records.sort(key=lambda rec: (rec["band"][0], rec["box"][0]))

    # --- Coordinate mapping ---
    scaled_rect = transform.get("scaled_content_rect") or {
        "x": 0, "y": 0, "width": img_w, "height": img_h}
    sc_x, sc_y = int(scaled_rect["x"]), int(scaled_rect["y"])
    sc_w, sc_h = int(scaled_rect["width"]), int(scaled_rect["height"])
    source_size = transform.get("source_size") or {"width": img_w, "height": img_h}
    src_w, src_h = int(source_size["width"]), int(source_size["height"])
    if mode == "parent":
        pr = {k: int(v) for k, v in zip(("x", "y", "width", "height"), parent_root_box)}
        po = {k: int(v) for k, v in zip(("x", "y", "width", "height"), parent_original_box)}

    def _box_with_percent(x: int, y: int, w: int, h: int,
                          basis_x: int, basis_y: int, basis_w: int, basis_h: int) -> dict:
        """Pixel box with normalised/percent fields relative to an offset basis.

        Parent-mode root boxes carry the parent offset in their pixel coords
        (§8.4.5: x = P.root_box.x + ...) while their percent fields stay
        relative to the parent box itself.
        """
        normalized = {"x": (x - basis_x) / basis_w, "y": (y - basis_y) / basis_h,
                      "width": w / basis_w, "height": h / basis_h}
        return {"x": x, "y": y, "width": w, "height": h,
                "percentage": normalized,
                "percent": {key: value * 100 for key, value in normalized.items()}}

    def map_component(box: Tuple[int, int, int, int]) -> dict:
        lx, ly, lw, lh = box
        if mode == "root":
            x_local = round(sc_w * lx / crop_w)
            y_local = round(sc_h * ly / crop_h)
            x_right = round(sc_w * (lx + lw) / crop_w)
            y_bottom = round(sc_h * (ly + lh) / crop_h)
            root = _box_with_percent(x_local, y_local,
                                     max(0, x_right - x_local), max(0, y_bottom - y_local),
                                     0, 0, sc_w, sc_h)
            root["image_x"] = x_local + sc_x
            root["image_y"] = y_local + sc_y
        else:
            x_abs = pr["x"] + round(pr["width"] * lx / crop_w)
            y_abs = pr["y"] + round(pr["height"] * ly / crop_h)
            x_right = pr["x"] + round(pr["width"] * (lx + lw) / crop_w)
            y_bottom = pr["y"] + round(pr["height"] * (ly + lh) / crop_h)
            root = _box_with_percent(x_abs, y_abs,
                                     max(0, x_right - x_abs), max(0, y_bottom - y_abs),
                                     pr["x"], pr["y"], pr["width"], pr["height"])
            root["image_x"] = x_abs
            root["image_y"] = y_abs
        if mode == "root":
            ox, oy = crop_x + lx, crop_y + ly
        else:
            ox, oy = po["x"] + lx, po["y"] + ly
        orig = percentage_rect(ox, oy, lw, lh, src_w, src_h)
        rel = {
            "x": root["x"], "y": root["y"],
            "width": root["width"], "height": root["height"],
            "percentage": root["percentage"], "percent": root["percent"],
            "image_x": root["image_x"], "image_y": root["image_y"],
        }
        return {"root_box": root, "original_image_box": orig, "relative_parent_box": rel}

    diag_color = _diagnostic_color(key_color)
    box_rgb = tuple(diag_color[:3])
    text_rgb = (0, 0, 0) if sum(diag_color[:3]) > 382 else (255, 255, 255)
    annotated = crop.copy()
    out_components: List[dict] = []
    for idx, rec in enumerate(blob_records):
        lx, ly, lw, lh = rec["box"]
        mapped = map_component(rec["box"])
        crop_file = f"component_{idx + 1:03d}.png"
        crop_path = output_dir / crop_file
        Image.fromarray(np.ascontiguousarray(crop[ly:ly + lh, lx:lx + lw, :3]), "RGB").save(crop_path)
        provenance = {
            "source_image": str(image_path),
            "transform": str(transform_path),
            "source_size": source_size,
            "square_size": transform.get("square_size", 0),
            "scale": transform.get("scale", 1.0),
            "content_rect": scaled_rect,
            "contour_box": {"x": lx, "y": ly, "width": lw, "height": lh},
            "detection": {
                "channel": selected,
                "crop_rect": {"x": crop_x, "y": crop_y,
                              "width": crop_w, "height": crop_h},
                "mode": mode,
                "band": {"y0": rec["band"][0], "y1": rec["band"][1]},
                "trim": rec["trim"],
                "geometry_contract": {
                    "tight_bbox": "raw (non-dilated) mask; horizontal dilation groups only",
                    "root_box_basis": "scaled_content_rect space (root mode) / "
                                      "parent root_box space (parent mode)",
                },
            },
        }
        out_components.append({
            "node_id": f"component-{idx + 1:03d}",
            "file": crop_file,
            "file_path": str(crop_path),
            "root_box": mapped["root_box"],
            "original_image_box": mapped["original_image_box"],
            "relative_parent_box": mapped["relative_parent_box"],
            "relative_box": mapped["relative_parent_box"],
            "provenance": provenance,
            "self_reference": False,
        })
        cv2.rectangle(annotated, (lx, ly), (lx + lw - 1, ly + lh - 1), box_rgb, 2)
        cv2.putText(annotated, str(idx + 1), (lx, max(10, ly - 5)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, text_rgb, 2)
        if rec["trim"] is not None:
            trims.append({"node_id": f"component-{idx + 1:03d}", **rec["trim"],
                          "box": {"x": lx, "y": ly, "width": lw, "height": lh}})

    # --- Diagnostics + audit ---
    diagnostics = {}
    for ch, raw in channel_masks.items():
        mask_file = f"diagnostic_mask_{ch}.png"
        cv2.imwrite(str(output_dir / mask_file), raw)
        diagnostics[f"mask_{ch}"] = str(output_dir / mask_file)
    if dilated is not None:
        cv2.imwrite(str(output_dir / "diagnostic_dilated.png"), dilated)
        diagnostics["dilated"] = str(output_dir / "diagnostic_dilated.png")
    cv2.imwrite(str(output_dir / "diagnostic_annotated.png"),
                cv2.cvtColor(annotated, cv2.COLOR_RGBA2BGRA))
    diagnostics["annotated"] = str(output_dir / "diagnostic_annotated.png")

    warnings: List[str] = []
    if known_rows is not None and not zero_detect:
        if len(accepted_bands) != known_rows:
            warnings.append(
                f"known_rows={known_rows} but accepted bands={len(accepted_bands)} "
                f"(validation={validation_note}, spacing={spacing})")

    audit = {
        "mode": mode,
        "crop_rect": {"x": crop_x, "y": crop_y, "width": crop_w, "height": crop_h},
        "band_cap": band_cap,
        "known_rows": known_rows,
        "channels": channel_audit,
        "channel_scores": {ch: list(score) for ch, score in score_by_channel.items()},
        "selected_channel": None if zero_detect else selected,
        "zero_detect": zero_detect,
        "validation": validation_note,
        "spacing": spacing,
        "bands": band_records,
        "trims": trims,
        "warnings": warnings,
    }

    result = {
        "components": out_components,
        "original_image_size": {"width": img_w, "height": img_h},
        "content_rect": scaled_rect,
        "self_reference_warnings": [],
        "detection_audit": audit,
        "diagnostics": diagnostics,
    }
    output_json = output_dir / "components.json"
    output_json.write_text(json.dumps(json_safe(result), ensure_ascii=False, indent=2),
                           encoding="utf-8")
    summary = {
        "detected": len(out_components),
        "selected_channel": None if zero_detect else selected,
        "zero_detect": zero_detect,
        "rejected_bands": sum(1 for b in band_records if not b["accepted"]),
        "warnings": warnings,
        "components_json": str(output_json),
    }
    print(json.dumps(json_safe(summary), ensure_ascii=False))
    return result


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Detect text labels on the ORIGINAL reference image (colour channels, "
                    "row-band anchoring, equidistant validation, raw-mask tight bboxes).")
    parser.add_argument("image", type=Path)
    parser.add_argument("transform_json", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--mode", choices=("root", "parent"), default="root")
    parser.add_argument("--crop-rect", type=int, nargs=4, metavar=("X", "Y", "WIDTH", "HEIGHT"))
    parser.add_argument("--parent-root-box", type=int, nargs=4,
                        metavar=("X", "Y", "WIDTH", "HEIGHT"))
    parser.add_argument("--parent-original-box", type=int, nargs=4,
                        metavar=("X", "Y", "WIDTH", "HEIGHT"))
    parser.add_argument("--channels", default="auto",
                        help="auto | bright | gold | dark")
    parser.add_argument("--bright-threshold", type=int, default=135)
    parser.add_argument("--gold-r", type=int, default=150)
    parser.add_argument("--gold-g", type=int, default=110)
    parser.add_argument("--dark-threshold", type=int, default=115)
    parser.add_argument("--known-rows", type=int, default=None,
                        help="prior row count for this region (audit warning on mismatch)")
    parser.add_argument("--min-area", type=int, default=100)
    parser.add_argument("--min-dimension", type=int, default=16)
    parser.add_argument("--dilate-width", type=int, default=50)
    parser.add_argument("--band-cap", type=int, default=0,
                        help="max plausible row-band height in px (0 = auto: max(40, crop_h//6))")
    args = parser.parse_args()
    if args.min_area < 1 or args.min_dimension < 1 or args.dilate_width < 1:
        parser.error("--min-area/--min-dimension/--dilate-width must be at least 1")
    if args.known_rows is not None and args.known_rows < 1:
        parser.error("--known-rows must be at least 1")
    if args.mode == "parent" and (args.crop_rect is None or args.parent_root_box is None
                                  or args.parent_original_box is None):
        parser.error("--mode parent requires --crop-rect, --parent-root-box, --parent-original-box")
    detect_text_on_original(
        args.image, args.transform_json, args.output_dir,
        mode=args.mode, crop_rect=args.crop_rect,
        parent_root_box=args.parent_root_box,
        parent_original_box=args.parent_original_box,
        channels=args.channels,
        bright_threshold=args.bright_threshold, gold_r=args.gold_r, gold_g=args.gold_g,
        dark_threshold=args.dark_threshold, known_rows=args.known_rows,
        min_area=args.min_area, min_dimension=args.min_dimension,
        dilate_width=args.dilate_width, band_cap=args.band_cap)
    return 0


if __name__ == "__main__":
    sys.exit(main())
