#!/usr/bin/env python3
"""Key-colour utilities for the UI reference processing pipeline.

The pipeline uses a **key colour** (chroma key) approach: AI outputs opaque RGB
images whose background is filled with a dynamically selected key colour.
Foreground detection is based on colour similarity to the key colour, and a
post-processing step converts key-colour pixels to transparent (alpha=0) for
the final transparent PNG output.

The key colour is selected by scanning all 180 OpenCV hues and picking the
one with the **minimum pixel overlap** — i.e. the hue least present in the
image content.  This is strictly better than checking a fixed candidate list
because it finds the actual quietest hue for any given image.
"""
from __future__ import annotations

from typing import Any, Dict, List, Optional, Tuple

import cv2
import numpy as np

# ---------------------------------------------------------------------------
# HSV tolerance — must match make_transparent defaults in make_key_color_transparent.py
# ---------------------------------------------------------------------------

HUE_TOLERANCE = 20      # degrees in OpenCV's 0-179 hue range
SAT_TOLERANCE = 100     # 0-255
VAL_TOLERANCE = 100     # 0-255


def _rgb_array(image: np.ndarray) -> np.ndarray:
    if image.ndim != 3 or image.shape[2] < 3:
        raise ValueError("Expected an RGB or RGBA image array")
    return image[:, :, :3]


def _rgb_to_hsv(rgb: Tuple[int, int, int]) -> Tuple[int, int, int]:
    """Convert an RGB tuple to OpenCV HSV (H: 0-179, S: 0-255, V: 0-255)."""
    arr = np.uint8([[list(rgb)]])
    hsv = cv2.cvtColor(arr, cv2.COLOR_RGB2HSV)[0, 0]
    return int(hsv[0]), int(hsv[1]), int(hsv[2])


def _hue_to_name(h: int) -> str:
    """Map an OpenCV hue value (0-179) to a human-readable colour name."""
    if h < 8 or h >= 173:
        return "red"
    if h < 23:
        return "orange"
    if h < 38:
        return "yellow"
    if h < 68:
        return "green"
    if h < 98:
        return "cyan"
    if h < 128:
        return "blue"
    if h < 143:
        return "violet"
    if h < 158:
        return "magenta"
    return "pink"


# ---------------------------------------------------------------------------
# Key colour selection — full hue scan
# ---------------------------------------------------------------------------

def select_key_color(image: np.ndarray,
                     hue_tolerance: int = HUE_TOLERANCE,
                     sat_tolerance: int = SAT_TOLERANCE,
                     val_tolerance: int = VAL_TOLERANCE) -> Dict[str, Any]:
    """Analyse *image* and select the key colour with minimum content overlap.

    Scans all 180 OpenCV hues.  For each hue *h* (at S=255, V=255), counts the
    fraction of image pixels that fall within HSV tolerance — i.e. pixels that
    would be **misclassified as background** if that hue were used as key
    colour.  Returns the hue with the lowest overlap.

    This replaces the previous fixed-candidate-list approach, which could miss
    hues that are absent from the image but not in the candidate list.
    """
    rgb = _rgb_array(image)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    total = hsv.shape[0] * hsv.shape[1]

    h_ch = hsv[:, :, 0].astype(np.int16)
    s_ch = hsv[:, :, 1].astype(np.int16)
    v_ch = hsv[:, :, 2].astype(np.int16)

    # Pre-filter: only pixels that pass S and V tolerance could match any
    # pure-saturated key colour.  Pixels with low S or low V are safe for all hues.
    sv_match = (np.abs(s_ch - 255) <= sat_tolerance) & (np.abs(v_ch - 255) <= val_tolerance)

    if not sv_match.any():
        # No saturated, bright pixels — every hue is equally safe; use magenta.
        best_h = 150
        best_overlap = 0.0
    else:
        # Histogram of hues for S/V-matching pixels only.
        match_hues = h_ch[sv_match]
        hist = np.bincount(match_hues, minlength=180).astype(np.float64)

        # For each candidate hue, sum histogram within ±hue_tolerance (circular).
        overlap_per_hue = np.zeros(180)
        for delta in range(-hue_tolerance, hue_tolerance + 1):
            overlap_per_hue += np.roll(hist, delta)

        best_h = int(np.argmin(overlap_per_hue))
        best_overlap = float(overlap_per_hue[best_h]) / total

    # Convert best hue (S=255, V=255) back to RGB.
    rgb_arr = cv2.cvtColor(np.uint8([[[best_h, 255, 255]]]), cv2.COLOR_HSV2RGB)[0, 0]

    return {
        "name": _hue_to_name(best_h),
        "rgb": {"r": int(rgb_arr[0]), "g": int(rgb_arr[1]), "b": int(rgb_arr[2])},
        "hsv": {"h": best_h, "s": 255, "v": 255},
        "overlap_ratio": round(best_overlap, 6),
    }


