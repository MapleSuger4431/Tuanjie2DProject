# 功能控件内部结构与自定义样式

> 本文件是 ui-panel-builder SKILL.md 步骤 5（USS 初稿）和步骤 7（USS 补全）的配套参考。当面板含 Slider/Toggle/ProgressBar/DropdownField 功能控件时**必须读**本文件。

> Tuanjie 2022.3（基于 Unity 2022.3）的 `ui:Slider` 和 `ui:Toggle` 内部元素类名与 Unity 文档/旧版不同。Brick USS 已修正，但自定义样式时必须使用正确的类名。

## Slider 内部结构

```
Slider (.unity-base-slider)
  └── .unity-base-field__input / .unity-slider__input
       └── unity-drag-container (.unity-base-slider__drag-container)  ← track 背景（固定宽度）
            ├── unity-tracker (.unity-base-slider__tracker)           ← fill（宽度应随 value 变化）
            ├── unity-dragger-border (.unity-base-slider__dragger-border) ← handle 外框
            └── unity-dragger (.unity-base-slider__dragger)           ← handle 旋钮
```

类名映射：旧文档中的 `unity-slider__tracker` / `unity-slider__dragger` 在 Tuanjie 2022.3 中实际是 `unity-base-slider__tracker` / `unity-base-slider__dragger`。

### 三层图片分工

- `.unity-base-slider__drag-container` = track 图（固定背景条，不随 value 变化）
- `.unity-base-slider__tracker` = fill 图（随 value 缩放宽度的部分）
- `.unity-base-slider__dragger` = handle 图（旋钮，跟随 value 移动位置）

> **UIBrick `SliderBase.uss` 已内置全部通用属性**（`height`、`top`、`border`、`background-color`、`scale-mode`、`overflow: hidden` 等）。游戏 USS 需要写的部分：
> 1. **外层容器**：`position`/`left`/`top`/`width`/`height`（从设计文档布局百分比读取）
> 2. **三层 `background-image`**：track（`.unity-base-slider__drag-container`）、handle（`.unity-base-slider__dragger`）各设图片路径；fill 图设在 `.br-slider-fill-texture` 子元素上
> 3. **handle 尺寸**：`width`/`height`（从 `image_requirements.json` 的 `size_ratio` 或设计文档读取）。
> 4. **label 隐藏**：`> .unity-label { display: none; }`

> **fill 纹理不拉伸机制**：tracker 设有 `overflow: hidden` 充当遮罩，内部注入一个固定宽度（= 轨道宽度）的 `.br-slider-fill-texture` 子元素承载 fill 纹理。tracker 随 value 变宽/变窄时裁剪子元素，视觉上纹理不变形，如同 UGUI 的 Mask。游戏 USS 只需在 `.br-slider-fill-texture` 上设 `background-image`，定位/overflow/高度均由 Brick 基座处理。

fill 图是必需的：Slider 控件必须包含 track/fill/handle 三张图。缺 fill 图时 `.unity-base-slider__tracker` 透明，用户只能看到空 track + handle 位置，无法直观判断当前值占总量多少。若 UI设计文档.md §15 资产清单遗漏 fill 子部件，builder 步骤 1 应报告缺失并要求补齐。

**fill 不可降级为 `uss_only`**：`functional_type` 非空时 `source` 被前置转换强制设为 `ai_generate`（前置转换规格见 `modules/ui-image-generator/shared/adjudication-tables.md` 路径判定表），`uss_only` 不适用于功能控件子部件。fill 的正确处理路径：
- 参考图中 fill 可见 → Path C sprite sheet（默认）或 Path D img2img（回退）
- 参考图中 fill 不可见（如截图时 slider 值为 0）→ Path D 文生图（`visible_in_reference: false`、`crop_path: text2img_fallback`，prompt 从同组件可见部件推导风格）
- fill 与 track 视觉完全相同（同色同材质，无法区分）→ 不作为独立层提取，track 图在 USS 中同时充当 fill（给 `.br-slider-fill-texture` 设 `background-color` 纯色覆盖），不走 `uss_only`

