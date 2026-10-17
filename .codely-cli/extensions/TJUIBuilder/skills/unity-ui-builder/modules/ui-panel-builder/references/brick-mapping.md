# Brick 映射规则

> 本文件是 ui-panel-builder SKILL.md 步骤 1 的配套参考，包含 UIBrick 映射的详细规则。步骤 1 执行 Brick 映射时读取本文件。

## ⛔ UIBrick 可用性硬规则

UIBrick 随 `cn.tuanjie.create.ui` 包内置，**必然存在于项目中**。禁止以任何理由判定"UIBrick 未部署"或"UIBrick 目录不存在"。

### 物理路径解析（必读，避免路径搜索浪费轮次）

包通过 `file:../` 引用，物理文件不在 `Packages/` 目录下。**`read_file`、`glob`、`search_file_content` 均无法通过 `Packages/cn.tuanjie.create.ui/...` 访问物理文件**。

**物理路径固定为**：`{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/`

示例（项目根 `E:\UnityProjects\UIBuilder\uibuildersample`）：
- BrickIndex: `E:\UnityProjects\UIBuilder\uibuildersample\.codely-cli\extensions\TJUIBuilder\Packages\cn.tuanjie.create.ui\Runtime\UIBrick\BrickIndex.ugui.json`
- BrickCheatsheet: `E:\UnityProjects\UIBuilder\uibuildersample\.codely-cli\extensions\TJUIBuilder\Packages\cn.tuanjie.create.ui\Runtime\UIBrick\BrickCheatsheet.ugui.md`

> ⚠️ **C# 脚本中的 `AssetDatabase.LoadAssetAtPath` 路径不同**：Unity 运行时通过虚拟路径 `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/...` 访问包内资源（Unity 自动解析 `file:../` 引用）。`exec_editor_script` 中的 C# 代码必须使用 `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/` 前缀，不能使用物理路径。

**路径速查表**：

| 用途 | 路径前缀 |
|------|---------|
| `read_file` / `glob` 读取 Brick 文件 | `{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/` |
| `AssetDatabase.LoadAssetAtPath` 加载 Brick Prefab | `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/` |

## UIBrick 信息来源（按优先级）

1. **若 UI设计文档.md §2 存在 UIBrick 实现候选信息**：优先读取其中的 Brick 覆盖评估和 assetPropsSchema 槽位信息（§2.1），直接获取每个组件对应的 Brick 映射，无需重新读取 BrickIndex.json
2. **若 UI设计文档.md §2 不存在或不包含 UIBrick 信息**：用 `read_file` 读取 Brick 速查卡（物理路径 `{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/BrickCheatsheet.md`）获取每个 Brick 的 assetSlots/styleProps/bindingProps/compositionPolicy/events，无需逐个读 definition.json

映射时需检查 BrickIndex 中每个 Brick 的 `definitionPath`、`snippetPath`、`stylePath` 三者是否齐全——缺一则该 Brick 不可选。用 `read_file` 读取 `{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/BrickIndex.json` 验证三件套路径。**"齐全" = 索引有声明且物理文件真实存在**：BrickIndex 声明了路径 ≠ 文件已落地（库更新可能改名/新增 definition 但 UXML/USS 尚未跟上），需用 `glob` 在物理路径 `.../Runtime/UIBrick/` 下确认对应 `Definitions/`、`UXML/`、`USS/` 文件存在；索引声明了但文件缺失的 Brick 同样不可选，该组件改走手写 UXML/USS 分支并在映射表标注原因。> ⚠️ 此三件套校验仅适用于 UI Toolkit 模式。UGUI 模式使用 `prefabPath` + `definitionPath` 二件套（`prefabPath` 缺失是库内置的正常降级场景，见下方「Brick Prefab 加载策略」），无 `snippetPath`/`stylePath`，详见下方「UGUI 模式 Brick 映射」区。

## brickId 命名格式

两层统一使用三段式 `{layer}.{category}.{name}`（如 `primitive.input.button_base`、`layout.stack.vertical`）。映射时以 BrickIndex.json 中的 `brickId` 字段为准，不以文件名推断 brickId。

## 映射表字段

映射表必须包含以下字段（有对应 Brick 时）：

```
| 界面 | 组件 | brickId | assetSlots（来自 assetPropsSchema） | 对应 image_requirements 条目 | 有对应 Brick？ |
|------|------|---------|-----------------------------------|---------------------------|--------------|
| 主菜单 | 开始按钮 | primitive.input.button_base | iconAsset | — (无图标) | ✅ |
| 主菜单 | 标题标签 | primitive.text.label | — | — | ✅ |
| 主菜单 | 页面根容器 | — | — | main_menu_bg | ❌ 手写 |
| HUD | 血条 | — | — | health_bar_frame | ❌ 需手写 |
```

## 映射结果处理