# ---------------------------------------------------------------------------
# Key colour mask (foreground vs. background)
# ---------------------------------------------------------------------------

def key_color_mask(image: np.ndarray, key_color: Dict[str, Any],
                   hue_tolerance: int = HUE_TOLERANCE,
                   sat_tolerance: int = SAT_TOLERANCE,
                   val_tolerance: int = VAL_TOLERANCE) -> np.ndarray:
    """Return a binary mask: 255 = foreground (not key colour), 0 = key colour background.

    Uses HSV colour similarity with circular hue distance.
    """
    rgb = _rgb_array(image)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)

    kc_hsv = key_color.get("hsv")
    if kc_hsv is not None:
        cand_h, cand_s, cand_v = int(kc_hsv["h"]), int(kc_hsv["s"]), int(kc_hsv["v"])
    else:
        kc_rgb = key_color["rgb"]
        cand_h, cand_s, cand_v = _rgb_to_hsv(
            (int(kc_rgb["r"]), int(kc_rgb["g"]), int(kc_rgb["b"]))
        )

    h_dist = np.minimum(
        np.abs(hsv[:, :, 0].astype(np.int16) - cand_h),
        180 - np.abs(hsv[:, :, 0].astype(np.int16) - cand_h),
    )
    s_dist = np.abs(hsv[:, :, 1].astype(np.int16) - cand_s)
    v_dist = np.abs(hsv[:, :, 2].astype(np.int16) - cand_v)

    is_key = (h_dist <= hue_tolerance) & (s_dist <= sat_tolerance) & (v_dist <= val_tolerance)
    return ((~is_key).astype(np.uint8)) * 255


# ---------------------------------------------------------------------------
# Foreground mask (supports both key colour and alpha modes)
# ---------------------------------------------------------------------------

def foreground_mask(image: np.ndarray, key_color: Optional[Dict[str, Any]] = None) -> np.ndarray:
    """Return foreground mask where pixels are visible.

    - If *key_color* is provided: foreground = pixels NOT similar to key colour.
    - Otherwise (backward compat): foreground = pixels with alpha > 0.
    """
    if key_color is not None:
        return key_color_mask(image, key_color)
    if image.shape[2] >= 4:
        return ((image[:, :, 3] > 0).astype(np.uint8) * 255)
    else:
        h, w = image.shape[:2]
        return np.ones((h, w), dtype=np.uint8) * 255


# ---------------------------------------------------------------------------
# RGBA conversion (supports both key colour and alpha modes)
# ---------------------------------------------------------------------------

def rgba_from_alpha(image: np.ndarray, key_color: Optional[Dict[str, Any]] = None) -> np.ndarray:
    """Ensure RGBA format and clean noise.

    - If *key_color* is provided: convert key colour pixels to alpha=0,
      other pixels to alpha=255, then suppress low-alpha noise.
    - Otherwise (backward compat): ensure consistent alpha channel from existing alpha.
    """
    rgb = _rgb_array(image)
    if key_color is not None:
        mask = key_color_mask(image, key_color)
        alpha = mask.copy()
    elif image.shape[2] >= 4:
        alpha = image[:, :, 3].copy()
    else:
        alpha = np.full(rgb.shape[:2], 255, dtype=np.uint8)
    # Suppress near-transparent noise
    alpha[alpha < 10] = 0
    return np.dstack((rgb, alpha))


