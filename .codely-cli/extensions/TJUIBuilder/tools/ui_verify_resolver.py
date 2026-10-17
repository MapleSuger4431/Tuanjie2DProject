#!/usr/bin/env python3
"""Resolve UI verify parameters from the compiler-owned UI panel contract."""
from __future__ import annotations

import hashlib
import json
import re
import sys
import yaml
from pathlib import Path, PurePosixPath
from typing import Any, Dict, Mapping, Optional

SCHEMA = "codely.ui-verify-invocation/v1"
_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
INPUT_FIELDS = {"compiled_plan", "logical_job_id", "actual_job_id", "source_plan_hash"}
PROJECT_MARKERS = ("Assets", "Packages",
                   ".codely-cli/extensions/TJUIBuilder/tools/ui_verify_resolver.py")
CONTRACT_FIELDS_TK = {"expected_panels", "ui_root", "panel_settings", "panel_registry",
                     "images_directory", "uxml", "uss", "image_requirements"}
CONTRACT_FIELDS_UGUI = {"expected_panels", "ui_root", "panel_registry",
                        "images_directory", "panel_prefabs", "image_requirements"}


class ResolverError(ValueError):
    pass


def _project_root() -> Path:
    root = Path(__file__).resolve().parents[4]
    if not root.is_dir() or any(not (root / marker).exists() for marker in PROJECT_MARKERS):
        raise ResolverError("UI_VERIFY_PROJECT_ROOT_REQUIRED")
    return root


