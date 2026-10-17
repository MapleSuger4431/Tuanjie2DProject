#!/usr/bin/env python3
"""Validate a producer-owned UI Toolkit/Create.GameFramework verify receipt."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple

import ui_image_contract

SCHEMA = "codely.ui-verify-receipt/v4"
PRODUCER_SCHEMA = "codely.ui-verify-receipt-producer/v4"
PRODUCER_NAME = "record_ui_gate_receipt.py"
PRODUCER_INPUTS = ("artifacts", "validate_images", "static_ui", "integration_probe")
INTEGRATION_PROBE_FIELDS = {
    "ui_root_components", "panel_settings_type", "uidocument_panel_settings",
    "uidocument_visual_tree_asset", "installer_panel_registry",
    "installer_uidocument_matches_host", "router_uidocument_matches_host",
    "router_target", "router_configured_panels", "registered_panels",
    "panel_controllers",
}
_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_IMAGE_PASS = re.compile(r"\[ImageCheck\]\s*✅\s*All\s+\d+\s+images passed")


def _error(errors: List[Dict[str, str]], code: str, detail: str) -> None:
    errors.append({"code": code, "detail": detail})


def _path(value: Any, root: Path, label: str,
          errors: List[Dict[str, str]]) -> Optional[Path]:
    if not isinstance(value, str) or not value.strip():
        _error(errors, "UI_PATH_INVALID", "%s must be a non-empty path" % label)
        return None
    candidate = Path(value).expanduser()
    if not candidate.is_absolute():
        candidate = root / candidate
    candidate = candidate.resolve(strict=False)
    try:
        candidate.relative_to(root)
    except ValueError:
        _error(errors, "UI_PATH_OUTSIDE_PROJECT", "%s: %s" % (label, value))
        return None
    return candidate


def _relative(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def normalize_uiv_path(value: Any, project_root: Path) -> Optional[str]:
    """Canonicalize paths emitted by UIV without trusting their presentation form."""
    if not isinstance(value, str) or not value.strip():
        return None
    text = re.split(r"[?#]", value.strip(), maxsplit=1)[0].replace("\\", "/")
    prefix = "project://database/"
    if text.casefold().startswith(prefix):
        text = text[len(prefix):]
    candidate = Path(text)
    if candidate.is_absolute():
        try:
            return candidate.resolve(strict=False).relative_to(
                project_root.resolve(strict=False)).as_posix()
        except (OSError, ValueError):
            return None
    while text.startswith("./"):
        text = text[2:]
    if not text or text.startswith("/") or any(part == ".." for part in text.split("/")):
        return None
    return Path(text).as_posix()


def ui_documents_sha256(artifacts: Any, project_root: Path) -> str:
    """Bind the receipt to the exact declared UXML/USS paths and bytes."""
    if not isinstance(artifacts, dict):
        raise ValueError("artifacts must be an object")
    root = project_root.expanduser().resolve(strict=False)
    records: List[Tuple[str, Path]] = []
    for key, suffix in (("uxml", ".uxml"), ("uss", ".uss")):
        values = artifacts.get(key)
        if not isinstance(values, list) or not values:
            raise ValueError("artifacts.%s must be non-empty" % key)
        for value in values:
            if not isinstance(value, str) or not value.strip():
                raise ValueError("artifacts.%s contains an invalid path" % key)
            path = Path(value).expanduser()
            if not path.is_absolute():
                path = root / path
            path = path.resolve(strict=False)
            try:
                relative = path.relative_to(root).as_posix()
            except ValueError as exc:
                raise ValueError("artifacts.%s path is outside project: %s" %
                                 (key, value)) from exc
            if path.suffix.lower() != suffix or not path.is_file():
                raise ValueError("artifacts.%s file is missing: %s" % (key, value))
            records.append((relative, path))
    relative_paths = [relative for relative, _ in records]
    if len(relative_paths) != len(set(relative_paths)):
        raise ValueError("duplicate UXML/USS artifact path")
    digest = hashlib.sha256()
    digest.update(b"codely.ui-documents/v1\0")
    for relative, path in sorted(records):
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
    return digest.hexdigest()


def integration_artifacts_sha256(artifacts: Any, project_root: Path) -> str:
    """Bind integration facts to the exact UIRoot, registry, and settings bytes."""
    if not isinstance(artifacts, dict):
        raise ValueError("artifacts must be an object")
    root = project_root.expanduser().resolve(strict=False)
    records: List[Tuple[str, Path]] = []
    for key, suffix in (("ui_root", ".prefab"), ("panel_registry", ".asset"),
                        ("panel_settings", ".asset")):
        value = artifacts.get(key)
        if not isinstance(value, str) or not value.strip():
            raise ValueError("artifacts.%s must be a non-empty path" % key)
        path = Path(value).expanduser()
        if not path.is_absolute():
            path = root / path
        path = path.resolve(strict=False)
        try:
            relative = path.relative_to(root).as_posix()
        except ValueError as exc:
            raise ValueError("artifacts.%s path is outside project: %s" %
                             (key, value)) from exc
        if path.suffix.lower() != suffix or not path.is_file():
            raise ValueError("artifacts.%s file is missing: %s" % (key, value))
        records.append((relative, path))
    digest = hashlib.sha256()
    digest.update(b"codely.ui-integration-artifacts/v1\0")
    for relative, path in sorted(records):
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
    return digest.hexdigest()


def _load_object(path: Path, code: str,
                 errors: List[Dict[str, str]]) -> Optional[Dict[str, Any]]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        _error(errors, code, "%s: %s" % (path, exc))
        return None
    if not isinstance(value, dict):
        _error(errors, code, "%s: root must be an object" % path)
        return None
    return value


def _entries(data: Dict[str, Any]) -> List[Dict[str, Any]]:
    value = data.get("images", data.get("image_requirements", []))
    return [item for item in value if isinstance(item, dict)] if isinstance(value, list) else []


def _expected_image_names(data: Dict[str, Any]) -> Set[str]:
    result: Set[str] = set()
    for item in _entries(data):
        if item.get("source") not in {"ai_generate", "direct_use", "user_import"}:
            continue
        crop = item.get("crop_path")
        reuse = item.get("reuse_from")
        if isinstance(crop, str) and crop.startswith("reuse_from:"):
            reuse = crop.split(":", 1)[1]
        name = reuse if isinstance(reuse, str) and reuse else item.get("name")
        if isinstance(name, str) and name:
            explicit = item.get("output_file") or item.get("filename")
            result.add(str(explicit) if explicit else "%s.png" % name)
    return result


def _referenced_image_names(paths: List[Path]) -> Set[str]:
    result: Set[str] = set()
    for path in paths:
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeError):
            continue
        for value in re.findall(r"url\(\s*['\"]?([^)'\"]+)", text, re.IGNORECASE):
            path_value = re.split(r"[?#]", value.strip(), maxsplit=1)[0]
            name = Path(path_value).name
            if name:
                result.add(name)
    return result


def _segmented_image_names(entries: List[Dict[str, Any]]) -> Set[str]:
    result: Set[str] = set()
    for item in entries:
        if item.get("is_segmentation") is not True:
            continue
        crop = item.get("crop_path")
        reuse = item.get("reuse_from")
        if isinstance(crop, str) and crop.startswith("reuse_from:"):
            reuse = crop.split(":", 1)[1]
        name = reuse if isinstance(reuse, str) and reuse else item.get("name")
        if not isinstance(name, str) or not name:
            continue
        explicit = item.get("output_file") or item.get("filename")
        result.add(str(explicit) if explicit else "%s.png" % name)
    return result


def _style_image_names(style: str) -> Set[str]:
    result: Set[str] = set()
    pattern = r'''background-image\s*:\s*url\(\s*['"]?([^)'"]+)'''
    for value in re.findall(pattern, style, re.IGNORECASE):
        path_value = re.split(r"[?#]", value.strip(), maxsplit=1)[0]
        name = Path(path_value).name
        if name:
            result.add(name)
    return result


def _has_transparent_background_color(style: str) -> bool:
    matches = re.findall(
        r"(?:^|;)\s*background-color\s*:\s*([^;]+)", style,
        re.IGNORECASE)
    if not matches:
        return False
    value = matches[-1].strip().lower()
    if value == "transparent":
        return True
    rgba = re.fullmatch(
        r"rgba\(\s*[-+\d.]+\s*,\s*[-+\d.]+\s*,\s*[-+\d.]+\s*,\s*([-+\d.]+)\s*\)",
        value)
    if rgba is not None:
        try:
            return float(rgba.group(1)) == 0.0
        except ValueError:
            return False
    if re.fullmatch(r"#[0-9a-f]{8}", value):
        return value[-2:] == "00"
    if re.fullmatch(r"#[0-9a-f]{4}", value):
        return value[-1] == "0"
    return False


def _validate_segmented_image_backgrounds(
        paths: List[Path], entries: List[Dict[str, Any]], root: Path,
        errors: List[Dict[str, str]]) -> None:
    """Require transparent image declarations to clear UI Toolkit's default tint."""
    segmented = _segmented_image_names(entries)
    if not segmented:
        return
    for path in paths:
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeError):
            continue
        declarations: List[Tuple[str, str]] = []
        if path.suffix.lower() == ".uss":
            without_comments = re.sub(r"/\*.*?\*/", " ", text, flags=re.DOTALL)
            declarations.extend(
                (match.group(1).strip(), match.group(2))
                for match in re.finditer(r"([^{}]+)\{([^{}]*)\}", without_comments))
        elif path.suffix.lower() == ".uxml":
            try:
                document = ET.fromstring(text)
            except ET.ParseError:
                continue
            for element in document.iter():
                style = element.attrib.get("style")
                if not style:
                    continue
                identity = (element.attrib.get("name")
                            or element.attrib.get("class")
                            or element.tag.rsplit("}", 1)[-1])
                declarations.append(("inline:%s" % identity, style))
        for owner, style in declarations:
            affected = sorted(_style_image_names(style) & segmented)
            if affected and not _has_transparent_background_color(style):
                _error(
                    errors, "UI_SEGMENTED_IMAGE_OPAQUE_BACKGROUND",
                    "%s [%s] images=%s must declare background-color: rgba(0,0,0,0) "
                    "in the same style rule" %
                    (_relative(path, root), owner, ",".join(affected)))


