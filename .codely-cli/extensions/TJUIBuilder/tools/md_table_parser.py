"""Strict shared Markdown-table IR for planning validators and compiler."""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

_POLICY_PATH = Path(__file__).parent / "asset_generation_policy.json"
ALIAS_SCHEMA_VERSION = 1
TABLE_KIND_UNKNOWN = "unknown"


class ParseDiagnostic(Exception):
    """Expected structured failure; CLI callers must serialize ``to_dict``."""

    def __init__(self, code: str, detail: str = "", *, file: str = "",
                 line: Optional[int] = None, field: str = "", raw: str = "",
                 canonical: str = "", kind: str = "") -> None:
        super().__init__(f"{code}:{detail}" if detail else code)
        self.code, self.detail, self.file = code, detail, file
        self.line, self.field, self.raw = line, field, raw
        self.canonical, self.kind = canonical, kind

    def to_dict(self) -> Dict[str, str]:
        result = {"code": self.code}
        for key in ("detail", "file", "field", "raw", "canonical", "kind"):
            value = getattr(self, key)
            if value:
                result[key] = value
        if self.line is not None:
            result["line"] = str(self.line)
        return result


def _load_policy() -> dict:
    with open(_POLICY_PATH, encoding="utf-8") as stream:
        return json.load(stream)


def check_alias_collisions(policy: Optional[dict] = None) -> List[str]:
    """Validate current-only entry-list aliases, including JSON-expressible collisions."""
    value = policy if policy is not None else _POLICY
    aliases = value.get("aliases", {})
    if not isinstance(aliases, dict):
        return ["aliases must be an object"]
    entries = aliases.get("entries", [])
    if not isinstance(entries, list):
        return ["aliases.entries must be a list"]
    seen: Dict[Tuple[str, str], set] = {}
    errors: List[str] = []
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict) or set(entry) != {"field", "alias", "canonical"}:
            errors.append(f"entry[{index}] must contain exactly field,alias,canonical")
            continue
        field_name = str(entry["field"]).casefold()
        alias = str(entry["alias"]).casefold()
        canonical = str(entry["canonical"])
        seen.setdefault((field_name, alias), set()).add(canonical)
    for (field_name, alias), canonicals in sorted(seen.items()):
        if len(canonicals) > 1:
            errors.append(f"{field_name}: alias {alias!r} maps to {sorted(canonicals)!r}")
    return errors


def _validate_alias_policy(policy: dict) -> None:
    aliases = policy.get("aliases")
    if not isinstance(aliases, dict):
        raise ParseDiagnostic("ALIAS_POLICY_SHAPE", "aliases must be an object")
    if aliases.get("schema_version") != ALIAS_SCHEMA_VERSION:
        raise ParseDiagnostic("ALIAS_POLICY_SCHEMA", f"expected schema_version={ALIAS_SCHEMA_VERSION}")
    if aliases.get("scope") != "current_only":
        raise ParseDiagnostic("ALIAS_POLICY_SCOPE", "scope must be current_only")
    collisions = check_alias_collisions(policy)
    if collisions:
        raise ParseDiagnostic("ALIAS_POLICY_COLLISION", "; ".join(collisions))


_POLICY = _load_policy()
_validate_alias_policy(_POLICY)
_FULLWIDTH_MAP = str.maketrans({
    "，": ",", "：": ":", "；": ";", "！": "!",
    "（": "(", "）": ")", "【": "[", "】": "]",
})
_ENUM_FIELDS = {
    "usage_mode": {str(v).casefold(): str(v) for v in _POLICY.get("usage_modes", [])},
    "alpha_mode": {str(v).casefold(): str(v) for v in _POLICY.get("alpha_modes", [])},
    "tile_mode": {str(v).casefold(): str(v) for v in _POLICY.get("tile_modes", [])},
    "vfx_texture_type": {str(v).casefold(): str(v) for v in _POLICY.get("vfx_texture_types", {})},
}
_ALIAS_INDEX: Dict[Tuple[str, str], str] = {
    (str(e["field"]).casefold(), str(e["alias"]).casefold()): str(e["canonical"])
    for e in _POLICY["aliases"]["entries"]
}