def _safe(root: Path, value: Any, code: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise ResolverError(code)
    candidate = Path(value)
    if not candidate.is_absolute():
        candidate = root / candidate
    try:
        resolved = candidate.resolve()
        resolved.relative_to(root.resolve())
    except (OSError, ValueError) as exc:
        raise ResolverError(code) from exc
    return resolved


def _relative_path(root: Path, value: Any, code: str, suffix: str,
                   prefix: Optional[str] = None) -> str:
    if not isinstance(value, str) or not value or value != value.strip() or "\\" in value:
        raise ResolverError(code)
    pure = PurePosixPath(value)
    if pure.is_absolute() or value != pure.as_posix() or any(part in ("", ".", "..") for part in pure.parts):
        raise ResolverError(code)
    try:
        (root / value).resolve().relative_to(root.resolve())
    except (OSError, ValueError) as exc:
        raise ResolverError(code) from exc
    if suffix and pure.suffix.lower() != suffix:
        raise ResolverError(code)
    if prefix is not None and (not pure.parts or pure.parts[0] != prefix):
        raise ResolverError(code)
    return value


def _path_list(root: Path, value: Any, code: str, suffix: str,
               unique: bool = True) -> list[str]:
    if not isinstance(value, list) or not value:
        raise ResolverError(code)
    result = [_relative_path(root, item, code, suffix, "Assets") for item in value]
    if unique and len(set(result)) != len(result):
        raise ResolverError(code)
    return result


def _load(path: Path, code: str) -> Mapping[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ResolverError(code) from exc
    if not isinstance(value, Mapping):
        raise ResolverError(code)
    return value


def _verify_source_hash(root: Path, compiled: Mapping[str, Any]) -> None:
    source_path = _safe(root, compiled.get("source_plan"), "UI_VERIFY_SOURCE_PLAN")
    if source_path.name != "job-plan.yaml":
        raise ResolverError("UI_VERIFY_SOURCE_PLAN")
    tools = root / ".codely-cli/extensions/TJUIBuilder/tools"
    if str(tools) not in sys.path:
        sys.path.insert(0, str(tools))
    from job_plan_yaml import normalize_plain_text_fields
    try:
        current_source = yaml.safe_load(normalize_plain_text_fields(source_path.read_text(encoding="utf-8")))
        current_hash = hashlib.sha256(json.dumps(
            current_source, ensure_ascii=False, sort_keys=True,
            separators=(",", ":")).encode("utf-8")).hexdigest()
    except (OSError, UnicodeError, ValueError, TypeError, yaml.YAMLError) as exc:
        raise ResolverError("UI_VERIFY_SOURCE_PLAN") from exc
    if current_hash != compiled.get("source_plan_hash"):
        raise ResolverError("UI_VERIFY_SOURCE_HASH_STALE")


def _read_ui_builder_state(root: Path, game: str) -> Optional[Mapping[str, Any]]:
    state_path = root / f"context/games/{game}/ui-panel-builder/ui-panel-builder-state.json"
    if not state_path.is_file():
        return None
    return _load(state_path, "UI_VERIFY_BUILDER_STATE_INVALID")


def _validate_contract_artifacts(root: Path, game: str,
                                 contract: Mapping[str, Any]) -> None:
    """Re-derive the simple UI artifact evidence when legacy builder state is absent."""
    artifacts = contract["artifacts"]
    if "uxml" in artifacts:
        # UI Toolkit mode: verify UXML/USS files and context↔Assets sync
        for key in ("uxml", "uss"):
            for relative in artifacts[key]:
                asset = root / relative
                if not asset.is_file():
                    raise ResolverError("UI_VERIFY_CONTRACT_ARTIFACT_MISSING")
                try:
                    tail = asset.resolve().relative_to(
                        (root / f"Assets/Games/{game}/UI").resolve())
                except ValueError as exc:
                    raise ResolverError("UI_VERIFY_CONTRACT_ARTIFACT_INVALID") from exc
                context = root / f"context/games/{game}/UI" / tail
                if not context.is_file():
                    raise ResolverError("UI_VERIFY_CONTRACT_CONTEXT_MISSING")
                if context.read_bytes() != asset.read_bytes():
                    raise ResolverError("UI_VERIFY_CONTRACT_CONTEXT_ASSETS_DRIFT")
        for key in ("ui_root", "panel_settings", "panel_registry"):
            if not (root / artifacts[key]).is_file():
                raise ResolverError("UI_VERIFY_CONTRACT_INTEGRATION_ARTIFACT_MISSING")
    else:
        # UGUI mode: verify panel prefabs and integration artifacts (no UXML/USS/panel_settings)
        for relative in artifacts.get("panel_prefabs", []):
            if not (root / relative).is_file():
                raise ResolverError("UI_VERIFY_CONTRACT_ARTIFACT_MISSING")
        for key in ("ui_root", "panel_registry"):
            if not (root / artifacts[key]).is_file():
                raise ResolverError("UI_VERIFY_CONTRACT_INTEGRATION_ARTIFACT_MISSING")
    if not (root / contract["image_requirements"]).is_file():
        raise ResolverError("UI_VERIFY_IMAGE_REQUIREMENTS_MISSING")
    images = root / artifacts["images_directory"]
    if not images.is_dir():
        raise ResolverError("UI_VERIFY_IMAGES_DIRECTORY_MISSING")


def _upstream_ui_job(compiled: Mapping[str, Any], target: Mapping[str, Any]) -> Mapping[str, Any]:
    jobs = compiled.get("jobs")
    blocked = target.get("blocked_by")
    if not isinstance(jobs, list) or not isinstance(blocked, list) or not blocked:
        raise ResolverError("UI_VERIFY_UI_UPSTREAM_MISSING")
    if any(not isinstance(item, str) or not item for item in blocked) or len(set(blocked)) != len(blocked):
        raise ResolverError("UI_VERIFY_BLOCKED_BY_INVALID")
    by_id: dict[str, Mapping[str, Any]] = {}
    for job in jobs:
        if not isinstance(job, Mapping):
            raise ResolverError("UI_VERIFY_COMPILED_INVALID")
        job_id = job.get("logical_id")
        if not isinstance(job_id, str) or job_id in by_id:
            raise ResolverError("UI_VERIFY_COMPILED_INVALID")
        by_id[job_id] = job
    if any(job_id not in by_id for job_id in blocked):
        raise ResolverError("UI_VERIFY_BLOCKED_DEPENDENCY_UNKNOWN")
    ui_jobs = [by_id[job_id] for job_id in blocked if by_id[job_id].get("kind") == "ui"]
    if not ui_jobs:
        raise ResolverError("UI_VERIFY_UI_UPSTREAM_MISSING")
    if len(ui_jobs) != 1:
        raise ResolverError("UI_VERIFY_UI_UPSTREAM_AMBIGUOUS")
    return ui_jobs[0]


def _compile_contract(root: Path, ui_job: Mapping[str, Any]) -> Dict[str, Any]:
    contract = ui_job.get("ui_panel_contract")
    if not isinstance(contract, Mapping):
        raise ResolverError("UI_VERIFY_PANEL_CONTRACT_INVALID")

    # Determine UI mode from contract fields
    is_ugui = "panel_prefabs" in contract and "uxml" not in contract
    if is_ugui:
        if set(contract) != CONTRACT_FIELDS_UGUI:
            raise ResolverError("UI_VERIFY_PANEL_CONTRACT_INVALID")
    else:
        if set(contract) != CONTRACT_FIELDS_TK:
            raise ResolverError("UI_VERIFY_PANEL_CONTRACT_INVALID")
    provenance = ui_job.get("provenance")
    if isinstance(provenance, Mapping) and "ui_panel_contract" in provenance:
        duplicate = provenance.get("ui_panel_contract")
        if not isinstance(duplicate, Mapping) or dict(duplicate) != dict(contract):
            raise ResolverError("UI_VERIFY_PANEL_CONTRACT_PROVENANCE_MISMATCH")

    panels = contract.get("expected_panels")
    if (not isinstance(panels, list) or not panels
            or any(not isinstance(name, str) or _ID.fullmatch(name) is None for name in panels)
            or len(set(panels)) != len(panels)):
        raise ResolverError("UI_VERIFY_EXPECTED_PANELS_INVALID")

    images = _relative_path(root, contract.get("images_directory"),
                            "UI_VERIFY_IMAGES_DIRECTORY_INVALID", "", "Assets")

    if is_ugui:
        # UGUI mode: validate panel prefabs instead of UXML/USS
        prefabs = _path_list(root, contract.get("panel_prefabs"), "UI_VERIFY_PREFABS_INVALID", ".prefab")
        if len(prefabs) != len(panels):
            raise ResolverError("UI_VERIFY_PANEL_FILES_MISMATCH")
        ui_dirs = {PurePosixPath(path).parent.as_posix() for path in prefabs}
        if len(ui_dirs) != 1:
            raise ResolverError("UI_VERIFY_UI_DIRECTORY_AMBIGUOUS")
        ui_directory = next(iter(ui_dirs))
        if images != (PurePosixPath(ui_directory).parent / "Images").as_posix():
            raise ResolverError("UI_VERIFY_IMAGES_DIRECTORY_INVALID")
        result = {
            "ui_directory": ui_directory,
            "images_directory": images,
            "panel_prefabs": prefabs,
            "panel_registry": _relative_path(root, contract.get("panel_registry"),
                                             "UI_VERIFY_PANEL_REGISTRY_INVALID", ".asset", "Assets"),
            "ui_root": _relative_path(root, contract.get("ui_root"),
                                       "UI_VERIFY_UI_ROOT_INVALID", ".prefab", "Assets"),
        }
    else:
        # UI Toolkit mode: validate UXML/USS
        uxml = _path_list(root, contract.get("uxml"), "UI_VERIFY_UXML_INVALID", ".uxml")
        uss = _path_list(root, contract.get("uss"), "UI_VERIFY_USS_INVALID", ".uss", unique=False)
        if len(uxml) != len(uss) or len(uxml) != len(panels):
            raise ResolverError("UI_VERIFY_PANEL_FILES_MISMATCH")
        if any(PurePosixPath(uxml_path).stem != PurePosixPath(uss_path).stem
               for uxml_path, uss_path in zip(uxml, uss)):
            raise ResolverError("UI_VERIFY_PANEL_STYLE_STEM_MISMATCH")
        ui_dirs = {PurePosixPath(path).parent.as_posix() for path in uxml + uss}
        if len(ui_dirs) != 1:
            raise ResolverError("UI_VERIFY_UI_DIRECTORY_AMBIGUOUS")
        ui_directory = next(iter(ui_dirs))
        if len(set(zip(panels, uxml, uss))) != len(panels):
            raise ResolverError("UI_VERIFY_PANEL_FILES_MISMATCH")
        if images != (PurePosixPath(ui_directory) / "Images").as_posix():
            raise ResolverError("UI_VERIFY_IMAGES_DIRECTORY_INVALID")
        result = {
            "ui_directory": ui_directory,
            "images_directory": images,
            "uxml": uxml,
            "uss": uss,
            "panel_settings": _relative_path(root, contract.get("panel_settings"),
                                              "UI_VERIFY_PANEL_SETTINGS_INVALID", ".asset", "Assets"),
            "panel_registry": _relative_path(root, contract.get("panel_registry"),
                                              "UI_VERIFY_PANEL_REGISTRY_INVALID", ".asset", "Assets"),
            "ui_root": _relative_path(root, contract.get("ui_root"),
                                       "UI_VERIFY_UI_ROOT_INVALID", ".prefab", "Assets"),
        }
    image_requirements = _relative_path(root, contract.get("image_requirements"),
                                        "UI_VERIFY_IMAGE_REQUIREMENTS_INVALID", ".json", "context")
    return {"expected_panels": list(panels), "image_requirements": image_requirements,
            "artifacts": result}


def _validate_builder_state(state: Mapping[str, Any], game: str,
                            contract: Mapping[str, Any]) -> None:
    if state.get("game_name") != game:
        raise ResolverError("UI_VERIFY_BUILDER_STATE_GAME_MISMATCH")
    stages = state.get("stages")
    ui_mode = state.get("ui_mode", "ui_toolkit")
    is_ugui = ui_mode == "ugui"
    stage_one_key = "01-ugui-prefab" if is_ugui else "01-uxml-uss"
    stage_two_key = "02-ugui-csharp" if is_ugui else "02-csharp-integration"
    one = stages.get(stage_one_key) if isinstance(stages, Mapping) else None
    two = stages.get(stage_two_key) if isinstance(stages, Mapping) else None
    if (not isinstance(one, Mapping) or one.get("status") != "completed"
            or one.get("uiv_validation_passed") is not True or one.get("uiv_errors") != []):
        raise ResolverError("UI_VERIFY_BUILDER_STAGE_INVALID")
    if not isinstance(two, Mapping) or two.get("status") != "completed":
        raise ResolverError("UI_VERIFY_BUILDER_STAGE_INVALID")
    panels = state.get("panels")
    if not isinstance(panels, list) or not panels:
        raise ResolverError("UI_VERIFY_PANELS_MISSING")
    names: list[str] = []
    if is_ugui:
        prefabs: list[str] = []
        for panel in panels:
            if not isinstance(panel, Mapping):
                raise ResolverError("UI_VERIFY_PANEL_INVALID")
            if not all(isinstance(panel.get(field), str) for field in
                       ("panel_name", "prefab_assets_path")):
                raise ResolverError("UI_VERIFY_PANEL_PATHS_MISSING")
            names.append(panel["panel_name"])
            prefabs.append(panel["prefab_assets_path"])
        artifacts = contract["artifacts"]
        if names != contract["expected_panels"]:
            raise ResolverError("UI_VERIFY_BUILDER_PANELS_MISMATCH")
        if prefabs != artifacts.get("panel_prefabs", []):
            raise ResolverError("UI_VERIFY_BUILDER_PATHS_MISMATCH")
    else:
        uxml: list[str] = []
        uss: list[str] = []
        for panel in panels:
            if not isinstance(panel, Mapping):
                raise ResolverError("UI_VERIFY_PANEL_INVALID")
            if not all(isinstance(panel.get(field), str) for field in
                       ("panel_name", "uxml_assets_path", "uss_assets_path")):
                raise ResolverError("UI_VERIFY_PANEL_PATHS_MISSING")
            names.append(panel["panel_name"])
            uxml.append(panel["uxml_assets_path"])
            uss.append(panel["uss_assets_path"])
        artifacts = contract["artifacts"]
        if names != contract["expected_panels"]:
            raise ResolverError("UI_VERIFY_BUILDER_PANELS_MISMATCH")
        if uxml != artifacts["uxml"] or uss != artifacts["uss"]:
            raise ResolverError("UI_VERIFY_BUILDER_PATHS_MISMATCH")


def resolve_invocation(parameters: Mapping[str, Any]) -> Dict[str, Any]:
    if not isinstance(parameters, Mapping) or set(parameters) != INPUT_FIELDS:
        raise ResolverError("UI_VERIFY_INPUT_FIELDS")
    root = _project_root()
    compiled_path = _safe(root, parameters.get("compiled_plan"), "UI_VERIFY_COMPILED_PLAN")
    compiled = _load(compiled_path, "UI_VERIFY_COMPILED_INVALID")
    if compiled.get("compiled_schema_version") != 1:
        raise ResolverError("UI_VERIFY_COMPILED_INVALID")
    if compiled.get("source_plan_hash") != parameters.get("source_plan_hash"):
        raise ResolverError("UI_VERIFY_SOURCE_HASH_MISMATCH")
    _verify_source_hash(root, compiled)
    logical = parameters.get("logical_job_id")
    actual = parameters.get("actual_job_id")
    if not isinstance(logical, str) or not _ID.fullmatch(logical):
        raise ResolverError("UI_VERIFY_LOGICAL_JOB_ID")
    if not isinstance(actual, str) or not _ID.fullmatch(actual):
        raise ResolverError("UI_VERIFY_ACTUAL_JOB_ID")
    jobs = [j for j in compiled.get("jobs", [])
            if isinstance(j, Mapping) and j.get("logical_id") == logical]
    if len(jobs) != 1 or jobs[0].get("kind") != "ui_verify":
        raise ResolverError("UI_VERIFY_JOB_NOT_FOUND")
    game = compiled.get("game")
    if not isinstance(game, str) or not _ID.fullmatch(game):
        raise ResolverError("UI_VERIFY_GAME")
    contract = _compile_contract(root, _upstream_ui_job(compiled, jobs[0]))
    state = _read_ui_builder_state(root, game)
    if state is None:
        _validate_contract_artifacts(root, game, contract)
    else:
        _validate_builder_state(state, game, contract)
    return {
        "schema": SCHEMA, "game": game, "logical_job_id": logical,
        "actual_job_id": actual, "source_plan_hash": parameters["source_plan_hash"],
        "compiled_plan": compiled_path.relative_to(root.resolve()).as_posix(),
        "receipt_path": f"context/games/{game}/evidence/job-{logical}.ui-gate.json",
        "raw_dir": f"context/games/{game}/evidence/raw/{logical}",
        "image_requirements": contract["image_requirements"],
        "expected_panels": contract["expected_panels"],
        "artifacts": contract["artifacts"], "project": str(root),
    }


def main(argv: Optional[list] = None) -> int:
    import argparse
    parser = argparse.ArgumentParser(allow_abbrev=False)
    parser.add_argument("parameters_json", help="JSON object with the 4 standard fields")
    args = parser.parse_args(argv)
    try:
        params = json.loads(args.parameters_json)
        invocation = resolve_invocation(params)
    except (OSError, UnicodeError, json.JSONDecodeError, ResolverError) as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False, sort_keys=True))
        return 1
    print(json.dumps({"ok": True, "invocation": invocation}, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
