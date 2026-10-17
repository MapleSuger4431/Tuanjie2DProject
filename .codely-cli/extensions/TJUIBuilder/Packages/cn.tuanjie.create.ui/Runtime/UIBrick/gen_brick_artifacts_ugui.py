#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
UGUI Brick artifact generator.

Single source of truth: UGUI/{Controls,Layout,Container}/Definitions/ugui.*.definition.json.
Generates BOTH derived artifacts in one run:
  1. BrickIndex.ugui.json  — machine view (BrickFactory.LoadDefinition lookup)
  2. BrickCheatsheet.ugui.md — AI view (token-compressed reference for skill subagents)

Run after any UGUI brick add/remove/parameter change — the skill then needs no updates.
Prose that is not derivable from definitions (用途/层级/用例/prefabPath naming) lives in
the static tables below (ORDER / PREFAB_PATHS / ANNOTATIONS).

用法:
    {PYTHON} Runtime/UIBrick/gen_brick_artifacts_ugui.py            # 生成两个产物
    {PYTHON} Runtime/UIBrick/gen_brick_artifacts_ugui.py --check     # CI 校验是否最新，过时非零退出

``{PYTHON}`` 遵循包维护合同：Windows 为 ``python`` → ``py -3``，
macOS/Linux 为 ``python3`` → ``python``；仅使用 ``--version`` 成功且明确为
Python 3 的候选，不假设项目内解释器。命令从包目录运行。
"""
import os
import json
import sys
import argparse

HERE = os.path.dirname(os.path.abspath(__file__))
UGUI_DIR = os.path.join(HERE, "UGUI")
INDEX_OUT = os.path.join(HERE, "BrickIndex.ugui.json")
SHEET_OUT = os.path.join(HERE, "BrickCheatsheet.ugui.md")

# Deterministic output order (matches the historical index order).
# Bricks not listed here are appended alphabetically at the end.
ORDER = [
    "ugui.button", "ugui.label", "ugui.value_text", "ugui.icon", "ugui.sprite",
    "ugui.slider", "ugui.toggle", "ugui.dropdown", "ugui.progress_bar", "ugui.input_field",
    "ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view",
    "ugui.list_item", "ugui.panel", "ugui.modal",
]

# brickId → prefab path (relative to Runtime/UIBrick/). Prefab file names are not
# derivable from brickId (GridBasic, ModalShell, ...), so they are mapped here.
PREFAB_PATHS = {
    "ugui.button": "UGUI/Controls/Prefabs/ButtonBase.prefab",
    "ugui.label": "UGUI/Controls/Prefabs/Label.prefab",
    "ugui.value_text": "UGUI/Controls/Prefabs/ValueText.prefab",
    "ugui.icon": "UGUI/Controls/Prefabs/Icon.prefab",
    "ugui.sprite": "UGUI/Controls/Prefabs/Sprite.prefab",
    "ugui.slider": "UGUI/Controls/Prefabs/SliderBase.prefab",
    "ugui.toggle": "UGUI/Controls/Prefabs/ToggleBase.prefab",
    "ugui.dropdown": "UGUI/Controls/Prefabs/DropdownBase.prefab",
    "ugui.progress_bar": "UGUI/Controls/Prefabs/ProgressBar.prefab",
    "ugui.input_field": "UGUI/Controls/Prefabs/InputFieldBase.prefab",
    "ugui.stack_vertical": "UGUI/Layout/Prefabs/StackVertical.prefab",
    "ugui.stack_horizontal": "UGUI/Layout/Prefabs/StackHorizontal.prefab",
    "ugui.grid": "UGUI/Layout/Prefabs/GridBasic.prefab",
    "ugui.scroll_view": "UGUI/Layout/Prefabs/ScrollView.prefab",
}

# Prose not derivable from definitions: purpose, component summary, hierarchy, use cases.
ANNOTATIONS = {
    "ugui.stack_vertical": ("垂直布局", "Root[RectTransform+VerticalLayoutGroup]；子元素直挂 Root 自动排列，preferred 高度沿布局链向上传播", "设置面板、主菜单按钮组；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill"),
    "ugui.stack_horizontal": ("水平布局", "Root[RectTransform+HorizontalLayoutGroup]；子元素直挂 Root 自动排列，preferred 尺寸沿布局链向上传播", "HUD 底部栏、工具栏；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill"),
    "ugui.grid": ("网格布局", "GridLayoutGroup + RectTransform", "背包、关卡选择网格"),
    "ugui.scroll_view": ("滚动/列表容器", "ScrollRect + Image(Mask) + Scrollbar×2(可选)", "设置列表滚动、商店滚动；Content 可直接挂 ugui.list_item / stack / grid（排行榜、好友列表用 ugui.list_item 动态填充）；背景配色传 viewportColor、隐藏背景传 viewportShowMaskGraphic:false（保留滚动遮罩）——勿直接改 Viewport 的 Image.alpha（alpha=0 会挖空 stencil，子物体全部隐形）或 Mask 组件值（装配时按默认/参数重置）"),
    "ugui.list_item": ("列表条目（图标+标签+值三段式）", "Button + Image(背景) + Text(标签) + Text(值,可选) + Image(图标,可选)", "排行榜行、好友列表行、邮件列表行"),
    "ugui.modal": ("弹窗壳体（独立 Canvas 层）", "Canvas + GraphicRaycaster + Image(遮罩) + Image(面板)", "确认弹窗、设置弹窗（独立 Canvas 层）"),
    "ugui.panel": ("面板容器（9-slice 拉伸）", "Image(Sliced 9-slice)，根无布局组——子节点零驱动（自由摆位层）", "面板容器、导航栏底色、标题栏背景、卡片面板、排队容器下的自由布局壳；assemblyOnly 的 preferredHeight 实测仅 10（占位假值）——挂入 stack / scroll_view(contentLayout) 等 preferred 排队场景必须显式传 height，否则被排成 10px 扁条；grid 不受影响（cellSize 硬驱动不读 preferred）；装配默认带白色 9-slice 底图，当透明壳用请换 sprite 或改用纯容器节点（省略 brickId）"),
    "ugui.button": ("按钮（带完整视听参数化）", "Button + Image(背景) + Text(文字) + Image(图标,可选)", "开始游戏、确认、暂停"),
    "ugui.label": ("静态文字标签", "Text", "标题、栏目名、说明文字"),
    "ugui.value_text": ("动态数值文字", "Text", "分数、金币、倒计时（动态数值显示）"),
    "ugui.icon": ("小图标（可点击，支持音效/缩放动效/交互着色）", "Image + Button(可点击)", "功能图标、状态图标、可点击的返回/关闭图标"),
    "ugui.sprite": ("大图/精灵图（可填充区域）", "Image", "头像、背景图、物品展示"),
    "ugui.slider": ("滑块（双向绑定，可选居中数值文字）", "Slider + Image×3(背景/填充/手柄) + Label/Text(可选)", "音量、亮度、难度；数值文字默认 inactive 不渲染，开启须 label.active:true 与 text 成对传（同 icon.active↔spritePath 惯例），文字参数集同 progress_bar；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层"),
    "ugui.toggle": ("开关（带勾选框和文字）", "Toggle + Image×2(背景/勾选) + Text(标签) + UGUIFlexLayoutGroup(根,声明式)", "音效/音乐开关；根挂 flex（Background 固定方 20×20 + Label 吃剩余，间距 3）——改勾选框大小传 backgroundWidth/Height（自动转 LE preferred，文本让位）；挪列配方=paddingLeft+label.width+label.flexibleWidth:0+checkbox.margin（label.width 须伴随非空 text）+checkbox.position:\"right\"；checkbox.position 控勾选框左/右/上（right 走 reverseOrder、打包安全），childAlignment 控整列打包；rect 直改类参数已删除（组驱动下直改 rect 会被布局 pass 覆写，操作 LayoutElement.preferred 与组 padding 是正道）"),
    "ugui.dropdown": ("下拉选择框", "Dropdown + Caption Area[Text(当前值)+Image(箭头)+UGUIFlexLayoutGroup] + Template(展开面板)", "语言、分辨率、画质选择；caption 对收在 Caption Area 包装层内挂 flex（Label 吃剩余 + Arrow 固定 20×20，间距 5）——root/Template 无布局组（引擎交接面），arrow.width/height 自动让位（组下转 LE preferred）；Template 子树结构勿动（Dropdown.Show 手动排布依赖）"),
    "ugui.progress_bar": ("进度条（无手柄，可带居中百分比文字）", "Slider + Image×2(背景/填充) + Label/Text(居中文字)", "加载、血量、经验；进度百分比传 text:'50%'（默认空串不渲染，运行时由游戏代码更新），文字参数集同 label brick；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层"),
    "ugui.input_field": ("输入框", "InputField + Image(背景) + Text×2(占位符/实际输入)", "玩家昵称输入、搜索框；自定义光标色须 customCaretColor:true 与 caretColor 成对传——uGUI 的 caretColor getter 被 customCaretColor 开关门控，单传 caretColor 只写入存储、运行时光标仍用文本色（不可见生效），与 shadowEnabled↔shadowColor 伴随使能同惯例"),
}

ASSET_HANDLERS = {"LoadSprite", "LoadFont", "LoadAudioClip"}


def load_definitions():
    bricks = []
    for root, _, files in os.walk(UGUI_DIR):
        for fn in sorted(files):
            if not fn.endswith(".definition.json") or not fn.startswith("ugui."):
                continue
            path = os.path.join(root, fn)
            with open(path, encoding="utf-8-sig") as f:
                data = json.load(f)
            data["_defPath"] = os.path.relpath(path, HERE).replace("\\", "/")
            bricks.append(data)
    return bricks


def sort_bricks(bricks):
    by_id = {b["brickId"]: b for b in bricks}
    ordered = [by_id.pop(bid) for bid in ORDER if bid in by_id]
    ordered += sorted(by_id.values(), key=lambda b: b["brickId"])
    return ordered


def fmt_default(value):
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, float) and value == int(value):
        return str(int(value))
    if isinstance(value, list):
        return "x".join(fmt_default(v) for v in value)
    return str(value)


def fmt_param(name, param):
    ptype = param.get("type", "?")
    desc = f"{name}({ptype}"
    if "default" in param:
        desc += f",{fmt_default(param['default'])}"
    if param.get("handler") in ASSET_HANDLERS:
        desc += ",路径"
    desc += ")"
    if ptype == "Sprite":
        desc = desc[:-1] + ",支持传Assets/路径字符串)"
    return desc


def group_params(parameters):
    """Group parameters: dotted prefixes get their own group, rest share one line."""
    groups, plain = {}, []
    for name, param in parameters.items():
        if "." in name:
            prefix = name.split(".")[0]
            groups.setdefault(f"{prefix}.*", []).append(fmt_param(name, param))
        else:
            plain.append(fmt_param(name, param))
    result = []
    if plain:
        result.append(("基础", "; ".join(plain)))
    for key in groups:
        result.append((key, "; ".join(groups[key])))
    return result


def fmt_composition(policy):
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
    if not schema:
        return "—"
    return "; ".join(f"{k}({v})" for k, v in schema.items())


def fmt_binding(schema):
    if not schema:
        return "—"
    return "; ".join(f"{k}({v.get('runtimeType', '?')})" for k, v in schema.items())


def fmt_layout(contract):
    if not contract or "self" not in contract:
        return "—"
    s = contract["self"]

    def j(v):
        return ",".join(v) if isinstance(v, list) else str(v)
    return f"widthMode={j(s.get('widthMode', '?'))}; heightMode={j(s.get('heightMode', '?'))}"


def render_index(bricks):
    entries = []
    for b in bricks:
        bid = b.get("brickId", "")
        assembly_only = bool(b.get("assemblyOnly"))
        prefab = PREFAB_PATHS.get(bid)
        if not prefab and not assembly_only:
            print(f"[WARN] no PREFAB_PATHS entry for '{bid}' — index entry will lack prefabPath", file=sys.stderr)
        entries.append({
            "brickId": bid,
            "category": b.get("category", ""),
            "prefabPath": prefab or "",
            "definitionPath": b["_defPath"],
            "requiredComponents": b.get("requiredComponents", []),
            "childStructure": b.get("childStructure", []),
            "eventBindings": b.get("eventBindings", {}),
            "bindingProps": b.get("bindingProps", {}),
            "assemblyOnly": b.get("assemblyOnly", False),
            "aiSelectable": b.get("aiSelectable", False),
            "aiPriority": b.get("aiPriority", "medium"),
        })

    index = {
        "schemaVersion": "1.1-ugui",
        "generatedFrom": "UGUI/{Controls,Layout,Container}/Definitions/ugui.*.definition.json",
        "brickCount": len(entries),
        "note": "UGUI brick index, generated by gen_brick_artifacts_ugui.py. Each non-assemblyOnly brick has a Prefab (prefabPath) + definition; assemblyOnly bricks (assemblyOnly: true) have no prefab file and are built directly from their definition. BrickFactory loads the Prefab when present (falls back to definition-driven construction otherwise), instantiates, then applies parameters.",
        "bricks": entries,
    }
    return json.dumps(index, indent=2, ensure_ascii=False) + "\n"


HEADER_RULES = """## 传参规则（全部 Brick 通用，务必先读）