_SORTING_POLICY = _POLICY.get("sorting_layer_policy", {})
ROLE_LAYERS = {str(role).casefold(): str(layer)
               for role, layer in _SORTING_POLICY.get("role_layers", {}).items()}
CRITICAL_RENDER_ROLES = set(ROLE_LAYERS)
NEUTRAL_LAYERS = {str(value).casefold()
                  for value in _SORTING_POLICY.get("neutral_layers", [])}

_HEADER_ALIASES = {
    "资产名": "asset_id", "name": "asset_id", "id": "asset_id",
    "动画id": "animation_id",
    "frame size mode": "frame_size_mode", "expected frame px": "expected_frame_px",
    "子sprite名称（有序）": "sprite_names", "单帧目标世界尺寸": "target_frame_world_size",
    "clip路径": "clip_path", "fps/loop": "fps_loop",
    "prefab对象路径→初始sprite": "prefab_initial_sprite",
    "controller路径→字面参数": "controller_parameters",
    "prefab(资产名)": "prefab", "prefab（资产名）": "prefab",
    "组件": "components", "视觉角色": "render_role", "role": "render_role",
    "layer": "sorting_layer", "sortinglayername": "sorting_layer",
    "sortingorder": "sorting_order", "子类": "subclass", "类别": "category",
    "纹理类型": "vfx_texture_type", "生成_prompt": "prompt", "生成prompt": "prompt",
    "资产规范名（逗号分隔）": "asset_names", "用途": "purpose",
    "vfx名称": "legacy_vfx_name", "vfx 名称": "legacy_vfx_name",
    "引用贴图": "legacy_vfx_texture", "引用贴图(资产名)": "legacy_vfx_texture",
    "引用贴图（资产名）": "legacy_vfx_texture",
    "wrapmode": "wrap_mode", "texturesheetanimation": "texture_sheet_animation",
    "期望世界尺寸": "expected_world_size", "期望世界尺寸（宽×高）": "expected_world_size",
    "世界尺寸允许误差": "world_size_tolerance",
    "世界尺寸允许误差（宽×高，绝对世界单位）": "world_size_tolerance",
    "误差": "world_size_tolerance",
    "generator_kind": "generator_kind", "generator kind": "generator_kind",
    "最终产物合同(json)": "final_artifacts", "最终产物合同（json）": "final_artifacts",
    "final artifacts": "final_artifacts",
}


def _header_key(value: str) -> str:
    return re.sub(r"[\s_-]+", "", value.casefold())


_HEADER_INDEX = {
    _header_key(alias.translate(_FULLWIDTH_MAP)): canonical
    for alias, canonical in _HEADER_ALIASES.items()
}

# Complete minimum signatures. Classification never uses a partial subset chosen
# by a consumer; malformed managed tables fail before entering the IR.
_KIND_SIGNATURES = (
    ("finalize_spec", frozenset({"asset_id", "generator_kind", "final_artifacts"})),
    ("asset_spec", frozenset({"asset_id", "usage_mode", "alpha_mode", "tile_mode", "prompt"})),
    ("sprite_sheet_spec", frozenset({
        "animation_id", "asset_id", "rows", "columns", "frame_size_mode",
        "expected_frame_px", "alignment", "sprite_names", "target_frame_world_size",
        "clip_path", "fps_loop", "prefab_initial_sprite", "controller_parameters",
    })),
    ("vfx_spec", frozenset({"effect_id", "texture_asset_id", "vfx_texture_type"})),
    ("vfx_catalog", frozenset({"vfx_texture_type", "purpose", "wrap_mode", "texture_sheet_animation"})),
    ("audio_spec", frozenset({"asset_id", "subclass"})),
    ("prefab_spec", frozenset({"prefab", "components"})),
    ("summary", frozenset({"category", "asset_names"})),
)
_MANAGED_COLUMNS = set().union(*(set(signature) for _, signature in _KIND_SIGNATURES)) | {
    "effect_id", "texture_asset_id", "legacy_vfx_name", "legacy_vfx_texture"
}
_DEFINITION_FIELD = {
    "asset_spec": "asset_id", "audio_spec": "asset_id", "prefab_spec": "prefab",
    "vfx_spec": "effect_id", "finalize_spec": "asset_id",
    "sprite_sheet_spec": "animation_id",
}
_REFERENCE_FIELDS = {
    "vfx_spec": frozenset({"texture_asset_id"}),
    "sprite_sheet_spec": frozenset({"asset_id"}),
}