若 AI 分析确定 fill 与 track 视觉相同而不提取 fill 层，可用 USS 纯色为 tracker 补充可见性：
```css
/* fill 与 track 视觉相同：纯色 tracker，颜色取自 UI设计文档.md §3 主题色 */
.{GameName}-slider .br-slider-fill-texture {
    background-color: rgba(255, 107, 157, 0.6);
}
```
此场景下 `image_requirements.json` 中不包含 fill 子部件条目。C# `SetupSliderFill` 仍必须执行。

### ⚠️ fill 宽度必须手动同步（Tuanjie 2022.3 已知缺陷）

> **不添加此代码 → tracker 始终 100% 填充 drag-container，不随 value 变化。** 这是引擎缺陷，覆盖 USS 无法修复。所有含 Slider 的面板必须在 C# Panel 脚本中调用 `SetupSliderFill`，无例外。

Tuanjie 2022.3 的 tracker 元素宽度不会自动随 slider value 变化。`UIPanelBase` 提供了 `SetupSliderFill(Slider)` 方法，封装了 tracker 宽度同步 + fill-texture 子元素注入 + 响应式宽度同步 + Track() 自动清理。在 `OnBind()` 中设置 slider 初始值后调用一次即可：

```csharp
// 在 OnBind() 的 SetupMusicSlider() / SetupSfxSlider() 中
slider.value = SaveSystem.GetFloat(key, defaultValue);
slider.RegisterValueChangedCallback(evt => { /* 存档 */ });
SetupSliderFill(slider);  // ← 一行搞定，无需手写 UpdateTrackerWidth
```

> **禁止**在游戏 Panel 脚本中手写 `UpdateTrackerWidth` / `RegisterValueChangedCallback` / `GeometryChangedEvent` 逻辑——这些已封装在 `UIPanelBase.SetupSliderFill` 中。

### dragger 尺寸

USS 中设 `width`/`height` 控制旋钮大小（百分比）。尺寸值必须从 `image_requirements.json` 的 `visual_layers` 中 `layer_name: "knob"`（或 `"handle"`）的 `size_ratio` 读取（`size_ratio.width`/`size_ratio.height` × 100 = 百分比值）。示例：`size_ratio: {"width": 0.12, "height": 0.85}` → `width: 12%; height: 85%;`。禁止从 UIBrick 默认值复制 px（如 `22px`）——那是未自定义时的占位尺寸，参考图模式下一律用 `size_ratio` 百分比。若 `image_requirements.json` 不含 `visual_layers`（无参考图模式），按 UI设计文档.md §11 尺寸占比或 AI 根据裁剪图视觉判断，仍用百分比。
同时需要让 dragger 垂直居中。dragger 是 `position: Relative` 的 flex 子元素，`top` 百分比不生效，正确写法是 `margin-top: auto; margin-bottom: auto;`。

### dragger-border 必须透明

`.unity-base-slider__dragger-border` 默认有边框，自定义时必须设 `background-color: rgba(0,0,0,0); border-width: 0;`。

### tracker 必须清 border

`.unity-base-slider__tracker` 默认有 1px 灰色边框（`rgb(100,100,100)`），仅设 `background-color: rgba(0,0,0,0)` 不够——残留的 border 会显示为横铺整条 track 的灰色矩形。必须同时设 `border-width: 0;`。Brick 基座已处理。

游戏 USS 中 tracker 只需清 border（overflow:hidden 由 Brick 处理）：

```css
.{GameName}-slider .unity-base-slider__tracker {
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
}
```

fill 纹理设在 `.br-slider-fill-texture` 子元素上：

```css
.{GameName}-slider .br-slider-fill-texture {
    background-image: url("Images/slider-fill.png");
}
```

## Toggle 内部结构

```
Toggle (.unity-toggle)
  └── .unity-base-field__input / .unity-toggle__input   ← track 背景
       └── unity-checkmark (.unity-toggle__checkmark)    ← knob（注意是 checkmark 不是 check）
```

knob 元素是 `.unity-toggle__checkmark`（不是 `.unity-toggle__check`）。checkmark 默认有深色背景，必须设 `background-color: rgba(0,0,0,0)` 清除默认色，否则自定义 knob 图片被深色覆盖。Brick 基座已处理。

### Toggle 两种模式与 variant 选择

Unity Toggle 有两种合法视觉模式，必须根据设计意图选择正确的 variant：

