#!/usr/bin/env python3
"""Atomically persist one managed Generator item checkpoint and derive receipt completion."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional

SOURCE_SCHEMA = "codely.generator-source-receipt/v4"
TERMINAL_SCHEMA = "codely.generator-terminal-result/v2"
RECORDER_ID = "record_generator_result.py"
ITEM_STATUSES = {"submitted", "succeeded", "failed", "cancelled"}
_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_CONTROL = re.compile(r"[\x00-\x1f\x7f]")


class RecordError(ValueError):
    pass


def _atomic_write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".%s." % path.name, suffix=".tmp", dir=str(path.parent))
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(text); stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary, str(path))
    except Exception:
        try: os.unlink(temporary)
        except OSError: pass
        raise


def _canonical(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def _identifier(value: Any, label: str) -> str:
    if not isinstance(value, str) or not _ID.fullmatch(value):
        raise RecordError("%s is not a safe identifier" % label)
    return value


def _inside(value: Any, root: Path, label: str, must_exist: bool = False) -> Path:
    if not isinstance(value, str) or not value.strip() or _CONTROL.search(value):
        raise RecordError("%s must be a safe non-empty path" % label)
    candidate = Path(value).expanduser()
    if not candidate.is_absolute(): candidate = root / candidate
    resolved = candidate.resolve(strict=False)
    try: resolved.relative_to(root)
    except ValueError: raise RecordError("%s is outside project" % label)
    if must_exist and not resolved.is_file(): raise RecordError("%s does not exist" % label)
    return resolved


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""): digest.update(block)
    return digest.hexdigest()


def _expected(payload: Dict[str, Any]) -> List[Dict[str, Any]]:
    items = payload.get("items")
    if not isinstance(items, list) or not items:
        raise RecordError("items must be the compiled non-empty Generator contract")
    normalized = []
    for index, item in enumerate(items):
        allowed = {"ordinal", "semantic_id", "generation_request_id",
                   "usage_mode", "alpha_mode", "tile_mode", "is_segmentation"}
        if (not isinstance(item, dict) or not {"ordinal", "semantic_id"}.issubset(item)
                or not set(item).issubset(allowed)
                or item.get("ordinal") != index or not isinstance(item.get("semantic_id"), str)
                or not _ID.fullmatch(item["semantic_id"])):
            raise RecordError("items must be compiled Generator items with contiguous ordinal and safe semantic_id")
        request_id = item.get("generation_request_id")
        if request_id is not None and (not isinstance(request_id, str) or not _SHA256.fullmatch(request_id)):
            raise RecordError("items generation_request_id must be sha256 when present")
        normalized.append({"ordinal": index, "semantic_id": item["semantic_id"]})
    if len({item["semantic_id"] for item in normalized}) != len(normalized):
        raise RecordError("item semantic_id values must be unique")
    return normalized


def _artifacts(value: Any, root: Path, succeeded: bool) -> List[Dict[str, str]]:
    if not isinstance(value, list):
        raise RecordError("terminal artifacts must be an array")
    if not succeeded:
        if value: raise RecordError("non-succeeded item cannot claim artifacts")
        return []
    if not value:
        raise RecordError("succeeded item requires artifacts")
    result: List[Dict[str, str]] = []
    roles = set()
    for index, artifact in enumerate(value):
        if not isinstance(artifact, dict) or set(artifact) != {"role", "actual_path", "sha256"}:
            raise RecordError("terminal artifact exact fields required")
        role = _identifier(artifact.get("role"), "terminal.artifacts[%d].role" % index)
        if role in roles: raise RecordError("terminal artifact roles must be unique")
        roles.add(role)
        path = _inside(artifact.get("actual_path"), root, "terminal.artifacts[%d].actual_path" % index, True)
        actual_hash = _sha256(path)
        if artifact.get("sha256") != actual_hash or not _SHA256.fullmatch(str(artifact.get("sha256", ""))):
            raise RecordError("terminal artifact sha256 does not match actual file")
        result.append({"role": role, "actual_path": path.relative_to(root).as_posix(), "sha256": actual_hash})
    if sum(item["role"] == "primary" for item in result) != 1:
        raise RecordError("succeeded item requires exactly one primary artifact")
    return result


def _terminal(payload: Dict[str, Any], expected: List[Dict[str, Any]], root: Path) -> Dict[str, Any]:
    terminal = payload.get("terminal", payload)
    if not isinstance(terminal, dict) or terminal.get("schema") != TERMINAL_SCHEMA:
        raise RecordError("exact terminal schema required: %s" % TERMINAL_SCHEMA)
    allowed = {"schema", "ordinal", "semantic_id", "status", "backend_task_id", "task_id",
               "type", "artifacts", "error", "updated_at"}
    if set(terminal) != allowed:
        raise RecordError("terminal result exact fields required")
    ordinal = terminal.get("ordinal")
    if not isinstance(ordinal, int) or ordinal < 0 or ordinal >= len(expected):
        raise RecordError("terminal ordinal is outside compiled items")
    if terminal.get("semantic_id") != expected[ordinal]["semantic_id"]:
        raise RecordError("terminal semantic_id/ordinal mismatch")
    status = terminal.get("status")
    if status not in ITEM_STATUSES:
        raise RecordError("terminal status must be submitted/succeeded/failed/cancelled")
    for key in ("backend_task_id", "task_id", "type", "updated_at"):
        if not isinstance(terminal.get(key), str) or not terminal[key].strip():
            raise RecordError("terminal %s must be non-empty" % key)
    if terminal["type"] != payload.get("kind"):
        raise RecordError("terminal type must match Generator kind")
    item = {key: terminal.get(key) for key in ("ordinal", "semantic_id", "status", "backend_task_id",
                                                "task_id", "type", "artifacts", "error", "updated_at")}
    item["artifacts"] = _artifacts(item["artifacts"], root, status == "succeeded")
    if status == "succeeded":
        if item.get("error") not in (None, ""): raise RecordError("succeeded item cannot contain error")
        item["error"] = None
    elif status in {"failed", "cancelled"}:
        if not isinstance(item.get("error"), str) or not item["error"].strip():
            raise RecordError("failed/cancelled item requires error")
    else:
        item["error"] = None
    return item


def _merge_manifest(root: Path, game: str, kind: str, succeeded: List[Dict[str, Any]],
                    override: Optional[Path], replace_semantic_ids: Optional[set[str]] = None) -> Path:
    path = _inside(str(override or Path("context/games/%s/资源清单.yaml" % game)), root, "asset manifest")
    if path.exists():
        if path.suffix.lower() == ".json": data = json.loads(path.read_text(encoding="utf-8")); fmt = "json"
        else:
            import yaml
            data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}; fmt = "yaml"
    else: data, fmt = {}, ("json" if path.suffix.lower() == ".json" else "yaml")
    if not isinstance(data, dict): raise RecordError("asset manifest root must be an object")
    records = data.get("created_assets", []) or []
    if not isinstance(records, list) or any(not isinstance(item, dict) for item in records):
        raise RecordError("created_assets manifest section must be an array")
    merged = [dict(item) for item in records]
    for item in succeeded:
        artifacts = [{"role": artifact["role"], "actual_path": artifact["actual_path"],
                      "sha256": artifact["sha256"], "primary": artifact["role"] == "primary"}
                     for artifact in item["artifacts"]]
        primary = next(artifact for artifact in artifacts if artifact["primary"])
        record = {"semantic_id": item["semantic_id"], "actual_path": primary["actual_path"],
                  "type": kind, "purpose": item["semantic_id"], "task_id": item["task_id"],
                  "artifacts": artifacts}
        matches = [old for old in merged if old.get("semantic_id") == record["semantic_id"]]
        if matches and any(_canonical(old) != _canonical(record) for old in matches):
            if record["semantic_id"] not in (replace_semantic_ids or set()):
                raise RecordError("created_assets conflict: %s" % record["semantic_id"])
            merged = [record if old.get("semantic_id") == record["semantic_id"] else old for old in merged]
        artifact_paths = {artifact["actual_path"] for artifact in artifacts}
        if any(old.get("semantic_id") != record["semantic_id"] and
               (old.get("actual_path") in artifact_paths or artifact_paths &
                {a.get("actual_path") for a in old.get("artifacts", []) if isinstance(a, dict)}) for old in merged):
            raise RecordError("created_assets artifact path conflict: %s" % record["semantic_id"])
        if not matches: merged.append(record)
    data["created_assets"] = merged
    if fmt == "json": rendered = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    else:
        import yaml
        rendered = yaml.safe_dump(data, allow_unicode=True, sort_keys=False)
    if not path.exists() or path.read_text(encoding="utf-8") != rendered: _atomic_write(path, rendered)
    return path


def record_result(payload: Dict[str, Any], project_root: Path, manifest_path: Optional[Path] = None,
                  replace_succeeded: bool = False) -> Dict[str, Any]:
    root = project_root.expanduser().resolve(strict=True)
    if not isinstance(payload, dict): raise RecordError("input must be an object")
    game = _identifier(payload.get("game"), "game"); logical = _identifier(payload.get("logical_id"), "logical_id")
    kind = payload.get("kind")
    if not isinstance(kind, str) or not kind.strip(): raise RecordError("kind required")
    expected = _expected(payload); item = _terminal(payload, expected, root)
    receipt_path = root / "context" / "games" / game / "generation" / (logical + ".source.json")
    if receipt_path.exists():
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        receipt_fields = {"schema", "producer", "game", "logical_id", "kind", "checkpoint_mode",
                          "expected_items", "items", "complete"}
        if set(receipt) not in (receipt_fields, receipt_fields | {"revisions"}) \
                or receipt.get("schema") != SOURCE_SCHEMA \
                or receipt.get("producer") != RECORDER_ID or receipt.get("checkpoint_mode") != "per_item":
            raise RecordError("source receipt conflict")
        if receipt.get("game") != game or receipt.get("logical_id") != logical or receipt.get("kind") != kind or receipt.get("expected_items") != expected:
            raise RecordError("source receipt identity conflict")
    else:
        receipt = {"schema": SOURCE_SCHEMA, "producer": RECORDER_ID, "game": game, "logical_id": logical,
                   "kind": kind, "checkpoint_mode": "per_item", "expected_items": expected, "items": [],
                   "revisions": [], "complete": False}
    if "revisions" not in receipt:
        receipt["revisions"] = []
    if not isinstance(receipt.get("revisions"), list):
        raise RecordError("source receipt revisions conflict")
    by_ordinal = {old["ordinal"]: old for old in receipt.get("items", []) if isinstance(old, dict) and isinstance(old.get("ordinal"), int)}
    old = by_ordinal.get(item["ordinal"])
    replaced_semantic_ids: set[str] = set()
    if old is not None:
        if old.get("status") == "succeeded" and _canonical(old) != _canonical(item):
            if not replace_succeeded:
                raise RecordError("succeeded item fact conflict")
            if item.get("status") != "succeeded":
                raise RecordError("succeeded replacement must remain succeeded")
            if old.get("task_id") == item.get("task_id") or old.get("backend_task_id") == item.get("backend_task_id"):
                raise RecordError("succeeded replacement requires new task identities")
            if _canonical(old.get("artifacts")) == _canonical(item.get("artifacts")):
                raise RecordError("succeeded replacement artifacts must change")
            receipt["revisions"].append({
                "ordinal": item["ordinal"],
                "replaced_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "item": old,
            })
            replaced_semantic_ids.add(item["semantic_id"])
        elif old.get("status") == "succeeded":
            item = old
        elif old.get("semantic_id") != item["semantic_id"]: raise RecordError("item identity conflict")
    by_ordinal[item["ordinal"]] = item
    receipt["items"] = [by_ordinal[index] for index in sorted(by_ordinal)]
    receipt["complete"] = (len(receipt["items"]) == len(expected) and all(value.get("status") == "succeeded" for value in receipt["items"]))
    _atomic_write(receipt_path, json.dumps(receipt, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
    manifest = _merge_manifest(root, game, kind, receipt["items"] if receipt["complete"] else [],
                               manifest_path, replaced_semantic_ids)
    return {"ok": True, "receipt_path": str(receipt_path), "source_hash": _sha256(receipt_path),
            "manifest_path": str(manifest), "complete": receipt["complete"], "item_status": item["status"]}


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(allow_abbrev=False); parser.add_argument("--project-root", default=".")
    parser.add_argument("--input"); parser.add_argument("--manifest")
    parser.add_argument("--replace-succeeded", action="store_true")
    args = parser.parse_args(argv)
    try:
        text = Path(args.input).read_text(encoding="utf-8") if args.input else sys.stdin.read()
        result = record_result(json.loads(text), Path(args.project_root),
                               Path(args.manifest) if args.manifest else None,
                               replace_succeeded=args.replace_succeeded)
    except (OSError, UnicodeError, json.JSONDecodeError, RecordError) as exc:
        result = {"ok": False, "error": str(exc)}
    print(json.dumps(result, ensure_ascii=False, sort_keys=True)); return 0 if result["ok"] else 1


if __name__ == "__main__": raise SystemExit(main())