def _validate_ai_image_receipts(entries: List[Dict[str, Any]], game: str, root: Path,
                                images_dir: Path, references: Set[str],
                                errors: List[Dict[str, str]]) -> None:
    validator_path = Path(__file__).with_name("validate_generator_source_receipt.py")
    try:
        import importlib.util
        spec = importlib.util.spec_from_file_location("_ui_source_validator", validator_path)
        if spec is None or spec.loader is None:
            raise ImportError("validator unavailable")
        validator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(validator)
    except Exception as exc:
        _error(errors, "UI_IMAGE_SOURCE_VALIDATOR", str(exc))
        return
    compiled_path = root / "context" / "games" / game / "job-plan.compiled.json"
    compiled = _load_object(compiled_path, "UI_IMAGE_COMPILED_PLAN_READ", errors)
    if compiled is None:
        return
    try:
        batch_map = ui_image_contract.semantic_batch_map(compiled)
    except ui_image_contract.UiImageContractError as exc:
        _error(errors, exc.code, exc.detail); return
    grouped: Dict[str, List[str]] = {}
    for entry in ui_image_contract.ai_entries(entries):
        semantic_id = entry["name"]
        logical_id = batch_map.get(semantic_id)
        if not isinstance(logical_id, str):
            _error(errors, "UI_IMAGE_BATCH_MAPPING", semantic_id); continue
        grouped.setdefault(logical_id, []).append(semantic_id)
    results: Dict[str, Dict[str, Any]] = {}
    for logical_id, semantic_ids in grouped.items():
        receipt_path = root / ui_image_contract.source_receipt_relative_path(game, logical_id)
        source = _load_object(receipt_path, "UI_IMAGE_SOURCE_RECEIPT_READ", errors)
        if source is None: continue
        results[logical_id] = validator.validate_source(source, root, "ui_image", sorted(semantic_ids))
        for item in results[logical_id].get("errors", []):
            _error(errors, "UI_IMAGE_SOURCE_" + str(item.get("code")),
                   "%s:%s" % (logical_id, item.get("detail")))
    for entry in ui_image_contract.ai_entries(entries):
        semantic_id = entry["name"]
        logical_id = batch_map.get(semantic_id)
        result = results.get(logical_id, {})
        actual = result.get("artifacts", {}).get(semantic_id, {}).get("primary")
        expected_name = ui_image_contract.output_filename(entry)
        if not isinstance(actual, str):
            continue
        actual_path = (root / actual).resolve(strict=False)
        try:
            actual_path.relative_to(images_dir.resolve(strict=False))
        except ValueError:
            _error(errors, "UI_IMAGE_ACTUAL_PATH", "%s:%s" % (semantic_id, actual))
        if actual_path.name != expected_name:
            _error(errors, "UI_IMAGE_ACTUAL_NAME", "%s expected=%s actual=%s" %
                   (semantic_id, expected_name, actual_path.name))
        if expected_name not in references:
            _error(errors, "UI_IMAGE_UNREFERENCED", semantic_id)