- **只传需要覆盖的参数**：`CreateWithParams` 的 `Dictionary<string, object>` 中未传的参数保持 Prefab/definition 默认值
- **参数顺序无关**：工厂内部按依赖分层应用（SetActive 创建可选子节点 → Ensure*/Load* 挂组件与资产 → 其余），`icon.active` 与 `icon.spritePath` 谁先谁后都正确
- **Color**：接受 `"#RRGGBB[AA]"` 字符串、`[r,g,b,a]` 数组或命名色（white/black/red/…）；**Vector2/Vector3** 接受数组；**枚举**（Image.Type、TextAnchor、FontStyle、Selectable.Transition…）传字符串名
- **资产路径参数**（`backgroundSprite`、`icon.spritePath`、`customFontPath`、`*SoundPath` 等）传 `Assets/` 开头路径字符串；`type:"Sprite"` 的参数（ugui.panel 的 `sprite` 等）同样支持路径字符串或直接传 `Sprite` 引用；路径无效时告警并跳过
- **optional 子节点预创建但默认 inactive**：childStructure 中标 `optional` 的子节点（Button 的 Icon、progress_bar 的 HandleArea、list_item 的 Value 等）会预创建进 Prefab 但默认不激活；`*.active` 参数（icon.active / handle.active / value.active 等）传 `true` 激活。Dropdown 的 Scrollbar 默认 active（可由 scrollbar.active 关闭）
- **assemblyOnly Brick**：`assemblyOnly: true` 的 brick（panel、list_item、modal）无 Prefab 文件，工厂直接从 definition 构建等效层级（Editor 与 Runtime 一致）
- **PostProcess 自动处理**：Button.targetGraphic 连线、Slider.fillRect/handleRect/targetGraphic、Toggle.graphic/targetGraphic、ScrollRect.viewport/content、Dropdown.template（含隐藏 + Template ScrollRect/Scrollbar/Item Toggle 接线 + 默认 Option A/B/C）、Text 默认字体、无 sprite 的 Icon 自动隐藏（Image.enabled=false）、Selectable.navigation 默认 Automatic（与编辑器一致）
- **面板级 API**：`BrickPanelBuilder.BuildBrickTrees(treesJsonPath, prefabDir)` 一次调用输出 N 个 Prefab + 元素 SafeFind 路径清单 + 组合校验警告，Canvas 配置与保存修复（防黑屏）已内置
"""

CROSS_MODE_TABLE = """## 双模式对照表

