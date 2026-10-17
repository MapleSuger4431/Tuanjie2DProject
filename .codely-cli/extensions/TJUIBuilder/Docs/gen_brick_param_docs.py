#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate UIBrick UGUI parameter reference docs (MD for AI + HTML for developers).

Source of truth: every ugui.*.definition.json under Runtime/UIBrick/UGUI/**/Definitions.
Re-run this script after ANY definition change (same discipline as
gen_brick_artifacts_ugui.py) so the docs never drift from the parameter surface.

Every parameter is classified for the reference-image-to-UI pipeline:
  REQUIRED    - the intermediate LLM MUST supply (content / skin resources / key sizes);
                omission leaves placeholder text, white engine sprites or squashed layout.
  RECOMMENDED - engine default exists (generic look); the intermediate LLM should
                override from the design/reference in most cases.
  OPTIONAL    - fine-tuning / effects / runtime state; safe to omit.

Outputs (this Docs directory):
  UIBrickParameters.md     - machine-oriented reference for AI agents (trees.json authoring)
  UIBrickParameters.html   - human-oriented reference (dark theme, TOC, per-brick cards)
"""

import datetime
import html
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))            # <ext root>/Docs (script home)
EXT_ROOT = os.path.abspath(os.path.join(HERE, ".."))          # <ext root>
UGUI_ROOT = os.path.join(EXT_ROOT, "Packages", "cn.tuanjie.create.ui",
                         "Runtime", "UIBrick", "UGUI")
DOCS_DIR = HERE

# Directory name -> (display name, doc section order, per-brick display order)
CATEGORY_ORDER = [
    ("Controls", "Controls 控件类", [
        "ugui.button", "ugui.toggle", "ugui.slider", "ugui.progress_bar",
        "ugui.input_field", "ugui.dropdown", "ugui.label", "ugui.value_text",
        "ugui.icon", "ugui.sprite",
    ]),
    ("Layout", "Layout 布局类", [
        "ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid",
        "ugui.scroll_view",
    ]),
    # modal migrated to Container (definition now lives in UGUI/Container/Definitions,
    # package commit 27e86a9 2026-09-15).
    ("Container", "Container 容器类（面板树根 / 弹窗 / 列表模板）", [
        "ugui.panel", "ugui.modal", "ugui.list_item",
    ]),
]

# ----------------------------------------------------------------------------
# classification for the reference-image-to-UI pipeline
# ----------------------------------------------------------------------------

L_REQUIRED, L_RECOMMENDED, L_OPTIONAL = "REQUIRED", "RECOMMENDED", "OPTIONAL"
LEVEL_LABEL = {
    L_REQUIRED: "🔴 必传",
    L_RECOMMENDED: "🟡 建议",
    L_OPTIONAL: "🟢 可选",
}
# parameter tables are grouped by level (REQUIRED → RECOMMENDED → OPTIONAL);
# within a level the definition order is preserved (stable sort).
_LEVEL_RANK = {L_REQUIRED: 0, L_RECOMMENDED: 1, L_OPTIONAL: 2}

# width/height semantics: interactive controls & images need explicit preferred
# sizes inside layout containers (LayoutElement); text bricks can fall back to
# content sizing; layout containers have fill/hug semantics.
SIZE_REQUIRED_BRICKS = {
    "ugui.button", "ugui.toggle", "ugui.slider", "ugui.progress_bar",
    "ugui.input_field", "ugui.dropdown", "ugui.icon", "ugui.sprite",
}
SIZE_RECOMMENDED_BRICKS = {
    "ugui.label", "ugui.value_text", "ugui.list_item",
    "ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view",
    "ugui.panel",  # tree roots are force-stretched; nested panels should size explicitly
}

# Skin resources that make or break the reference look.
REQUIRED_SPRITES = {
    ("ugui.button", "backgroundSprite"),
    ("ugui.toggle", "backgroundSprite"),
    ("ugui.slider", "backgroundSprite"), ("ugui.slider", "fillSprite"),
    ("ugui.slider", "handle.spritePath"),
    ("ugui.progress_bar", "backgroundSprite"), ("ugui.progress_bar", "fillSprite"),
    ("ugui.input_field", "backgroundSprite"),
    ("ugui.dropdown", "backgroundSprite"),
    ("ugui.icon", "spritePath"),
    ("ugui.sprite", "spritePath"),
    ("ugui.panel", "sprite"),
}
# Skin resources with a workable engine/default fallback, but the reference
# design usually carries its own texture.
RECOMMENDED_SPRITES = {
    ("ugui.toggle", "checkmarkSprite"),
    ("ugui.dropdown", "arrow.spritePath"),
    ("ugui.dropdown", "templateBackgroundSprite"),
    ("ugui.dropdown", "itemBackgroundSprite"),
    ("ugui.dropdown", "itemCheckmarkSprite"),
    ("ugui.modal", "panelSprite"),
    ("ugui.list_item", "backgroundSprite"),
    ("ugui.list_item", "iconSprite"),
}
# Text content params.
REQUIRED_TEXT = {
    ("ugui.button", "text"), ("ugui.label", "text"), ("ugui.value_text", "text"),
    ("ugui.list_item", "text"),
}
RECOMMENDED_TEXT = {
    ("ugui.dropdown", "value"),           # caption text
    ("ugui.list_item", "value.text"),
    ("ugui.input_field", "placeholderText"),
    ("ugui.toggle", "text"),
    ("ugui.progress_bar", "text"),       # 居中标签文本（默认空），设计稿带文案时应覆盖
}

_TEXTSTYLE_TAIL = {"fontSize", "textColor", "fontStyle", "textAlignment", "lineSpacing"}


def _is_sprite_param(name, p):
    if p.get("type") == "Sprite" or p.get("handler") in ("LoadSprite", "LoadFont", "LoadAnimatorController", "LoadAudioClip"):
        return True
    low = name.lower()
    return low.endswith("sprite") or low.endswith("spritepath")


def classify(bid, name, p):
    """Return (level, reason) for one parameter of one brick."""
    tail = name.split(".")[-1]
    low = name.lower()

    # -- effect / transition / navigation families and child-geometry micro-tuning
    if name.startswith(("spriteSwap.", "animation.", "navigation.", "tint.",
                        "effects.", "textShadow", "textOutline", "textOutline.",
                        "shadow", "outline", "gradient", "underline", "strikethrough")):
        return L_OPTIONAL, "特效/过渡/导航族，有默认"
    if name.startswith("background."):
        return L_OPTIONAL, "Background 子物体几何/渲染微调"

    # -- content text
    if (bid, name) in REQUIRED_TEXT:
        return L_REQUIRED, "内容文本，缺失=占位文案"
    if (bid, name) in RECOMMENDED_TEXT:
        return L_RECOMMENDED, "内容文本（有默认占位），按设计稿覆盖"
    if bid == "ugui.input_field" and name == "text":
        return L_OPTIONAL, "初始内容通常留空，交互后由玩家输入"

    # -- skin / font resources
    if _is_sprite_param(name, p):
        if (bid, name) in REQUIRED_SPRITES:
            return L_REQUIRED, "视觉皮肤，缺失=引擎白底/无图"
        if (bid, name) in RECOMMENDED_SPRITES:
            return L_RECOMMENDED, "有引擎默认图，参考图皮肤应替换"
        if name.startswith("icon.") or name.startswith("handle.") and bid != "ugui.slider":
            return L_RECOMMENDED, "对应子件启用时应传生成图"
        if p.get("handler") == "LoadFont" or name == "customFontPath":
            return L_RECOMMENDED, "有默认字体，美术字体应传"
        if "scrollbar" in low or "handle" in low:
            return L_OPTIONAL, "滚动条皮肤，默认样式可用"
        if bid in ("ugui.label", "ugui.value_text") and name == "backgroundSprite":
            return L_OPTIONAL, "文本底图为可选装饰（backgroundEnabled 配套）"
        return L_RECOMMENDED, "资源类参数，参考图有对应素材时应传"

    # -- explicit sizes
    if name in ("width", "height"):
        if bid in SIZE_REQUIRED_BRICKS:
            return L_REQUIRED, "布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸"
        if bid in SIZE_RECOMMENDED_BRICKS:
            return L_RECOMMENDED, "容器有 fill/hug 语义，具体布局通常应显式给"
    if name in ("backgroundWidth", "backgroundHeight", "checkmarkWidth", "checkmarkHeight"):
        return L_OPTIONAL, "子件尺寸微调"
    if name in ("label.width", "icon.width", "icon.height") or tail == "flexibleWidth":
        if tail == "flexibleWidth":
            return L_OPTIONAL, "布局权重微调"
        return L_RECOMMENDED, "行内件尺寸，模板观感应覆盖"

    # -- modal dialog geometry
    if bid == "ugui.modal":
        if name in ("panelWidth", "panelHeight"):
            return L_REQUIRED, "缺失则 ModalPanel 全屏拉伸、不像弹窗"
        if name in ("headerHeight", "footerHeight", "panelPadding"):
            return L_RECOMMENDED, "有兜底布局，弹窗版式应按设计稿给"
        if name in ("overlayColor", "sortingOrder", "panelColor", "panelType"):
            return L_RECOMMENDED, "有引擎默认，应按设计稿覆盖"

    # -- scroll_view content layout
    if bid == "ugui.scroll_view" and name == "contentLayout":
        return L_REQUIRED, "缺失则 Content 无 LayoutGroup、子项重叠"
    if name in ("contentSpacing", "contentPadding", "contentCellSize"):
        return L_RECOMMENDED, "内容布局观感，参考图应给出"
    if name in ("templateHeight", "itemHeight"):
        return L_RECOMMENDED, "展开列表几何，字号大时必须调大"

    # -- text styling family
    if tail in _TEXTSTYLE_TAIL or name in ("itemFontSize", "itemTextColor",
                                           "placeholderFontSize", "placeholderColor",
                                           "fontSize", "fontStyle", "textColor", "textAlignment"):
        return L_RECOMMENDED, "有引擎默认（通用风格），参考图场景应覆盖"

    # -- colors (visual overrides) except caret/selection micro-tuning
    if "color" in low and not low.startswith(("caret", "selection")):
        return L_RECOMMENDED, "有引擎默认色，应按参考图调色"

    # -- layout knobs with generic defaults
    if name in ("spacing", "padding", "cellSize", "childAlignment",
                "paddingLeft", "paddingRight", "paddingTop", "paddingBottom"):
        return L_RECOMMENDED, "有通用默认（spacing=4/padding=0），版式应按参考图给"
    if name in ("value", "isOn", "minValue", "maxValue", "wholeNumbers",
                "direction", "characterLimit", "contentType", "lineType",
                "interactable", "opacity", "imageType"):
        return L_OPTIONAL, "运行态/行为开关，默认可用"

    return L_OPTIONAL, "微调项，省略安全"


# ----------------------------------------------------------------------------
# example values for minimal trees.json snippets
# ----------------------------------------------------------------------------

_SIZE_EXAMPLES = {
    ("ugui.button", "width"): 420, ("ugui.button", "height"): 64,
    ("ugui.toggle", "width"): 300, ("ugui.toggle", "height"): 64,
    ("ugui.slider", "width"): 420, ("ugui.slider", "height"): 36,
    ("ugui.progress_bar", "width"): 420, ("ugui.progress_bar", "height"): 36,
    ("ugui.input_field", "width"): 480, ("ugui.input_field", "height"): 64,
    ("ugui.dropdown", "width"): 360, ("ugui.dropdown", "height"): 64,
    ("ugui.icon", "width"): 64, ("ugui.icon", "height"): 64,
    ("ugui.sprite", "width"): 128, ("ugui.sprite", "height"): 128,
    ("ugui.label", "width"): 260, ("ugui.label", "height"): 48,
    ("ugui.value_text", "width"): 200, ("ugui.value_text", "height"): 40,
    ("ugui.modal", "panelWidth"): 520, ("ugui.modal", "panelHeight"): 380,
}

_SPRITE_NAME_HINT = {
    "backgroundSprite": "btn_primary", "fillSprite": "bar_fill",
    "handle.spritePath": "bar_handle", "spritePath": "icon_default",
    "sprite": "panel_card",
}


def example_value(bid, name, p):
    tail = name.split(".")[-1]
    if name in ("text", "value.text"):
        return "按钮文本" if bid == "ugui.button" else "文本内容"
    if name == "value":
        return "选项 A"
    if name == "placeholderText":
        return "请输入…"
    if name in ("width", "height", "panelWidth", "panelHeight"):
        return _SIZE_EXAMPLES.get((bid, name), 300 if name in ("width", "panelWidth") else 64)
    if name == "contentLayout":
        return "vertical"
    if name in ("fontSize", "value.fontSize", "itemFontSize"):
        return 32 if bid == "ugui.button" else 28
    if tail in ("fontStyle",):
        return "Bold"
    if tail in ("textAlignment",):
        return "MiddleCenter"
    if tail in ("lineSpacing",):
        return 1
    if name in ("textColor", "value.textColor", "itemTextColor", "placeholderColor"):
        return "#FFFFFFFF"
    if name == "childAlignment":
        return "MiddleCenter"
    if _is_sprite_param(name, p):
        hint = _SPRITE_NAME_HINT.get(name, name.split(".")[-1].replace("Sprite", "").replace("spritePath", "img"))
        return "Assets/Games/<GameName>/UI/Images/%s.png" % hint
    if p.get("type") == "bool":
        return True
    if p.get("type") == "int":
        return 0
    if p.get("type") == "float":
        return 1.0
    return "<值>"


# ----------------------------------------------------------------------------
# generic helpers
# ----------------------------------------------------------------------------

HANDLER_DOC = {
    "SetSizeDelta": "写尺寸（axis 指定 x/y）。目标处于 LayoutGroup 子级时自动改写 LayoutElement preferred 尺寸，防被布局覆写；否则写 sizeDelta。",
    "LoadSprite": "从项目路径加载 Sprite 到目标 Image（目标无 Image 时自动补挂并 enabled=true）。路径形如 Assets/.../x.png 或包内相对 Icons/xxx。",
    "LoadFont": "从项目路径加载 Font 到目标 Text。",
    "LoadAudioClip": "从项目路径加载 AudioClip（配合 UIAudioFeedback）。",
    "SetAudioProp": "设置 UIAudioFeedback 的属性。",
    "EnsureShadow": "确保目标挂 Shadow 组件（缺失补挂）。",
    "SetShadowProp": "设置 Shadow 组件属性（prop 指定属性名）。",
    "EnsureOutline": "确保目标挂 Outline 组件（缺失补挂）。",
    "SetOutlineProp": "设置 Outline 组件属性（prop 指定属性名）。",
    "EnsureGradient": "确保目标挂 UGUITextGradient（缺失补挂）。",
    "SetGradientProp": "设置 UGUITextGradient 渐变属性。",
    "EnsureUnderline": "确保下划线装饰（UGUITextDecoration）。",
    "SetUnderlineProp": "设置下划线装饰属性。",
    "EnsureStrikethrough": "确保删除线装饰（UGUITextDecoration）。",
    "SetBackgroundEnabled": "背景子物体启用开关（backgroundEnabled）。",
    "SetIconLayout": "按钮图标的布局参数（位置/边距驱动的布局处理）。",
    "SetFlexTextProp": "弹性文本属性（自动排版适配）。",
    "SetScaleEffect": "按钮按压缩放反馈（ButtonScaleEffect：pressScale/hoverScale 等）。",
    "SetBorderRadius": "圆角：以 Icons/RoundedRectBorder 九宫格替换目标 Image 的 sprite 并切 Sliced。",
    "SetPadding": "布局容器 padding（缓存后统一应用）。",
    "SetActive": "子物体显隐开关（component 指定须存在的组件）。",
    "EnsureAnimator": "确保目标挂 Animator（缺失补挂）。",
    "LoadAnimatorController": "从项目路径加载 AnimatorController。",
    "SetContentLayout": "在 scroll_view 的 Viewport/Content 上挂指定 LayoutGroup（vertical|horizontal|grid|none）。",
    "SetContentLayoutProp": "设置该 Content LayoutGroup 的属性（prop：spacing/padding/cellSize）。",
    "SetModalSize": "modal 面板宽/高（axis 区分；缓存后在 PostProcess 按序统一应用，任意 JSON 键序安全）。",
    "SetModalPadding": "modal 面板内边距（缓存后统一应用）。",
    "SetModalSlotHeight": "modal 头/脚槽高度（prop=header/footer；缓存后统一应用）。",
}

# Built-in no-op placeholder handlers registered in BrickFactory but referenced by
# no definition — trees.json can never trigger them; footnote-only in the docs.
HANDLER_NOOPS = ["SetBorderWidth", "EnsureInlineSprite", "SetInlineSpriteFormat"]


def handler_usage(bricks):
    """Derive per-handler usage live from definitions: extra spec keys + param names."""
    usage = {}
    for b in bricks:
        for name, p in (b.get("parameters") or {}).items():
            h = p.get("handler")
            if not h:
                continue
            u = usage.setdefault(h, {"extras": set(), "params": []})
            for extra in ("axis", "prop", "component"):
                if extra in p:
                    u["extras"].add(extra)
            if name not in u["params"]:
                u["params"].append(name)
    return usage


# Verified against BrickFactory.ApplyParameters / SortByDependencyTier / ApplyTarget /
# HandleSetSizeDelta / ApplyModalLayout — keep in sync when the factory changes.
HANDLER_MECH = [
    ("应用管线",
     "BuildBrickTrees / CreateWithParams → ApplyParameters：每个参数按 definition 声明分派——"
     "有 `handler` 走内建处理程序注册表（共 31 个，其中 3 个为占位 no-op），无 handler 而有 `target` "
     "走反射赋值；值先按 `type` 转换（type:\"Sprite\" 支持直接引用或项目路径字符串）。"),
    ("依赖分层，键序无关",
     "tier 0 `SetActive`（创建/显隐可选子节点，如 icon.active）→ tier 1 `Ensure*` / `LoadSprite` / "
     "`LoadFont` / `LoadAudioClip` / `LoadAnimatorController` / `SetBackgroundEnabled` / `SetScaleEffect` / "
     "`SetContentLayout`（补挂组件、加载资源、在 Content 挂 LayoutGroup——tier 2 的 contentSpacing 等属性"
     "要写到它上面，必须先存在）→ tier 2 其余全部（属性写入/几何/皮肤）。同层保持 JSON 键序，"
     "trees.json 参数书写顺序不影响结果。"),
    ("几何缓存 + PostProcess 统一应用",
     "`SetModalSize` / `SetModalPadding` / `SetModalSlotHeight` / `SetPadding` 只缓存值，"
     "整树装配完成后由 PostProcess（ApplyModalLayout 等）按固定顺序统一应用——任意 JSON 键序下布局一致；"
     "一个 modal 几何参数都没传时保持 definition 默认 stretch 不动。"),
    ("作用点",
     "参数可声明 `child`（子物体路径，支持 `A/B` 斜杠）；表中『作用点』列的 根 / 子:Icon 即此信息。"
     "执行前先解析作用点，找不到子物体则告警跳过该参数。"),
    ("SetSizeDelta 布局感知",
     "目标处于 LayoutGroup 子级时改写 `LayoutElement.preferredWidth/Height`（缺件自动补挂），"
     "并同步 sizeDelta 供布局外读取；`LayoutElement.ignoreLayout=true` 的目标直写 sizeDelta；`axis` 缺省 x。"),
    ("LoadSprite",
     "目标无 Image 时自动补挂且 enabled=true；路径支持项目路径（`Assets/...`）与包内相对路径（`Icons/xxx`）。"),
    ("反射赋值（target: 前缀）",
     "① `GetComponent<X>().属性` —— 作用点上取 X，缺失自动 AddComponent；"
     "② `GetComponentInChildren<X>().属性` —— 深搜子树（含未激活）取首个，缺失仅告警不自动建；"
     "③ 裸属性名 —— 扫作用点全部组件，首个可写同名属性收值。均支持 `a.b.c` 嵌套属性路径。"),
    ("组件类型短名",
     "definition 中的 `Button` / `Image` / `CanvasScaler` 等短名经 `ShortNameMap` 映射为完整类型"
     "（Type.GetType 失败再全程序集扫描兜底）。"),
]


def load_definitions():
    bricks = []
    for dirpath, _dirnames, filenames in os.walk(UGUI_ROOT):
        for fn in filenames:
            if not fn.endswith(".definition.json"):
                continue
            with open(os.path.join(dirpath, fn), encoding="utf-8") as fh:
                bricks.append(json.load(fh))
    return bricks


def fmt_default(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, float) and value == int(value):
        return str(int(value))
    if isinstance(value, (list, tuple)):
        return json.dumps(value, ensure_ascii=False, separators=(",", ":"))
    if isinstance(value, str):
        return value
    return json.dumps(value, ensure_ascii=False)


def mech_of(p):
    if "handler" in p:
        parts = [p["handler"]]
        for extra in ("axis", "prop", "component"):
            if extra in p:
                parts.append("%s=%s" % (extra, p[extra]))
        return "handler:" + " ".join(parts)
    if "target" in p:
        return "target: " + p["target"]
    return ""


def scope_of(p):
    if p.get("child"):
        return "子:" + p["child"]
    return "根"


def child_structure_lines(nodes, depth=0):
    out = []
    for n in nodes or []:
        comp = n.get("component") or (",".join(n.get("components", [])) if n.get("components") else "")
        label = n.get("name", "?") + ("(" + comp + ")" if comp else "")
        if n.get("optional"):
            label += " [可选]"
        out.append((depth, label))
        out.extend(child_structure_lines(n.get("children"), depth + 1))
    return out


def brick_role(brick):
    comps = brick.get("requiredComponents") or []
    return "requiredComponents: " + (", ".join(comps) if comps else "—")


def level_stats(brick):
    params = brick.get("parameters") or {}
    counts = {L_REQUIRED: 0, L_RECOMMENDED: 0, L_OPTIONAL: 0}
    for name, p in params.items():
        counts[classify(brick["brickId"], name, p)[0]] += 1
    return counts


def minimal_snippet(brick):
    """Compact trees.json node with REQUIRED params (values = examples) plus
    the most common text-styling RECOMMENDED params."""
    bid = brick["brickId"]
    params = brick.get("parameters") or {}
    req = [(n, p) for n, p in params.items() if classify(bid, n, p)[0] == L_REQUIRED]
    extra = [(n, p) for n, p in params.items()
             if classify(bid, n, p)[0] == L_RECOMMENDED
             and n.split(".")[-1] in _TEXTSTYLE_TAIL][:3]
    node = {"brickId": bid, "name": bid.split(".", 1)[1] + "_demo", "parameters": {}}
    for n, p in req + extra:
        node["parameters"][n] = example_value(bid, n, p)
    return json.dumps(node, ensure_ascii=False, indent=2)


def md_escape(text):
    return str(text).replace("|", "\\|").replace("\n", " ")


# ----------------------------------------------------------------------------
# MD
# ----------------------------------------------------------------------------

def gen_md(groups, total_params, generated_at):
    L = []
    A = L.append
    A("# UIBrick UGUI 参数参考（AI 版）")
    A("")
    A("> 本文档由 `gen_brick_param_docs.py` 从 17 个 `ugui.*.definition.json` **自动生成**（%s），"
      "请勿手改；definition 变更后重跑生成脚本。" % generated_at)
    A("> 用途：为 AI 代理编写 / 校验 trees.json 的 `parameters` 提供权威参数面（含全部扩展参数与必要性分级）。")
    A("")
    tr = sum(level_stats(b)[L_REQUIRED] for g in groups for b in g["bricks"])
    tc = sum(level_stats(b)[L_RECOMMENDED] for g in groups for b in g["bricks"])
    to = total_params - tr - tc
    A("**参数总量**：%d 项 / 17 bricks（Controls %d · Layout %d · Container %d）——"
      "🔴 必传 %d · 🟡 建议 %d · 🟢 可选 %d" % (
          total_params,
          sum(len(b["parameters"] or {}) for g in groups if g["dir"] == "Controls" for b in g["bricks"]),
          sum(len(b["parameters"] or {}) for g in groups if g["dir"] == "Layout" for b in g["bricks"]),
          sum(len(b["parameters"] or {}) for g in groups if g["dir"] == "Container" for b in g["bricks"]),
          tr, tc, to))
    A("")

    # ---------------- reference-image pipeline guide ----------------
    A("## 参考图生 UI 对接指南（三级必要性契约）")
    A("")
    A("| 标记 | 级别 | 契约 |")
    A("|---|---|---|")
    A("| 🔴 必传 | REQUIRED | 中间大模型**必须**从设计稿/参考图提取写入 trees.json；缺失 = 占位文本、引擎白底图、尺寸被布局压扁/错乱 |")
    A("| 🟡 建议 | RECOMMENDED | 有引擎默认（通用风格）；参考图生 UI 场景通常应由中间大模型按设计稿覆盖 |")
    A("| 🟢 可选 | OPTIONAL | 微调/特效/过渡/运行态，省略安全 |")
    A("")
    A("**对接职责切分**：")
    A("")
    A("- **外部（参考图/图生图产物）提供的有限输入**：各 UI 元素的 Sprite 图片资产"
      "（对应 image_requirements.json 清单），以项目路径形式填入各 brick 的**资源类参数**（`*Sprite` / `spritePath` 等）。")
    A("- **中间大模型必须补齐**：文本内容（text/value）、颜色、字号、对齐、显式尺寸"
      "（width/height/spacing/cellSize/panelWidth…）、层级 sortingOrder、滚动内容布局 contentLayout 等——"
      "即全部 🔴 项与绝大多数 🟡 项。")
    A("- 每个 brick 章节末尾给出**最小参数集**（必传清单 + 建议清单 + 可直接复制的最小 trees.json 片段）。")
    A("- 各 brick 参数表按 **🔴 必传 → 🟡 建议 → 🟢 可选** 分组排列，同级内保持 definition 顺序——扫表即得『哪些必须传』。")
    A("")
    A("## trees.json 使用约定")
    A("")
    A("节点结构（`BrickNode`）：")
    A("")
    A("```json")
    A('{ "brickId": "ugui.button", "name": "btn_start", "slot": "ButtonGroup",')
    A('  "parameters": { "text": "开始游戏", "fontSize": 32, "borderRadius": 16 },')
    A('  "children": [ /* 子节点，结构同上 */ ] }')
    A("```")
    A("")
    A("- `parameters` 的键 = 本文档各表的**参数**列；类型 / 默认值照表；未传的参数走定义默认值。")
    A("- `slot`：显式挂载点（相对父 brick 根的路径）。省略时按推理规则：ScrollRect → `Viewport/Content`；"
      "根挂 LayoutGroup（stack/grid）→ 根；否则挂父根自身。modal 的子元素用 `ModalPanel/ModalHeaderSlot`、"
      "`ModalPanel/ModalContentSlot`、`ModalPanel/ModalFooterSlot`。")
    A("- **组合规则（双向 AND）**：子可挂到父下 ⇔ 子的 `canNestUnder` 含父 brickId **且** 父的 "
      "`allowedChildren` 含子 brickId。各 brick 的两表见其章节。")
    A("- `ugui.panel` 树根：构建（`BrickPanelBuilder.BuildBrickTrees`）时自动加 Canvas + CanvasScaler"
      "（ScaleWithScreenSize）+ GraphicRaycaster 并做防黑屏拉伸修复；`ugui.modal` 树根同样处理。")
    A("- **树规格字段（`BrickTreeSpec`，树根级而非 brick 参数）**：`sortingOrder`（Canvas 层级，缺省 10，"
      "惯例 10 全屏 / 20 弹窗 / 30 确认框）、`referenceWidth` / `referenceHeight`（参考分辨率，缺省 1920×1080，"
      "CanvasScaler=ScaleWithScreenSize + MatchWidthOrHeight 0.5）。仅 panel/modal 树根生效；"
      "树根保存后强制全屏拉伸，`width`/`height` 参数实际作用于嵌套 panel。")
    A("- 资源类参数（Sprite / Font / AnimatorController / AudioClip）传**项目内路径**"
      "（如 `Assets/Games/xxx/UI/Images/btn.png`、包内 `Icons/RoundedRectBorder`）。")
    A("")
    A("### handler 机制详解（机制列的 `handler:` 前缀）")
    A("")
    usage = handler_usage([b for g in groups for b in g["bricks"]])
    for title, body in HANDLER_MECH:
        A("- **%s**：%s" % (title, body))
    A("")
    A("| handler | 附加键 | 语义 | 典型参数（全 definitions 自动汇总） |")
    A("|---|---|---|---|")
    for name, desc in HANDLER_DOC.items():
        u = usage.get(name)
        extras = " / ".join(sorted(u["extras"])) if u and u["extras"] else "—"
        params = "、".join("`%s`" % x for x in u["params"][:6]) if u else ""
        if u and len(u["params"]) > 6:
            params += " …共%d项" % len(u["params"])
        A("| `%s` | %s | %s | %s |" % (name, extras, md_escape(desc), params or "—"))
    A("")
    unused = "、".join("`%s`" % h for h in HANDLER_DOC if h not in usage)
    if unused:
        A("> 未被任何 definition 引用的内建 handler（trees.json 无法触发，速查保留备查）：%s。" % unused)
        A("")
    A("> 另有 %d 个内建 handler 为占位 no-op（%s），同样无 definition 引用，不入速查表。" % (
        len(HANDLER_NOOPS), "、".join("`%s`" % h for h in HANDLER_NOOPS)))
    A("")
    A("`target:` 前缀 = 直接反射赋值表达式（`GetComponent<X>()` 在作用点解析；`GetComponentInChildren<X>()` "
      "取作用点下首个匹配；裸属性名写在作用点组件上）。")
    A("")

    # ---------------- per category ----------------
    for sec_no, g in enumerate(groups, start=2):
        A("## %d. %s" % (sec_no, g["title"]))
        A("")
        for brick in g["bricks"]:
            bid = brick["brickId"]
            params = brick.get("parameters") or {}
            st = level_stats(brick)
            A("### %s（%d 参数 · 🔴%d 🟡%d 🟢%d）" % (bid, len(params), st[L_REQUIRED], st[L_RECOMMENDED], st[L_OPTIONAL]))
            A("")
            A("- **brickId**：`%s` · **layer**：%s · **category**：%s%s" % (
                bid, brick.get("layer", ""), brick.get("category", ""),
                " · **assemblyOnly**（纯装配、无源 Prefab）" if brick.get("assemblyOnly") else ""))
            if "aiPriority" in brick or "aiSelectable" in brick:
                A("- **AI**：priority=%s selectable=%s" % (
                    brick.get("aiPriority", "—"), brick.get("aiSelectable", "—")))
            A("- **%s**" % brick_role(brick))
            cs = brick.get("childStructure")
            if cs:
                A("- **childStructure**（工厂自动创建的内部结构）：")
                for depth, label in child_structure_lines(cs):
                    A("  " + "  " * depth + "- " + label)
            A("")
            A("| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |")
            A("|---|---|---|---|---|---|---|---|")
            rows = [(n, p) + classify(bid, n, p) for n, p in params.items()]
            rows.sort(key=lambda r: _LEVEL_RANK[r[2]])
            for name, p, level, reason in rows:
                A("| %s | `%s` | %s | %s | %s | `%s` | %s | %s |" % (
                    LEVEL_LABEL[level],
                    md_escape(name),
                    md_escape(p.get("type", "")),
                    md_escape(fmt_default(p.get("default"))),
                    md_escape(scope_of(p)),
                    md_escape(mech_of(p)),
                    md_escape(reason),
                    md_escape(p.get("description", "")),
                ))
            A("")
            req_names = [n for n, p in params.items() if classify(bid, n, p)[0] == L_REQUIRED]
            rec_names = [n for n, p in params.items() if classify(bid, n, p)[0] == L_RECOMMENDED]
            A("- **最小参数集**：🔴 必传 → %s" % ("、".join("`%s`" % n for n in req_names) or "（无）"))
            A("  - 🟡 建议 → %s" % ("、".join("`%s`" % n for n in rec_names) or "（无）"))
            A("")
            A("  ```json")
            for line in minimal_snippet(brick).splitlines():
                A("  " + line)
            A("  ```")
            A("")
            pol = brick.get("compositionPolicy") or {}
            A("- **组合策略**：allowedChildren=%s · canNestUnder=%s" % (
                md_escape(json.dumps(pol.get("allowedChildren"), ensure_ascii=False)),
                md_escape(json.dumps(pol.get("canNestUnder"), ensure_ascii=False)),
            ))
            lc = brick.get("layoutContract") or {}
            modes = " · ".join(
                "%s: %s" % (k, "/".join(v2.get("widthMode", []) + v2.get("heightMode", [])))
                for k, v2 in (lc or {}).items())
            A("- **布局契约**：%s" % (modes or "—"))
            eb = brick.get("eventBindings") or {}
            if eb:
                A("- **事件绑定**：%s" % md_escape(json.dumps(eb, ensure_ascii=False)))
            es = brick.get("eventSchema") or {}
            if es:
                A("- **事件 Schema（ui_event）**：%s" % md_escape(json.dumps(es, ensure_ascii=False)))
            bp = brick.get("bindingProps") or {}
            if bp:
                A("- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：%s" % md_escape(
                    json.dumps(bp, ensure_ascii=False)))
            A("")
    A("---")
    A("")
    A("*生成时间：%s · 生成器：`Docs/gen_brick_param_docs.py`*" % generated_at)
    return "\n".join(L)


# ----------------------------------------------------------------------------
# HTML
# ----------------------------------------------------------------------------

HTML_CSS = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>UIBrick UGUI 参数参考</title>
<style>
  :root {
    --bg: #0f1117; --panel: #171a23; --panel2: #1d2130; --line: #2a2f42;
    --text: #dbe2f0; --muted: #8b93a8; --accent: #4ea1ff; --accent2: #7ee0a3;
    --warn: #ffb454; --danger: #ff6b6b; --code-bg: #12151f;
  }
  * { box-sizing: border-box; }
  html { scroll-behavior: smooth; }
  body { margin: 0; font-family: "Segoe UI", "Microsoft YaHei", sans-serif; background: var(--bg); color: var(--text); line-height: 1.65; display: flex; align-items: flex-start; }
  h1, h2, h3 { scroll-margin-top: 14px; }
  .wrap { flex: 1 1 auto; min-width: 0; max-width: 1180px; margin: 0 auto; padding: 32px 28px 80px; }
  h1 { font-size: 28px; margin: 0 0 6px; }
  h1 .ns { color: var(--accent); font-family: Consolas, monospace; }
  h2 { font-size: 20px; margin: 44px 0 12px; padding-bottom: 8px; border-bottom: 1px solid var(--line); }
  h3 { font-size: 16px; margin: 26px 0 8px; color: var(--accent2); font-family: Consolas, monospace; }
  .sub { color: var(--muted); margin-bottom: 20px; font-size: 14px; }
  .badge { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; margin-right: 6px; background: var(--panel2); border: 1px solid var(--line); color: var(--muted); }
  .badge.on { color: var(--accent2); border-color: var(--accent2); }
  .badge.warn { color: var(--warn); border-color: var(--warn); }
  .lv { font-family: Consolas, monospace; font-size: 12px; padding: 1px 7px; border-radius: 9px; white-space: nowrap; }
  .lv.req { color: #ff6b6b; border: 1px solid #ff6b6b; }
  .lv.rec { color: #ffb454; border: 1px solid #ffb454; }
  .lv.opt { color: #7ee0a3; border: 1px solid #3a5f46; }
  table { width: 100%; border-collapse: collapse; margin: 10px 0 14px; font-size: 13px; }
  th, td { padding: 7px 9px; border: 1px solid var(--line); text-align: left; vertical-align: top; }
  th { background: var(--panel2); color: var(--muted); font-weight: 600; white-space: nowrap; }
  td code, p code, li code { font-family: Consolas, monospace; background: var(--code-bg); padding: 1px 6px; border-radius: 4px; color: var(--accent2); font-size: 92%; }
  .card { background: var(--panel); border: 1px solid var(--line); border-radius: 10px; padding: 16px 18px; margin: 16px 0; }
  .card.tip { border-left: 3px solid var(--accent2); }
  pre { background: var(--code-bg); border: 1px solid var(--line); border-radius: 8px; padding: 14px 16px; overflow-x: auto; font-size: 13px; }
  a { color: var(--accent); text-decoration: none; }
  .sidenav { position: sticky; top: 0; flex: 0 0 320px; width: 320px; max-height: 100vh; overflow-y: auto; background: #13161f; border-right: 1px solid var(--line); padding: 16px 14px 26px; }
  .sidenav .brand { display: block; padding: 4px 10px 12px; margin-bottom: 8px; border-bottom: 1px solid var(--line); font-weight: 700; font-size: 14px; color: var(--text); }
  .sidenav .brand .ns { color: var(--accent); font-family: Consolas, monospace; }
  .sidenav .navsec { margin: 14px 2px 4px; padding: 0 8px; font-size: 11px; letter-spacing: 0.5px; color: #9aa8c0; font-weight: 600; }
  .sidenav a.navsec { display: block; }
  .sidenav a.navsec:hover { color: var(--accent); }
  .sidenav a.nl { display: block; padding: 4px 8px 4px 16px; border-radius: 6px; color: var(--text); font-size: 12px; font-family: Consolas, "Microsoft YaHei", monospace; white-space: normal; line-height: 1.5; overflow-wrap: break-word; }
  .sidenav a.nl:hover { background: var(--panel2); color: var(--accent); }
  .sidenav a.nl.active { background: var(--panel2); color: var(--accent); box-shadow: inset 2px 0 0 var(--accent); }
  .sidenav a.nl .cnt { float: right; color: #aab6cc; font-size: 11px; font-family: Consolas, monospace; }
  .sidenav a.nl .cnt .rc { color: #ff7b72; font-weight: 700; }
  @media (max-width: 1100px) {
    body { display: block; }
    .sidenav { display: none; }
    .wrap { max-width: none; padding: 20px 16px 60px; }
  }
  footer { margin-top: 60px; color: var(--muted); font-size: 12px; border-top: 1px solid var(--line); padding-top: 16px; }
  .mut { color: var(--muted); font-size: 12.5px; }
  .why { color: var(--muted); font-size: 12px; }
</style>
</head>
<body>
"""


