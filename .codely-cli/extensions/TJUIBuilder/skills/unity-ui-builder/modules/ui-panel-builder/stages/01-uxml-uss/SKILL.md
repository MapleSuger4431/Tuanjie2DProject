# Stage 01: UXML/USS 构建

> ⛔ **模式守卫**：本 Stage 仅适用于 `ui_mode == "ui_toolkit"`。`ui_mode == "ugui"` 时走 `stages/01-ugui-prefab/SKILL.md`。

Steps 1-9：Brick 映射 → PanelSettings → 图片验证 → USS 初稿 → UXML 布局 → USS 补全 → 部署 → 验证 → 写状态文件。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `UI设计文档.md`、`image_requirements.json` |
| 输出 | `brick-mapping.md`、`GamePanelSettings.asset`、UXML/USS 文件（context + Assets）、`ui-panel-builder-state.json` |
| 前置门禁 | UI设计文档.md 存在；image_requirements.json 存在 |
| 完成判据 | UIV.Validate 零错误 + ui-panel-builder-state.json 已写入（含完整元素清单） |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |
| 必须激活技能 | unity-operator——PanelSettings/UXML/USS 资产创建与 reimport 经 execute_csharp_script；subagent 第 0 步先激活再读文档 |

> **共享契约**：框架守卫、路径守卫、UIGameInstaller 门禁、gameId namespace、Panel 注册名约定、状态文件 schema、阶段路由规则见 `shared/contracts.md`。执行本阶段前应已读取。

## 本阶段核心规则