def derive_validate_images_status(raw_output: Any) -> str:
    passed = (isinstance(raw_output, str) and _IMAGE_PASS.search(raw_output)
              and "❌" not in raw_output)
    return "passed" if passed else "failed"


def _diagnostics(value: Any) -> Optional[List[Dict[str, Any]]]:
    if not isinstance(value, list) or any(not isinstance(item, dict) for item in value):
        return None
    return value


def _static_ui_facts(value: Any) -> Tuple[str, List[Dict[str, str]]]:
    """Re-derive static closure/class coverage without trusting C# summary fields."""
    issues: List[Dict[str, str]] = []
    if not isinstance(value, dict):
        return "failed", [{"code": "UIV_STRUCTURED_SHAPE", "detail": "object required"}]
    allowed = {"target", "roots", "diagnostics", "errorCount", "warningCount", "passed"}
    if set(value) != allowed:
        issues.append({"code": "UIV_STRUCTURED_FIELDS", "detail": "exact ValidationResult fields required"})
    roots = value.get("roots")
    top = _diagnostics(value.get("diagnostics"))
    if not isinstance(roots, list) or not roots or top is None:
        issues.append({"code": "UIV_STRUCTURED_ROOTS", "detail": "non-empty roots and diagnostics arrays required"})
        return "failed", issues
    all_diagnostics = list(top)
    expected_root_fields = {"root", "uxmlClosure", "ussClosure", "usedClasses", "definedClasses",
                            "diagnostics", "errorCount", "warningCount", "passed"}
    seen_roots: Set[str] = set()
    derived_error_count = sum(item.get("severity") == "error" for item in top)
    derived_warning_count = sum(item.get("severity") == "warning" for item in top)
    for index, root in enumerate(roots):
        label = "roots[%d]" % index
        if not isinstance(root, dict) or set(root) != expected_root_fields:
            issues.append({"code": "UIV_ROOT_FIELDS", "detail": label}); continue
        root_path = root.get("root")
        if not isinstance(root_path, str) or not root_path or root_path in seen_roots:
            issues.append({"code": "UIV_ROOT_IDENTITY", "detail": str(root_path)})
        else: seen_roots.add(root_path)
        for field in ("uxmlClosure", "ussClosure", "usedClasses", "definedClasses"):
            items = root.get(field)
            if (not isinstance(items, list) or any(not isinstance(item, str) or not item for item in items)
                    or items != sorted(set(items))):
                issues.append({"code": "UIV_ROOT_DETERMINISM", "detail": "%s.%s" % (label, field)})
        diagnostics = _diagnostics(root.get("diagnostics"))
        if diagnostics is None:
            issues.append({"code": "UIV_ROOT_DIAGNOSTICS", "detail": label}); continue
        all_diagnostics.extend(diagnostics)
        errors = sum(item.get("severity") == "error" for item in diagnostics)
        warnings = sum(item.get("severity") == "warning" for item in diagnostics)
        derived_error_count += errors; derived_warning_count += warnings
        if root.get("errorCount") != errors or root.get("warningCount") != warnings or root.get("passed") is not (errors == 0):
            issues.append({"code": "UIV_ROOT_SUMMARY_DERIVATION", "detail": str(root_path)})
        used = set(root.get("usedClass", [])); defined = set(root.get("definedClasses", []))
        uncovered = sorted(used - defined)
        reported = sorted(item.get("reference") for item in diagnostics
                          if item.get("severity") == "error" and item.get("code") == "CLASS_NOT_DEFINED_IN_CLOSURE"
                          and isinstance(item.get("reference"), str))
        if uncovered != reported:
            issues.append({"code": "UIV_CLASS_COVERAGE_DERIVATION",
                           "detail": "%s expected=%s actual=%s" % (root_path, uncovered, reported)})
    for diagnostic in all_diagnostics:
        if diagnostic.get("severity") not in {"error", "warning"}:
            issues.append({"code": "UIV_DIAGNOSTIC_SEVERITY", "detail": str(diagnostic.get("severity"))})
    if (value.get("errorCount") != derived_error_count or value.get("warningCount") != derived_warning_count
            or value.get("passed") is not (derived_error_count == 0)):
        issues.append({"code": "UIV_SUMMARY_DERIVATION", "detail": "top-level summary differs from roots"})
    return ("passed" if not issues and derived_error_count == 0 else "failed"), issues


