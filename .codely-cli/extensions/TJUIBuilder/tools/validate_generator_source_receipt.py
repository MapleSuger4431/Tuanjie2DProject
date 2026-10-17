#!/usr/bin/env python3
"""Fail-closed validator for current multi-artifact Generator source receipts."""
from __future__ import annotations
import argparse, hashlib, importlib.util, json, re
from pathlib import Path
from typing import Any, Dict, List, Optional

SOURCE_SCHEMA = "codely.generator-source-receipt/v4"
RECORDER_ID = "record_generator_result.py"
IMAGE_KINDS = {"image_asset", "sprite_asset", "publish_thumbnail", "ui_image"}
IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff"}
AUDIO_MIN_BYTES = 5 * 1024
_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def _err(errors, code, detail): errors.append({"code": code, "detail": detail})
def _hash(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def _path(value, root, label, errors):
    if not isinstance(value, str) or not value.strip() or any(ord(ch) < 32 or ord(ch) == 127 for ch in value):
        _err(errors, "SOURCE_PATH_INVALID", label); return None
    path = Path(value).expanduser(); path = path if path.is_absolute() else root / path; path = path.resolve(strict=False)
    try: path.relative_to(root)
    except ValueError: _err(errors, "SOURCE_PATH_OUTSIDE_PROJECT", label); return None
    return path


def _image_validator():
    path = Path(__file__).with_name("validate_generated_image.py")
    spec = importlib.util.spec_from_file_location("_generator_image", path); module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader; spec.loader.exec_module(module); return module


def _image_contracts(compiled_plan: Optional[Path], logical_id: Any,
                     errors: List[Dict[str, str]]) -> Optional[Dict[str, Dict[str, str]]]:
    if compiled_plan is None:
        return None
    try:
        compiled = json.loads(compiled_plan.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        _err(errors, "SOURCE_COMPILED_READ", str(exc)); return {}
    matches = [job for job in compiled.get("jobs", []) if isinstance(job, dict)
               and job.get("kind") == "asset_finalize"
               and isinstance(job.get("asset_finalize_batch_contract"), dict)
               and job["asset_finalize_batch_contract"].get("source_logical_id") == logical_id]
    if len(matches) != 1:
        _err(errors, "SOURCE_FINALIZE_CONTRACT", str(logical_id)); return {}
    contracts: Dict[str, Dict[str, str]] = {}
    for item in matches[0]["asset_finalize_batch_contract"].get("items", []):
        if not isinstance(item, dict):
            continue
        primary = [artifact for artifact in item.get("artifacts", [])
                   if isinstance(artifact, dict) and artifact.get("role") == "primary"]
        semantic_id = item.get("source_semantic_id")
        contract = primary[0].get("contract") if len(primary) == 1 else None
        if not isinstance(semantic_id, str) or not isinstance(contract, dict):
            _err(errors, "SOURCE_IMAGE_CONTRACT", str(semantic_id)); continue
        image_contract = {key: contract.get(key) for key in ("usage_mode", "alpha_mode", "tile_mode")}
        if any(not isinstance(value, str) or not value for value in image_contract.values()):
            _err(errors, "SOURCE_IMAGE_CONTRACT", semantic_id); continue
        contracts[semantic_id] = image_contract
    return contracts


def validate_source(receipt: Dict[str, Any], project_root: Path, expected_kind: Optional[str] = None,
                    expected_artifacts: Optional[List[str]] = None,
                    compiled_plan: Optional[Path] = None) -> Dict[str, Any]:
    errors: List[Dict[str, str]] = []; root = project_root.resolve(strict=False)
    if not isinstance(receipt, dict): return {"ok": False, "errors": [{"code": "SOURCE_SHAPE", "detail": "object required"}]}
    required = {"schema", "producer", "game", "logical_id", "kind", "checkpoint_mode", "expected_items", "items", "complete"}
    if set(receipt) not in (required, required | {"revisions"}):
        _err(errors, "SOURCE_FIELDS", "exact current receipt fields required")
    if receipt.get("schema") != SOURCE_SCHEMA: _err(errors, "SOURCE_SCHEMA", str(receipt.get("schema")))
    if receipt.get("producer") != RECORDER_ID: _err(errors, "SOURCE_PRODUCER", str(receipt.get("producer")))
    if receipt.get("checkpoint_mode") != "per_item": _err(errors, "SOURCE_CHECKPOINT_MODE", str(receipt.get("checkpoint_mode")))
    for key in ("game", "logical_id"):
        if not isinstance(receipt.get(key), str) or not _ID.fullmatch(receipt[key]): _err(errors, "SOURCE_IDENTITY", key)
    kind = receipt.get("kind")
    if expected_kind and kind != expected_kind: _err(errors, "SOURCE_KIND_MISMATCH", "expected=%s actual=%s" % (expected_kind, kind))
    if kind in {"image_asset", "sprite_asset"} and compiled_plan is None:
        _err(errors, "SOURCE_COMPILED_PLAN_REQUIRED", str(receipt.get("logical_id")))
    image_contracts = (_image_contracts(compiled_plan, receipt.get("logical_id"), errors)
                       if kind in {"image_asset", "sprite_asset"} and compiled_plan is not None else None)
    expected = receipt.get("expected_items"); items = receipt.get("items")
    if not isinstance(expected, list) or not expected: _err(errors, "SOURCE_EXPECTED_ITEMS", "non-empty array required"); expected = []
    if not isinstance(items, list): _err(errors, "SOURCE_ITEMS", "array required"); items = []
    revisions = receipt.get("revisions", [])
    if not isinstance(revisions, list):
        _err(errors, "SOURCE_REVISIONS", "array required"); revisions = []
    expected_ids = []
    for ordinal, item in enumerate(expected):
        if not isinstance(item, dict) or set(item) != {"ordinal", "semantic_id"} or item.get("ordinal") != ordinal or not isinstance(item.get("semantic_id"), str) or not _ID.fullmatch(item["semantic_id"]):
            _err(errors, "SOURCE_EXPECTED_ITEM", str(ordinal)); continue
        expected_ids.append(item["semantic_id"])
    if len(expected_ids) != len(set(expected_ids)): _err(errors, "SOURCE_EXPECTED_ITEM_DUPLICATE", "semantic_id")
    if expected_artifacts is not None and expected_ids != expected_artifacts: _err(errors, "SOURCE_ARTIFACT_SET", "expected compiled items mismatch")
    succeeded = []; artifact_map: Dict[str, Dict[str, str]] = {}
    item_fields = {"ordinal", "semantic_id", "status", "backend_task_id", "task_id", "type", "artifacts", "error", "updated_at"}
    for index, revision in enumerate(revisions):
        label = "revision[%s]" % index
        if not isinstance(revision, dict) or set(revision) != {"ordinal", "replaced_at", "item"}:
            _err(errors, "SOURCE_REVISION_SHAPE", label); continue
        ordinal = revision.get("ordinal"); historical = revision.get("item")
        if not isinstance(ordinal, int) or ordinal < 0 or ordinal >= len(expected):
            _err(errors, "SOURCE_REVISION_ORDINAL", label); continue
        if not isinstance(revision.get("replaced_at"), str) or not revision["replaced_at"].strip():
            _err(errors, "SOURCE_REVISION_TIME", label)
        if not isinstance(historical, dict) or set(historical) != item_fields:
            _err(errors, "SOURCE_REVISION_ITEM", label); continue
        if historical.get("ordinal") != ordinal or historical.get("semantic_id") != expected[ordinal].get("semantic_id") \
                or historical.get("status") != "succeeded" or historical.get("type") != kind:
            _err(errors, "SOURCE_REVISION_IDENTITY", label)
        historical_artifacts = historical.get("artifacts")
        if not isinstance(historical_artifacts, list) or not historical_artifacts:
            _err(errors, "SOURCE_REVISION_ARTIFACTS", label); continue
        for artifact in historical_artifacts:
            if not isinstance(artifact, dict) or set(artifact) != {"role", "actual_path", "sha256"} \
                    or not isinstance(artifact.get("role"), str) or not _ID.fullmatch(artifact["role"]) \
                    or not isinstance(artifact.get("actual_path"), str) \
                    or not isinstance(artifact.get("sha256"), str) or not _SHA256.fullmatch(artifact["sha256"]):
                _err(errors, "SOURCE_REVISION_ARTIFACTS", label); break
    seen = set()
    for item in items:
        if not isinstance(item, dict) or set(item) != item_fields: _err(errors, "SOURCE_ITEM_SHAPE", "exact fields required"); continue
        ordinal = item.get("ordinal")
        if not isinstance(ordinal, int) or ordinal in seen or ordinal < 0 or ordinal >= len(expected): _err(errors, "SOURCE_ITEM_ORDINAL", str(ordinal)); continue
        seen.add(ordinal)
        semantic_id = item.get("semantic_id")
        if semantic_id != expected[ordinal].get("semantic_id"): _err(errors, "SOURCE_ITEM_IDENTITY", str(ordinal))
        status = item.get("status")
        if status not in {"submitted", "succeeded", "failed", "cancelled"}: _err(errors, "SOURCE_ITEM_STATUS", str(status))
        for key in ("backend_task_id", "task_id", "type", "updated_at"):
            if not isinstance(item.get(key), str) or not item[key].strip(): _err(errors, "SOURCE_ITEM_FIELD", "%s:%s" % (ordinal, key))
        if item.get("type") != kind: _err(errors, "SOURCE_ITEM_TYPE", str(ordinal))
        artifacts = item.get("artifacts")
        if not isinstance(artifacts, list): _err(errors, "SOURCE_ITEM_ARTIFACTS", str(ordinal)); artifacts = []
        if status == "succeeded":
            if not artifacts: _err(errors, "SOURCE_ITEM_ARTIFACTS", "%s:empty" % ordinal)
            roles = set(); valid: Dict[str, str] = {}
            for artifact_index, artifact in enumerate(artifacts):
                label = "item[%s].artifacts[%s]" % (ordinal, artifact_index)
                if not isinstance(artifact, dict) or set(artifact) != {"role", "actual_path", "sha256"}:
                    _err(errors, "SOURCE_ARTIFACT_SHAPE", label); continue
                role = artifact.get("role")
                if not isinstance(role, str) or not _ID.fullmatch(role): _err(errors, "SOURCE_ARTIFACT_ROLE", label); continue
                if role in roles: _err(errors, "SOURCE_ARTIFACT_ROLE_DUPLICATE", "%s:%s" % (ordinal, role)); continue
                roles.add(role)
                path = _path(artifact.get("actual_path"), root, label + ".actual_path", errors)
                if path is None or not path.is_file(): _err(errors, "SOURCE_OUTPUT_NOT_FILE", str(artifact.get("actual_path"))); continue
                digest = artifact.get("sha256")
                if not isinstance(digest, str) or not _SHA256.fullmatch(digest) or digest != _hash(path): _err(errors, "SOURCE_OUTPUT_HASH", str(path))
                if path.stat().st_size <= 0: _err(errors, "SOURCE_OUTPUT_EMPTY", str(path))
                if role == "primary" and kind == "audio_asset" and path.stat().st_size < AUDIO_MIN_BYTES: _err(errors, "SOURCE_AUDIO_PLACEHOLDER_SIZE", str(path))
                if role == "primary" and kind in IMAGE_KINDS:
                    if path.suffix.lower() not in IMAGE_SUFFIXES: _err(errors, "SOURCE_IMAGE_TYPE", str(path))
                    else:
                        contract = image_contracts.get(semantic_id) if image_contracts is not None else None
                        if image_contracts is not None and contract is None:
                            _err(errors, "SOURCE_IMAGE_CONTRACT", str(semantic_id))
                            continue
                        contract = contract or {"usage_mode": "full_frame", "alpha_mode": "opaque", "tile_mode": "none"}
                        check = _image_validator().validate_image(
                            path, contract["usage_mode"], contract["alpha_mode"], contract["tile_mode"])
                        for issue in check.get("errors", []):
                            code = str(issue.get("code", "SOURCE_IMAGE_CONTRACT"))
                            if image_contracts is not None or code in {
                                    "PILLOW_MISSING", "IMAGE_READ", "IMAGE_PLACEHOLDER_SIZE", "IMAGE_PLACEHOLDER_MARKER"}:
                                _err(errors, code, "%s:%s" % (path, issue.get("detail", "")))
                valid[role] = artifact.get("actual_path")
            if sum(isinstance(artifact, dict) and artifact.get("role") == "primary" for artifact in artifacts) != 1:
                _err(errors, "SOURCE_PRIMARY_ARTIFACT", str(ordinal))
            if item.get("error") not in (None, ""): _err(errors, "SOURCE_SUCCESS_ERROR", str(ordinal))
            if semantic_id in expected_ids: artifact_map[semantic_id] = valid
            succeeded.append(item)
        else:
            if artifacts: _err(errors, "SOURCE_NON_SUCCESS_ARTIFACT", str(ordinal))
            if status in {"failed", "cancelled"} and (not isinstance(item.get("error"), str) or not item["error"].strip()): _err(errors, "SOURCE_ITEM_ERROR", str(ordinal))
            if status == "submitted" and item.get("error") not in (None, ""): _err(errors, "SOURCE_ITEM_ERROR", str(ordinal))
    derived_complete = len(items) == len(expected) and len(succeeded) == len(expected)
    if receipt.get("complete") is not derived_complete: _err(errors, "SOURCE_COMPLETE_DERIVATION", str(receipt.get("complete")))
    if not derived_complete: _err(errors, "SOURCE_INCOMPLETE", "not every compiled item succeeded")
    return {"ok": not errors, "complete": derived_complete, "artifacts": artifact_map, "errors": errors}


def main(argv=None):
    parser = argparse.ArgumentParser(allow_abbrev=False); parser.add_argument("receipt"); parser.add_argument("--project-root", default="."); parser.add_argument("--expected-kind"); parser.add_argument("--expected-artifact", action="append"); parser.add_argument("--compiled-plan")
    args = parser.parse_args(argv)
    try: result = validate_source(json.loads(Path(args.receipt).read_text(encoding="utf-8")), Path(args.project_root), args.expected_kind, args.expected_artifact, Path(args.compiled_plan) if args.compiled_plan else None)
    except Exception as exc: result = {"ok": False, "errors": [{"code": "SOURCE_RECEIPT_READ", "detail": str(exc)}]}
    print(json.dumps(result, ensure_ascii=False, sort_keys=True)); return 0 if result["ok"] else 1

if __name__ == "__main__": raise SystemExit(main())