@dataclass
class ParsedField:
    raw_value: str
    canonical_value: str
    name: str = ""
    file: str = ""
    line: int = 0
    kind: str = TABLE_KIND_UNKNOWN
    role: str = "value"


@dataclass
class ParsedRow:
    row_id: str
    fields: Dict[str, ParsedField]
    file: str = ""
    line: int = 0
    kind: str = TABLE_KIND_UNKNOWN
    definition_id: str = ""
    references: Dict[str, List[str]] = field(default_factory=dict)

    def raw(self, name: str, default: str = "") -> str:
        value = self.fields.get(name)
        return value.raw_value if value else default

    def value(self, name: str, default: str = "") -> str:
        value = self.fields.get(name)
        return value.canonical_value if value else default

    def has(self, name: str) -> bool:
        return name in self.fields


@dataclass
class ParsedTable:
    kind: str
    heading: str
    headers: List[str]
    raw_headers: List[str]
    file: str
    line: int
    rows: List[ParsedRow] = field(default_factory=list)

    def has_columns(self, *names: str) -> bool:
        return all(name in self.headers for name in names)


@dataclass
class ParsedDocument:
    file: str
    tables: List[ParsedTable]

    def tables_of(self, kind: str) -> List[ParsedTable]:
        return [table for table in self.tables if table.kind == kind]

    def rows_of(self, kind: str):
        for table in self.tables_of(kind):
            for row in table.rows:
                yield table, row

    def rows_with(self, *names: str):
        """Compatibility query; contract consumers must prefer rows_of(kind)."""
        for table in self.tables:
            if table.has_columns(*names):
                for row in table.rows:
                    yield table, row

    def find_definition(self, identity: str, kind: Optional[str] = None):
        for table in self.tables:
            if kind is not None and table.kind != kind:
                continue
            for row in table.rows:
                if row.definition_id == identity:
                    return table, row
        return None, None

    def find_row(self, asset_id: str):
        return self.find_definition(asset_id, "asset_spec")


def canonicalize(value: str, field_type: str = "") -> str:
    result = value.strip().strip("`").strip().translate(_FULLWIDTH_MAP)
    field_name = field_type.casefold()
    enum = _ENUM_FIELDS.get(field_name)
    if enum and result.casefold() in enum:
        return enum[result.casefold()]
    return _ALIAS_INDEX.get((field_name, result.casefold()), result)


def _canonical_header(header: str) -> str:
    raw = canonicalize(header)
    return _HEADER_INDEX.get(
        _header_key(raw), raw.casefold().replace(" ", "_").replace("-", "_"))


def find_column(headers: List[str], *names: str) -> Optional[int]:
    values = [_canonical_header(header) for header in headers]
    for name in names:
        canonical = _canonical_header(name)
        if canonical in values:
            return values.index(canonical)
    return None


def _classify(headers: List[str]) -> str:
    header_set = set(headers)
    matches = [kind for kind, signature in _KIND_SIGNATURES if signature.issubset(header_set)]
    return matches[0] if len(matches) == 1 else TABLE_KIND_UNKNOWN


def _parse_cells(line: str) -> List[str]:
    text = line.strip().removeprefix("|").removesuffix("|")
    return [cell.strip() for cell in text.split("|")]


def _is_separator(line: str) -> bool:
    return bool(re.match(r"^\|?[\s\-:]+(\|[\s\-:]+)*\|?$", line.strip()))


def _template(cells: List[str]) -> bool:
    return any("{" in cell for cell in cells)


