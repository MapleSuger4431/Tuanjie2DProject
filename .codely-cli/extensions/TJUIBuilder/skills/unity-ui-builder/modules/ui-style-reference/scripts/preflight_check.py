#!/usr/bin/env python3
"""Preflight checks for ui-style-reference Step 6.3, Step 8.4, Step 9b and Step 10.

Consolidates validation checks that were previously inline Python in SKILL.md:

1. field_completeness: Verify component-tree nodes have all semantic fields
   from semantic_records (Step 6.3).
2. quality_existence: Verify every state=valid node has a non-null quality
   field (Step 6.3 hard-gate).
3. preflight: Full preflight before Step 10 output — quality, recursion_done,
   final_transparent.png existence, coverage, recursion completion (Step 10).
4. validate_recursion: Post-OpenCV gate (Step 8.4) — detect self-reference
   children and invalid recursion_done reasons before merging tree.
5. validate_shell: Three-state gate (Step 9b) — residual-content variance band
   plus enclosed-hole over-clean detection (contract: shared/contracts.md 三态门禁).

Usage:
    {PYTHON} scripts/preflight_check.py <command> <args> [options]

Commands:
    field_completeness  Check semantic field propagation from records to tree
    quality_existence   Check quality field exists on all valid nodes
    label_geometry      Text-label mounting self-consistency (Step 5.5.4/8.4.5)
    preflight            Full preflight before output (Step 10)
    validate_recursion   Post-OpenCV validation of child extraction results (Step 8.4)
    validate_shell       Three-state gate for composite shell images (Step 9b):
                        exit 0 = pass (skip visual), 2 = review (visual arbitration),
                        1 = error. Over-clean detection via enclosed-hole ratio.
    validate_background  Three-state gate for full-screen background images (Step 8.5):
                        scattered small-detail statistics after excluding large
                        content blocks (character art). exit 0/2/1.

Validator subcommands follow the shared/contracts.md "三态门禁" contract:
verdict JSON on stdout (verdict/metrics/thresholds/reasons/evidence_files/audit_file),
exit codes 0 = pass, 2 = review, 1 = fail or error.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional


def _load_tree(tree_path: str) -> Dict[str, Any]:
    # utf-8-sig transparently strips a UTF-8 BOM (written by some Windows
    # tools) and reads BOM-less files unchanged.
    return json.loads(Path(tree_path).read_text(encoding="utf-8-sig"))


def _semantic_of(node: dict) -> dict:
    """Semantic dict of a tree node, tolerant of malformed values.

    JSON ``"semantic": null`` (and other non-dict shapes) must not crash the
    validators — such nodes are simply treated as having no semantics.
    """
    sem = node.get("semantic")
    return sem if isinstance(sem, dict) else {}


def _validate_alpha(image_path: str) -> str:
    """Validate that a PNG has an alpha channel and at least some transparent pixels.

    Returns an error message string if validation fails, or empty string on success.
    Catches two cases:
    1. NOT_RGBA: img2img RGB output moved directly (no alpha channel at all)
    2. ZERO_TRANSPARENT: RGBA file but all alpha=255 (e.g. Unity saved RGB as RGBA
       with dummy alpha, and key-color→alpha conversion was skipped)

    Note: ZERO_TRANSPARENT can theoretically false-positive on a component that
    fills its entire bounding box (no key-color padding). This is extremely rare;
    the cost of investigating a false positive is much lower than letting a
    broken image with magenta background flow downstream.
    """
    try:
        from PIL import Image
        img = Image.open(image_path)
        if img.mode != "RGBA":
            return f"NOT_RGBA (mode={img.mode}) — img2img RGB output was likely moved directly without key-color→alpha conversion"
        alpha = img.split()[3]
        colors = alpha.getcolors()
        if colors is not None:
            transparent = sum(c for c, v in colors if v == 0)
        else:
            hist = alpha.histogram()
            transparent = hist[0] if hist else 0
        if transparent == 0:
            return "ZERO_TRANSPARENT — RGBA but all alpha=255, key-color→alpha conversion was likely skipped"
        return ""
    except ImportError:
        return ""  # PIL not available, skip
    except Exception as e:
        return f"VALIDATION_ERROR ({e})"


def cmd_field_completeness(args: argparse.Namespace) -> int:
    """Step 6.3: Verify component-tree nodes have all semantic fields from records."""
    tree = _load_tree(args.tree)
    records_path = Path(args.records) if args.records else None
    if records_path is None or not records_path.exists():
        print("SKIP: semantic_records path not provided or does not exist")
        return 0

    records = {
        r["node_id"]: r
        for r in json.loads(records_path.read_text(encoding="utf-8-sig"))
        if isinstance(r, dict) and r.get("node_id")
    }
    errors: List[str] = []
    for node in tree.get("nodes", []):
        nid = node.get("node_id") or "<unnamed>"
        sem = _semantic_of(node)
        rec = records.get(nid, {})
        for field in ("style_group_hint", "functional_type", "contamination_description"):
            if field in rec and rec[field] and field not in sem:
                errors.append(f"MISSING: {nid}.{field} (value in records: {rec[field]!r})")

    if errors:
        for e in errors:
            print(f"FIELD ERROR: {e}")
        return 1
    print("FIELD COMPLETENESS: all checks passed.")
    return 0


def cmd_quality_existence(args: argparse.Namespace) -> int:
    """Step 6.3 hard-gate: Verify every state=valid node has a non-null quality.

    Exception: uss_only nodes (source=uss_only / render_mode=text) are text-only
    labels rendered by UXML Label — they have no image to verify for contamination,
    so quality is not applicable and is skipped.
    """
    tree = _load_tree(args.tree)
    errors: List[str] = []
    for node in tree.get("nodes", []):
        sem = _semantic_of(node)
        nid = node.get("node_id") or "<unnamed>"
        if sem.get("state") != "valid":
            continue
        source = sem.get("source", "")
        render_mode = sem.get("render_mode", "")
        if source == "uss_only" or render_mode == "text":
            continue
        quality = sem.get("quality")
        if quality not in ("clean", "contaminated"):
            errors.append(
                f"{nid}: quality MISSING "
                f"(value: {quality!r}, expected clean/contaminated)"
            )

    if errors:
        for e in errors:
            print(f"QUALITY GATE FAIL: {e}")
        return 1
    print("QUALITY EXISTENCE: all valid nodes have quality.")
    return 0


def _pct_of(box: Any) -> Optional[Dict[str, float]]:
    """Percent dict of a geometry box (accepts ``percent`` or ``percentage``)."""
    if not isinstance(box, dict):
        return None
    keys = ("x", "y", "width", "height")
    pct = box.get("percent")
    if isinstance(pct, dict) and all(k in pct for k in keys):
        return {k: float(pct[k]) for k in keys}
    pf = box.get("percentage")
    if isinstance(pf, dict) and all(k in pf for k in keys):
        return {k: float(pf[k]) * 100.0 for k in keys}
    return None


def _project_box(base, pct):
    return (
        base[0] + base[2] * pct["x"] / 100.0,
        base[1] + base[3] * pct["y"] / 100.0,
        base[2] * pct["width"] / 100.0,
        base[3] * pct["height"] / 100.0,
    )


def _absolute_box(by_id: Dict[str, dict], nid: str, seen=None):
    """Node box in its detection (root-level) space, per the mounting contract:
    ``parent.root_box ⊕ node.relative_parent_box.percent`` along the ancestor chain."""
    node = by_id.get(nid)
    if node is None:
        return None
    if seen is None:
        seen = set()
    if nid in seen:
        return None
    seen.add(nid)
    pid = node.get("parent_id")
    if not pid:
        b = node.get("root_box") or {}
        try:
            return (float(b["x"]), float(b["y"]), float(b["width"]), float(b["height"]))
        except (KeyError, TypeError, ValueError):
            return None
    parent_abs = _absolute_box(by_id, pid, seen)
    if parent_abs is None:
        return None
    pct = _pct_of(node.get("relative_parent_box"))
    if pct is None:
        return None
    return _project_box(parent_abs, pct)


def _is_text_label_node(node: dict) -> bool:
    """True for nodes the label-geometry gate must check (Step 5.5.4/§8.4.5)."""
    sem = _semantic_of(node)
    if not (sem.get("render_mode") == "text" or sem.get("source") == "uss_only"):
        return False
    return sem.get("state") in ("valid", None, "")


def _text_label_count(tree: dict) -> int:
    """Count of text labels eligible for the geometry gate.

    Zero is legitimate (icon-only panels have no text labels); callers use
    this to distinguish "nothing to check" from "checked and clean".
    """
    return sum(1 for n in tree.get("nodes", [])
               if isinstance(n, dict) and _is_text_label_node(n))


def _label_geometry_errors(tree: dict) -> List[str]:
    """Self-consistency gate for uss_only text labels (Step 5.5.4 / §8.4.5).

    A tree with zero eligible labels returns no errors — icon-only panels are
    legitimate; callers surface that case as a note, not a failure.

    Catches the two mounting defect classes observed in the wild:
    - stale percents computed against the wrong parent/base (labels silently
      compressed onto a smaller split child; panel-level labels recomputed
      against the square canvas instead of content_rect);
    - four top-level scalars out of sync with ``relative_parent_box`` (board
      import silently falls back to stale geometry).

    AI-estimated root_box values that are internally consistent cannot be
    caught here — that class is prevented by the §8.4.5 rule (geometry must
    come from OpenCV pixel detection, never AI percentage estimation).
    """
    errors: List[str] = []
    nodes = tree.get("nodes", [])
    by_id = {n.get("node_id"): n for n in nodes if isinstance(n, dict)}

    for node in nodes:
        if not isinstance(node, dict):
            continue
        if not _is_text_label_node(node):
            continue
        nid = node.get("node_id") or "<unnamed>"
        rb = node.get("root_box") or {}
        try:
            rwx, rwy, rww, rwh = (
                float(rb["x"]), float(rb["y"]), float(rb["width"]), float(rb["height"])
            )
        except (KeyError, TypeError, ValueError):
            errors.append(f"{nid}: text label has no numeric root_box")
            continue
        if rww <= 0 or rwh <= 0:
            errors.append(f"{nid}: text label root_box is zero-sized ({rwx},{rwy},{rww},{rwh})")
            continue
        pct = _pct_of(node.get("relative_parent_box"))
        if pct is None:
            errors.append(f"{nid}: relative_parent_box has no percent/percentage")
            continue

        pid = node.get("parent_id")
        if pid:
            base = _absolute_box(by_id, pid)
            if base is None:
                errors.append(f"{nid}: cannot resolve parent chain for mounting check (parent={pid!r})")
                continue
            rec = _project_box(base, pct)
            actual = (rwx, rwy, rww, rwh)
            if any(abs(rec[i] - actual[i]) > 1.01 for i in range(4)):
                errors.append(
                    f"{nid}: mount reconstruction mismatch — percent+parent="
                    f"{tuple(round(v, 1) for v in rec)} != root_box="
                    f"{tuple(round(v, 1) for v in actual)} (parent={pid}; error may "
                    "originate from an ancestor node's pct/root_box desync, not this label)"
                )
        else:
            root_pct = _pct_of(rb)
            if root_pct is None:
                errors.append(f"{nid}: panel-level label root_box has no percent to compare against")
            elif any(abs(root_pct[k] - pct[k]) > 0.01 for k in pct):
                errors.append(
                    f"{nid}: panel-level label relative_parent_box != root_box percent "
                    f"(rpb={ {k: round(v, 2) for k, v in pct.items()} } vs "
                    f"root={ {k: round(v, 2) for k, v in root_pct.items()} }) — base must be content_rect"
                )

        scalar_map = (
            ("x", "relative_parent_x_pct"), ("y", "relative_parent_y_pct"),
            ("width", "relative_parent_w_pct"), ("height", "relative_parent_h_pct"),
        )
        for short, key in scalar_map:
            raw = node.get(key)
            if not isinstance(raw, (int, float)) or isinstance(raw, bool):
                errors.append(f"{nid}: four-scalar {key} missing (board import needs it)")
            elif abs(float(raw) - pct[short]) > 0.01:
                errors.append(
                    f"{nid}: four-scalar {key}={float(raw)} out of sync with "
                    f"relative_parent_box percent ({pct[short]:.2f})"
                )
    return errors


def cmd_label_geometry(args: argparse.Namespace) -> int:
    """Step 5.5.4/8.4.5 gate: text-label mounting self-consistency."""
    tree = _load_tree(args.tree)
    errors = _label_geometry_errors(tree)
    if errors:
        for e in errors:
            print(f"LABEL GEOMETRY FAIL: {e}")
        return 1
    if _text_label_count(tree) == 0:
        print("LABEL GEOMETRY: no uss_only text labels found — nothing to check "
              "(icon-only panel, pass).")
        return 0
    print("LABEL GEOMETRY: all text labels consistent.")
    return 0


def cmd_preflight(args: argparse.Namespace) -> int:
    """Step 10 preflight: Full validation before output.

    Trees produced by the Stage 04.5 resolver (identified by a top-level
    ``layout_resolution`` block) are validated against the corrected layout:
    parent-child containment uses the final ``root_box`` (canvas space), the
    OpenCV self-reference check (2c) is skipped, source nodes must carry
    ``source_reference_node_id`` + ``source_crop_box``, added nodes
    (``origin=layout_added``) skip detection-only checks (quality /
    recursion_done), and ``reuse_from`` references must resolve to a node that
    still exists in the final tree.
    """
    tree = _load_tree(args.tree)
    processing_dir = Path(args.processing_dir)
    errors: List[str] = []
    errors.extend(_label_geometry_errors(tree))
    if _text_label_count(tree) == 0:
        print("LABEL GEOMETRY: no uss_only text labels found — nothing to check "
              "(icon-only panel, pass).")

    resolution = tree.get("layout_resolution")
    is_refined = isinstance(resolution, dict)
    deleted_ids = set(
        resolution.get("deleted_source_node_ids") or []
    ) if is_refined else set()
    refined_name_to_id: Dict[str, str] = {}
    refined_node_ids: set[str] = set()
    if is_refined:
        for node in tree.get("nodes", []):
            if not isinstance(node, dict):
                continue
            nid = node.get("node_id")
            if nid:
                refined_node_ids.add(nid)
            sem = _semantic_of(node)
            cname = str(sem.get("component_name") or "").strip()
            own_crop = str(node.get("crop_path") or sem.get("crop_path") or "").strip()
            # Skip reuse_from nodes: they share the representative's component name
            # and must not shadow it (would misroute reuse targets to non-reps and
            # trigger false "unknown representative"/dangling-representative errors).
            if (cname and cname not in refined_name_to_id
                    and not own_crop.startswith("reuse_from:")):
                refined_name_to_id[cname] = nid

    # Valid recursion_done reasons per recursive-extraction.md
    valid_recursion_reasons = {
        "all_children_are_leaf",
        "zero_children_after_retry",
        "self_reference_after_retry",
        "degraded_to_leaf_after_retry",
        "size_too_small",
    }

    for node in tree.get("nodes", []):
        nid = node.get("node_id") or "<unnamed>"
        sem = _semantic_of(node)
        if sem.get("state") != "valid":
            continue
        origin = str(node.get("origin") or "").strip()
        is_added = origin == "layout_added"
        is_background = nid == "background"

        # 1. quality field must exist (detection nodes only; added nodes skip)
        if not is_added and "quality" not in sem:
            errors.append(f"{nid}: quality MISSING (go back to Step 6)")

        # 2. composite nodes must have recursion_done (detection nodes only)
        if not is_added and sem.get("kind") == "composite" and not node.get("recursion_done"):
            errors.append(f"{nid}: composite not recursion_done (go back to Step 8)")

        # 2b. recursion_reason must be a valid reason (not "recursive_extraction_complete")
        if not is_added and node.get("recursion_done"):
            reason = node.get("recursion_reason", "")
            if reason and reason not in valid_recursion_reasons:
                errors.append(
                    f"{nid}: invalid recursion_reason '{reason}' "
                    f"(valid: {', '.join(sorted(valid_recursion_reasons))}) — "
                    f"'recursive_extraction_complete' is not a valid reason"
                )

        # 2c. self-reference child detection: skipped for resolver-corrected
        # trees — layout validation already guarantees containment, and a child
        # legitimately covering most of a parent is user intent, not an OpenCV
        # self-reference.
        if not is_refined:
            children = [n for n in tree.get("nodes", []) if n.get("parent_id") == nid]
            parent_box = node.get("root_box") or {}
            parent_area = parent_box.get("width", 0) * parent_box.get("height", 0)
            for child in children:
                child_box = child.get("root_box") or {}
                child_area = child_box.get("width", 0) * child_box.get("height", 0)
                if parent_area > 0 and child_area / parent_area >= 0.90:
                    errors.append(
                        f"{nid}: child {child.get('node_id') or '<unnamed>'} covers "
                        f"{100*child_area/parent_area:.1f}% of parent (>=90%) — "
                        f"self-reference detected, recursion_done is invalid"
                    )

        # 3. direct_use and ai_generate nodes must have final_transparent.png
        #    (composite shell nodes use shell_transparent.png instead)
        #    Exceptions: reuse_from nodes (reference a representative's image),
        #    text2img_fallback nodes (generation is deferred to the downstream
        #    ui-image-generator), and the injected background node.
        src = node.get("source", sem.get("source", ""))
        render_mode = node.get("render_mode", sem.get("render_mode", ""))
        crop_path = node.get("crop_path", sem.get("crop_path", ""))
        is_reuse_from = isinstance(crop_path, str) and crop_path.startswith("reuse_from:")
        is_text2img = crop_path == "text2img_fallback"
        if (src in ("direct_use", "ai_generate") and not is_background
                and not is_reuse_from and not is_text2img):
            if render_mode == "shell_background":
                ft = processing_dir / "nodes" / nid / "shell_transparent.png"
                ft_label = "shell_transparent.png"
            else:
                ft = processing_dir / "nodes" / nid / "final_transparent.png"
                ft_label = "final_transparent.png"
            if not ft.exists():
                errors.append(f"{nid}: {src} but {ft_label} missing")
            else:
                alpha_err = _validate_alpha(str(ft))
                if alpha_err:
                    errors.append(f"{nid}: {ft_label} {alpha_err}")

        # 3b. Refined-tree: source-linked nodes must carry source geometry.
        if is_refined and not is_added and not is_background:
            source_id = str(node.get("source_reference_node_id") or "").strip()
            if not source_id:
                errors.append(
                    f"{nid}: source node is missing source_reference_node_id "
                    "(resolver did not link it to the detected tree)"
                )
            needs_crop = (
                src not in ("uss_only", "skip_generation")
                and render_mode != "text"
                and not is_reuse_from
                and not is_text2img
            )
            if needs_crop and not node.get("source_crop_box"):
                errors.append(
                    f"{nid}: source node is missing source_crop_box "
                    "(detected tree lacked original_image_box)"
                )

        # 3c. Refined-tree: reuse_from targets must resolve to a surviving node.
        if is_refined and is_reuse_from:
            reference = crop_path.split(":", 1)[1].strip()
            representative = (
                reference if reference in refined_node_ids
                else refined_name_to_id.get(reference, "")
            )
            if not representative:
                errors.append(
                    f"{nid}: crop_path {crop_path!r} references unknown representative "
                    f"{reference!r} not present in the final tree"
                )
            elif representative in deleted_ids:
                errors.append(
                    f"{nid}: crop_path {crop_path!r} references representative "
                    f"{reference!r} which was deleted from the final layout — "
                    f"resolver must rewrite this node to independent generation"
                )

        # 4. background node must exist (no alpha check — background is opaque by design)
        if src == "shell_background" or nid == "background":
            ft = processing_dir / "nodes" / "background" / "final_transparent.png"
            if not ft.exists():
                errors.append("background: final_transparent.png missing")

    # 5. recursion completion check (added composites exempt — origin=layout_added)
    unfinished = [
        n for n in tree.get("nodes", [])
        if _semantic_of(n).get("kind") == "composite"
        and _semantic_of(n).get("state") == "valid"
        and not n.get("recursion_done")
        and str(n.get("origin") or "").strip() != "layout_added"
    ]
    if unfinished:
        errors.append(
            f"RECURSION INCOMPLETE: {len(unfinished)} composite node(s) "
            f"not recursion_done: {[n.get('node_id') or '<unnamed>' for n in unfinished]}"
        )

    # 5b. Refined-tree: parent-child containment via the final root_box.
    if is_refined:
        boxes: Dict[str, dict] = {}
        for node in tree.get("nodes", []):
            if not isinstance(node, dict):
                continue
            rb = node.get("root_box")
            if isinstance(rb, dict):
                boxes[node.get("node_id")] = rb
        eps = 1.0
        for node in tree.get("nodes", []):
            if not isinstance(node, dict):
                continue
            pid = node.get("parent_id")
            if not pid:
                continue
            parent_box = boxes.get(pid)
            child_box = boxes.get(node.get("node_id"))
            if parent_box and child_box:
                px, py = parent_box.get("x", 0), parent_box.get("y", 0)
                pw, ph = parent_box.get("width", 0), parent_box.get("height", 0)
                cx, cy = child_box.get("x", 0), child_box.get("y", 0)
                cw, ch = child_box.get("width", 0), child_box.get("height", 0)
                if (cx < px - eps or cy < py - eps
                        or cx + cw > px + pw + eps
                        or cy + ch > py + ph + eps):
                    errors.append(
                        f"{node.get('node_id')}: child root_box lies outside parent "
                        f"{pid} root_box (refined layout containment)"
                    )

    # 6. img2img provenance check — verify nodes that MUST have gone through
    #    img2img actually have a non-empty task_id. This catches cases where
    #    an agent skipped img2img and directly copied source images as output.
    #    Exception: crop_path=text2img_fallback is a legitimate fallback where
    #    img2img failed after retries; actual generation happens downstream.
    for node in tree.get("nodes", []):
        nid = node.get("node_id") or "<unnamed>"
        sem = _semantic_of(node)
        if sem.get("state") != "valid":
            continue

        crop_path = node.get("crop_path", sem.get("crop_path", ""))

        # Background node must have img2img_provenance.task_id (Step 8.5)
        # unless it fell back to text2img
        if nid == "background" and crop_path != "text2img_fallback":
            prov = node.get("img2img_provenance", {})
            task_id = prov.get("task_id", "") if isinstance(prov, dict) else ""
            if not task_id:
                errors.append(
                    f"{nid}: background node missing img2img_provenance.task_id "
                    f"— background must be generated via img2img (Step 8.5), "
                    f"not copied from source image"
                )

        # Composite shell nodes (render_mode=shell_background, non-background)
        # must have img2img_provenance.task_id (Step 9b)
        # unless they fell back to text2img
        render_mode = node.get("render_mode", sem.get("render_mode", ""))
        if (render_mode == "shell_background" and nid != "background"
                and crop_path != "text2img_fallback"):
            prov = node.get("img2img_provenance", {})
            task_id = prov.get("task_id", "") if isinstance(prov, dict) else ""
            if not task_id:
                errors.append(
                    f"{nid}: composite shell node (render_mode=shell_background) "
                    f"missing img2img_provenance.task_id — shell must be generated "
                    f"via img2img (Step 9b), not copied from source image"
                )

    # 7. Panel-level isolation provenance (Step 3)
    #    Accepts both flat (step3_isolation.task_id) and nested
    #    (step3_isolation.text_removal.task_id / background_removal.task_id)
    #    formats.  text_extraction is optional — its absence triggers
    #    fallback in Stage 02 Step 5.5 but does not block output.
    panel_prov = tree.get("panel_provenance", {})
    step3 = panel_prov.get("step3_isolation", {}) if isinstance(panel_prov, dict) else {}
    step3_task_id = ""
    if isinstance(step3, dict):
        step3_task_id = step3.get("task_id", "")
        if not step3_task_id:
            # Try nested format: text_removal.task_id or background_removal.task_id
            for sub_key in ("text_removal", "background_removal"):
                sub = step3.get(sub_key, {})
                if isinstance(sub, dict) and sub.get("task_id", ""):
                    step3_task_id = sub["task_id"]
                    break
    if not step3_task_id:
        errors.append(
            "panel_provenance.step3_isolation.task_id MISSING — "
            "Step 3 img2img isolation was likely skipped; "
            "the entire component tree geometry is unreliable"
        )

    if errors:
        for e in errors:
            print(f"PRE-FLIGHT FAIL: {e}")
        return 1
    print("PRE-FLIGHT: all checks passed.")
    return 0


def cmd_validate_recursion(args: argparse.Namespace) -> int:
    """Step 8.4 post-OpenCV gate: validate child extraction results before marking recursion_done.

    Checks a components.json (from extract_reference_components.py) for:
    1. Self-reference children (area >= 90% of parent content_rect)
    2. Zero children (AI failed to remove background)
    3. self_reference_warnings from extract_reference_components.py

    Exit code 0 = safe to proceed (merge tree / mark recursion_done)
    Exit code 1 = must enter retry flow (do NOT mark recursion_done)
    """
    import json as _json
    components_path = Path(args.components_json)
    manifest = _json.loads(components_path.read_text(encoding="utf-8-sig"))
    errors: List[str] = []

    content_rect = manifest.get("content_rect", {})
    parent_area = content_rect.get("width", 0) * content_rect.get("height", 0)
    components = manifest.get("components", [])

    # 1. Zero children
    if len(components) == 0:
        errors.append("ZERO_CHILDREN: 0 components extracted — AI likely failed to remove background")

    # 1b. Under-extraction detection: compare foreground mask connected component
    # count (excluding full-frame) against extracted component count.  If the mask
    # has significantly more blobs than the extraction returned, some components
    # were likely merged by morphological close or swallowed by filter_contained_bboxes.
    fg_mask_path = manifest.get("diagnostics", {}).get("foreground_mask")
    if fg_mask_path:
        try:
            import cv2 as _cv2
            import numpy as _np
            mask = _cv2.imread(str(Path(fg_mask_path)), _cv2.IMREAD_GRAYSCALE)
            if mask is not None:
                # Count connected components in the diagnostic mask (which is
                # post-projection-cut but pre-morphological, so it reflects what
                # OpenCV could have found before morph_close merged things).
                num_labels, _labels, stats, _ = _cv2.connectedComponentsWithStats(mask, connectivity=8)
                # Subtract 1 for background label (0).  Filter out full-frame and
                # tiny noise (area < 64) to get a comparable blob count.
                mask_blobs = sum(1 for i in range(1, num_labels)
                                 if stats[i, _cv2.CC_STAT_AREA] >= 64
                                 and not (stats[i, _cv2.CC_STAT_LEFT] <= 0
                                          and stats[i, _cv2.CC_STAT_TOP] <= 0
                                          and stats[i, _cv2.CC_STAT_LEFT] + stats[i, _cv2.CC_STAT_WIDTH] >= mask.shape[1]
                                          and stats[i, _cv2.CC_STAT_TOP] + stats[i, _cv2.CC_STAT_HEIGHT] >= mask.shape[0]))
                extracted = len(components)
                # Only warn when mask has significantly more blobs than extracted
                # (e.g. 8+ blobs but only 1-2 extracted = likely merged).  Small
                # differences (e.g. 8 blobs, 5 extracted) are expected due to
                # filter_contained_bboxes correctly discarding large containers
                # that encompass smaller components (e.g. border fragments).
                if mask_blobs > extracted + 2 and extracted <= 2:
                    errors.append(
                        f"UNDER_EXTRACTION: foreground mask has {mask_blobs} connected components "
                        f"but only {extracted} were extracted — morph_close may have merged "
                        f"adjacent controls or filter_contained_bboxes discarded a large container. "
                        f"Consider retrying with --morph-close-size 0 or checking content_rect dimensions."
                    )
        except Exception:
            pass  # Don't block on diagnostic failure

    # 2. Self-reference detection (check self_reference flag from extract_reference_components.py)
    warnings = manifest.get("self_reference_warnings", [])
    if warnings:
        for w in warnings:
            errors.append(f"SELF_REFERENCE: {w}")

    # 3. Manual area check (belt + suspenders)
    for comp in components:
        if comp.get("self_reference"):
            rb = comp.get("root_box", {})
            comp_area = rb.get("width", 0) * rb.get("height", 0)
            if parent_area > 0:
                pct = 100 * comp_area / parent_area
                errors.append(
                    f"SELF_REFERENCE: {comp.get('node_id') or '<unnamed>'} covers {pct:.1f}% of parent "
                    f"(>=90%) — do NOT mark recursion_done, enter retry flow"
                )

    if errors:
        for e in errors:
            print(f"RECURSION GATE FAIL: {e}")
        print()
        print("ACTION REQUIRED: Do NOT mark recursion_done.")
        print("Enter retry flow: retry-1 (stronger prompt) -> retry-2 (strategy change).")
        print("Only after retry-2 exhausted may you mark recursion_done.")
        return 1

    print(f"RECURSION GATE: passed ({len(components)} children, no self-reference).")
    return 0


def _emit_verdict(verdict: str, metrics: Dict[str, Any], thresholds: Dict[str, Any],
                  reasons: List[str], evidence_files: List[str],
                  audit_path: Optional[Path]) -> int:
    """Print the three-state verdict JSON (shared/contracts.md 三态门禁 contract).

    Exit codes: 0 = pass, 2 = review, 1 = fail. audit_path (when given) receives
    the same JSON for post-hoc replay and threshold calibration.
    """
    doc = {
        "verdict": verdict,
        "metrics": metrics,
        "thresholds": thresholds,
        "reasons": reasons,
        "evidence_files": evidence_files,
        "audit_file": str(audit_path) if audit_path else None,
    }
    text = json.dumps(doc, ensure_ascii=False, indent=2)
    print(text)
    if audit_path:
        audit_path.parent.mkdir(parents=True, exist_ok=True)
        audit_path.write_text(text + "\n", encoding="utf-8")
    return {"pass": 0, "review": 2, "fail": 1}[verdict]


def _enclosed_background_stats(content_mask):
    """Enclosed non-content area inside a content mask (swallowed-plate signal).

    Non-content pixels that connect to the image border are exterior padding;
    everything else is an enclosed hole — the signature of a plate/interior the
    img2img removed and the key-colour (or alpha) conversion hollowed out.
    Returns (enclosed_area, ratio) with ratio = enclosed / (content + enclosed).
    """
    import cv2 as cv2
    import numpy as np
    inv = (~content_mask).astype(np.uint8)
    num, labels = cv2.connectedComponents(inv, connectivity=4)
    border_labels = set(np.unique(np.concatenate([
        labels[0, :], labels[-1, :], labels[:, 0], labels[:, -1]])).tolist())
    content_area = int(content_mask.sum())
    enclosed_area = 0
    for label in range(1, num):
        if label in border_labels:
            continue
        enclosed_area += int((labels == label).sum())
    total = content_area + enclosed_area
    ratio = enclosed_area / total if total > 0 else 0.0
    return enclosed_area, ratio


def cmd_validate_shell(args: argparse.Namespace) -> int:
    """Step 9b three-state gate: validate composite shell images (contract: 三态门禁).

    A composite shell should contain only the border, frame, shadow, decorative
    elements and the background plate — interactive child UI must have been
    removed. Two failure directions, both with script evidence:

    - **清理不足**（residual interactive UI）: interior colour variance far above
      the plain-plate band (hundreds to thousands; structural decoration also
      lands here — that false-positive class is exactly why the mid/high bands
      route to visual arbitration, not to retry).
    - **过度清理**（plate swallowed by key colour）: the plate becomes key colour /
      transparent after --clean, leaving an ENCLOSED hole inside the shell —
      measured by the enclosed-hole ratio. This direction used to be a script
      blind spot ("uniform = PASS"); the hole ratio makes it mechanical evidence.

    Verdicts:
    - pass (exit 0): interior variance below the conservative pass band AND
      enclosed-hole ratio below threshold → skip visual.
    - review (exit 2): everything else → visual arbitration with the §9b
      two-question prompt (judgement table unchanged).
    - fail (exit 1): unreadable/missing image (error).

    Without --transform the key-colour fallback for RGB (pre-clean) shells is
    unavailable; alpha-less images then review with an explicit reason.
    """
    shell_path = Path(args.shell_image)
    if not shell_path.exists():
        return _emit_verdict(
            "fail", {"image": str(shell_path)},
            {"pass_variance": args.pass_variance,
             "max_enclosed_hole_ratio": args.max_enclosed_hole_ratio},
            [f"error: image not found: {shell_path}"], [], None)

    try:
        import cv2 as cv2
        import numpy as np
    except ImportError:
        print("SHELL VALIDATION SKIP: cv2/numpy not available, cannot validate shell image")
        return 0

    img = cv2.imread(str(shell_path), cv2.IMREAD_UNCHANGED)
    if img is None:
        return _emit_verdict(
            "fail", {"image": str(shell_path)},
            {"pass_variance": args.pass_variance,
             "max_enclosed_hole_ratio": args.max_enclosed_hole_ratio},
            [f"error: cannot read image: {shell_path}"], [], None)
    if img.ndim != 3 or img.shape[2] < 3:
        return _emit_verdict(
            "fail", {"image": str(shell_path)},
            {"pass_variance": args.pass_variance},
            [f"error: unexpected image shape {img.shape}"], [], None)

    key_color = None
    if getattr(args, "transform", None):
        transform = json.loads(Path(args.transform).read_text(encoding="utf-8-sig"))
        key_color = transform.get("key_color")

    rgb = cv2.cvtColor(img[:, :, :3], cv2.COLOR_BGR2RGB)
    has_alpha = img.shape[2] >= 4
    h, w = img.shape[:2]

    # Content mask: opaque pixels (post-clean RGBA) or non-key-colour pixels
    # (pre-clean RGB with --transform). This is the "shell substance".
    if has_alpha:
        content_mask = img[:, :, 3] > 0
    elif key_color is not None:
        import sys as _sys
        scripts_dir = Path(__file__).parent
        if str(scripts_dir) not in _sys.path:
            _sys.path.insert(0, str(scripts_dir))
        from key_color_processing import key_color_mask
        content_mask = key_color_mask(rgb, key_color) > 0
    else:
        return _emit_verdict(
            "review",
            {"image": str(shell_path), "has_alpha": False, "key_color": None},
            {"pass_variance": args.pass_variance},
            ["no alpha channel and no --transform key colour — cannot build a "
             "content mask mechanically; visual arbitration required"], [], None)

    # Over-clean signal BEFORE erosion: enclosed non-content area inside the shell.
    _, enclosed_hole_ratio = _enclosed_background_stats(content_mask)

    # Erode the content mask to peel off the border/frame ring (5% of min dim).
    kernel_size = max(10, int(min(h, w) * 0.05))
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (kernel_size, kernel_size))
    interior_mask = cv2.erode(content_mask.astype(np.uint8) * 255, kernel, iterations=1)
    interior_pixels = rgb[interior_mask > 0]

    metrics: Dict[str, Any] = {
        "image": str(shell_path),
        "has_alpha": has_alpha,
        "interior_pixels": int(len(interior_pixels)),
        "enclosed_hole_ratio": round(float(enclosed_hole_ratio), 6),
    }
    thresholds: Dict[str, Any] = {
        "pass_variance": args.pass_variance,
        "max_enclosed_hole_ratio": args.max_enclosed_hole_ratio,
    }

    if len(interior_pixels) == 0:
        # Eroded to nothing: either a thin border-only shell (historic PASS) or a
        # shell whose plate was swallowed (interior now key colour / transparent).
        # The enclosed-hole ratio separates the two mechanically.
        if enclosed_hole_ratio >= args.max_enclosed_hole_ratio:
            return _emit_verdict(
                "review", metrics, thresholds,
                [f"interior hollow after erosion and enclosed-hole ratio "
                 f"{enclosed_hole_ratio:.3f} >= {args.max_enclosed_hole_ratio} — "
                 "possible over-clean (plate removed); visual arbitration required"],
                [], getattr(args, "audit", None))
        return _emit_verdict(
            "pass", metrics, thresholds,
            ["border-only shell: no interior pixels after erosion and no enclosed hole"],
            [], getattr(args, "audit", None))

    color_var = float(np.mean(np.var(interior_pixels, axis=0)))
    quantized = (interior_pixels // 16) * 16
    unique_colors = len(np.unique(quantized, axis=0))
    metrics["interior_color_var"] = round(color_var, 2)
    metrics["unique_colors"] = int(unique_colors)

    reasons: List[str] = []
    if color_var >= args.pass_variance:
        reasons.append(
            f"interior colour variance {color_var:.1f} >= pass band "
            f"{args.pass_variance} — structural decoration or residual interactive "
            "elements; visual arbitration distinguishes them")
    if enclosed_hole_ratio >= args.max_enclosed_hole_ratio:
        reasons.append(
            f"enclosed-hole ratio {enclosed_hole_ratio:.3f} >= "
            f"{args.max_enclosed_hole_ratio} — possible over-clean (plate swallowed "
            "by key colour and hollowed by --clean)")
    verdict = "pass" if not reasons else "review"
    return _emit_verdict(verdict, metrics, thresholds, reasons, [],
                         getattr(args, "audit", None))


def cmd_validate_background(args: argparse.Namespace) -> int:
    """Step 8.5 three-state gate: is a full-screen background free of UI residue?

    Backgrounds LEGITIMATELY contain character art and large scene blocks
    (Stage 05 §8.5: "最终背景图包含角色立绘"), so global edge density is
    forbidden as a verdict basis — every art-bearing background would trip it.
    Instead:

    1. Complex images (edge density above threshold) go straight to review —
       the script cannot judge ornate-but-clean backgrounds mechanically.
    2. Otherwise, edge clusters are classified by bbox size: large clusters
       (character art / scene outlines) are excluded; remaining SMALL scattered
       clusters (buttons, icons, text fragments) are counted. Pass only when
       both count and area ratio are below conservative thresholds.

    Verdicts: pass (exit 0, skip visual), review (exit 2, visual arbitration),
    fail (exit 1, unreadable image). FAIL never auto-retries — busy-but-legal
    backgrounds are a known false-positive surface (v2 方案 B3 分级).
    """
    bg_path = Path(args.background_image)
    if not bg_path.exists():
        return _emit_verdict(
            "fail", {"image": str(bg_path)},
            {"max_edge_density": args.max_edge_density,
             "large_blob_ratio": args.large_blob_ratio,
             "small_max_side_ratio": args.small_max_side_ratio,
             "max_small_count": args.max_small_count,
             "max_small_area_ratio": args.max_small_area_ratio},
            [f"error: image not found: {bg_path}"], [], None)
    try:
        import cv2 as cv2
        import numpy as np
    except ImportError:
        print("BACKGROUND VALIDATION SKIP: cv2/numpy not available")
        return 0
    img = cv2.imread(str(bg_path), cv2.IMREAD_COLOR)
    if img is None:
        return _emit_verdict(
            "fail", {"image": str(bg_path)},
            {"max_edge_density": args.max_edge_density},
            [f"error: cannot read image: {bg_path}"], [], None)

    h, w = img.shape[:2]
    image_area = h * w
    min_side = min(h, w)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, 50, 150)
    edge_density = float((edges > 0).sum()) / image_area

    thresholds: Dict[str, Any] = {
        "max_edge_density": args.max_edge_density,
        "large_blob_ratio": args.large_blob_ratio,
        "small_max_side_ratio": args.small_max_side_ratio,
        "max_small_count": args.max_small_count,
        "max_small_area_ratio": args.max_small_area_ratio,
    }
    metrics: Dict[str, Any] = {
        "image": str(bg_path), "image_size": {"width": w, "height": h},
        "edge_density": round(edge_density, 6),
    }

    if edge_density > args.max_edge_density:
        return _emit_verdict(
            "review", metrics, thresholds,
            [f"edge density {edge_density:.4f} > {args.max_edge_density} — complex "
             "image (ornate pattern / dense scenery); cannot judge mechanically, "
             "visual arbitration required"], [], getattr(args, "audit", None))

    # Cluster edge pixels (3x3 dilation glues a contour line into one cluster).
    dilated = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=1)
    num, labels, stats, _ = cv2.connectedComponentsWithStats(dilated, connectivity=8)
    large_count = 0
    small_count = 0
    small_area = 0
    medium_count = 0
    for label in range(1, num):
        x, y, bw, bh, area = stats[label]
        if bw * bh >= args.large_blob_ratio * image_area:
            large_count += 1          # character art / scene block — excluded
        elif max(bw, bh) <= args.small_max_side_ratio * min_side:
            small_count += 1          # button / icon / text fragment candidate
            small_area += int(bw * bh)
        else:
            medium_count += 1         # decorative pattern — not counted
    small_area_ratio = small_area / image_area
    metrics.update({
        "excluded_large_clusters": large_count,
        "small_clusters": small_count,
        "medium_clusters": medium_count,
        "small_area_ratio": round(small_area_ratio, 6),
    })
    reasons: List[str] = []
    if small_count > args.max_small_count:
        reasons.append(
            f"{small_count} scattered small edge clusters > {args.max_small_count} — "
            "possible UI residue (buttons/icons/text); visual arbitration required")
    if small_area_ratio > args.max_small_area_ratio:
        reasons.append(
            f"small-cluster area ratio {small_area_ratio:.4f} > "
            f"{args.max_small_area_ratio} — possible UI residue")
    verdict = "pass" if not reasons else "review"
    return _emit_verdict(verdict, metrics, thresholds, reasons, [],
                         getattr(args, "audit", None))


def main() -> int:
    parser = argparse.ArgumentParser(description="Preflight checks for ui-style-reference.")
    sub = parser.add_subparsers(dest="command", required=True)

    p_fc = sub.add_parser("field_completeness", help="Check semantic field propagation (Step 6.3)")
    p_fc.add_argument("tree", type=Path, help="Path to component-tree.json")
    p_fc.add_argument("--records", type=Path, help="Path to semanticRecords.json")
    p_fc.set_defaults(func=cmd_field_completeness)

    p_qe = sub.add_parser("quality_existence", help="Check quality field on valid nodes (Step 6.3)")
    p_qe.add_argument("tree", type=Path, help="Path to component-tree.json")
    p_qe.set_defaults(func=cmd_quality_existence)

    p_lg = sub.add_parser("label_geometry", help="Text-label mounting self-consistency (Step 5.5.4/8.4.5)")
    p_lg.add_argument("tree", type=Path, help="Path to component-tree.json")
    p_lg.set_defaults(func=cmd_label_geometry)

    p_pf = sub.add_parser("preflight", help="Full preflight before output (Step 10)")
    p_pf.add_argument("tree", type=Path, help="Path to component-tree.json")
    p_pf.add_argument("processing_dir", type=Path, help="Processing directory")
    p_pf.set_defaults(func=cmd_preflight)

    p_vr = sub.add_parser("validate_recursion", help="Post-OpenCV validation (Step 8.4)")
    p_vr.add_argument("components_json", type=Path, help="Path to components.json from extract_reference_components.py")
    p_vr.set_defaults(func=cmd_validate_recursion)

    p_vs = sub.add_parser("validate_shell", help="Three-state gate for composite shell images (Step 9b)")
    p_vs.add_argument("shell_image", type=Path, help="Path to composite shell PNG (RGBA post-clean, or RGB pre-clean with --transform)")
    p_vs.add_argument("--transform", type=Path, default=None,
                      help="transform.json — provides key_colour for pre-clean RGB shells")
    p_vs.add_argument("--pass-variance", type=float, default=50.0,
                      help="Conservative pass band for interior colour variance (default 50; calibrate from audit replays)")
    p_vs.add_argument("--max-enclosed-hole-ratio", type=float, default=0.10,
                      help="Max enclosed non-content hole ratio inside the shell (over-clean signal; default 0.10)")
    p_vs.add_argument("--audit", type=Path, default=None, help="Write the verdict JSON to this file")
    p_vs.set_defaults(func=cmd_validate_shell)

    p_vb = sub.add_parser("validate_background", help="Three-state gate for full-screen background images (Step 8.5)")
    p_vb.add_argument("background_image", type=Path, help="Path to background PNG")
    p_vb.add_argument("--max-edge-density", type=float, default=0.04,
                      help="Edge-pixel density above which the image is too complex for mechanical judgement (default 0.04)")
    p_vb.add_argument("--large-blob-ratio", type=float, default=0.04,
                      help="Edge-cluster bbox area ratio treated as large content (character art), excluded (default 0.04 of image area)")
    p_vb.add_argument("--small-max-side-ratio", type=float, default=0.08,
                      help="Edge-cluster bbox max side below this fraction of min side counts as small residue candidate (default 0.08)")
    p_vb.add_argument("--max-small-count", type=int, default=5,
                      help="Max tolerated scattered small clusters for pass (default 5)")
    p_vb.add_argument("--max-small-area-ratio", type=float, default=0.01,
                      help="Max tolerated small-cluster total bbox area ratio for pass (default 0.01)")
    p_vb.add_argument("--audit", type=Path, default=None, help="Write the verdict JSON to this file")
    p_vb.set_defaults(func=cmd_validate_background)

    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
