#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
UIBrick (UI Toolkit) artifact generator — single script, two outputs.

Single source of truth: Definitions/**/*.definition.json.
Generates BOTH derived artifacts in one run (mirrors gen_brick_artifacts_ugui.py):
  1. BrickIndex.json     — machine view (skill + validation lookups)
  2. BrickCheatsheet.md  — AI view (grep-friendly parameter reference)

Replaces the former gen_brick_index.py + gen_brick_cheatsheet.py pair.

用法:
    {PYTHON} Runtime/UIBrick/gen_brick_artifacts.py            # 生成两个产物
    {PYTHON} Runtime/UIBrick/gen_brick_artifacts.py --check     # CI 校验是否最新，过时非零退出

``{PYTHON}`` 遵循包维护合同：Windows 为 ``python`` → ``py -3``，
macOS/Linux 为 ``python3`` → ``python``；仅使用 ``--version`` 成功且明确为
Python 3 的候选，不假设项目内解释器。命令从包目录运行。
"""
import os
import json
import sys
import argparse

HERE = os.path.dirname(os.path.abspath(__file__))
DEFS_DIR = os.path.join(HERE, "Definitions")
INDEX_OUT = os.path.join(HERE, "BrickIndex.json")
SHEET_OUT = os.path.join(HERE, "BrickCheatsheet.md")


# ═══════════════════════════════════════════
#  Shared collection
# ═══════════════════════════════════════════

def collect_bricks_with_paths():
    """遍历 Definitions/ 收集所有 definition.json，返回排序列表"""
    bricks = []
    for root, _, files in os.walk(DEFS_DIR):
        for fn in sorted(files):
            if not fn.endswith(".definition.json"):
                continue
            path = os.path.join(root, fn)
            with open(path, encoding="utf-8-sig") as f:
                data = json.load(f)
            data["_defPath"] = path
            bricks.append(data)
    bricks.sort(key=lambda b: (b.get("layer", ""), b.get("category", ""), b.get("brickId", "")))
    return bricks


# ═══════════════════════════════════════════
#  Artifact 1: BrickIndex.json (machine view)
# ═══════════════════════════════════════════

def build_entry(brick, base_dir=DEFS_DIR):
    """从 definition 构建 BrickIndex 条目"""
    def_path = os.path.relpath(brick.get("_defPath", ""), base_dir).replace("\\", "/")
    # Normalize to Definitions/... prefix
    if not def_path.startswith("Definitions/"):
        def_path = "Definitions/" + def_path

    snippet = brick.get("snippetPath", "")
    style = brick.get("stylePath", "")

    return {
        "brickId": brick.get("brickId", ""),
        "category": brick.get("category", ""),
        "definitionPath": def_path,
        "snippetPath": snippet,
        "stylePath": style,
        "aiSelectable": brick.get("aiSelectable", False),
        "aiPriority": brick.get("aiPriority", "medium"),
    }


def render_index(bricks):
    layers = {
        "primitive": [],
        "layout": [],
    }

    for b in bricks:
        layer = b.get("layer", "")
        if layer not in layers:
            layers[layer] = []
        layers[layer].append(build_entry(b))

    index = {
        "schemaVersion": "2.0",
        "generatedFrom": "Definitions/**/*.definition.json",
        "brickCount": len(bricks),
        "note": "brickCount must match the actual number of entries across all layers",
        "layers": layers,
    }

    return json.dumps(index, indent=2, ensure_ascii=False) + "\n"


# ═══════════════════════════════════════════
#  Artifact 2: BrickCheatsheet.md (AI view)
# ═══════════════════════════════════════════

def fmt_asset_slots(schema):
    """格式化 assetPropsSchema: {slotName: "asset?"} → 'slotName (asset?)'"""
    if not schema:
        return "—"
    return "; ".join(f"{k} ({v})" for k, v in schema.items())


def fmt_style_props(schema):
    """格式化 stylePropsSchema: {variant: [...], size: [...]} → 'variant[a,b]; size[s,m,l]'"""
    if not schema:
        return "—"
    parts = []
    for key, vals in schema.items():
        if isinstance(vals, list):
            parts.append(f"{key}[{','.join(vals)}]")
        else:
            parts.append(f"{key}={vals}")
    return "; ".join(parts)


def fmt_binding_props(schema):
    """格式化 bindingPropsSchema: {field: {bindable, runtimeType}} → 'field(string); visible(bool)'"""
    if not schema:
        return "—"
    parts = []
    for field, info in schema.items():
        if isinstance(info, dict):
            rt = info.get("runtimeType", "?")
            parts.append(f"{field}({rt})")
        else:
            parts.append(field)
    return "; ".join(parts)


def fmt_composition(policy):
    """格式化 compositionPolicy"""
    if not policy:
        return "—"
    allowed = policy.get("allowedChildren", "—")
    nest = policy.get("canNestUnder", "—")
    if isinstance(allowed, list):
        allowed = ",".join(allowed) if allowed else "none"
    if isinstance(nest, list):
        nest = ",".join(nest) if nest else "none"
    return f"allowedChildren={allowed}; canNestUnder={nest}"


def fmt_events(schema):
    """格式化 eventSchema: {event: type} → 'clicked(ui_event)'"""
    if not schema:
        return "—"
    return "; ".join(f"{k}({v})" for k, v in schema.items())


def fmt_layout(contract):
    """格式化 layoutContract.self"""
    if not contract or "self" not in contract:
        return "—"
    s = contract["self"]
    w = s.get("widthMode", "?")
    h = s.get("heightMode", "?")
    if isinstance(w, list):
        w = ",".join(w)
    if isinstance(h, list):
        h = ",".join(h)
    parts = [f"widthMode={w}", f"heightMode={h}"]
    for extra in ("grow", "shrink", "minWidth", "maxWidth", "minHeight", "maxHeight"):
        if extra in s:
            parts.append(f"{extra}={s[extra]}")
    return "; ".join(parts)


def render_sheet(bricks):
    out = []
    out.append(f"# UIBrick 速查卡 — UIToolkit（{len(bricks)} Bricks，勿手改）\n")
    out.append("> 由 `gen_brick_artifacts.py` 从 `Definitions/**/*.definition.json` 自动提取。Brick 增删/改后重跑该脚本刷新（BrickIndex.json 同步更新）。\n")
    out.append("> UGUI 模式见 `BrickCheatsheet.ugui.md`。\n")
    out.append("> **用法：写 UXML/USS/Panel 时 `grep` 你要用的 Brick 的 brickId 即可命中其全部参数。**\n")
    out.append("> 简单 Brick = 内联元素 + 引 USS；复杂 Brick = Template/Instance。详见 panel-builder SKILL.md。\n")

    cur_layer = None
    for b in bricks:
        layer = b.get("layer", "?")
        if layer != cur_layer:
            out.append(f"\n## {layer}\n")
            cur_layer = layer

        bid = b.get("brickId", "?")
        cat = b.get("category", "?")
        out.append(f"### {bid}")
        out.append(f"- Layer: {layer} | Category: {cat}")
        out.append(f"- AssetSlots: {fmt_asset_slots(b.get('assetPropsSchema'))}")
        out.append(f"- StyleProps: {fmt_style_props(b.get('stylePropsSchema'))}")
        out.append(f"- BindingProps: {fmt_binding_props(b.get('bindingPropsSchema'))}")
        out.append(f"- Composition: {fmt_composition(b.get('compositionPolicy'))}")
        out.append(f"- Events: {fmt_events(b.get('eventSchema'))}")
        out.append(f"- Layout: {fmt_layout(b.get('layoutContract'))}")
        out.append("")

    return "\n".join(out) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    bricks = collect_bricks_with_paths()
    index_content = render_index(bricks)
    sheet_content = render_sheet(bricks)

    if args.check:
        ok = True
        for path, content in ((INDEX_OUT, index_content), (SHEET_OUT, sheet_content)):
            old = open(path, encoding="utf-8-sig").read() if os.path.exists(path) else ""
            if old != content:
                print(f"{os.path.basename(path)} 已过时，请重跑 gen_brick_artifacts.py", file=sys.stderr)
                ok = False
        sys.exit(0 if ok else 1)

    with open(INDEX_OUT, "w", encoding="utf-8") as f:
        f.write(index_content)
    with open(SHEET_OUT, "w", encoding="utf-8") as f:
        f.write(sheet_content)
    print(f"已生成 {INDEX_OUT} ({json.loads(index_content)['brickCount']} bricks)")
    print(f"已生成 {SHEET_OUT}")


if __name__ == "__main__":
    main()