- **有对应 Brick**：步骤 6 引用 Brick 组件（简单引 USS + 内联；复杂 Template/Instance）
- **无对应 Brick**：步骤 6 手写 UXML/USS。**此路径是正式允许的主流程分支**，不是异常兜底
- **浮动元素**：角落/边缘的独立图标按钮，标记为「无 Brick + 浮动」，步骤 6 用 `position: absolute` 放在页面根容器直接子级。⚠️ 判定：对照参考图，若组件 X%/Y% 接近 0（屏幕边缘）则为浮动元素
- **UIBrick 是组件库非页面模板**：页面根容器由手写控制（UI Toolkit 模式手写 USS；UGUI 模式面板根为 Canvas + RectTransform stretch fill），不使用 Layout/Container 级 Brick 作为页面根入口。但 UGUI 模式**面板内部应优先使用 Layout Brick**（`category=layout`）组织子元素结构，而非全部手动设 RectTransform anchor。Container Brick（`category=container`）用于面板背景、列表条目和弹窗壳体
- **`assetSlots`**：从 `definition.json` 的 `assetPropsSchema` 提取，关联到 `image_requirements` 条目名。选了 brick 就确定了图片。无图片槽位标 `—`
- **`bindingPropsSchema`**：从 `definition.json` 读取可绑定字段及 runtimeType，用于步骤 10 Panel 脚本生成
- 映射结果沿管道传递，步骤 6 和 7 直接查表，**禁止推断**

## functional_type → Brick 映射

当 `image_requirements.json` 中条目的 `functional_type` 非 `null` 时，优先按功能控件映射到交互 Brick，而非按 `component_type` 映射到视觉 Brick：

| `functional_type` | 映射 brickId | UXML 元素 | 子部件引用方式 |
|---|---|---|---|
| `slider` | `primitive.input.slider_base` | `<ui:Slider>` | `{name}_track` → `.unity-base-slider__drag-container` 背景；`{name}_fill` → `.br-slider-fill-texture` 子元素背景（⚠️ Brick 基座已设 `overflow: hidden` 遮罩 + `SetupSliderFill(slider)` 在 C# 中注入子元素并同步 tracker 宽度，代码模板见 `functional-control-styles.md`「fill 宽度必须手动同步」）；`{name}_handle` → `.unity-base-slider__dragger` 背景 |
| `toggle` | `primitive.input.toggle_base` | `<ui:Toggle>` | `{name}_track` → `.unity-toggle__input` 背景；`{name}_knob` → `.unity-toggle__checkmark` 背景 |
| `dropdown` | `primitive.input.dropdown_base` | `<ui:DropdownField>` | `{name}_bg` → `.unity-base-field__input` 背景；`{name}_arrow` → `.unity-base-popup-field__arrow` 背景；`{name}_expanded_panel_bg` → `.unity-base-dropdown__container-inner` 背景（⚠️ 背景图必须贴在 `__container-inner` 上，禁止贴在 `.unity-base-dropdown`——它是全屏 overlay） |
| `text_field` | 手写 | `<ui:TextField>` | `{name}_bg` → TextField 元素 `background-image` |
| `progress_bar` | `primitive.input.progress_bar` | `<ui:ProgressBar>` | `{name}_track` → 根元素背景；`{name}_fill` → `.br-progress-fill-texture` 子元素背景（width 由 Unity 自动同步 value，无需 value callback；⚠️ track 图必须放根元素，不能放 `__background` 或 `__container`；`__progress` 必须设 `height`；fill 纹理含图案时须在 C# 调 `SetupProgressBarFill(bar)` 防拉伸，纯色 fill 可直接设 `__progress` 的 `background-color`） |
| `other` | 手写 | 按 AI 分析的 layer 结构手写 | 按 AI 分析返回的 `layer_name` 逐层映射到子元素 `background-image` |
| `null` | 按 `component_type` 正常映射 | — | — |

**子部件查表规则**：功能性控件的子部件（`sub_part` 非 `null`）在 `image_requirements.json` 中有独立条目。步骤 7 USS 补全时，通过 `functional_type` 和 `sub_part` 查表确定 USS 子元素选择器和背景图片路径，**禁止推断**。

**⚠️ `reuse_from` 复用规则**：当条目 `crop_path` 为 `reuse_from:{代表组件name}` 时，该条目不生成独立图片文件。USS 中必须引用代表组件的图片文件名（`Images/{代表组件name}.png`），而非条目自身 `name`。例如 `sfx-volume-slider-fill` 的 `crop_path` 为 `reuse_from:volume_slider-fill` → USS 写 `url("Images/volume_slider-fill.png")`。此规则同时适用于普通组件和功能控件子部件。

## Dropdown 展开态 USS 选择器关键规则

DropdownField 展开时 Unity 内部生成以下层级：

```
.unity-base-dropdown                    ← 全屏点击捕获 overlay，MUST 保持透明
  .unity-base-dropdown__container-outer  ← 布局容器，透明
    .unity-base-dropdown__container-inner ← 实际下拉列表区域，background-image 放这里
      .unity-base-dropdown__item          ← 各选项
      .unity-base-dropdown__checkmark     ← 选中标记
```