| 模式 | 视觉行为 | 适用场景 | UXML class |
|------|---------|---------|------------|
| **复选框式**（默认） | unchecked → knob 消失；checked → knob 显示 | 勾选确认、同意条款、开关复选项 | `br-toggle-base` |
| **滑动开关式** | knob 始终显示，unchecked 在左 / checked 在右 | 音效开关、音乐开关、设置项开关 | `br-toggle-base br-toggle-base--switch` |

滑动开关式必须加 `br-toggle-base--switch`——该 variant（位于 `ToggleBase.uss`）封装了 knob 滑动逻辑（`position: absolute` + `left: 0%` → `left: 50%` + `transition`）。不用 variant 而纯手写滑动 toggle 会导致 unchecked 时 knob 消失（Unity 默认 `display: none`），表现为"点击没反应"。

Tuanjie 2022.3 不支持 `:not(:checked)`（递归伪类报错），variant 内部用默认值 + `:checked` 覆盖实现双向状态。

```xml
<!-- 复选框式：只需 br-toggle-base -->
<ui:Style src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/USS/Primitive/Input/ToggleBase.uss" />
<ui:Toggle name="agree-toggle" class="br-toggle-base {GameName}-agree-toggle" />

<!-- 滑动开关式：br-toggle-base + br-toggle-base--switch -->
<ui:Toggle name="sfx-toggle" class="br-toggle-base br-toggle-base--switch {GameName}-sfx-toggle" />
```

### Toggle 自定义样式

游戏 USS 两种模式都只需设 track/knob 的 `background-image` 和 `background-scale-mode`，不需要手写滑动/显隐逻辑。滑动开关式的 `br-toggle-base--switch` 已内置默认 tint（unchecked 灰、checked 还原原图色），游戏只需在 `:checked` 覆盖主题色即可：

```css
.{GameName}-toggle .unity-toggle__input {
    background-color: rgba(0, 0, 0, 0);
    background-image: url("Images/toggle-bg.png");
    -unity-background-scale-mode: stretch-to-fill;
}
.{GameName}-toggle .unity-toggle__checkmark {
    background-color: rgba(0, 0, 0, 0);
    background-image: url("Images/toggle-knob.png");
    -unity-background-scale-mode: scale-to-fit;
}
/* 可选：checked 时用主题色 tint track */
.{GameName}-toggle:checked .unity-toggle__input {
    -unity-background-image-tint-color: rgba(255, 107, 157, 255);
}
```

`-unity-background-image-tint-color` 对 `background-image` 做乘法染色，不需要额外图片资源。Brick 默认 unchecked=`rgba(140,140,140,255)`（灰）、checked=`rgba(255,255,255,255)`（无色=原图）。tint 设在 `__input` 上仅影响 track 背景——`__checkmark`（knob）已显式设 `rgba(255,255,255,255)` 阻断继承，knob 图片始终显示原色。

Toggle 尺寸差异大（iOS 开关式、复选框、单选按钮等），不硬编码固定百分比，由 AI 根据裁剪图中控件的实际占比决定。用 `position: absolute` + 百分比定位在 shell 容器内。track 图片用 `stretch-to-fill`——药丸形 track 图片应铺满 `.unity-toggle__input`，`scale-to-fit` 会在宽高比不匹配时留透明边距导致视觉断裂。

### ⚠️ switch 变体 USS fallback（无图片纯色降级）必须清 background-image

> **不清 `background-image` → Unity 默认的 ✓ 图标残留在 knob 上，表现为 switch knob 带勾。**

滑动开关式（`br-toggle-base--switch`）在无图片纯色降级时，`.unity-toggle__checkmark` 用 `background-color` 当 knob 填充色。但 Unity 默认 Toggle 在 `__checkmark` 上有一张内置 ✓ 纹理（`background-image`），Brick base 只清了 `background-color` 和 `border`。Brick 的 `--switch` variant 已加 `background-image: none`（覆盖 base），但游戏自定义 USS 如果对 `__checkmark` 设了任何样式，CSS 层叠顺序可能导致 `background-image: none` 被覆盖回默认值。