HTML_NAV_JS = """<script>
(function () {
  var links = [].slice.call(document.querySelectorAll('.sidenav a.nl'));
  var targets = links.map(function (a) {
    return { a: a, el: document.getElementById(a.getAttribute('href').slice(1)) };
  }).filter(function (t) { return t.el; });
  function onScroll() {
    var cur = null;
    for (var i = 0; i < targets.length; i++) {
      if (targets[i].el.getBoundingClientRect().top <= 96) cur = targets[i];
      else break;
    }
    for (var i = 0; i < links.length; i++) links[i].classList.remove('active');
    if (cur) cur.a.classList.add('active');
  }
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();
})();
</script>"""


def gen_html(groups, total_params, generated_at):
    H = []
    W = H.append
    tr = sum(level_stats(b)[L_REQUIRED] for g in groups for b in g["bricks"])
    tc = sum(level_stats(b)[L_RECOMMENDED] for g in groups for b in g["bricks"])
    W(HTML_CSS)
    # ---- left navigation bar ----
    W('<nav class="sidenav">')
    W('<a class="brand" href="#top"><span class="ns">UIBrick</span> UGUI 参数参考</a>')
    W('<div class="navsec">总览</div>')
    W('<a class="nl" href="#pipeline">参考图生 UI 对接指南（三级必要性契约）</a>')
    W('<a class="nl" href="#usage">trees.json 使用约定</a>')
    W('<a class="nl" href="#handlers">handler 机制详解</a>')
    W('<a class="nl" href="#index">索引</a>')
    for sec_no, g in enumerate(groups, start=2):
        W('<a class="navsec" href="#sec-%d">%d. %s</a>' % (sec_no, sec_no, html.escape(g["title"])))
        for b in g["bricks"]:
            st = level_stats(b)
            cnt = "%d%s" % (len(b.get("parameters") or {}),
                            (' · <span class="rc">%d</span>' % st[L_REQUIRED]) if st[L_REQUIRED] else "")
            W('<a class="nl" href="#%s">%s<span class="cnt">%s</span></a>' % (
                b["brickId"].replace(".", "-"),
                html.escape(b["brickId"].split(".", 1)[1]), cnt))
    W('</nav>')
    W('<div class="wrap">')
    W('<h1 id="top"><span class="ns">UIBrick</span> UGUI 参数参考 '
      '<span class="badge on">17 bricks</span><span class="badge">%d 参数</span>'
      '<span class="lv req">🔴 必传 %d</span><span class="lv rec">🟡 建议 %d</span>'
      '<span class="lv opt">🟢 可选 %d</span></h1>' % (total_params, tr, tc, total_params - tr - tc))
    W('<div class="sub">面向开发者的参数手册：17 个 UGUI Brick（Controls / Layout / Container）的完整 '
      'trees.json 参数面，含全部扩展参数、三级必要性分级（参考图生 UI 对接契约）、组合策略与布局契约。'
      '参数表按 🔴 必传 → 🟡 建议 → 🟢 可选 分组排列（同级保持 definition 顺序）。'
      '本页由 <code>gen_brick_param_docs.py</code> 自动生成（%s），definition 变更后重跑。</div>' % generated_at)

    # ---- pipeline contract ----
    W('<h2 id="pipeline">参考图生 UI 对接指南（三级必要性契约）</h2>')
    W('<div class="card tip">')
    W('<table><tr><th>标记</th><th>级别</th><th>契约</th></tr>'
      '<tr><td><span class="lv req">🔴 必传</span></td><td><code>REQUIRED</code></td>'
      '<td>中间大模型<b>必须</b>从设计稿/参考图提取写入 trees.json；缺失 = 占位文本、引擎白底图、尺寸被布局压扁/错乱</td></tr>'
      '<tr><td><span class="lv rec">🟡 建议</span></td><td><code>RECOMMENDED</code></td>'
      '<td>有引擎默认（通用风格）；参考图生 UI 场景通常应由中间大模型按设计稿覆盖</td></tr>'
      '<tr><td><span class="lv opt">🟢 可选</span></td><td><code>OPTIONAL</code></td>'
      '<td>微调/特效/过渡/运行态，省略安全</td></tr></table>')
    W('<p><b>对接职责切分</b>：外部（图生图产物）提供有限输入 = 各元素 Sprite 图片（image_requirements 清单），'
      '以路径填入<b>资源类参数</b>；中间大模型补齐 = 文本、颜色、字号、对齐、显式尺寸、层级、滚动内容布局等'
      '（全部 🔴 与绝大多数 🟡）。每个 brick 附<b>最小参数集</b>与可直接复制的最小片段。</p>')
    W('</div>')

    # ---- usage ----
    W('<h2 id="usage">trees.json 使用约定</h2>')
    W('<div class="card tip">')
    W('<pre>BrickNode {\n  brickId, name, slot, parameters { ...本文档参数... }, children [ ... ]\n}\n\n'
      '构建：BrickPanelBuilder.BuildBrickTrees(treesPath, prefabDir)（经 execute_csharp_script 调用）</pre>')
    W('<ul>'
      '<li><b>slot</b> 省略时的挂载点推理：ScrollRect → <code>Viewport/Content</code>；根挂 LayoutGroup（stack/grid）→ 根；否则父根自身。'
      'modal 子元素显式用 <code>ModalPanel/ModalHeaderSlot|ModalContentSlot|ModalFooterSlot</code>。</li>'
      '<li><b>组合规则（双向 AND）</b>：子可挂父 ⇔ 子 <code>canNestUnder</code> 含父 <b>且</b> 父 <code>allowedChildren</code> 含子。</li>'
      '<li><b>panel / modal 树根</b>：构建时自动加 Canvas + CanvasScaler(ScaleWithScreenSize) + GraphicRaycaster + 防黑屏拉伸。</li>'
      '<li><b>树规格字段（BrickTreeSpec，树根级而非 brick 参数）</b>：<code>sortingOrder</code>（Canvas 层级，缺省 10，'
      '惯例 10 全屏 / 20 弹窗 / 30 确认框）、<code>referenceWidth/referenceHeight</code>（参考分辨率，缺省 1920×1080，'
      'MatchWidthOrHeight 0.5）。仅 panel/modal 树根生效；树根保存后强制全屏拉伸，<code>width/height</code> 参数实际作用于嵌套 panel。</li>'
      '<li><b>资源类参数</b>（Sprite / Font / AnimatorController / AudioClip）传项目内路径（<code>Assets/...</code> 或包内 <code>Icons/xxx</code>）。</li>'
      '<li><b>作用点列</b>：<code>根</code> = 参数作用在 brick 根上；<code>子:Name</code> = 作用在指定子物体（支持斜杠路径）。</li>'
      '<li><b>机制列</b>：<code>handler:Xxx</code> = BrickFactory 内建处理（机制详解见下节）；<code>target: ...</code> = 反射赋值表达式。</li>'
      '</ul>')
    W('</div>')

    W('<h3 id="handlers">handler 机制详解</h3>')
    usage = handler_usage([b for g in groups for b in g["bricks"]])
    W('<div class="card tip"><ul>')
    for title, body in HANDLER_MECH:
        W('<li><b>%s</b>：%s</li>' % (html.escape(title), html.escape(body)))
    W('</ul></div>')
    W('<table><tr><th>handler</th><th>附加键</th><th>语义</th><th>典型参数（全 definitions 自动汇总）</th></tr>')
    for name, desc in HANDLER_DOC.items():
        u = usage.get(name)
        extras = " / ".join(sorted(u["extras"])) if u and u["extras"] else "—"
        params = "、".join("<code>%s</code>" % html.escape(x) for x in u["params"][:6]) if u else ""
        if u and len(u["params"]) > 6:
            params += ' <span class="mut">…共%d项</span>' % len(u["params"])
        W('<tr><td><code>%s</code></td><td>%s</td><td>%s</td><td>%s</td></tr>' % (
            html.escape(name), extras, html.escape(desc), params or "—"))
    W('</table>')
    unused = "、".join("<code>%s</code>" % h for h in HANDLER_DOC if h not in usage)
    if unused:
        W('<div class="mut">未被任何 definition 引用的内建 handler（trees.json 无法触发，速查保留备查）：%s。</div>' % unused)
    W('<div class="mut">另有 %d 个内建 handler 为占位 no-op（%s），同样无 definition 引用，不入速查表。</div>' % (
        len(HANDLER_NOOPS), "、".join("<code>%s</code>" % h for h in HANDLER_NOOPS)))

    # ---- index table ----
    W('<h2 id="index">索引</h2>')
    W('<table><tr><th>brickId</th><th>类别</th><th>layer</th><th>参数数</th><th>🔴 必传</th><th>requiredComponents</th></tr>')
    for g in groups:
        for b in g["bricks"]:
            st = level_stats(b)
            W('<tr><td><a href="#%s"><code>%s</code></a></td><td>%s</td><td>%s</td><td>%d</td><td>%d</td><td>%s</td></tr>' % (
                b["brickId"].replace(".", "-"), html.escape(b["brickId"]),
                html.escape(b.get("category", "")), html.escape(b.get("layer", "")),
                len(b.get("parameters") or {}), st[L_REQUIRED],
                html.escape(", ".join(b.get("requiredComponents") or []))))
    W('</table>')

    # ---- per category ----
    for sec_no, g in enumerate(groups, start=2):
        W('<h2 id="sec-%d">%d. %s</h2>' % (sec_no, sec_no, html.escape(g["title"])))
        for b in g["bricks"]:
            bid = b["brickId"]
            params = b.get("parameters") or {}
            st = level_stats(b)
            W('<h3 id="%s">%s <span class="badge on">%d 参数</span>'
              '<span class="lv req">🔴%d</span><span class="lv rec">🟡%d</span><span class="lv opt">🟢%d</span>%s</h3>' % (
                  bid.replace(".", "-"), html.escape(bid), len(params),
                  st[L_REQUIRED], st[L_RECOMMENDED], st[L_OPTIONAL],
                  ' <span class="badge">assemblyOnly</span>' if b.get("assemblyOnly") else ""))
            ai_txt = ""
            if "aiPriority" in b or "aiSelectable" in b:
                ai_txt = " · AI priority=%s selectable=%s" % (
                    b.get("aiPriority", "—"), b.get("aiSelectable", "—"))
            W('<div class="mut">layer=%s · category=%s · %s%s</div>' % (
                html.escape(b.get("layer", "")), html.escape(b.get("category", "")),
                html.escape(brick_role(b)), ai_txt))
            cs = b.get("childStructure")
            if cs:
                W('<div class="mut"><b>childStructure</b>：<pre>')
                for depth, label in child_structure_lines(cs):
                    W(html.escape("  " * depth + "- " + label) + "\n")
                W('</pre></div>')
            W('<table><tr><th>必要性</th><th>参数</th><th>类型</th><th>默认值</th><th>作用点</th><th>机制</th><th>分级理由</th><th>说明</th></tr>')
            rows = [(n, p) + classify(bid, n, p) for n, p in params.items()]
            rows.sort(key=lambda r: _LEVEL_RANK[r[2]])
            for name, p, level, reason in rows:
                lv_cls = {"REQUIRED": "req", "RECOMMENDED": "rec", "OPTIONAL": "opt"}[level]
                lv_txt = {"REQUIRED": "🔴 必传", "RECOMMENDED": "🟡 建议", "OPTIONAL": "🟢 可选"}[level]
                W('<tr><td><span class="lv %s">%s</span></td><td><code>%s</code></td><td>%s</td><td>%s</td><td>%s</td>'
                  '<td><code>%s</code></td><td class="why">%s</td><td class="why">%s</td></tr>' % (
                      lv_cls, lv_txt, html.escape(name), html.escape(p.get("type", "")),
                      html.escape(fmt_default(p.get("default"))),
                      html.escape(scope_of(p)), html.escape(mech_of(p)), html.escape(reason),
                      html.escape(p.get("description", ""))))
            W('</table>')
            req_names = [n for n, p in params.items() if classify(bid, n, p)[0] == L_REQUIRED]
            rec_names = [n for n, p in params.items() if classify(bid, n, p)[0] == L_RECOMMENDED]
            W('<div class="card"><b>最小参数集</b>（参考图生 UI）<br>'
              '<span class="lv req">🔴 必传</span> %s<br>'
              '<span class="lv rec">🟡 建议</span> <span class="mut">%s</span>'
              '<pre>%s</pre></div>' % (
                  html.escape("、".join(req_names) or "（无）"),
                  html.escape("、".join(rec_names) or "（无）"),
                  html.escape(minimal_snippet(b))))
            pol = b.get("compositionPolicy") or {}
            W('<div class="mut"><b>组合策略</b>：allowedChildren=%s · canNestUnder=%s</div>' % (
                html.escape(json.dumps(pol.get("allowedChildren"), ensure_ascii=False)),
                html.escape(json.dumps(pol.get("canNestUnder"), ensure_ascii=False))))
            lc = b.get("layoutContract") or {}
            modes = " · ".join(
                "%s: %s" % (k, "/".join(v2.get("widthMode", []) + v2.get("heightMode", [])))
                for k, v2 in (lc or {}).items())
            W('<div class="mut"><b>布局契约</b>：%s</div>' % html.escape(modes or "—"))
            eb = b.get("eventBindings") or {}
            if eb:
                W('<div class="mut"><b>事件绑定</b>：%s</div>' % html.escape(json.dumps(eb, ensure_ascii=False)))
            es = b.get("eventSchema") or {}
            if es:
                W('<div class="mut"><b>事件 Schema</b>：%s</div>' % html.escape(json.dumps(es, ensure_ascii=False)))
            bp = b.get("bindingProps") or {}
            if bp:
                W('<div class="mut"><b>运行时绑定属性</b>（UIBindingBuilder 用，非 trees.json 参数）：%s</div>'
                  % html.escape(json.dumps(bp, ensure_ascii=False)))

    W('<footer>UIBrick UGUI 参数参考 · 生成自 17 个 ugui.*.definition.json · '
      '生成器：Docs/gen_brick_param_docs.py · '
      '生成时间 %s · 配套 AI 版：UIBrickParameters.md</footer>' % generated_at)
    W('</div>')
    W(HTML_NAV_JS)
    W('</body></html>')
    return "\n".join(H)


