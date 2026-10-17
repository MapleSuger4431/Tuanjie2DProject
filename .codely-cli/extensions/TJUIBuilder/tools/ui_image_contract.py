#!/usr/bin/env python3
"""Shared deterministic contract for compiler-owned UI image jobs."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from typing import Any, Dict, List, Optional

import semantic_id as semantic_id_api

SOURCES = {"ai_generate", "direct_use", "uss_only", "user_import"}
TEXT_TYPES = {"art_text", "label", "none"}
_ID_PART = re.compile(r"[^A-Za-z0-9._-]+")


class UiImageContractError(ValueError):
    def __init__(self, code: str, detail: str):
        super().__init__("%s:%s" % (code, detail))
        self.code = code
        self.detail = detail


def requirements_relative_path(game: str) -> str:
    return "context/games/%s/image_requirements.json" % game


def source_receipt_relative_path(game: str, logical_id: str) -> str:
    return "context/games/%s/generation/%s.source.json" % (game, logical_id)


def batch_logical_id(semantic_ids: List[str]) -> str:
    ordered = sorted(semantic_ids)
    if not ordered or len(ordered) != len(set(ordered)):
        raise UiImageContractError("UI_IMAGE_BATCH_ITEMS", "non-empty unique semantic IDs required")
    digest = hashlib.sha256("\0".join(ordered).encode("utf-8")).hexdigest()[:16]
    return "ui-image-batch-%s" % digest


def semantic_batch_map(compiled: Dict[str, Any]) -> Dict[str, str]:
    jobs = compiled.get("jobs") if isinstance(compiled, dict) else None
    if not isinstance(jobs, list):
        raise UiImageContractError("UI_IMAGE_COMPILED_PLAN", "jobs must be an array")
    result: Dict[str, str] = {}
    for job in jobs:
        if not isinstance(job, dict) or job.get("kind") != "ui_image":
            continue
        logical_id = job.get("logical_id")
        contract = job.get("ui_image_batch_contract")
        items = contract.get("items") if isinstance(contract, dict) else None
        if not isinstance(logical_id, str) or not isinstance(items, list):
            raise UiImageContractError("UI_IMAGE_BATCH_CONTRACT", str(logical_id))
        for item in items:
            semantic_id = item.get("semantic_id") if isinstance(item, dict) else None
            if not isinstance(semantic_id, str) or semantic_id in result:
                raise UiImageContractError("UI_IMAGE_BATCH_MAPPING", str(semantic_id))
            result[semantic_id] = logical_id
    return result


def output_filename(entry: Dict[str, Any]) -> str:
    explicit = entry.get("output_file") or entry.get("filename")
    return str(explicit) if isinstance(explicit, str) and explicit.strip() else "%s.png" % entry["name"]


def _positive_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and value > 0


def validate_requirements(value: Any) -> List[Dict[str, Any]]:
    if not isinstance(value, dict):
        raise UiImageContractError("UI_IMAGE_REQUIREMENTS_SHAPE", "root must be an object")
    images = value.get("images", value.get("image_requirements"))
    if not isinstance(images, list):
        raise UiImageContractError("UI_IMAGE_REQUIREMENTS_IMAGES", "images must be an array")
    result: List[Dict[str, Any]] = []
    names = set()
    for index, item in enumerate(images):
        label = "images[%d]" % index
        if not isinstance(item, dict):
            raise UiImageContractError("UI_IMAGE_REQUIREMENT_SHAPE", label)
        name = item.get("name")
        source = item.get("source")
        if not isinstance(name, str) or not name.strip():
            raise UiImageContractError("UI_IMAGE_REQUIREMENT_NAME", label)
        name = name.strip()
        # `name` is this image's machine semantic ID; Chinese and descriptive
        # wording belong in the visual/text fields, not in the identity.
        if not semantic_id_api.is_safe_semantic_id(name):
            raise UiImageContractError(
                "UI_IMAGE_REQUIREMENT_NAME",
                "%s:%r %s" % (label, name, semantic_id_api.semantic_id_rejection(name)))
        if name in names:
            raise UiImageContractError("UI_IMAGE_REQUIREMENT_DUPLICATE", name)
        names.add(name)
        if source not in SOURCES:
            raise UiImageContractError("UI_IMAGE_REQUIREMENT_SOURCE", "%s:%s" % (name, source))
        for field in ("component_type", "panel_name", "panel_type", "uxml_element"):
            if not isinstance(item.get(field), str) or not item[field].strip():
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:%s" % (name, field))
        if source in {"ai_generate", "direct_use"}:
            if not isinstance(item.get("crop_path"), str) or not item["crop_path"].strip():
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:crop_path" % name)
            if item.get("is_segmentation") is not None and not isinstance(item.get("is_segmentation"), bool):
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:is_segmentation" % name)
            for field in ("width", "height"):
                if not _positive_number(item.get(field)):
                    raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:%s" % (name, field))
        if source == "ai_generate":
            text_type = item.get("text_type")
            if text_type not in TEXT_TYPES:
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:text_type" % name)
            if text_type in {"art_text", "label"} and not isinstance(item.get("display_text"), str):
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:display_text" % name)
            if text_type == "art_text" and (not isinstance(item.get("art_text_style"), str)
                                              or not item["art_text_style"].strip()):
                raise UiImageContractError("UI_IMAGE_REQUIREMENT_FIELD", "%s:art_text_style" % name)
        normalized = dict(item)
        normalized["name"] = name
        result.append(normalized)
    _validate_functional_subparts(result)
    return result


def _validate_functional_subparts(entries: List[Dict[str, Any]]) -> None:
    """Validate that functional_type controls have mandatory sub-parts."""
    # Group by composite_group or expanded_from to find sibling sub-parts
    groups: Dict[str, List[Dict[str, Any]]] = {}
    for entry in entries:
        key = entry.get("composite_group") or entry.get("expanded_from")
        if key:
            groups.setdefault(key, []).append(entry)
    for group_name, members in groups.items():
        functional_type = next(
            (m.get("functional_type") for m in members if m.get("functional_type")),
            None,
        )
        if not functional_type:
            continue
        sub_parts = {m.get("sub_part", "") for m in members}
        if functional_type == "slider":
            if "fill" not in sub_parts:
                has_uss_only_fill = any(
                    m.get("source") == "uss_only" and "fill" in m.get("sub_part", "")
                    for m in members
                )
                if not has_uss_only_fill:
                    raise UiImageContractError(
                        "UI_IMAGE_REQUIREMENT_SLIDER_FILL_MISSING",
                        "slider group '%s' requires 'fill' sub-part or explicit uss_only fill" % group_name,
                    )
        elif functional_type == "dropdown":
            if "expanded_panel_bg" not in sub_parts:
                raise UiImageContractError(
                    "UI_IMAGE_REQUIREMENT_DROPDOWN_EXPANDED_MISSING",
                    "dropdown group '%s' requires 'expanded_panel_bg' sub-part separate from collapsed bg" % group_name,
                )


def load_requirements(path: Path) -> List[Dict[str, Any]]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise UiImageContractError("UI_IMAGE_REQUIREMENTS_READ", str(exc)) from exc
    return validate_requirements(value)


def ai_entries(entries: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    return [entry for entry in entries if entry.get("source") == "ai_generate"]


def ui_document_declares_images(path: Optional[Path]) -> bool:
    if path is None or not path.is_file():
        return False
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeError):
        return False
    return bool(re.search(r"\b(?:ai_generate|direct_use|uss_only|user_import)\b", text)
                or re.search(r"\.png(?:\s|\||`|$)", text, re.IGNORECASE))