def derive_static_ui_status(value: Any) -> str:
    return _static_ui_facts(value)[0]


def derive_integration_status(integration: Any, artifacts: Any,
                              expected_panels: Any) -> str:
    if not isinstance(integration, dict) or not isinstance(artifacts, dict):
        return "failed"
    if set(integration) not in (INTEGRATION_PROBE_FIELDS,
                                INTEGRATION_PROBE_FIELDS | {"status"}):
        return "failed"
    if not isinstance(expected_panels, list):
        return "failed"
    components = integration.get("ui_root_components")
    registered = integration.get("registered_panels")
    controllers = integration.get("panel_controllers")
    router_panels = integration.get("router_configured_panels")
    required = {"UIDocument", "UIPanelRouter", "UIGameInstaller"}
    controller_panels = {
        item.get("panel") for item in controllers
        if isinstance(item, dict)
        and isinstance(item.get("panel"), str)
        and isinstance(item.get("type"), str) and item.get("type")
        and item.get("base_type") in {"UIPanelBase", "GameUIControllerBase"}
    } if isinstance(controllers, list) else set()
    passed = (
        isinstance(components, list)
        and required.issubset(set(components))
        and integration.get("panel_settings_type") == "PanelSettings"
        and integration.get("uidocument_panel_settings") == artifacts.get("panel_settings")
        and integration.get("uidocument_visual_tree_asset") in (None, "")
        and integration.get("installer_panel_registry") == artifacts.get("panel_registry")
        and integration.get("installer_uidocument_matches_host") is True
        and integration.get("router_uidocument_matches_host") is True
        and integration.get("router_target") == "UIFramePanels"
        and isinstance(router_panels, list)
        and set(expected_panels).issubset(set(router_panels))
        and isinstance(registered, list)
        and set(expected_panels).issubset(set(registered))
        and isinstance(controllers, list)
        and set(expected_panels).issubset(controller_panels)
    )
    return "passed" if passed else "failed"


def _producer_receipt_path(game: str, logical_id: str) -> str:
    return "context/games/%s/evidence/job-%s.ui-gate.json" % (game, logical_id)