# ----------------------------------------------------------------------------
# main
# ----------------------------------------------------------------------------

def main():
    all_bricks = load_definitions()
    by_id = {b["brickId"]: b for b in all_bricks}

    groups = []
    for dirname, title, order in CATEGORY_ORDER:
        bricks = []
        for bid in order:
            if bid in by_id:
                bricks.append(by_id.pop(bid))
        group_dir = os.path.join(UGUI_ROOT, dirname)
        leftovers = [b for b in all_bricks
                     if b in by_id.values()
                     and os.path.dirname(_find_def_file(b["brickId"])).startswith(group_dir)]
        bricks.extend(sorted(leftovers, key=lambda x: x["brickId"]))
        groups.append({"dir": dirname, "title": title, "bricks": bricks})
    for b in sorted(by_id.values(), key=lambda x: x["brickId"]):
        groups.append({"dir": "Other", "title": "Other 其他", "bricks": [b]})

    total_params = sum(len(b.get("parameters") or {}) for g in groups for b in g["bricks"])
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

    os.makedirs(DOCS_DIR, exist_ok=True)
    md_path = os.path.join(DOCS_DIR, "UIBrickParameters.md")
    html_path = os.path.join(DOCS_DIR, "UIBrickParameters.html")
    with open(md_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(gen_md(groups, total_params, now))
    with open(html_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(gen_html(groups, total_params, now))

    print("bricks=%d params=%d" % (sum(len(g["bricks"]) for g in groups), total_params))
    tr = tc = to = 0
    for g in groups:
        for b in g["bricks"]:
            st = level_stats(b)
            tr += st[L_REQUIRED]
            tc += st[L_RECOMMENDED]
            to += st[L_OPTIONAL]
    print("levels: REQUIRED=%d RECOMMENDED=%d OPTIONAL=%d" % (tr, tc, to))
    for g in groups:
        print("  %-9s %2d bricks %3d params" % (
            g["dir"], len(g["bricks"]),
            sum(len(b.get("parameters") or {}) for b in g["bricks"])))
    print("written: %s" % md_path)
    print("written: %s" % html_path)


_DEF_FILE_CACHE = {}


def _find_def_file(brick_id):
    if brick_id in _DEF_FILE_CACHE:
        return _DEF_FILE_CACHE[brick_id]
    for dirpath, _dn, filenames in os.walk(UGUI_ROOT):
        for fn in filenames:
            if fn == brick_id + ".definition.json":
                _DEF_FILE_CACHE[brick_id] = os.path.join(dirpath, fn)
                return _DEF_FILE_CACHE[brick_id]
    return ""


if __name__ == "__main__":
    main()