| 功能 | UIToolkit brickId | UGUI brickId |
|------|-------------------|-------------|
| 垂直布局 | layout.stack.vertical | ugui.stack_vertical |
| 水平布局 | layout.stack.horizontal | ugui.stack_horizontal |
| 网格布局 | layout.grid.basic | ugui.grid |
| 滚动/列表容器 | layout.scroll.view | ugui.scroll_view |
| 列表条目 | layout.list.item | ugui.list_item |
| 弹窗壳体 | layout.modal.shell | ugui.modal |
| 面板容器 | primitive.container.panel | ugui.panel |
| 按钮 | primitive.input.button_base | ugui.button |
| 标签文字 | primitive.text.label | ugui.label |
| 数值文字 | primitive.text.value_text | ugui.value_text |
| 图标 | primitive.image.icon | ugui.icon |
| 大图 | primitive.image.sprite | ugui.sprite |
| 滑块 | primitive.input.slider_base | ugui.slider |
| 开关 | primitive.input.toggle_base | ugui.toggle |
| 下拉选择 | primitive.input.dropdown_base | ugui.dropdown |
| 进度条 | primitive.input.progress_bar | ugui.progress_bar |
| 输入框 | primitive.input.text_field | ugui.input_field |
"""


def render_sheet(bricks):
    out = []
    out.append("# UGUI Brick 速查卡（自动生成，勿手改）\n")
    out.append("> 由 `gen_brick_artifacts_ugui.py` 从 `UGUI/{Controls,Layout,Container}/Definitions/ugui.*.definition.json` 生成。")
    out.append("> Brick 增删/参数修改后重跑该脚本刷新（BrickIndex.ugui.json 同步更新）。\n")
    out.append(HEADER_RULES)

    out.append("## 总览\n")
    out.append("| brickId | 用途 | 典型用例 |")
    out.append("|---------|------|---------|")
    for b in bricks:
        bid = b["brickId"]
        purpose, _comps, use = ANNOTATIONS.get(bid, (bid, "", ""))
        out.append(f"| {bid} | {purpose} | {use} |")
    out.append("")

    out.append("## 详细参数\n")
    for b in bricks:
        bid = b["brickId"]
        purpose, comps, use = ANNOTATIONS.get(bid, (bid, "", ""))
        out.append(f"#### {bid}")
        out.append(f"- 用途: {purpose}")
        out.append(f"- UGUI 组件: `{comps}`" if comps else "- UGUI 组件: —")
        if b.get("assemblyOnly"):
            out.append("- AssemblyOnly: true (无 Prefab 文件，从 definition 构建)")
        else:
            out.append(f"- Prefab: `{PREFAB_PATHS.get(bid, '—')}`")
        params = b.get("parameters") or {}
        if params:
            out.append("- Parameters:")
            for label, line in group_params(params):
                out.append(f"  - {label}: {line}")
        else:
            out.append("- Parameters: —")
        out.append(f"- BindingProps: {fmt_binding(b.get('bindingProps'))}")
        out.append(f"- Composition: {fmt_composition(b.get('compositionPolicy'))}")
        out.append(f"- Events: {fmt_events(b.get('eventSchema') or {k: 'ui_event' for k in b.get('eventBindings', {})})}")
        out.append(f"- Layout: {fmt_layout(b.get('layoutContract'))}")
        out.append(f"- 用例: {use}")
        out.append("")

    out.append(CROSS_MODE_TABLE)
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    bricks = sort_bricks(load_definitions())
    index_content = render_index(bricks)
    sheet_content = render_sheet(bricks)

    if args.check:
        ok = True
        for path, content in ((INDEX_OUT, index_content), (SHEET_OUT, sheet_content)):
            old = open(path, encoding="utf-8-sig").read() if os.path.exists(path) else ""
            if old != content:
                print(f"{os.path.basename(path)} 已过时，请重跑 gen_brick_artifacts_ugui.py", file=sys.stderr)
                ok = False
        sys.exit(0 if ok else 1)

    with open(INDEX_OUT, "w", encoding="utf-8") as f:
        f.write(index_content)
    with open(SHEET_OUT, "w", encoding="utf-8") as f:
        f.write(sheet_content)
    print(f"已生成 {INDEX_OUT} ({len(bricks)} bricks)")
    print(f"已生成 {SHEET_OUT}")


if __name__ == "__main__":
    main()
