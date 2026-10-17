#!/usr/bin/env python3
"""Single source of truth for Solo machine semantic identifiers.

A ``semantic_id`` is the stable machine identity of a produced asset. It is not
a path and not a display name:

- ``semantic_id`` — stable, short, machine safe, unchanged across path moves.
- ``purpose`` — natural-language (Chinese) description of what the asset is for.
- ``actual_path`` — the Generator's real output path.
- ``logical_id`` — Job identity; never a substitute for asset identity.

Every producer and validator in the chain imports from this module so the rule
exists exactly once.
"""
from __future__ import annotations

import re
from typing import Any

SEMANTIC_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
SEMANTIC_ID_ERROR_CODE = "SEMANTIC_ID_UNSAFE"
PUBLISH_THUMBNAIL_SEMANTIC_ID = "thumbnail"

_CONTROL = re.compile(r"[\x00-\x1f\x7f]")


def is_safe_semantic_id(value: Any) -> bool:
    """Return True when ``value`` is a usable machine semantic identifier."""
    if not isinstance(value, str) or not value.strip():
        return False
    if _CONTROL.search(value):
        return False
    return bool(SEMANTIC_ID_PATTERN.fullmatch(value))


def semantic_id_rejection(value: Any) -> str:
    """Explain why ``value`` is not a safe semantic ID, for stable diagnostics."""
    if not isinstance(value, str):
        return "must be a string"
    if not value.strip():
        return "must be non-empty"
    if _CONTROL.search(value):
        return "must not contain control characters"
    if "/" in value or "\\" in value:
        return ("must be a stable identifier, not a path; keep the path in "
                "actual_path and give the asset a short machine ID")
    if " " in value:
        return "must not contain spaces"
    if not SEMANTIC_ID_PATTERN.fullmatch(value):
        return ("must match %s; put Chinese or descriptive names in 'purpose'"
                % SEMANTIC_ID_PATTERN.pattern)
    return "is not a safe semantic identifier"


def require_safe_semantic_id(value: Any, label: str) -> str:
    """Return ``value`` when safe, else raise ValueError with a stable code."""
    if is_safe_semantic_id(value):
        return value
    raise ValueError("%s:%s %s" % (SEMANTIC_ID_ERROR_CODE, label, semantic_id_rejection(value)))