1. **UXML/USS 先写入 context/games/{GameName}/UI/，再部署到 Assets/Games/{GameName}/UI/**
2. **UI 图片由 ui-image-generator 生成**（UI图片生成阶段独立执行），本阶段步骤 3 验证图片已存在。禁止自行判定"简约风格=纯USS"跳过图片。禁止将参考图裁剪件或原始参考图直接复制/重命名到 `Images/` 目录——步骤 4 通过文件哈希比对检测此违规
3. **首次执行必须顺序执行全部步骤**。中断恢复时检查已有产出文件，从首个缺失产出的步骤继续（规则见下方「中断恢复」章节）
4. **UIBrick 组件引用优先**：生成页面 UXML 时优先引用 `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/` 中的 Brick 组件（简单 Brick 引 USS + 内联结构；复杂 Brick Template/Instance），禁止复制 Brick UXML 结构到游戏页面。无对应 Brick 时才手写，手写产物落到 `Assets/Games/{GameName}/UI/`。UIBrick 基础库禁止 AI 修改——AI 只读引用，不写入。如需新组件，写到 `Assets/Games/{GameName}/UI/CustomUIBrick`
5. **全屏面板背景检查**：如果某面板的 `panel_type` 为 `full_screen`（如 MainMenuPanel），其 `image_requirements` 中必须包含至少一个 `component_type: "background"` 的条目。若 UI设计文档.md §12.2 未识别出该面板的背景组件，自动补一条文生图背景需求：`name: {PanelName}_bg`、`component_type: background`、`is_segmentation: false`、`source: "ai_generate"`。在 UXML 中作为面板根容器的第一个子元素，USS 设 `position: absolute; left:0; top:0; width:100%; height:100%`

## 起草优先原则

1. 读完步骤规则即可动笔，实现细节边写边查 reference
2. 拿不准的先按最合理写法写，步骤 9 validate 兜底
3. 连续 3 轮只读不写 = 过度准备，立即起草

## 通用模式：验证修复循环

| 验证场景 | 验证方式 | 修复方式 | 最大重试 |
|---------|---------|---------|----------|
| USS/UXML 验证（步骤 9） | `UIV.Validate()` | `replace` 修复 + 重新部署 | 7 |

**通用流程**：验证 → 有错误则修复 → 重新验证 → 循环直到零错误或达到最大重试。

> Panel 脚本编译验证不在此阶段范围内：Panel 脚本不做 RoslynValidator 预检，部署到 Assets 后由宿主提供的 C# 编译部署闭环统一兜底。

## 中断恢复

首次执行必须顺序执行全部步骤。中断后重入时，检查已有产出文件，从首个缺失/无效产出的步骤继续：

| 检查项 | 产出文件 | 存在则跳到 |
|--------|---------|-----------|
| 步骤 1 完成 | `context/games/{GameName}/ui-panel-builder/brick-mapping.md` | 步骤 2 |
| 步骤 2 完成 | `Assets/Games/{GameName}/UI/GamePanelSettings.asset` | 步骤 3 |
| 步骤 5/6 完成 | `context/games/{GameName}/UI/*.uss` + `*.uxml` | 步骤 7 |
| 步骤 7.5 通过 | .uss 数 = .uxml 数，前缀一一匹配 | 步骤 7.6 |
| 步骤 7.6 通过 | UXML 含 `project:Packages/...UIBrick/` 引用 + `br-` class | 步骤 8 |
| 步骤 8 完成 | `Assets/Games/{GameName}/UI/*.uss` + `*.uxml` | 步骤 9 |
| 步骤 9 通过 | validate 零错误 | 写 state file（完成） |

---

## 步骤索引

| 步骤 | 名称 | 输入 | 产出 |
|------|------|------|------|
| 1 | Brick 映射 | UI设计文档.md, image_requirements.json | brick-mapping.md |
| 2 | 创建 PanelSettings | UI设计文档.md §1 设计分辨率 | GamePanelSettings.asset |
| 3 | 验证 UI 图片 | image_requirements.json, Images/*.png | 校验报告 |
| 4 | 图片完整性校验 | 步骤 3 报告 | 校验报告 |
| 5 | 创建 USS 初稿 | brick-mapping.md, UI设计文档.md §3 | {PanelName}Panel.uss |
| 6 | 创建 UXML 布局 | brick-mapping.md, USS 初稿 | {PanelName}Panel.uxml |
| 7 | UXML→USS 完整性补全 | UXML, brick-mapping.md | 补全后 USS |
| 7.5 | USS 独立性验证 | context 侧 USS/UXML | 通过/回退 |
| 7.6 | UIBrick 合规验证 | brick-mapping.md, BrickIndex.json, UXML/USS | 通过/回退 |
| 8 | 部署 UXML/USS | context 侧 UXML/USS | Assets 侧 UXML/USS |
| 9 | 验证 USS/UXML + 写状态文件 | Assets 侧 UXML/USS | 验证报告 + ui-panel-builder-state.json |

### 步骤1：阅读 UI设计文档.md + Brick 映射

1. 阅读 UI设计文档.md（`context/games/{GameName}/UI设计文档.md`）。若文件不存在，向调用方报错并中止。若用户请求的面板不在 UI设计文档.md 的界面清单中，向调用方报错"该面板未在 UI设计文档.md 中登记，请先执行 `ui-doc-builder` Step 0"并中止
2. 阅读 UI设计文档.md §9（游戏事件与 UI 面板触发映射）与 §7（DI 注册清单），了解游戏逻辑脚本暴露的事件接口与服务。从 `UI设计文档.md` §6 数据绑定关系获取绑定信息
3. 按需查 UI 框架 API：签名不确定时查 `references/UIFrame-API.md`，仍不确定再查 `Packages/cn.tuanjie.create.ui/Runtime/Panel/UIPanelBase.cs` 源码（含 Bind/BindButton/SafeQ/Push/Pop/Switch/Resolve/Track 等方法签名）
4. **验证 HUD 数据源**：对 HUD 需要绑定的每个 BindableProperty，确认对应游戏脚本已暴露公共访问器/事件
5. 确认每个界面需要绑定哪些 BindableProperty，需要哪些 BindButton
6. 规划任务清单：每个界面 = 1 个 UXML + 1 个面板 USS。对于 HUD 面板，步骤 9 需在 state file 中标记 `is_hud: true`（Stage 2 据此选择 `GameUIControllerBase` 而非 `UIPanelBase`）
7. **读取图生图配置**：检查 UI设计文档.md 是否包含 §12 图生图配置章节：
   - **若存在**（参考图模式）：读取 §12.1 参考图路径映射、§12.2 组件裁剪映射表（列集以 ui-style-reference `references/output-format.md` 为权威：含 `display_text`、`保真度`、`text_type`、`override_dimensions` 等；值为 `skip_generation` 的组件是纯文字标签，不生成图片）、§13 文生图回退风格参数（若有）。游戏内容区域清单 `game_area_list` 为文档级字段（定义见 output-format.md），不是 §12.2 的逐组件列
   - **若不存在**（无视觉参考）：按 UI设计文档.md §3 的样式描述生成
8. **读取 image_requirements.json**：读取 `context/games/{GameName}/image_requirements.json`（由 `ui-doc-builder` Step 3 产出）。若文件不存在，向调用方报错"ui-doc-builder Step 3 未完成（image_requirements.json 缺失）"并中止。JSON 字段定义读 `modules/shared/references/image_requirements_schema.md`。
   - `component_type` 是图片生成的分类维度，**不是** Brick 的 `assetPropsSchema` 槽位名——两者是不同维度，映射关系通过 Brick 映射表建立
   - `functional_type` 非 `null` 时优先按 `references/brick-mapping.md` 的 functional_type → Brick 映射表映射到交互 Brick
   - `sub_part`：功能控件子部件标识，步骤 7 通过此字段确定 USS 子元素选择器和背景图片
   - **OpenCV 组件树布局**：若条目含 `parent_component` / `parent_uxml_element`、`depth`、`render_mode`、`shell_path`、相对父级 X/Y/W/H% 与根 X/Y/W/H% 字段，按父子树生成 parent container + shell/background + 零 padding content layer。子元素只用相对父级百分比定位。缺少这些可选字段时保持旧文档行为
   - **全屏面板背景检查**：见本阶段核心规则 5
9. **Brick 映射**（必须执行）：将 UI设计文档.md 中每个 UI 组件映射到砖块。读 `references/brick-mapping.md` 获取映射规则、brickId 命名格式、映射表字段。映射结果沿管道传递，步骤 6 和 7 直接查表，禁止推断图片对应关系。
10. **产出 brick-mapping.md**：将映射结果写入 `context/games/{GameName}/ui-panel-builder/brick-mapping.md`，格式：

```markdown
# Brick Mapping — {GameName}

| 组件名 | brickId | assetSlots | image_requirements 条目 | functional_type |
|--------|---------|------------|------------------------|-----------------|
| play-button | primitive.input.button_base | iconAsset | button_style_1 | null |
| volume-slider | primitive.input.slider_base | — | slider_track, slider_fill, slider_handle | slider |
```

### 步骤2：创建 PanelSettings 资产

> 设计分辨率来源：UI设计文档.md §1 的「设计分辨率」字段，未指定时回退到 `1920×1080`。

使用 `execute_csharp_script` 创建 PanelSettings（将 `refWidth` / `refHeight` 替换为 UI设计文档.md 中的设计分辨率）：

```csharp
// 从 UI设计文档.md 读取的设计分辨率，默认回退 1920×1080
int refWidth = 1920;
int refHeight = 1080;

var panelSettings = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
panelSettings.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
panelSettings.referenceResolution = new UnityEngine.Vector2Int(refWidth, refHeight);
panelSettings.screenMatchMode = UnityEngine.UIElements.PanelScreenMatchMode.MatchWidthOrHeight;
panelSettings.match = 0.5f;
var dir = "Assets/Games/{GameName}/UI";
if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
UnityEditor.AssetDatabase.CreateAsset(panelSettings, $"{dir}/GamePanelSettings.asset");
UnityEditor.AssetDatabase.SaveAssets();
```

### 步骤3：验证 UI 图片存在

> UI 图片由 ui-image-generator 独立生成，本步骤只验证图片已存在。

1. `glob Assets/Games/{GameName}/UI/Images/*.png` 列出已存在的图片文件
2. 对每张 `image_requirements` 目标图用 AQ 验证 `TextureImporter.textureType == Sprite`、mipmap 关闭；任一不是 Sprite 视为后处理未完成，回退 ui-image-generator Stage 03 后处理修复
3. 对照步骤 1 的 `image_requirements` 列表，逐项检查每张标记为 `"ai_generate"` 的图片是否存在
4. **缺失图片处理**：记录缺失列表并在输出中明确报告，由 orchestrator 决定是否重做 1ui。不中止后续步骤——继续处理有图片的面板，缺失图片的 USS 规则用占位 `/* TODO: 图片 {name} 缺失，待 1ui 补齐 */` 标注
5. 若 `image_requirements` 为空，跳过本步骤

### 步骤4：图片完整性校验

步骤 3 确认图片存在后，必须校验图片的后处理是否已执行（由 ui-image-generator Stage 03 完成）。若步骤 4 报告图片缺失，本步骤仅校验已存在的图片。仅当所有图片都缺失时才中止。

通过 `execute_csharp_script` **一行调用部署型工具**：`Create.GameFramework.TJValidateAssets.Images();`（自动扫描所有 `Assets/Games/*/UI/Images/` 目录；无需读取/重发脚本全文）。

**校验内容**（缺一不可）：
1. **文件存在性**：每张图片是否已生成到 `Assets/Games/{GameName}/UI/Images/`
2. **TextureImporter 设置**：`textureType == Sprite`、`mipmapEnabled == false`、`spriteImportMode == Single`
3. **尺寸合理性**：图片任一维度不应 ≥ 2000px
4. **来源合法性（防直接复制）**：比对 `Images/` 下每张图片与 `context/games/{GameName}/UIReferences/` 下所有文件的 SHA256 哈希。排除 `source=direct_use` 的图片（合法的 File.Copy 产出）。仅对 `source=ai_generate` 的图片执行哈希比对
5. **透明 padding（Warning）**：不透明内容占画布 < 70%（任一维度）→ 图案缩在元素一角、与文字/布局错位。修法：裁到紧贴不透明内容再用

**校验失败处理**：后处理未执行或缺图片 → 报错并中止，提示调用方 ui-image-generator Stage 03 的后处理步骤未正确执行

### 步骤5：创建 USS 样式文件（初稿）

> USS 编写规范读 `references/uitk/uss-reference.md`（禁止属性、`background-size` 决策表、颜色格式、路径规则、弹窗/按钮布局规则）。

使用 `write_file` 写入 `context/games/{GameName}/UI/` 目录：

**USS 文件组织规则（强制 — 每面板独立，防止类名冲突）**：

一个UXML文件对应一个USS文件。
每个面板拥有独立的 USS 文件 `{PanelName}Panel.uss`，**完全自包含**——包含该面板所需的全部样式规则。禁止创建共享 USS 文件（如 `GameStyle.uss`）——USS 没有 namespace 机制，共享 USS 中同名 class 会互相覆盖。

- 类名命名约定：类名应包含面板标识前缀（如 `{GameName}-main-menu-btn` 而非 `{GameName}-menu-btn`）
- 按 UI设计文档.md §3 的视觉设计编写样式。若读取到 §3.1 布局参数，优先使用其布局参数和 §13 回退组件风格表
- **引用 Brick USS，不复制规则**：简单 Brick 的 USS 通过页面 UXML 中的 `<ui:Style>` 引入；复杂 Brick 的 USS 由 Brick UXML 自引用。游戏 USS 只负责页面布局、主题差异、图片资源引用和样式覆盖，不重写 Brick 基础样式规则
- **禁止 USS 隐藏按钮文字**：当按钮 `text` 属性非空时，USS 禁止使用 `color: rgba(0, 0, 0, 0)` / `font-size: 0` / `display: none` / `visibility: hidden` / `opacity: 0` 隐藏文字
- **USS 布局定位属性用 %**：`left`/`top`/`width`/`height`/`padding`/`margin` 必须使用百分比。例外：`border-width`、`border-radius`、`font-size`、`transition` 数值可用 px。UI设计文档.md §12.2 的 `X%`/`Y%`/`宽%`/`高%` 列已经是百分比值，直接写入 USS
- **单面板栈限制**：`UIFrame` 的 `Push()` 会隐藏当前栈顶面板，不支持多面板同时显示（如 HUD + 暂停菜单叠加）。需要 HUD 始终可见时：方案 A — 将 HUD 元素直接放在游戏面板的 UXML 中（推荐）；方案 B — 使用独立 UIDocument 渲染 HUD。读 `references/UIFrame-API.md` 适用范围表
- 完成后不单独部署——步骤 8 统一部署

> 此步骤产出的 USS 是初稿，步骤 7 中通过 UXML→USS 完整性校验补全。

### 步骤6：创建 UXML 布局文件

> Brick 组合规则读 `references/uitk/uxml-brick-composition.md`（Brick 组合约束、布局契约、Button text、纯文字标签、图片路径映射、变体 class、slot 容器、OpenCV 组件树容器结构）。

使用 `write_file` 写入 `context/games/{GameName}/UI/` 目录：
- 每个界面一个 UXML 文件，命名为 `{PanelName}Panel.uxml`
- 引用步骤 5 创建的 USS 样式表
- 所有需要在代码中访问的元素必须设置 `name` 属性

**页面 UXML 引用 Brick USS + 面板私有 USS**：

```xml
<ui:Style src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/USS/Primitive/Input/ButtonBase.uss" />
<ui:Style src="{PanelName}Panel.uss" />
```

Brick USS/UXML 位于 Package 内，引用时使用 `project:` 前缀。面板私有 USS 用相对路径（同目录直接用文件名）。禁止 `Assets/...` 开头——会被拼接为 `{UXML目录}/Assets/...`。

**从 UIBrick 砖块引用组合 UXML**：

步骤 1 的 Brick 映射结果（brick-mapping.md）指导本步骤的 UXML 编写。按 Brick 类型选择引用方式：

**简单 Brick（单元素）** — 内联元素 + `<ui:Style>` 引用 USS：

```xml
<ui:Button name="play-button" class="br-button-base br-button-base--md {GameName}-action-btn" text="开始游戏" />
```

**复杂 Brick（多元素/多 slot）** — `<ui:Template>` + `<ui:Instance>` 引用 UXML：

```xml
<ui:Template src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/UXML/Layout/Modal/ModalShell.uxml" name="ModalShell" />
<ui:Instance template="ModalShell" name="quit-modal" class="{GameName}-quit-modal" />
```

页面结构由 UI设计文档 决定。AI 从 Primitive 粒度的砖块自由组合。页面根容器由手写 USS 控制。

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:Style src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/USS/Primitive/Input/ButtonBase.uss" />
    <ui:Style src="MainMenuPanel.uss" />
    <ui:Template src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/UXML/Layout/Modal/ModalShell.uxml" name="ModalShell" />

    <ui:VisualElement name="MainMenuRoot" class="{GameName}-main-menu-root">
        <ui:VisualElement name="Background" class="{GameName}-main-menu-bg" />
        <ui:VisualElement name="CenterContent" class="{GameName}-center-content">
            <ui:Label name="TitleLabel" class="br-label br-label--xl {GameName}-title" text="MY GAME" />
            <ui:Button name="play_button" class="br-button-base br-button-base--md {GameName}-action-btn" text="播放" />
        </ui:VisualElement>
        <ui:Instance template="ModalShell" name="settings-modal" class="{GameName}-settings-modal" />
    </ui:VisualElement>
</ui:UXML>
```

**对无对应 Brick 的组件**：直接手写该部分的 UXML/USS。此路径是正式允许的主流程分支。禁止将手写的页面结构回写为正式 Brick。

**功能性控件 UXML（`functional_type` 非 `null`）**：当步骤 1 Brick 映射通过 `functional_type` 映射到交互 Brick 时，UXML 使用真实的交互控件元素：

| `functional_type` | UXML 元素 | 说明 |
|---|---|---|
| `slider` | `<ui:Slider name="{name}" class="br-slider-base {GameName}-{name}" low-value="0" high-value="100" />` | 引用 SliderBase USS |
| `toggle` | `<ui:Toggle name="{name}" class="br-toggle-base {GameName}-{name}" />` | 引用 ToggleBase USS |
| `dropdown` | `<ui:DropdownField name="{name}" class="br-dropdown-base {GameName}-{name}" />` | 引用 DropdownBase USS |
| `text_field` | `<ui:TextField name="{name}" class="{GameName}-{name}" />` | 手写 |

功能控件的内部结构、USS 选择器、C# 同步代码模板读 `references/uitk/functional-control-styles.md`。该文件按控件分章（`##` 标题），**按面板实际使用的控件读对应章节即可**，无需全读。

**functional_type 控件必须创建 shell 容器**：当 `functional_type` 非 null 的组件的 OpenCV 裁剪包含非控件视觉元素（如标签文字区域、行背景底板）时，UXML 必须创建一个 `VisualElement` shell 容器匹配裁剪尺寸，再将控件放在 shell 内部适当位置和大小。禁止将控件直接等同于整个裁剪区域。

**浮动元素**：步骤 1 标记为「浮动」的元素（角落图标按钮等）使用 `position: absolute` + `top`/`left` 定位，作为页面根容器直接子级（与全屏背景同级），禁止放在面板 flex 布局内。

**HUD 面板根容器必须透明**：HUD 叠加面板的根容器和全屏背景元素禁止使用不透明的 `background-image` 或 `background-color`，否则会完全遮挡 Camera 渲染的游戏画面。HUD 根容器在 C# 中设 `Root.pickingMode = PickingMode.Ignore` 让点击穿透（USS `picking-mode` 在 Tuanjie 2022.3 不支持）；HUD 中需要交互的子元素在 C# 中设 `pickingMode = PickingMode.Position` 恢复点击响应。

**游戏内容区域**：`game_area_list` 中列出的区域使用空 VisualElement，不引用 background-image，`background-color` 强制 `rgba(0,0,0,0)`，仅保留边框装饰。

完成后不单独部署——步骤 8 统一部署。

### 步骤7：UXML→USS 完整性校验与补全

步骤 6 产出的 UXML 中使用了游戏自定义 USS 类（非 `br-*` 前缀），这些类必须在 USS 中有对应的样式定义。本步骤系统性检查并补全缺失的 USS 规则。

**核心原则**：每个自定义类的用途和图片引用已在步骤 1 的 brick-mapping.md 中确定，本步骤查表填写，禁止推断。

**执行流程**：

1. **提取 UXML 自定义类清单**：扫描步骤 6 生成的所有 UXML 文件，提取 `class` 属性中的所有 USS 类名，过滤掉 `br-*` 前缀，得到自定义类集合
2. **查表关联图片**：对每个自定义类，通过 brick-mapping.md 获取其用途和图片引用。禁止通过元素 name 语义、类名关键词或文件名匹配来推断
3. **交叉比对 USS**：读取步骤 5 创建的 USS 文件，检查清单中的每个自定义类是否已有对应的 USS 规则定义
4. **补全缺失规则**：对 USS 中缺失的自定义类，根据查表获得的用途和图片信息生成对应的 USS 规则，追加到 USS 文件末尾：
   - **有图片引用的类**：`background-color: rgba(0, 0, 0, 0);` + `background-image: url("Images/xxx.png")` + `-unity-background-scale-mode`（值选择读 `references/uitk/uss-reference.md` §3 决策表）
   - **布局容器类**（查表确认为无图片）：`position`、`width`/`height`、`background-color`、`border-radius`、`padding` 等布局属性（从 UI设计文档.md 读取）
   - **查表无结果的自定义类**（罕见，可能是步骤 1 遗漏）：添加空规则注释 `/* TODO: 步骤1映射遗漏，补充样式 */`

   **功能性控件子部件 USS 补全**：当元素有 `functional_type` 时，USS 需为 Unity 控件的内部子元素应用对应的子部件背景。通过 `functional_type` 和 `sub_part` 查 `image_requirements` 获取图片文件名。内部元素结构、USS 选择器、Tuanjie 2022.3 类名读 `references/uitk/functional-control-styles.md` 对应控件章节。

   **⚠️ `reuse_from` 图片路径映射（硬门禁）**：当 `image_requirements.json` 条目的 `crop_path` 为 `reuse_from:{代表组件name}` 时，该条目不生成独立图片文件。USS 中必须使用**代表组件的图片文件名**（即 `reuse_from:` 后面的 name 对应的 `Images/{name}.png`），而非条目自身的 `name`。例如 `sfx-volume-slider-fill` 的 `crop_path` 为 `reuse_from:volume_slider-fill`，则 USS 应写 `url("Images/volume_slider-fill.png")`，禁止写 `url("Images/sfx-volume-slider-fill.png")`。此规则同时适用于 `functional_type` 子部件和普通组件。

   **⚠️ 无参考图模式功能控件可见性兜底（强制，禁止跳过）**：当 `image_requirements.json` 中功能控件子部件的 `source` 为 `uss_only` 或 `image_requirements.json` 中完全缺失该功能控件的子部件条目时，必须用 USS 纯色为每个子元素补充可见样式。缺失会导致 Slider/Toggle/Dropdown 在运行时隐形——控件存在于 Visual Tree 中但肉眼不可见。兜底模板（`<主题色>` / `<强调色>` / `<前景色>` 从 `style_guidance.style_baseline` 或 UI设计文档.md §3 配色方案推导）：

   **功能控件子部件尺寸规则**：当 `functional_type` 非空的控件需要为内部子元素设 `width`/`height` 时，必须从 `image_requirements.json` 的 `visual_layers` 数组读取对应 `layer_name` 的 `size_ratio`，将 `size_ratio.width`/`size_ratio.height`（0..1）乘以 100 转为百分比写入 USS。禁止从 UIBrick 默认值复制 px。若 `image_requirements.json` 无 `visual_layers`（无参考图模式）：按 UI设计文档.md §11 尺寸占比或 AI 根据裁剪图视觉判断，仍用百分比。

   **⚠️ DropdownField popup 选择器硬门禁**：当面板含 DropdownField 时，popup 展开态的 USS 选择器（`.unity-base-dropdown__container-inner`、`.unity-base-dropdown__item` 等）**必须 unscoped**——禁止加 `.{GameName}-dropdown` 前缀。popup 挂载在 `panel.visualTree`，不是 DropdownField 的后代，scoped 后代选择器永远不命中。详见 `references/uitk/functional-control-styles.md` DropdownField 章节。

5. **图片路径规范**：图片路径相对于 USS 文件所在目录：`url("Images/xxx.png")`。图片文件名从步骤 3 验证结果中获取，禁止凭记忆猜测路径——必须对照 `Images/` 目录下实际存在的文件名。**`reuse_from` 条目**：不生成独立文件，USS 引用代表组件的图片文件名（`crop_path` 中 `reuse_from:` 后面的 name），非条目自身 `name`
6. **更新 context USS**：使用 `replace` 更新 context 中的 USS 文件。步骤 8 统一部署到 Assets
7. **HUD 根容器透明度自检**：对每个 Playing 态常驻的 HUD 类面板，核对其根容器及根 background 元素的 USS：`background-color` 必须 `rgba(0,0,0,0)`、不得设 `background-image`。这是编辑期自检。

**完整示例**读 `references/uitk/uss-completeness-example.md`。

### 步骤7.5：USS 独立性验证（部署前）

部署前检查 `context/games/{GameName}/UI/` 目录，禁止进入步骤 8：

1. `.uss` 文件数必须等于 `.uxml` 文件数
2. 每个 `.uss` 文件名前缀必须与某个 `.uxml` 文件名前缀一一匹配（如 `MainMenuPanel.uss` ↔ `MainMenuPanel.uxml`）
3. 禁止存在 `{GameName}.uss`、`shared.uss` 等共享文件名

不满足时禁止进入步骤 8，回退到步骤 5/6 重新生成 USS。

### 步骤7.6：UIBrick 合规验证（禁止跳过）

> 本步骤是部署前的强制自验闸门，确保 UXML 正确引入了 Brick USS 并使用 Brick class 名。跳过此步骤会导致 Brick 基础样式缺失，运行时控件显示为 Unity 默认外观。

**数据源**：brick-mapping.md（步骤 1 产出）中所有"有对应 Brick？= ✅"的条目 + 用 `read_file` 读取 `{projectRoot}/.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/BrickIndex.json`。

对每个面板的 .uxml 文件依次执行以下检查：

#### 检查 A：Brick USS / UXML 引用完整性（硬门禁）

对 brick-mapping.md 中每个"有对应 Brick？= ✅"的条目：

1. **判定层级**：brickId 以 `primitive.` 开头 = 简单 Brick；`layout.` 开头 = 复杂 Brick
2. **简单 Brick**：从 BrickIndex.json 查该 brickId 的 `stylePath`（如 `USS/Primitive/Input/ButtonBase.uss`），用 `search_file_content`（`fixed_strings: true`）在该面板的 .uxml 文件中搜索路径片段 `project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/USS/Primitive/Input/ButtonBase.uss`。**0 匹配 = 失败**
3. **复杂 Brick**：从 BrickIndex.json 查该 brickId 的 `snippetPath`（如 `UXML/Layout/Modal/ModalShell.uxml`），搜索路径片段 `project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/UXML/Layout/Modal/ModalShell.uxml`。**0 匹配 = 失败**

> 同一 Brick 被多个面板使用时，每个面板的 UXML 都必须独立引入。

#### 检查 B：Brick class 名使用（硬门禁）

用 `search_file_content`（pattern `class="[^"]*br-`）在每个 .uxml 文件中搜索 `br-` class 引用。

判定规则：
- brick-mapping.md 中该面板有至少 1 个"有对应 Brick？= ✅"的条目 → 该面板 UXML 中 `br-` 匹配数必须 ≥ 1。**0 匹配 = 失败**
- brick-mapping.md 中该面板全部为"有对应 Brick？= ❌"（纯手写）→ 跳过，不检查

> 游戏 USS 中不出现 `br-` 是正常的——游戏 USS 使用游戏自定义类名（如 `.{GameName}-slider`）做覆盖，Brick 基础样式由 Brick USS 通过 UXML 元素上的 `br-*` class 提供。USS 中 `.unity-` 子元素选择器（如 `.unity-base-slider__tracker`）在 Brick USS 已导入的前提下是正确的子部件定制方式（见 `references/uitk/functional-control-styles.md`），不判为违规。

#### 检查 C：原生 selector 滥用检测（软警告）

用 `search_file_content`（pattern `^\.unity-(base-slider|toggle|button|base-dropdown|progress-bar)`，`multiline: true`）扫描每个 .uss 文件。

- **命中**：说明 USS 在行首使用 unscoped 的原生选择器直接覆盖控件基础样式，而非通过 Brick class 体系 → **软警告**
- **例外**：以下两类不算违规：
  1. DropdownField popup 选择器（`.unity-base-dropdown__container-inner`、`.unity-base-dropdown__item` 等）步骤 7 明确要求 unscoped
  2. Slider/Toggle/Dropdown 的**内部子元素选择器**（`.unity-base-slider__tracker`、`.unity-base-slider__dragger`、`.unity-toggle__input`、`.unity-toggle__checkmark`、`.unity-base-popup-field__input`、`.unity-base-popup-field__text` 等）在 Brick USS 已导入的前提下是正确的子部件定制方式（见 `references/uitk/functional-control-styles.md`），不算违规

> 软警告不阻断流程，但必须在输出中报告，供调用方审查。如果检查 A 和 B 都通过，此警告通常是误报（agent 在覆盖 Brick 未覆盖的子元素）。

#### 失败处理

| 检查 | 失败原因 | 修复动作 | 回退到 |
|------|---------|---------|--------|
| A | UXML 缺少 Brick `<ui:Style>` 或 `<ui:Template>` | 在 UXML `<ui:UXML>` 根下补充对应声明 | 步骤 6 |
| B | UXML 元素 class 属性缺少 `br-*` 类名 | 在对应元素 class 中添加 Brick class（brickId 最后一段 `_` 替换为 `-`，加 `br-` 前缀） | 步骤 6 |

修复后必须重新执行步骤 7→7.5→7.6 完整链路，禁止仅重跑 7.6。

### 步骤8：部署 UXML/USS 文件

使用 `run_shell_command` 将 context 中的 UI 文件部署到 Assets。部署后必须使用 `execute_csharp_script` 执行以下操作：

1. 新增/变更文件的逐资产导入（`ImportAsset`——工具内部执行，**不调用全局 `AssetDatabase.Refresh()`**，见 unity-operator 横切红线）
2. **Touch + 强制重新导入所有 USS 文件** — USS 导入器在首次导入时解析 `url()` 引用并缓存结果。如果 USS 先于图片部署到 Assets，`url()` 引用会缓存为 null，运行时显示为黄色警告三角形。

通过 `execute_csharp_script` **一行调用部署型工具**：`Create.GameFramework.TJValidateAssets.ReimportUss();`（自动扫描所有 `Assets/Games/*/UI/` 目录，执行 touch + ForceUpdate 逐文件导入 + 反射验证 `url()` 解析）。

> 仅 `AssetDatabase.Refresh()` 或仅 `ImportAssetOptions.ForceUpdate` 都不够——USS 导入器在文件内容未变化时可能跳过 `url()` 重新解析。脚本通过 touch 文件（追加无害注释改变内容）再 ForceUpdate。

### 步骤9：验证 USS/UXML 文件 + 写状态文件

> GameDoctor 检查项清单读 `references/game-doctor-checks.md`（CheckPanelNameConsistency / CheckSerializedFieldDrift / CheckOrphanShow / CheckPanelReachability / CheckUIOcclusion / CheckInstantDeath）；GameDoctor 属外部 extension，未安装时跳过这些检查。

部署到 Assets 后，必须执行验证脚本：

```csharp
UIV.Validate("Assets/Games/{GameName}/UI")
```

`UIV` 是 `cn.tuanjie.create.ui` 包（`Packages/cn.tuanjie.create.ui/Editor/Query/UIV.cs`，命名空间 `Create.Utilities`）提供的静态验证工具类。

**检查输出并执行验证修复循环**：
- `[USS ERROR]` / `[UXML ERROR]` → 使用 `replace` 修复对应文件，重新部署，重新验证
- Unity 导入警告 → 同样修复
- `✅ 全部通过!` → 继续写状态文件

`<ui:Style>` 是 Unity UXML 的正确写法。UIV 已支持匹配 `<ui:Style>` 和 `project:` 前缀路径。

**额外校验**：
- **UXML 自定义类 USS 覆盖率**：确认 UXML 中所有自定义类都在部署后的 USS 中有对应定义
- **Brick 引用完整性**：验证页面 UXML 中引用的 Brick USS 路径和 Brick UXML 模板路径是否存在
- **USS `url()` 资源解析验证**：对 UI 目录下每个 USS 文件，用反射读取 `assets` 数组，确认含 `url()` 引用的 USS 其 `assets` 数组非零

**失败后的处理**：如果 USS/UXML 验证在 7 次重试后仍有错误，记录所有未解决的错误列表传递给调用方 Job，不可跳过此步骤进入写状态文件。

#### 验证通过后：写 ui-panel-builder-state.json

UIV.Validate 零错误后，必须执行以下操作产出 `ui-panel-builder-state.json`：

1. **提取元素清单**：扫描所有已部署 UXML 文件，提取每个带 `name` 属性的元素，记录 `name`/`type`/`functional_type`
   - `type`：从 UXML 标签名映射（`<ui:Button>` → `Button`，`<ui:Label>` → `Label`，`<ui:Slider>` → `Slider`，`<ui:Toggle>` → `Toggle`，`<ui:DropdownField>` → `DropdownField`，`<ui:TextField>` → `TextField`，`<ui:VisualElement>` → `VisualElement`，`<ui:Instance>` → `Instance`）
   - `functional_type`：从 brick-mapping.md 查表关联，不推断
2. **`has_dropdown` 标记**：扫描 UXML 中是否存在 `<ui:DropdownField>` 元素
3. **`panel_type` / `is_hud`**：从 UI设计文档.md §1 面板清单读取
4. **`design_resolution`**：从 UI设计文档.md §1 读取（或回退 1920×1080）
5. **组装 state file**：先读取既有文件再按 `shared/contracts.md` 中的 schema 组装写入 `context/games/{GameName}/ui-panel-builder/ui-panel-builder-state.json`——stages 中已有的 `status`/`started_at`/`completed_at` **原样透传**（调用方派发时经 `SetStage` 写入的 `started_at` 必须保留；时钟值只允许透传既有值，禁止新造），最终 `status`/`completed_at` 由第 7 步工具写入
   - `01-uxml-uss` 的 `uiv_validation_passed: true`
   - `02-csharp-integration` 设为 `pending`
   - `artifacts.brick_mapping` 指向 brick-mapping.md 路径
   - `artifacts.panel_settings` 指向 GamePanelSettings.asset 路径
   - `interfaces[]` 每个面板含完整元素清单
6. 写入后确认 JSON 格式有效
7. 用部署型工具收尾（禁止手写状态与时钟）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "01-uxml-uss", "completed", timestamps: true);` —— 写入 `status: completed` + `completed_at`（系统时钟，与 schema 格式一致）

---

## Completion Checklist

**以下全部满足方可报告"Stage 1 完成"**：

1. [步骤3] 图片验证已执行，无缺失图片（除非 `image_requirements` 为空）
2. [步骤4] 图片完整性校验已执行，无校验失败项
3. [步骤7] UXML 自定义类 USS 覆盖率校验已执行，零遗漏类
3b. [步骤7] `reuse_from` 校验：所有 `crop_path` 为 `reuse_from:xxx` 的 `image_requirements` 条目，其 USS 引用的图片文件名为代表组件的文件名（非条目自身 `name`），且 `Images/` 目录下不存在该条目自身 `name` 对应的冗余复制文件
4. [步骤7] DropdownField popup 选择器 unscoped 校验：若面板含 DropdownField，确认 `.unity-base-dropdown__container-inner` / `.unity-base-dropdown__item` 等选择器无 `.{GameName}-dropdown` 前缀
5. [步骤7.5] USS 独立性验证通过：.uss 文件数 = .uxml 文件数，每个 .uss 与 .uxml 前缀一一匹配，无共享 USS 文件
6. [步骤6+8] Brick 组件引用正确（简单 Brick 引用 USS，复杂 Brick Template/Instance，无复制结构）
7. [步骤7.6] UIBrick 合规验证通过：brick-mapping.md 中每个 ✅ 条目的 Brick 在 UXML 中有对应 `<ui:Style>` / `<ui:Template>` 引用（检查 A），且 UXML 元素使用了 `br-*` class 名（检查 B）
8. [步骤8] 所有产出已部署到 Assets
9. [步骤9] UIV.Validate 零错误
10. [步骤9] **ui-panel-builder-state.json 已写入**（含完整元素清单、artifacts 路径、验证状态）

**以下验证不在此阶段范围内，由 Stage 2 或调用方 Job 负责**：
- Panel 脚本编写 — Stage 2
- 编译验证 — 由宿主提供的 C# 编译部署闭环统一做
- Play Mode 验证 — 由集成验证阶段做
- UIRoot 装配 — Stage 2

## 与其他模块的协作关系

### 依赖 ui-image-generator 产出的 UI 图片
- UI 图片由 ui-image-generator 生成到 `Assets/Games/{GameName}/UI/Images/`
- 步骤 3 验证图片已存在，步骤 4 校验后处理是否正确
- 步骤 7 通过 brick-mapping.md 查表引用图片路径到 USS `url("Images/xxx.png")`
