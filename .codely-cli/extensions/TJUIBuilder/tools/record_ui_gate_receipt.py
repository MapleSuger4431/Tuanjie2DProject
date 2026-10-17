#!/usr/bin/env python3
"""Produce the deterministic, compiler-owned UI verification receipt."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import sys
import tempfile
from pathlib import Path
from typing import Any, Dict, List, Mapping, Optional

import ui_image_contract

SCHEMA = "codely.ui-verify-receipt/v4"
PRODUCER_SCHEMA = "codely.ui-verify-receipt-producer/v4"
PRODUCER_NAME = "record_ui_gate_receipt.py"
_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


class ProduceError(ValueError):
    pass


def _project_path(value: str, root: Path, label: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise ProduceError("%s must be a non-empty path" % label)
    candidate = Path(value).expanduser()
    if not candidate.is_absolute():
        candidate = root / candidate
    candidate = candidate.resolve(strict=False)
    try:
        candidate.relative_to(root)
    except ValueError as exc:
        raise ProduceError("%s is outside project: %s" % (label, value)) from exc
    if not candidate.is_file():
        raise ProduceError("%s is not a file: %s" % (label, candidate))
    return candidate


def _load_object(path: Path, label: str) -> Dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ProduceError("%s: %s" % (label, exc)) from exc
    if not isinstance(value, dict):
        raise ProduceError("%s root must be an object" % label)
    return value


def _load_validator() -> Any:
    path = Path(__file__).with_name("validate_ui_gate_receipt.py")
    spec = importlib.util.spec_from_file_location("_ui_receipt_validator", path)
    if spec is None or spec.loader is None:
        raise ProduceError("validate_ui_gate_receipt.py is unavailable")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def receipt_relative_path(game: str, logical_id: str) -> Path:
    if not isinstance(game, str) or _ID.fullmatch(game) is None:
        raise ProduceError("unsafe or missing game")
    if not isinstance(logical_id, str) or _ID.fullmatch(logical_id) is None:
        raise ProduceError("unsafe or missing logical_id")
    return Path("context/games") / game / "evidence" / ("job-%s.ui-gate.json" % logical_id)


def _input_record(path: Path, root: Path) -> Dict[str, str]:
    return {
        "path": path.relative_to(root).as_posix(),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def build_receipt(game: str, logical_id: str, project_root: Path,
                  artifacts_input: Path, validate_images_input: Path,
                  static_ui_input: Path, integration_probe_input: Path) -> Dict[str, Any]:
    root = project_root.expanduser().resolve(strict=False)
    if not root.is_dir():
        raise ProduceError("project root is not a directory: %s" % root)
    paths = {
        "artifacts": _project_path(str(artifacts_input), root, "artifacts"),
        "validate_images": _project_path(str(validate_images_input), root, "validate_images"),
        "static_ui": _project_path(str(static_ui_input), root, "static_ui"),
        "integration_probe": _project_path(str(integration_probe_input), root,
                                            "integration_probe"),
    }
    manifest = _load_object(paths["artifacts"], "artifacts")
    if set(manifest) != {"image_requirements", "expected_panels", "artifacts"}:
        raise ProduceError("artifacts must contain only image_requirements, expected_panels and artifacts")
    if not isinstance(manifest["artifacts"], dict):
        raise ProduceError("artifacts.artifacts must be an object")
    requirements_path = _project_path(manifest["image_requirements"], root,
                                      "image_requirements")
    try:
        ui_image_contract.load_requirements(requirements_path)
    except ui_image_contract.UiImageContractError as exc:
        raise ProduceError(str(exc)) from exc
    integration = _load_object(paths["integration_probe"], "integration_probe")
    forbidden = {"passed", "status", "ok"} & set(integration)
    if forbidden:
        raise ProduceError("integration_probe contains derived fields: %s" %
                           ", ".join(sorted(forbidden)))
    image_raw = paths["validate_images"].read_text(encoding="utf-8")
    static_ui = _load_object(paths["static_ui"], "static_ui")
    validator = _load_validator()
    artifacts = manifest["artifacts"]
    expected_panels = manifest["expected_panels"]
    integration_check = dict(integration)
    integration_check["status"] = validator.derive_integration_status(
        integration, artifacts, expected_panels)
    relative_receipt = receipt_relative_path(game, logical_id).as_posix()
    return {
        "schema": SCHEMA,
        "game": game,
        "logical_id": logical_id,
        "producer": {
            "schema": PRODUCER_SCHEMA,
            "name": PRODUCER_NAME,
            "receipt_path": relative_receipt,
            "inputs": {name: _input_record(path, root) for name, path in paths.items()},
            "ui_documents_sha256": validator.ui_documents_sha256(artifacts, root),
            "integration_artifacts_sha256": validator.integration_artifacts_sha256(
                artifacts, root),
        },
        "image_requirements": manifest["image_requirements"],
        "expected_panels": expected_panels,
        "artifacts": artifacts,
        "checks": {
            "validate_images": {
                "status": validator.derive_validate_images_status(image_raw),
                "raw_output": image_raw,
            },
            "static_ui": {
                "status": validator.derive_static_ui_status(static_ui),
                "sha256": hashlib.sha256(paths["static_ui"].read_bytes()).hexdigest(),
                "structured": static_ui,
            },
            "integration": integration_check,
        },
    }


def _atomic_write(path: Path, value: Mapping[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary: Optional[Path] = None
    try:
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent,
                                         delete=False) as stream:
            json.dump(value, stream, ensure_ascii=False, sort_keys=True, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
            temporary = Path(stream.name)
        os.replace(str(temporary), str(path))
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def produce(game: str, logical_id: str, project_root: Path,
            artifacts_input: Path, validate_images_input: Path,
            static_ui_input: Path, integration_probe_input: Path) -> Path:
    root = project_root.expanduser().resolve(strict=False)
    output = (root / receipt_relative_path(game, logical_id)).resolve(strict=False)
    try:
        output.relative_to(root)
    except ValueError as exc:
        raise ProduceError("compiler-owned receipt path escapes project: %s" % output) from exc
    receipt = build_receipt(game, logical_id, root, artifacts_input,
                            validate_images, static_ui_input, integration_probe_input)
    validator = _load_validator()
    result = validator.validate_ui(receipt, root, output)
    if not result.get("ok"):
        raise ProduceError("UI receipt self-validation failed: %s" %
                           json.dumps(result.get("errors", []), ensure_ascii=False,
                                      sort_keys=True))
    _atomic_write(output, receipt)
    return output


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(allow_abbrev=False)
    parser.add_argument("--game", required=True)
    parser.add_argument("--logical-id", required=True)
    parser.add_argument("--artifacts", required=True)
    parser.add_argument("--validate-images", required=True)
    parser.add_argument("--static-ui", required=True,
                        help="JSON file produced from JsonUtility.ToJson(UIV.ValidateStructured(...))")
    parser.add_argument("--integration-probe", required=True)
    parser.add_argument("--project-root", default=".")
    args = parser.parse_args(argv)
    try:
        output = produce(args.game, args.logical_id, Path(args.project_root),
                         Path(args.artifacts), Path(args.validate_images), Path(args.static_ui),
                         Path(args.integration_probe))
    except (OSError, UnicodeError, ProduceError) as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False,
                         sort_keys=True))
        return 1
    print(json.dumps({"ok": True, "receipt": output.as_posix()},
                     ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