**安全写法**：游戏 USS 对 switch 变体的 `__checkmark` 始终显式写 `background-image: none`（即使用图片模式也写 `none` 再用 `background-color` 或单独 `background-image` 属性覆盖）：

```css
/* USS fallback 纯色 switch toggle — 必须清 background-image */
.{GameName}-toggle .unity-toggle__checkmark {
    background-color: rgb(255, 138, 80);  /* knob 填充色 */
    background-image: none;                /* 清除 Unity 默认 ✓ 图标 */
    border-radius: 10px;
}
```

复选框式（不带 `--switch`）不需要清 `background-image`——checked 时显示 ✓ 是预期行为。

## ProgressBar 内部结构

ProgressBar 是纯展示控件（无 handle、无用户交互），比 Slider 更简单。fill 宽度由 Unity 自动同步 value，不需要 C# 手动控制。

```
ProgressBar (.unity-progress-bar) ← track 背景（固定宽度）  
    └── .unity-progress-bar__progress  ← fill（宽度自动随 value 变化）
```

### 两层图片分工

- **根元素**（游戏自定义 class，如 `.{GameName}-health-bar`）= track 图（固定背景条）
- `.unity-progress-bar__progress` = fill 图（宽度自动随 value 缩放）

> ⚠️ **track 图不能放在 `__background` 或 `__container` 上**，只能放在根元素上。`__background` 和 `__container` 由 UIBrick 已清除背景色和边框，放在它们上面会导致 track 图被 `__progress` 的默认行为遮盖或无法正确显示。

> **UIBrick `ProgressBar.uss` 已内置通用属性**（`border`、`background-color` 清零、`__progress` 的 `scale-mode` + `overflow: hidden`、`__title-container` 隐藏等），但**不包含 `height`**。游戏 USS 需要写的部分：
> 1. **根元素**：`position`/`left`/`top`/`width`/`height`（从设计文档布局百分比读取）+ `background-image`（track 图）+ `-unity-background-scale-mode: stretch-to-fill`
> 2. **`__progress`**：`background-image`（fill 图）+ **必须设置 `height` 为 100%** + `border-width: 0`（overflow:hidden 由 Brick 处理）
> 3. **`.br-progress-fill-texture`**：若 fill 纹理含图案/细节，将 `background-image` 从 `__progress` 移到 `.br-progress-fill-texture` 子元素上，避免宽度变化时纹理被拉伸（同 Slider 机制）。C# 中调用 `SetupProgressBarFill(bar)` 注入子元素

> ⚠️ **`__progress` 高度不撑满的 Tuanjie 2022.3 引擎缺陷（必须 C# 修复）**：ProgressBar 内部 `__container` 硬编码 24px 高度，USS 的 `height: 100%` / `flex-grow` / `position: absolute` 均无法覆盖，导致 `__progress` 和 `.br-progress-fill-texture` 只有 24px 而非根元素的实际高度。**必须在 C# `OnBind()` 中用 `progress.style.height = bar.layout.height` 设像素值，并监听 `GeometryChangedEvent` 在 bar 尺寸变化时同步**：

```csharp
var bar = SafeQ<ProgressBar>("hp-bar");
if (bar != null)
{
    SetupProgressBarFill(bar);
    var progress = bar.Q(className: "unity-progress-bar__progress");
    if (progress != null)
    {
        progress.style.height = bar.layout.height;
        bar.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            progress.style.height = bar.layout.height;
        });
    }
}
```

> **fill 纹理不拉伸机制（同 Slider）**：`__progress` 设有 `overflow: hidden` 充当遮罩，C# `SetupProgressBarFill(ProgressBar)` 注入一个固定宽度（= 进度条宽度）的 `.br-progress-fill-texture` 子元素承载 fill 纹理。`__progress` 随 value 变宽/变窄时裁剪子元素，视觉上纹理不变形。

与 Slider 的关键区别：ProgressBar 的 `__progress` 宽度由 Unity 内部自动同步 `value` 属性，无需 C# 注册 value callback。但 fill-texture 子元素的宽度仍需通过 `SetupProgressBarFill` 同步到进度条宽度（监听 `GeometryChangedEvent`）。

### ⚠️ fill-texture 子元素必须通过 C# 注入