def _absolute_file(file: str) -> str:
    return str(Path(file).expanduser().resolve()) if file else ""


def parse_document(text: str, file: str = "") -> ParsedDocument:
    file = _absolute_file(file)
    lines, tables, current_heading = text.splitlines(), [], ""
    definitions: Dict[str, Tuple[str, int, str]] = {}
    index = 0
    while index < len(lines):
        heading = re.match(r"^#{1,6}\s+(.*)", lines[index])
        if heading:
            current_heading = heading.group(1).strip()
            index += 1
            continue
        if "|" not in lines[index] or index + 1 >= len(lines) or not _is_separator(lines[index + 1]):
            index += 1
            continue
        table_line = index + 1
        raw_headers = _parse_cells(lines[index])
        headers = [_canonical_header(header) for header in raw_headers]
        if len(headers) != len(set(headers)):
            raise ParseDiagnostic("TABLE_DUPLICATE_COLUMN", file=file, line=table_line)
        kind = _classify(headers)
        managed = sorted(set(headers) & _MANAGED_COLUMNS)
        if kind == TABLE_KIND_UNKNOWN and managed:
            legacy = "legacy_vfx_name" in headers or "legacy_vfx_texture" in headers
            code = "LEGACY_VFX_TABLE" if legacy else ("VFX_SCHEMA" if {"effect_id", "texture_asset_id"} & set(headers) else "TABLE_SCHEMA")
            raise ParseDiagnostic(code, f"headers={','.join(headers)}", file=file,
                                  line=table_line, kind=kind)
        definition_field = _DEFINITION_FIELD.get(kind)
        reference_fields = _REFERENCE_FIELDS.get(kind, frozenset())
        index += 2
        rows: List[ParsedRow] = []
        while index < len(lines) and "|" in lines[index]:
            row_line = index + 1
            cells = (_parse_cells(lines[index]) + [""] * len(headers))[:len(headers)]
            is_template = _template(cells)
            fields: Dict[str, ParsedField] = {}
            references: Dict[str, List[str]] = {}
            for column, name in enumerate(headers):
                raw = cells[column]
                canonical = canonicalize(raw, name)
                role = "definition" if name == definition_field else ("reference" if name in reference_fields else "value")
                fields[name] = ParsedField(raw, canonical, name, file, row_line, kind, role)
                if role == "reference" and not is_template:
                    references[name] = [part.strip() for part in re.split(r"[,，]", canonical) if part.strip()]
            definition_id = fields[definition_field].canonical_value if definition_field else ""
            if is_template:
                definition_id = ""
            if definition_id:
                previous = definitions.get(definition_id.casefold())
                if previous:
                    raise ParseDiagnostic("DUPLICATE_DEFINITION",
                                          f"{definition_id} (already defined at {previous[0]}:{previous[1]})",
                                          file=file, line=row_line, field=definition_field,
                                          raw=definition_id, kind=kind)
                definitions[definition_id.casefold()] = (file, row_line, kind)
            role = fields.get("render_role")
            layer = fields.get("sorting_layer")
            if role and layer and not is_template:
                expected_layer = ROLE_LAYERS.get(role.canonical_value.casefold())
                layer_val = layer.canonical_value.casefold()
                if expected_layer and layer_val != expected_layer.casefold() and layer_val not in NEUTRAL_LAYERS:
                    raise ParseDiagnostic("ROLE_LAYER_CONFLICT",
                                          f"role={role.canonical_value} requires layer={expected_layer} but got layer={layer.canonical_value}",
                                          file=file, line=row_line, field="sorting_layer",
                                          raw=layer.canonical_value, kind=kind)
            rows.append(ParsedRow(definition_id, fields, file, row_line, kind,
                                  definition_id, references))
            index += 1
        tables.append(ParsedTable(kind, current_heading, headers, raw_headers,
                                  file, table_line, rows))
    return ParsedDocument(file, tables)


def parse_tables(text: str, file: str = "") -> List[ParsedTable]:
    return parse_document(text, file).tables