# ---------------------------------------------------------------------------
# Projection splitting (unchanged)
# ---------------------------------------------------------------------------

def split_by_projection(mask: np.ndarray, threshold: float = 0.35,
                        min_valley_height: int = 6,
                        absolute_floor: float = 0.02) -> np.ndarray:
    """Cut through projection valleys to separate adjacent UI sections.

    Computes foreground ratio per row and per column. A valley is a run of
    >= ``min_valley_height`` consecutive positions whose foreground ratio is
    below the adaptive threshold.  Only valleys that have significant
    foreground (peaks) on **both** sides are cleared, and within such a
    valley only positions below ``absolute_floor`` are zeroed — this
    preserves small components that sit inside a valley region while still
    severing thin connecting lines between large components.

    **Hollow element protection**: before cutting a valley, checks if every
    row (or column) in the valley has foreground on both sides with a
    significant gap — the signature of a hollow element (dropdown box, input
    field) whose interior is key-colour. Such valleys are skipped to keep
    the hollow element as a single component.

    Threshold is adaptive: if the max foreground ratio across all rows/columns
    is below the fixed threshold (e.g. small or sparse components), the
    threshold is lowered to 50% of the max to avoid clearing all content.

    Returns a new mask (original is not modified).
    """
    result = mask.copy()
    h, w = result.shape

    def _is_hollow_horizontal(valley_start: int, valley_end: int) -> bool:
        """Check if a horizontal valley is inside a hollow element (borders on both sides)."""
        valley = result[valley_start:valley_end, :] > 0  # (valley_h, w)
        if not valley.any():
            return False
        has_fg = valley.any(axis=1)  # (valley_h,)
        if not np.all(has_fg):
            return False
        leftmost = np.argmax(valley, axis=1)  # (valley_h,)
        rightmost = w - 1 - np.argmax(valley[:, ::-1], axis=1)
        span = rightmost - leftmost
        # Every row has foreground spanning > 10% of width → borders on both sides
        return bool(np.all(span > w * 0.1))

    def _is_hollow_vertical(valley_start: int, valley_end: int) -> bool:
        """Check if a vertical valley is inside a hollow element (borders on top and bottom)."""
        valley = result[:, valley_start:valley_end] > 0  # (h, valley_w)
        if not valley.any():
            return False
        has_fg = valley.any(axis=0)  # (valley_w,)
        if not np.all(has_fg):
            return False
        topmost = np.argmax(valley, axis=0)  # (valley_w,)
        bottommost = h - 1 - np.argmax(valley[::-1, :], axis=0)
        span = bottommost - topmost
        return bool(np.all(span > h * 0.1))

    # Horizontal: cut through rows with low foreground
    row_fg = result.sum(axis=1).astype(np.float32) / (w * 255)
    row_threshold = min(threshold, max(row_fg.max() * 0.5, 0.05))
    is_valley = row_fg < row_threshold
    i = 0
    while i < h:
        if is_valley[i]:
            start = i
            while i < h and is_valley[i]:
                i += 1
            end = i
            if end - start >= min_valley_height:
                has_peak_above = bool(np.any(row_fg[:start] >= row_threshold))
                has_peak_below = bool(np.any(row_fg[end:] >= row_threshold))
                if has_peak_above and has_peak_below:
                    if _is_hollow_horizontal(start, end):
                        continue  # Skip cutting — hollow element with continuous borders
                    clear_mask = row_fg[start:end] < absolute_floor
                    rows_to_clear = np.where(clear_mask)[0] + start
                    result[rows_to_clear, :] = 0
        else:
            i += 1

    # Vertical: cut through columns with low foreground
    col_fg = result.sum(axis=0).astype(np.float32) / (h * 255)
    col_threshold = min(threshold, max(col_fg.max() * 0.5, 0.05))
    is_valley_col = col_fg < col_threshold
    j = 0
    while j < w:
        if is_valley_col[j]:
            start = j
            while j < w and is_valley_col[j]:
                j += 1
            end = j
            if end - start >= min_valley_height:
                has_peak_left = bool(np.any(col_fg[:start] >= col_threshold))
                has_peak_right = bool(np.any(col_fg[end:] >= col_threshold))
                if has_peak_left and has_peak_right:
                    if _is_hollow_vertical(start, end):
                        continue  # Skip cutting — hollow element with continuous borders
                    clear_mask = col_fg[start:end] < absolute_floor
                    cols_to_clear = np.where(clear_mask)[0] + start
                    result[:, cols_to_clear] = 0
        else:
            j += 1

    return result