ProgressBar 的 `__progress` 是 Unity 内部元素，无法在 UXML 中给它加子元素。必须在 C# Panel 脚本的 `OnBind()` 中调用 `SetupProgressBarFill(bar)`：

```csharp
var bar = SafeQ<ProgressBar>("hp-bar");
if (bar != null) SetupProgressBarFill(bar);
```

> 若 fill 纯色无图案（无需防拉伸），可跳过 `SetupProgressBarFill`，直接在 USS 中给 `__progress` 设 `background-color` 或 `background-image`。

数据绑定：`Bind(property)?.ToProgress("name")` 绑定 `BindableProperty<int>` 到 ProgressBar 的 value（仅 int 类型）。

## DropdownField 内部结构

```
DropdownField (.unity-base-popup-field)
  └── .unity-base-field__input / .unity-base-popup-field__input   ← 实际渲染背景的元素（默认灰色 bg + 1px border）
       ├── .unity-base-popup-field__text                           ← 文字
       └── .unity-base-popup-field__arrow                          ← 下拉箭头
```

### bg 图片必须设在 `__input` 上

DropdownField 的外层元素（`.unity-base-popup-field`）不渲染背景，实际背景由内部 `__input` 元素渲染。`__input` 默认有灰色背景（`rgb(149,149,149)`）和 1px border，必须在此元素上清除默认色并设 `background-image` + `border-width: 0`：

```css
/* ❌ 错误：设在外层，灰色 __input 覆盖在外层图片之上 */
.{GameName}-dropdown {
    background-image: url("Images/dropdown-bg.png");
}

/* ✅ 正确：设在 __input 上，清除默认色 + border */
.{GameName}-dropdown .unity-base-field__input {
    background-color: rgba(0, 0, 0, 0);
    background-image: url("Images/dropdown-bg.png");
    -unity-background-scale-mode: scale-to-fit;
    border-width: 0;
}
```

arrow 图片：`.unity-base-popup-field__arrow` 默认无背景图，直接设 `background-image` 即可。

### 展开后的 popup 挂载在 panel.visualTree

DropdownField 展开后的弹出列表（`.unity-base-dropdown` 及其子元素）挂载在 `panel.visualTree`（即 UIDocument `rootVisualElement` 的父级 panel root），而非 UXML 可视化树的后代。UXML `<ui:Style>` 引入的 USS 只级联到 UXML 根的后代元素，因此面板 USS 中对 `.unity-base-dropdown__container-inner` / `.unity-base-dropdown__item` 等选择器的样式规则无法到达 popup，导致展开后的列表始终显示为 Unity 默认灰色样式。

修复在 UIGameInstaller 中统一处理（步骤 12），不在每个 Panel 的 OnBind() 中重复。在 `OnPanelPushed` 回调中将面板 USS 样式表添加到 `panel.visualTree`。此时 Panel 已完全挂载到 UIDocument 树，`panel.GetRoot().panel` 必然可用：

```csharp
// 在 UIGameInstaller 中订阅 OnPanelPushed，面板入栈时统一修复 DropdownField popup USS
private void OnPanelPushed(UIPanelBase panel)
{
    var panelRoot = panel?.GetRoot()?.panel?.visualTree;
    if (panelRoot != null)
    {
        for (int i = 0; i < panel.GetRoot().styleSheets.count; i++)
        {
            var ss = panel.GetRoot().styleSheets[i];
            if (!panelRoot.styleSheets.Contains(ss))
                panelRoot.styleSheets.Add(ss);
        }
    }
}
```

此代码放在 UIGameInstaller 的 `OnPanelPushed` 回调中（在 `Start()` 里 `uiManager.OnPanelPushed += OnPanelPushed` 注册）。无需每个 Panel 重复——所有含 DropdownField 的面板入栈时都会自动修复。面板无 DropdownField 时此代码无害（仅做冗余 USS 添加，panel root 会自动去重）。`panel.visualTree` 是运行时概念，USS 的级联范围在 UXML 解析时就已固定为可视化树后代，此问题无法通过纯 USS/UXML 解决。

### DropdownField 展开态 USS 选择器规则

DropdownField 展开时 Unity 内部生成以下层级：