def _validate_input_record(name: str, value: Any, root: Path,
                           errors: List[Dict[str, str]]) -> Optional[Path]:
    label = "producer.inputs.%s" % name
    if not isinstance(value, dict) or set(value) != {"path", "sha256"}:
        _error(errors, "UI_PRODUCER_INPUT", "%s must contain only path and sha256" % label)
        return None
    path_value = value.get("path")
    if not isinstance(path_value, str) or Path(path_value).is_absolute():
        _error(errors, "UI_PRODUCER_INPUT_PATH", "%s.path must be project-relative" % label)
        return None
    path = _path(path_value, root, label + ".path", errors)
    if path is None:
        return None
    if not path.is_file():
        _error(errors, "UI_PRODUCER_INPUT_MISSING", str(path))
        return None
    digest = value.get("sha256")
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if not isinstance(digest, str) or _SHA256.fullmatch(digest) is None or digest != actual:
        _error(errors, "UI_PRODUCER_INPUT_STALE", "%s expected=%s actual=%s" %
               (name, digest, actual))
    return path


def _validate_producer(receipt: Dict[str, Any], root: Path,
                       receipt_path: Optional[Path],
                       errors: List[Dict[str, str]]) -> None:
    producer = receipt.get("producer")
    if not isinstance(producer, dict):
        _error(errors, "UI_PRODUCER", "producer metadata is required")
        return
    expected_fields = {"schema", "name", "receipt_path", "inputs",
                       "ui_documents_sha256", "integration_artifacts_sha256"}
    if set(producer) != expected_fields:
        _error(errors, "UI_PRODUCER_FIELDS",
               "producer must contain exactly %s" % ",".join(sorted(expected_fields)))
    if producer.get("schema") != PRODUCER_SCHEMA:
        _error(errors, "UI_PRODUCER_SCHEMA", str(producer.get("schema")))
    if producer.get("name") != PRODUCER_NAME:
        _error(errors, "UI_PRODUCER_NAME", str(producer.get("name")))
    game, logical_id = receipt.get("game"), receipt.get("logical_id")
    expected = (_producer_receipt_path(game, logical_id)
                if isinstance(game, str) and isinstance(logical_id, str) else "")
    declared = producer.get("receipt_path")
    if declared != expected:
        _error(errors, "UI_PRODUCER_RECEIPT_PATH", "expected=%s actual=%s" % (expected, declared))
    if receipt_path is not None:
        actual_receipt = receipt_path.expanduser()
        if not actual_receipt.is_absolute():
            actual_receipt = root / actual_receipt
        actual_receipt = actual_receipt.resolve(strict=False)
        expected_receipt = (root / expected).resolve(strict=False)
        if actual_receipt != expected_receipt:
            _error(errors, "UI_RECEIPT_PATH", "expected=%s actual=%s" %
                   (expected_receipt, actual_receipt))

    inputs = producer.get("inputs")
    if not isinstance(inputs, dict) or set(inputs) != set(PRODUCER_INPUTS):
        _error(errors, "UI_PRODUCER_INPUTS", "exact deterministic input set is required")
        return
    paths = {name: _validate_input_record(name, inputs.get(name), root, errors)
             for name in PRODUCER_INPUTS}
    artifacts_path = paths.get("artifacts")
    if artifacts_path is not None:
        manifest = _load_object(artifacts_path, "UI_PRODUCER_ARTIFACTS_READ", errors)
        if manifest is not None:
            expected_manifest = {
                "image_requirements": receipt.get("image_requirements"),
                "expected_panels": receipt.get("expected_panels"),
                "artifacts": receipt.get("artifacts"),
            }
            if manifest != expected_manifest:
                _error(errors, "UI_PRODUCER_ARTIFACTS_MISMATCH",
                       "receipt artifacts differ from deterministic input")
    for name, check_name in (("validate_images", "validate_images"),):
        path = paths.get(name)
        checks = receipt.get("checks")
        check = checks.get(check_name) if isinstance(checks, dict) else None
        if path is not None and isinstance(check, dict):
            try:
                raw = path.read_text(encoding="utf-8")
            except (OSError, UnicodeError) as exc:
                _error(errors, "UI_PRODUCER_RAW_READ", "%s: %s" % (path, exc))
            else:
                if check.get("raw_output") != raw:
                    _error(errors, "UI_PRODUCER_RAW_MISMATCH", name)
    static_path = paths.get("static_ui")
    checks = receipt.get("checks")
    static_check = checks.get("static_ui") if isinstance(checks, dict) else None
    if static_path is not None and isinstance(static_check, dict):
        structured = _load_object(static_path, "UI_PRODUCER_STATIC_UI_READ", errors)
        if structured is not None and static_check.get("structured") != structured:
            _error(errors, "UI_PRODUCER_STATIC_UI_MISMATCH", "structured input differs")
        digest = hashlib.sha256(static_path.read_bytes()).hexdigest()
        if static_check.get("sha256") != digest:
            _error(errors, "UI_PRODUCER_STATIC_UI_HASH", "expected=%s actual=%s" % (static_check.get("sha256"), digest))
    probe_path = paths.get("integration_probe")
    checks = receipt.get("checks")
    integration = checks.get("integration") if isinstance(checks, dict) else None
    if probe_path is not None and isinstance(integration, dict):
        probe = _load_object(probe_path, "UI_PRODUCER_PROBE_READ", errors)
        observed = dict(integration)
        observed.pop("status", None)
        if probe is not None and probe != observed:
            _error(errors, "UI_PRODUCER_PROBE_MISMATCH",
                   "receipt integration differs from deterministic input")