def filter_component_contours(mask: np.ndarray, min_area: int = 64,
                              min_dimension: int = 32,
                              max_cover_ratio: float = 0.95,
                              morph_kernel_size: int = 5,
                              morph_close_size: int = 11,
                              min_border_ratio: float = 0.06,
                              edge_tolerance: int = 5) -> Tuple[List[np.ndarray], np.ndarray, List[int]]:
    """Extract useful component contours via connected components, rejecting noise and full-frame covers.

    Returns a 3-tuple ``(contours, labels, label_ids)``:

    - ``contours``: sorted list of contours (by position), same as before.
    - ``labels``: the full label array from ``connectedComponentsWithStats``,
      shape ``(H, W)``.  Pixel value ``0`` = background, ``>=1`` = component.
      Callers use this to build per-component masks, preventing adjacent
      components' pixels from leaking into each other's alpha channel.
    - ``label_ids``: list of the connected-component label IDs corresponding
      to each contour in ``contours`` (same index order).

    Uses connectedComponentsWithStats instead of findContours to correctly separate
    densely-packed UI elements.  morph_close_size bridges small vertical gaps caused
    by img2img artifacts (e.g. a character's neck replaced with key colour) before
    the morphological open removes thin bridges between adjacent components.
    Uses a vertical-only kernel (H×1) to avoid merging horizontally adjacent
    components like reward chests in a row.

    Bug fix: morph_close_size is dynamically scaled based on mask height.
    The original fixed value of 11px was designed for ~2048px images, but in
    recursive layers the slot image can be as small as ~600px, where 11px
    merges vertically adjacent UI controls (sliders, toggles) that are only
    ~10px apart.  The close size is now capped at max(3, mask_height // 200)
    so it scales proportionally — 11px on 2048px images, ~4px on 800px images.

    A contour touching all four canvas edges is the full-screen blob (the entire
    foreground mask), not a discrete UI component — always filtered.

    A contour that touches one canvas edge with a very thin cross-section
    (width or height < min_border_ratio * canvas dimension) is a decorative
    border strip isolated by projection cutting, not a discrete UI component.
    Only the dimension perpendicular to the touched edge is checked: a strip
    touching left/right is filtered if its width is too thin; a strip touching
    top/bottom is filtered if its height is too thin.
    """
    if mask.ndim != 2:
        raise ValueError("Expected a single-channel mask")
    # Dynamically scale morph_close_size to image height — fixed 11px merges
    # adjacent controls on small slot images from recursive layers.
    if morph_close_size > 0:
        effective_close = min(morph_close_size, max(3, mask.shape[0] // 200))
    else:
        effective_close = 0
    if effective_close > 0:
        closed = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((effective_close, 1), np.uint8))
    else:
        closed = mask
    cleaned = cv2.morphologyEx(closed, cv2.MORPH_OPEN, np.ones((morph_kernel_size, morph_kernel_size), np.uint8))
    num_labels, labels, stats, _ = cv2.connectedComponentsWithStats(cleaned, connectivity=8)
    accepted: List[np.ndarray] = []
    accepted_label_ids: List[int] = []
    for label in range(1, num_labels):  # skip background (0)
        x, y, width, height, area = stats[label]
        if area < min_area:
            continue
        if width < min_dimension or height < min_dimension:
            continue
        touches_all_edges = (x <= 0 and y <= 0 and x + width >= mask.shape[1] and y + height >= mask.shape[0])
        if touches_all_edges:
            continue
        touches_left = x <= edge_tolerance
        touches_right = x + width >= mask.shape[1] - edge_tolerance
        touches_top = y <= edge_tolerance
        touches_bottom = y + height >= mask.shape[0] - edge_tolerance
        min_w = mask.shape[1] * min_border_ratio
        min_h = mask.shape[0] * min_border_ratio
        if (touches_left or touches_right) and width < min_w:
            continue
        if (touches_top or touches_bottom) and height < min_h:
            continue
        # Build a contour from the component for downstream boundingRect compatibility
        component_mask = (labels == label).astype(np.uint8) * 255
        contours, _ = cv2.findContours(component_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if contours:
            accepted.append(max(contours, key=cv2.contourArea))
            accepted_label_ids.append(label)
    # Sort contours and label_ids together by contour position (top-left first)
    sorted_pairs = sorted(zip(accepted, accepted_label_ids),
                           key=lambda pair: cv2.boundingRect(pair[0])[0:2])
    sorted_contours = [pair[0] for pair in sorted_pairs]
    sorted_label_ids = [pair[1] for pair in sorted_pairs]
    return sorted_contours, labels, sorted_label_ids


def filter_contained_bboxes(items: List[Dict[str, Any]],
                            canvas_width: int, canvas_height: int,
                            min_area_ratio: float = 0.10,
                            composite_area_multiplier: float = 3.0,
                            composite_min_children: int = 2) -> List[Dict[str, Any]]:
    """Discard large boxes that significantly overlap a smaller box, but preserve composite containers.

    Each item must have ``node_id`` and ``bbox`` keys where ``bbox`` is a dict with
    ``x``, ``y``, ``width``, ``height`` in pixel coordinates.

    When box A (large) overlaps box B (small), and at least ``min_area_ratio`` of
    B's area falls inside A, the **large box A is discarded** — it is likely
    a border fragment, background blob, or partial frame, not a real UI component.
    The small box B is kept as an independent root-level component.

    Full containment is the extreme case: intersection / small_area = 100%,
    so the large box is always discarded.

    The container must be **strictly larger** than the contained box
    (``large_area > small_area``); identical-size overlapping boxes are both
    kept to avoid accidentally discarding a real component.

    A bbox touching all four canvas edges (full-frame) is never discarded, so its
    inner items are kept.

    **Composite container protection**: A large box that contains multiple smaller
    boxes (≥ ``composite_min_children``) is a composite UI panel (e.g. a task list
    panel containing buttons and icons), not a border fragment — it is kept so that
    Stage 04 can recursively extract its children. Similarly, a large box that is
    ≥ ``composite_area_multiplier`` × any single contained box's area is kept, as it
    has substantial foreground content of its own (panel background, border, etc.).
    """
    if len(items) <= 1:
        return list(items)
    keep = [True] * len(items)
    for i in range(len(items)):
        if not keep[i]:
            continue
        ib = items[i]["bbox"]
        ix, iy, iw, ih = ib["x"], ib["y"], ib["width"], ib["height"]
        touches_all_edges = (ix <= 0 and iy <= 0
                             and ix + iw >= canvas_width and iy + ih >= canvas_height)
        if touches_all_edges:
            continue
        large_area = iw * ih
        # Collect all smaller boxes contained in this large box
        contained_indices: List[int] = []
        max_small_area = 0
        for j in range(len(items)):
            if i == j or not keep[j]:
                continue
            jb = items[j]["bbox"]
            jx, jy, jw, jh = jb["x"], jb["y"], jb["width"], jb["height"]
            small_area = jw * jh
            # Compute intersection area between the two boxes
            inter_x = max(ix, jx)
            inter_y = max(iy, jy)
            inter_right = min(ix + iw, jx + jw)
            inter_bottom = min(iy + ih, jy + jh)
            intersection_area = max(0, inter_right - inter_x) * max(0, inter_bottom - inter_y)
            if (intersection_area >= small_area * min_area_ratio
                    and large_area > small_area):
                contained_indices.append(j)
                if small_area > max_small_area:
                    max_small_area = small_area

        if not contained_indices:
            continue

        # Composite container check: keep large box if it has multiple children
        # or is significantly larger than any single contained box
        is_composite = (len(contained_indices) >= composite_min_children
                        or large_area >= max_small_area * composite_area_multiplier)

        if is_composite:
            continue  # Keep the large box — it's a composite container

        # Otherwise, it's a border fragment — discard it
        keep[i] = False
    return [items[i] for i in range(len(items)) if keep[i]]


def json_safe(value: Any) -> Any:
    """Convert numpy scalars and arrays into JSON-serialisable built-in values."""
    if isinstance(value, np.ndarray):
        return [json_safe(item) for item in value.tolist()]
    if isinstance(value, np.generic):
        return value.item()
    if isinstance(value, dict):
        return {str(key): json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [json_safe(item) for item in value]
    return value


def detect_adjacent_components(items: List[Dict[str, Any]],
                                gap_threshold: int = 10,
                                overlap_ratio: float = 0.5) -> List[Dict[str, Any]]:
    r"""Detect adjacent component pairs that likely belong to the same visual element.

    Two boxes are flagged as "adjacent" when:
    1. They are near each other along one axis (gap < *gap_threshold* pixels)
    2. They overlap significantly along the other axis (>= *overlap_ratio*)

    This catches cases like a long title split into two boxes by OpenCV due to
    internal gaps between characters.  The function **does not merge** the boxes —
    it only reports pairs so that Step 6 semantics can keep both as ``valid``
    instead of marking one ``skip``.

    Returns a list of dicts: ``{a: node_id, b: node_id, axis: "x"|"y", gap: int, overlap: float}``
    """
    if len(items) <= 1:
        return []

    pairs: List[Dict[str, Any]] = []
    for i in range(len(items)):
        ib = items[i]["bbox"]
        for j in range(i + 1, len(items)):
            jb = items[j]["bbox"]
            # Check horizontal adjacency (side by side, overlap in Y)
            gap_x = max(0, max(ib["x"], jb["x"]) - min(ib["x"] + ib["width"], jb["x"] + jb["width"]))
            if gap_x < gap_threshold:
                y_overlap = (min(ib["y"] + ib["height"], jb["y"] + jb["height"]) -
                             max(ib["y"], jb["y"]))
                y_min_h = min(ib["height"], jb["height"])
                if y_min_h > 0 and y_overlap / y_min_h >= overlap_ratio:
                    pairs.append({
                        "a": items[i].get("node_id", str(i)),
                        "b": items[j].get("node_id", str(j)),
                        "axis": "x", "gap": int(gap_x),
                        "overlap": round(y_overlap / y_min_h, 3),
                    })
                    continue
            # Check vertical adjacency (stacked, overlap in X)
            gap_y = max(0, max(ib["y"], jb["y"]) - min(ib["y"] + ib["height"], jb["y"] + jb["height"]))
            if gap_y < gap_threshold:
                x_overlap = (min(ib["x"] + ib["width"], jb["x"] + jb["width"]) -
                             max(ib["x"], jb["x"]))
                x_min_w = min(ib["width"], jb["width"])
                if x_min_w > 0 and x_overlap / x_min_w >= overlap_ratio:
                    pairs.append({
                        "a": items[i].get("node_id", str(i)),
                        "b": items[j].get("node_id", str(j)),
                        "axis": "y", "gap": int(gap_y),
                        "overlap": round(x_overlap / x_min_w, 3),
                    })
    return pairs