```
.unity-base-dropdown                    ← 全屏点击捕获 overlay，MUST 保持透明
  .unity-base-dropdown__container-outer  ← 布局容器，透明
    .unity-base-dropdown__container-inner ← 实际下拉列表区域，background-image 放这里
      .unity-base-dropdown__item          ← 各选项
      .unity-base-dropdown__checkmark     ← 选中标记
```

`expanded_panel_bg` 图片的 `background-image` 必须放在 `.unity-base-dropdown__container-inner` 上，禁止放在 `.unity-base-dropdown` 上——后者是全屏 overlay，放背景图会铺满整个屏幕。`DropdownBase.uss` Brick 已内置此规则（根元素和 container-outer 强制透明），游戏 USS 只需覆盖 `__container-inner` 的 `background-image`。

> ⚠️ **expanded panel 必须使用九宫格（9-slice）**：展开态面板高度不固定（取决于选项数量），`scale-to-fit` 会在宽高比不匹配时留透明边距或裁切，导致边框断裂。必须用 `-unity-background-scale-mode: stretch-to-fill` + `-unity-slice-*` 属性，四角和边框在拉伸时保持不变形，只拉伸中间内容区。slice 值取图片边框宽度的像素数（通常 8-16px）。

### ⚠️ popup USS 选择器必须 unscoped（硬门禁）

DropdownField 展开后的 popup 挂载在 `panel.visualTree`（panel root），**不是 DropdownField 元素的后代**。即使 UIGameInstaller 的 `OnPanelPushed` 已将面板 USS 添加到 `panel.visualTree`（使 USS 规则可达 popup），**后代选择器仍然不命中**——因为 USS 后代选择器 `.a .b` 要求 `.b` 在可视化树中是 `.a` 的后代，而 popup 与 DropdownField 是兄弟关系（同在 panel root 下），不存在祖先-后代关系。

因此 popup 相关的 USS 选择器 **必须去掉游戏前缀，使用全局选择器**：

```css
/* ❌ 错误：scoped 后代选择器——popup 不是 .mygame-dropdown 的后代，永远不命中 */
.mygame-dropdown .unity-base-dropdown__container-inner {
    background-color: rgba(74, 124, 78, 0.9);
    background-image: url("Images/dropdown-expanded-bg.png");
    -unity-background-scale-mode: stretch-to-fill;
    -unity-slice-left: 12;
    -unity-slice-right: 12;
    -unity-slice-top: 12;
    -unity-slice-bottom: 12;
    border-width: 2px;
    border-radius: 8px;
    border-color: rgb(212, 168, 67);
}
.mygame-dropdown .unity-base-dropdown__item {
    color: rgb(255, 255, 255);
    font-size: 20px;
}

/* ✅ 正确：unscoped 全局选择器——直接匹配 panel root 下的 popup 元素 */
.unity-base-dropdown__container-inner {
    background-color: rgba(74, 124, 78, 0.9);
    background-image: url("Images/dropdown-expanded-bg.png");
    -unity-background-scale-mode: stretch-to-fill;
    -unity-slice-left: 12;
    -unity-slice-right: 12;
    -unity-slice-top: 12;
    -unity-slice-bottom: 12;
    border-width: 2px;
    border-radius: 8px;
    border-color: rgb(212, 168, 67);
}
.unity-base-dropdown__item {
    color: rgb(255, 255, 255);
    font-size: 20px;
}
```

**受影响的选择器清单**——以下选择器在游戏 USS 中一律使用 unscoped 写法（不加 `.{GameName}-dropdown` 前缀）：

| 选择器 | 作用 |
|--------|------|
| `.unity-base-dropdown` | 全屏 overlay，保持透明 |
| `.unity-base-dropdown__container-outer` | 布局容器，保持透明 |
| `.unity-base-dropdown__container-inner` | 实际下拉列表背景 |
| `.unity-base-dropdown__item` | 各选项文字颜色/字号 |
| `.unity-base-dropdown__item:hover` | hover 态 |
| `.unity-base-dropdown__item:checked` | 选中态 |
| `.unity-base-dropdown__checkmark` | 选中标记 |

> **注意**：unscoped 选择器是全局生效的——同一游戏中所有 DropdownField 的 popup 会共享同一套 popup 样式。这在实践中是合理的（一个游戏通常只有一种 dropdown 展开风格）。