def _validate_sync(path: Path, suffix: str, game: str, root: Path,
                   errors: List[Dict[str, str]]) -> None:
    assets_root = (root / "Assets" / "Games" / game / "UI").resolve(strict=False)
    try:
        relative = path.relative_to(assets_root)
    except ValueError:
        _error(errors, "UI_%s_ASSETS_PATH" % suffix.upper(), str(path))
        return
    context_path = root / "context" / "games" / game / "UI" / relative
    if not context_path.is_file():
        _error(errors, "UI_%s_CONTEXT_MISSING" % suffix.upper(), str(context_path))
        return
    try:
        synchronized = path.read_bytes() == context_path.read_bytes()
    except OSError as exc:
        _error(errors, "UI_%s_SYNC_READ" % suffix.upper(), str(exc))
        return
    if not synchronized:
        _error(errors, "UI_%s_SYNC_MISMATCH" % suffix.upper(),
               "%s != %s" % (_relative(context_path, root), _relative(path, root)))


def validate_ui(receipt: Dict[str, Any], project_root: Path,
                receipt_path: Optional[Path] = None) -> Dict[str, Any]:
    errors: List[Dict[str, str]] = []
    root = project_root.expanduser().resolve(strict=False)
    if not root.is_dir():
        return {"ok": False, "errors": [{"code": "PROJECT_ROOT", "detail": str(root)}]}
    if not isinstance(receipt, dict):
        return {"ok": False, "errors": [{"code": "UI_RECEIPT_SHAPE", "detail": "receipt must be an object"}]}
    if receipt.get("schema") != SCHEMA:
        _error(errors, "UI_SCHEMA", str(receipt.get("schema")))
    game, logical_id = receipt.get("game"), receipt.get("logical_id")
    if not isinstance(game, str) or not _ID.fullmatch(game):
        _error(errors, "UI_GAME", "unsafe or missing game")
    if not isinstance(logical_id, str) or not _ID.fullmatch(logical_id):
        _error(errors, "UI_LOGICAL_ID", "unsafe or missing logical_id")
    _validate_producer(receipt, root, receipt_path, errors)

    requirements_path = _path(receipt.get("image_requirements"), root,
                              "image_requirements", errors)
    requirements = None
    requirement_entries: List[Dict[str, Any]] = []
    if requirements_path is not None:
        if not requirements_path.is_file():
            _error(errors, "UI_IMAGE_REQUIREMENTS_MISSING", str(requirements_path))
        else:
            requirements = _load_object(requirements_path, "UI_IMAGE_REQUIREMENTS_READ", errors)
            if requirements is not None:
                try:
                    requirement_entries = ui_image_contract.validate_requirements(requirements)
                except ui_image_contract.UiImageContractError as exc:
                    _error(errors, exc.code, exc.detail)

    artifacts = receipt.get("artifacts")
    if not isinstance(artifacts, dict):
        _error(errors, "UI_ARTIFACTS", "artifacts must be an object")
        artifacts = {}
    try:
        current_documents_sha256 = ui_documents_sha256(artifacts, root)
    except (OSError, ValueError) as exc:
        _error(errors, "UI_DOCUMENTS_HASH", str(exc))
    else:
        producer = receipt.get("producer")
        recorded_documents_sha256 = (producer.get("ui_documents_sha256")
                                     if isinstance(producer, dict) else None)
        if (not isinstance(recorded_documents_sha256, str)
                or _SHA256.fullmatch(recorded_documents_sha256) is None
                or recorded_documents_sha256 != current_documents_sha256):
            _error(errors, "UI_DOCUMENTS_STALE",
                   "expected=%s actual=%s" %
                   (recorded_documents_sha256, current_documents_sha256))
    try:
        current_integration_sha256 = integration_artifacts_sha256(artifacts, root)
    except (OSError, ValueError) as exc:
        _error(errors, "UI_INTEGRATION_ARTIFACTS_HASH", str(exc))
    else:
        producer = receipt.get("producer")
        recorded_integration_sha256 = (producer.get("integration_artifacts_sha256")
                                       if isinstance(producer, dict) else None)
        if (not isinstance(recorded_integration_sha256, str)
                or _SHA256.fullmatch(recorded_integration_sha256) is None
                or recorded_integration_sha256 != current_integration_sha256):
            _error(errors, "UI_INTEGRATION_ARTIFACTS_STALE",
                   "expected=%s actual=%s" %
                   (recorded_integration_sha256, current_integration_sha256))
    ui_dir = _path(artifacts.get("ui_directory"), root, "artifacts.ui_directory", errors)
    if ui_dir is not None and not ui_dir.is_dir():
        _error(errors, "UI_DIRECTORY_MISSING", str(ui_dir))

    expected_panels = receipt.get("expected_panels")
    if (not isinstance(expected_panels, list) or not expected_panels
            or any(not isinstance(item, str) or not item for item in expected_panels)
            or len(set(expected_panels)) != len(expected_panels)):
        _error(errors, "UI_EXPECTED_PANELS", "expected_panels must be a unique non-empty string array")
        expected_panels = []
    expected_set = set(expected_panels)
    if requirements is not None:
        image_panels = {str(item.get("panel_name")) for item in _entries(requirements)
                        if item.get("panel_name")}
        missing = image_panels - expected_set
        if missing:
            _error(errors, "UI_PANEL_COVERAGE", ", ".join(sorted(missing)))

    ui_documents: List[Path] = []
    artifact_documents: Dict[str, List[Path]] = {"uxml": [], "uss": []}
    for key, suffix, code in (("uxml", ".uxml", "UI_UXML"),
                              ("uss", ".uss", "UI_USS")):
        values = artifacts.get(key)
        if not isinstance(values, list) or not values:
            _error(errors, code + "_LIST", "artifacts.%s must be non-empty" % key)
            continue
        stems: Set[str] = set()
        for index, value in enumerate(values):
            path = _path(value, root, "artifacts.%s[%d]" % (key, index), errors)
            if path is not None:
                stems.add(path.stem)
                if path.suffix.lower() != suffix or not path.is_file():
                    _error(errors, code + "_MISSING", str(path))
                elif isinstance(game, str) and _ID.fullmatch(game):
                    ui_documents.append(path)
                    artifact_documents[key].append(path)
                    _validate_sync(path, key, game, root, errors)
        uncovered = {panel for panel in expected_set
                     if panel not in stems and panel + "Panel" not in stems}
        if uncovered:
            _error(errors, code + "_PANEL_COVERAGE", ", ".join(sorted(uncovered)))

    images_dir = _path(artifacts.get("images_directory"), root,
                       "artifacts.images_directory", errors)
    if images_dir is not None and not images_dir.is_dir():
        _error(errors, "UI_IMAGES_DIRECTORY_MISSING", str(images_dir))
    if requirements is not None and images_dir is not None and images_dir.is_dir():
        actual = {path.name for path in images_dir.glob("*.png")}
        expected_images = _expected_image_names(requirements)
        missing = expected_images - actual
        if missing:
            _error(errors, "UI_IMAGE_MISSING", ", ".join(sorted(missing)))
        references = _referenced_image_names(ui_documents)
        unused = expected_images - references
        if unused:
            _error(errors, "UI_IMAGE_UNREFERENCED", ", ".join(sorted(unused)))
        if isinstance(game, str) and _ID.fullmatch(game):
            _validate_ai_image_receipts(requirement_entries, game, root, images_dir,
                                        references, errors)
    _validate_segmented_image_backgrounds(
        ui_documents, requirement_entries, root, errors)

    for key, suffix in (("panel_settings", ".asset"), ("panel_registry", ".asset"),
                        ("ui_root", ".prefab")):
        path = _path(artifacts.get(key), root, "artifacts.%s" % key, errors)
        if path is not None and (path.suffix.lower() != suffix or not path.is_file()):
            _error(errors, "UI_" + key.upper() + "_MISSING", str(path))

    checks = receipt.get("checks")
    if not isinstance(checks, dict):
        _error(errors, "UI_CHECKS", "checks must be an object")
        checks = {}
    image_check = checks.get("validate_images")
    image_raw = image_check.get("raw_output") if isinstance(image_check, dict) else None
    image_status = derive_validate_images_status(image_raw)
    if not isinstance(image_check, dict) or image_check.get("status") != image_status:
        _error(errors, "UI_IMAGE_STATUS_DERIVATION", "status must be derived from raw output")
    if image_status != "passed":
        _error(errors, "UI_IMAGE_CHECK_FAILED", "TJValidateAssets.Images() did not report a clean pass")

    static_ui = checks.get("static_ui")
    structured = static_ui.get("structured") if isinstance(static_ui, dict) else None
    static_status, static_issues = _static_ui_facts(structured)
    if not isinstance(static_ui, dict) or set(static_ui) != {"status", "sha256", "structured"}:
        _error(errors, "UIV_CHECK_SHAPE", "static_ui must contain only status, sha256 and structured")
    if not isinstance(static_ui, dict) or static_ui.get("status") != static_status:
        _error(errors, "UIV_STATUS_DERIVATION", "status must be re-derived from structured closure")
    for issue in static_issues:
        _error(errors, issue["code"], issue["detail"])
    if static_status != "passed":
        _error(errors, "UIV_FAILED", "UIV.ValidateStructured did not report a clean static closure")
    if isinstance(structured, dict):
        structured_roots = structured.get("roots")
        root_by_path = {
            normalize_uiv_path(item.get("root"), root): item for item in structured_roots
            if isinstance(item, dict) and normalize_uiv_path(item.get("root"), root) is not None
        } if isinstance(structured_roots, list) else {}
        declared_uss = {_relative(path, root) for path in artifact_documents["uss"]}
        for uxml_path in artifact_documents["uxml"]:
            uxml_relative = _relative(uxml_path, root)
            paired_relative = _relative(uxml_path.with_suffix(".uss"), root)
            root_result = root_by_path.get(uxml_relative)
            closure = root_result.get("ussClosure") if isinstance(root_result, dict) else None
            normalized_closure = ({normalize_uiv_path(item, root) for item in closure}
                                  if isinstance(closure, list) else set())
            normalized_closure.discard(None)
            if paired_relative not in declared_uss:
                _error(errors, "UIV_PANEL_USS_PAIR_MISSING",
                       "%s requires declared %s" % (uxml_relative, paired_relative))
            elif paired_relative not in normalized_closure:
                _error(errors, "UIV_STYLE_CLOSURE_MISSING",
                       "%s did not load %s" % (uxml_relative, paired_relative))

    integration = checks.get("integration")
    integration_status = derive_integration_status(integration, artifacts, expected_panels)
    if not isinstance(integration, dict) or integration.get("status") != integration_status:
        _error(errors, "UI_INTEGRATION_STATUS_DERIVATION",
               "status must be derived from integration facts")
    if integration_status != "passed" or not isinstance(integration, dict):
        _error(errors, "UI_INTEGRATION_FAILED", "Unity integration probe must pass")
    if isinstance(integration, dict):
        if set(integration) != INTEGRATION_PROBE_FIELDS | {"status"}:
            _error(errors, "UI_INTEGRATION_FIELDS",
                   "integration must contain exactly the v4 probe facts and derived status")
        components = integration.get("ui_root_components")
        required = {"UIDocument", "UIPanelRouter", "UIGameInstaller"}
        if not isinstance(components, list) or not required.issubset(set(components)):
            _error(errors, "UI_ROOT_COMPONENTS", ", ".join(sorted(required)))
        if integration.get("panel_settings_type") != "PanelSettings":
            _error(errors, "UI_PANEL_SETTINGS_TYPE", str(integration.get("panel_settings_type")))
        if integration.get("uidocument_panel_settings") != artifacts.get("panel_settings"):
            _error(errors, "UI_UIDOCUMENT_PANEL_SETTINGS",
                   "UIDocument does not reference receipt PanelSettings")
        if integration.get("uidocument_visual_tree_asset") not in (None, ""):
            _error(errors, "UI_UIDOCUMENT_TREE", "UIRoot UIDocument visualTreeAsset must be empty")
        if integration.get("installer_panel_registry") != artifacts.get("panel_registry"):
            _error(errors, "UI_INSTALLER_REGISTRY",
                   "UIGameInstaller does not reference receipt PanelRegistry")
        if integration.get("installer_uidocument_matches_host") is not True:
            _error(errors, "UI_INSTALLER_UIDOCUMENT_WIRING",
                   "UIGameInstaller UIDocument reference must point to the host UIDocument")
        if integration.get("router_uidocument_matches_host") is not True:
            _error(errors, "UI_ROUTER_UIDOCUMENT_WIRING",
                   "UIPanelRouter must resolve the UIDocument on its own host GameObject")
        if integration.get("router_target") != "UIFramePanels":
            _error(errors, "UI_ROUTER_TARGET",
                   "Separate [Panel] UXML assets require UIPanelRouter target UIFramePanels")
        router_panels = integration.get("router_configured_panels")
        if not isinstance(router_panels, list) or not expected_set.issubset(set(router_panels)):
            actual_router_panels = set(router_panels) if isinstance(router_panels, list) else set()
            _error(errors, "UI_ROUTER_PANEL_COVERAGE",
                   ", ".join(sorted(expected_set - actual_router_panels)))
        registered = integration.get("registered_panels")
        if not isinstance(registered, list) or not expected_set.issubset(set(registered)):
            actual_registered = set(registered) if isinstance(registered, list) else set()
            _error(errors, "UI_REGISTRY_COVERAGE",
                   ", ".join(sorted(expected_set - actual_registered)))
        controllers = integration.get("panel_controllers")
        controller_panels = {
            item.get("panel") for item in controllers
            if isinstance(item, dict)
            and isinstance(item.get("panel"), str)
            and isinstance(item.get("type"), str) and item.get("type")
            and item.get("base_type") in {"UIPanelBase", "GameUIControllerBase"}
        } if isinstance(controllers, list) else set()
        if not isinstance(controllers, list) or not expected_set.issubset(controller_panels):
            _error(errors, "UI_PANEL_CONTROLLER_COVERAGE",
                   ", ".join(sorted(expected_set - controller_panels)))

    return {"ok": not errors, "schema": SCHEMA, "game": game,
            "logical_id": logical_id, "errors": errors}


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(allow_abbrev=False)
    parser.add_argument("receipt", nargs="?", help="receipt JSON; defaults to UTF-8 stdin")
    parser.add_argument("--project-root", default=".")
    args = parser.parse_args(argv)
    try:
        path = Path(args.receipt) if args.receipt else None
        text = path.read_text(encoding="utf-8") if path else sys.stdin.buffer.read().decode("utf-8")
        result = validate_ui(json.loads(text), Path(args.project_root), path)
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        result = {"ok": False, "errors": [{"code": "UI_RECEIPT_READ", "detail": str(exc)}]}
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