⚠️ **`expanded_panel_bg` 图片的 `background-image` 必须放在 `.unity-base-dropdown__container-inner` 上**，禁止放在 `.unity-base-dropdown` 上——后者是全屏 overlay，放背景图会铺满整个屏幕。`DropdownBase.uss` Brick 已内置此规则（根元素和 container-outer 强制透明），游戏 USS 只需覆盖 `__container-inner` 的 `background-image`。

⚠️ **popup USS 选择器必须 unscoped（硬门禁）**：DropdownField 展开后的 popup 挂载在 `panel.visualTree`，不是 DropdownField 元素的后代。后代选择器（如 `.{GameName}-dropdown .unity-base-dropdown__container-inner`）永远不命中。popup 相关选择器（`.unity-base-dropdown__container-inner`、`.unity-base-dropdown__item` 等）必须去掉游戏前缀，使用全局选择器。详见 `functional-control-styles.md` DropdownField 章节。

---

## UGUI 模式 Brick 映射

> 以下规则仅适用于 `ui_mode == "ugui"`。UI Toolkit 模式使用上方规则。

### UGUI 映射信息来源

UGUI 模式使用独立的 UGUI Brick 体系，与 UI Toolkit Brick 完全分离。不查 `snippetPath`/`stylePath`（UXML/USS 路径），改查以下文件：

- **索引文件**：`BrickIndex.ugui.json`（非 `BrickIndex.json`），包含全部 UGUI Brick 条目（数量随版本变化，以索引文件为准）。每条含 `brickId`、`category`、`prefabPath`、`definitionPath`、`requiredComponents`、`childStructure`、`bindingProps`、`eventBindings`
- **速查卡**：`BrickCheatsheet.ugui.md`，包含每个 Brick 的 hierarchy、parameters、bindingProps、compositionPolicy 和用例
- **定义文件**：`UGUI/{Controls,Layout,Container}/Definitions/ugui.*.definition.json`（BrickIndex.ugui.json 条目的 `definitionPath` 字段即相对 `Runtime/UIBrick/` 的完整子路径，已含 `UGUI/Controls|Layout|Container/Definitions/` 前缀）

用 `read_file` 读取上述文件（物理路径以 `{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/` 为根）。

> ⚠️ **brickId 命名差异**：UGUI Brick 使用 `ugui.{name}` 命名（如 `ugui.button`、`ugui.label`、`ugui.slider`），与 UI Toolkit 的 `primitive.*` / `layout.*` 独立。UGUI 模式映射表中 brickId 必须使用 `ugui.*` 前缀。

### 映射表字段（UGUI 模式）

映射表应同时列出交互控件（`category=input/text/image` 等）和 Layout/Container 级 Brick（`category=layout/container`）。以下为格式示例（brickId 和 prefabPath 以 BrickIndex.ugui.json 为准）：

```
| 界面 | 组件 | brickId | prefabPath | requiredComponents | image_requirements 条目 | functional_type |
|------|------|---------|-----------|-------------------|------------------------|-----------------|
| 主菜单 | 按钮组容器 | {brickId} | {prefabPath} | {requiredComponents} | — | null |
| 主菜单 | 背景容器 | {brickId} | {prefabPath} | {requiredComponents} | — | null |
| 主菜单 | PlayButton | {brickId} | {prefabPath} | {requiredComponents} | button_style_1 | null |
| 主菜单 | ScoreLabel | {brickId} | {prefabPath} | {requiredComponents} | — | null |
| HUD | 底部栏容器 | {brickId} | {prefabPath} | {requiredComponents} | — | null |
| HUD | VolumeSlider | {brickId} | {prefabPath} | {requiredComponents} | slider_track, slider_fill, slider_handle | slider |
```

### UGUI functional_type → 组件映射

| `functional_type` | UGUI 组件 | 子部件图片赋值方式 |
|---|---|---|
| `slider` | `Slider` | `{name}_track` → Slider 根 Image.sprite；`{name}_fill` → fillRect Image.sprite；`{name}_handle` → handleRect Image.sprite |
| `toggle` | `Toggle` | `{name}_track` → Toggle 根 Image.sprite；`{name}_knob` → checkmark Image.sprite |
| `dropdown` | `Dropdown` | `{name}_collapsed_bg` → Dropdown 根 Image.sprite；`{name}_expanded_panel_bg` → Template 内 Image.sprite |
| `text_field` | `InputField` | `{name}_bg` → InputField 根 Image.sprite |
| `progress_bar` | `Slider`(readonly) | `{name}_track` → Slider 根 Image.sprite；`{name}_fill` → fillRect Image.sprite |
| `other` | 按 AI 分析手动构建 | 按 AI 分析返回的 `layer_name` 逐层映射 |
| `null` | 按 `component_type` 正常映射 | — |

### Brick Prefab 加载策略（库内置，skill 无需处理）

`BrickFactoryEditor.CreateWithParams` / `BrickPanelBuilder.BuildBrickTrees` 内部：非 `assemblyOnly` 的 brick 从 Prefab 实例化；`assemblyOnly: true` 的 brick（panel、list_item、modal）直接从 definition 构建——两种情况产出等价，skill 无需区分。

详细构建规则和代码模板见 `ugui-prefab-construction.md`。
