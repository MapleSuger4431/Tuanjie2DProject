---
name: ui-panel-builder
description: UI 界面构建模块 - 根据 UI设计文档.md 和 ui_mode 创建完整的 UI 界面。UGUI 模式（默认）：构建 Tree Prefab（panel 树 + list_item 模板树，根节点 brick 决定根结构）+ C# Panel 脚本（继承 UGUIPanelBase），引用 UGUI Brick 组件库（BrickIndex.ugui.json）。UI Toolkit 模式：创建 UXML/USS + C# Panel 脚本（继承 UIPanelBase），引用 UIBrick 的 UXML/USS 组件。使用此模块当：需要为 Unity 游戏创建 UI 界面（HUD、菜单、弹窗等）、需要创建 UGUI Tree Prefab 或 UXML/USS 文件、需要编写 UI Panel 脚本绑定游戏数据到 UI 元素。分成多个 stage, 每1个 stage 委派1个独立的 subagent 执行。两种模式不可混用。
---

# Unity UI Panel Builder

## ⛔ 框架守卫 / 路径守卫 / 跨阶段核心规则

见 `shared/contracts.md`（框架守卫、路径守卫、UIGameInstaller 归属硬门禁、gameId namespace 一致性、Panel 注册名约定、状态文件 schema、阶段路由规则）。

本模块附加硬规则：**BuildBrickTrees 构建必须串行**——它会改写包内共享 Brick Prefab，并行任务同时构建会互相破坏（详见 `references/ugui/ugui-prefab-construction.md` §5）。

## Overview

此模块提供 UI 界面构建的完整工作流程。根据 UI设计文档.md（由 `ui-doc-builder` 生成），按 `ui_mode` 分为两条完全独立的构建路径：

### UGUI 模式（默认）

从 UI设计文档.md 读取面板结构和 UIBrick 映射，通过 `execute_csharp_script` 构建 Tree Prefab（根节点 brick 决定根结构：panel/modal→Canvas，list_item→无 Canvas）。Panel 脚本继承 `UGUIPanelBase`（普通面板）或 `UGUIGameControllerBase`（HUD），使用 `SafeFind<T>(path)` 查询元素，`UGUIBindingBuilder` 做数据绑定。UIRoot 为 Canvas + CanvasScaler + GraphicRaycaster，通过 `UIFrame.InitializeUGUI()` 初始化。

### UI Toolkit 模式（可选）

参考 UIBrick 设计系统库的砖块定义（UXML 结构参考 + USS 样式参考），生成页面级 UXML 布局、USS 样式、PanelSettings 资产和 UI Panel 脚本（继承 UIPanelBase），实现游戏数据到 UI 的声明式绑定。UIRoot 为 UIDocument + PanelSettings，通过 `UIFrame.Initialize()` 初始化。

本模块是一个**两阶段流水线**（两种模式各有独立的两阶段）。执行任何阶段前，先读 `shared/contracts.md` 获取共享硬约束、路径守卫、核心规则、状态文件 schema 和阶段路由规则，再读对应阶段的 `stages/XX-xxx/SKILL.md` 获取详细步骤。

⚠️ 禁止一次性阅读所有阶段的相关文档。

### UIBrick 基础组件库（v2）

UIBrick 位于 `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/`（Package 内，无需部署）。两种模式引用方式完全不同，详细规则见各自 references：

- **UI Toolkit 模式**：通过 `project:` 前缀引用 Brick USS/UXML。简单 Brick 内联使用 `br-*` 标准类名，复杂 Brick 通过 `<ui:Template>` + `<ui:Instance>` 引用。class 名映射统一为 `br-{brick}--{value}`。详细组合规则见 `references/uitk/uxml-brick-composition.md`
- **UGUI 模式**：skill 用 `write_file` 写声明式 trees.json（一个界面包含多棵 brick 树，每棵树根节点是 panel/modal/list_item），一次 `BrickPanelBuilder.BuildBrickTrees(treesPath, prefabDir)` 调用输出 N 个 Tree Prefab + 元素 SafeFind 路径清单 + 组合校验警告；使用独立的 `BrickIndex.ugui.json` 索引和 `ugui.*` 前缀 brickId（`assemblyOnly: true` 的 brick 从 definition 动态构建，skill 无手写路径）。详细规则见 `references/ugui/ugui-prefab-construction.md`

核心规则：
- 找不到合适 Brick 时必须直接手写页面结构，禁止回写 Brick 库
- UGUI 模式面板内部应优先使用 Layout Brick 组织子元素布局

## When to Use This Skill

使用此技能当：
- 需要创建游戏 UI 界面（HUD、主菜单、游戏结束界面等）
- 需要创建 UGUI Canvas Prefab（UGUI 模式）或 UXML 布局和 USS 样式文件（UI Toolkit 模式）
- 需要编写 UI Panel 脚本（继承 UGUIPanelBase 或 UIPanelBase）将游戏数据绑定到 UI 元素
- 需要创建 PanelSettings 资产（仅 UI Toolkit 模式）
- 需要验证 UI 框架（Create.GameFramework）已存在于 Package

## 两阶段流水线（按 ui_mode 分流）

```
── ui_mode == "ugui"（默认）──
Stage 01-ugui-prefab    Canvas Prefab 构建   Steps 1-8    brick-mapping→Canvas配置→图片验证→Prefab构建→验证→部署→状态文件
  │
  │  ui-panel-builder-state.json（含元素清单 + Transform 路径）
  │
Stage 02-ugui-csharp    C# 脚本 + 集成       Steps 10-12   Panel脚本(UGUIPanelBase)→部署→UIRoot(Canvas)+UIGameInstaller(InitializeUGUI)

── ui_mode == "ui_toolkit" ──
Stage 01-uxml-uss       UXML/USS 构建         Steps 1-9    brick-mapping→PanelSettings→图片验证→USS初稿→UXML布局→USS补全→部署→验证
  │
  │  ui-panel-builder-state.json（含元素清单）
  │
Stage 02-csharp-integration  C# 脚本 + 集成   Steps 10-12   Panel脚本(UIPanelBase)→部署→UIRoot(UIDocument)+UIGameInstaller(Initialize)
```

| 阶段 | SKILL.md | 输入 | 输出 | required_skills |
|------|----------|------|------|-----------------|
| 01-ugui-prefab | `stages/01-ugui-prefab/SKILL.md` | UI设计文档.md、image_requirements.json | brick-mapping.md、Tree Prefab（panel 树 + list_item 模板树）、ui-panel-builder-state.json | unity-operator（BuildBrickTrees 经 execute_csharp_script 构建/改写 Prefab） |
| 02-ugui-csharp | `stages/02-ugui-csharp/SKILL.md` | ui-panel-builder-state.json、brick-mapping.md、UI设计文档.md §6/§8/§9 | Panel 脚本、PanelNames.cs、UIGameInstaller.cs、UIRoot.prefab、PanelRegistry.asset | csharp-code-writer + unity-operator（Panel/Installer C# 编写；UIRoot.prefab/PanelRegistry 经 execute_csharp_script） |
| 01-uxml-uss | `stages/01-uxml-uss/SKILL.md` | UI设计文档.md、image_requirements.json | brick-mapping.md、GamePanelSettings.asset、UXML/USS（context+Assets）、ui-panel-builder-state.json | unity-operator（PanelSettings/资产创建与 reimport 经 execute_csharp_script） |
| 02-csharp-integration | `stages/02-csharp-integration/SKILL.md` | ui-panel-builder-state.json、brick-mapping.md、UI设计文档.md §6/§8/§9 | Panel 脚本、PanelNames.cs、UIGameInstaller.cs、UIRoot.prefab、PanelRegistry.asset | csharp-code-writer + unity-operator（Panel/Installer C# 编写；UIRoot.prefab/PanelRegistry 经 execute_csharp_script） |

## Stage References 矩阵

> Stage References 矩阵通用规则（必读 references 强制读取、禁止读取非本 stage 文件、subagent 自行读取矩阵）见主 SKILL.md「⛔ 阶段派发守卫」。

每个 stage 执行前**必须读取** `shared/contracts.md`（跨阶段共享契约）+ 对应阶段的 `stages/XX-xxx/SKILL.md`，然后按下表读取该 stage 的必读 references。

### Stage 01 必读 references

#### UGUI 模式（01-ugui-prefab）

| 文件 | 用途 | 备注 |
|------|------|------|
| `references/ugui/ugui-prefab-construction.md` | trees.json schema、传参规则（指向 BrickCheatsheet）、布局换算、构建 runner（BuildBrickTrees）、修改/重建流程、警告处理、命名约定 | 步骤 5 构建时边查 |
| `references/brick-mapping.md` | Brick 映射规则（含 UGUI 模式 BrickIndex.ugui.json 映射）、brickId 命名、映射表字段 | **仅 Step 1 需要**；调用方预产出 brick-mapping.md 时可跳过 |
| `references/game-doctor-checks.md` | Prefab 完整性检查项清单（UGUI 检查项） | 步骤 6 验证时边查 |
| `references/UIFrame-API.md` | UGUIPanelBase API、PanelRegistryBuilder.ReplacePrefabEntries | 步骤 8 写 state file 时查 |

#### UI Toolkit 模式（01-uxml-uss）

| 文件 | 用途 | 备注 |
|------|------|------|
| `references/brick-mapping.md` | Brick 映射规则、brickId 命名、映射表字段 | **仅 Step 1 需要**；调用方预产出 brick-mapping.md 时可跳过 |
| `references/uitk/uss-reference.md` | USS 编写规范（禁止属性、`background-size` 决策表、颜色格式、路径规则、§10 功能控件必须读 functional-control-styles.md） | 步骤 5-7 写 USS 时边查 |
| `references/uitk/uxml-brick-composition.md` | Brick 组合规则、布局契约、Button text、图片路径映射 | 步骤 5 写 UXML 时边查 |
| `references/uitk/uss-completeness-example.md` | USS 完整性校验示例 | 步骤 7 USS 补全时边查 |
| `references/uitk/functional-control-styles.md` | **Slider**：track/fill/handle 三层图片分工、`overflow: hidden` 遮罩 + `.br-slider-fill-texture` 防拉伸、USS 选择器、dragger 尺寸与居中、dragger-border 透明；**Toggle**：复选框式 vs 滑动开关式 variant 选择（`br-toggle-base--switch`）、track/knob USS 选择器、switch 变体 background-image 清除；**ProgressBar**：track/fill 两层图片分工、`overflow: hidden` + `.br-progress-fill-texture` 防拉伸、`__progress` height 100% + C# 高度同步；**DropdownField**：`__input` 上设 bg 图片、popup 挂载在 panel.visualTree、unscoped 选择器规则、9-slice | 步骤 5-7 功能控件时边查 |
| `references/game-doctor-checks.md` | UIV.ValidateStructured 检查项清单 | 步骤 8 验证时边查 |
| `references/UIFrame-API.md` | Push/Show 适用范围表（步骤 5 单面板栈限制判断） | 步骤 5 时查 |

### Stage 02 必读 references

#### UGUI 模式（02-ugui-csharp）

| 文件 | 用途 |
|------|------|
| `references/UIFrame-API.md` | UGUIPanelBase/UGUIGameControllerBase API、UGUIBindingBuilder 方法、SafeFind、InitializeUGUI、PanelRegistryBuilder.ReplacePrefabEntries/UpsertPrefab |
| `references/uipanel-patterns.md` | UGUI 面板模式（SafeFind 路径约定、UGUIBindingBuilder 使用示例、UGUIGameControllerBase 模式、导航去重规则） |
| `references/UIAnimation-API.md` | UIAnimationHelper 动效 API、LayoutGroup 安全矩阵（位移/alpha/scale 适用范围）、host 重载约定、录制时序规则 |
| `references/game-doctor-checks.md` | 导航完整性检查（UGUI 模式检查项） |

#### UI Toolkit 模式（02-csharp-integration）

| 文件 | 用途 |
|------|------|
| `references/UIFrame-API.md` | Bind/BindButton/SafeQ/Push/Pop/Switch/Resolve/Track 方法签名 |
| `references/uipanel-patterns.md` | Slider 双向绑定、UIGameInstaller 适配器模式、导航去重规则 |
| `references/uitk/functional-control-styles.md` | **Slider**：`SetupSliderFill(slider)` 同步 tracker 宽度与 fill 纹理防拉伸（Tuanjie 2022.3 引擎缺陷，USS 无法修复，必须 C# 调用）；**ProgressBar**：`SetupProgressBarFill(bar)` 注入 fill 纹理防拉伸 + `__progress` 高度 C# 同步（Tuanjie 2022.3 引擎缺陷，`__container` 硬编码 24px，USS `height:100%` 无效，必须 C# 设像素值 + `GeometryChangedEvent`）；**DropdownField**：popup USS 修复代码（OnPanelPushed 回调将面板 USS 添加到 panel.visualTree） |
| `references/game-doctor-checks.md` | 导航完整性检查（CheckPanelReachability）理解 |

### 共享说明

`functional-control-styles.md`、`game-doctor-checks.md`、`UIFrame-API.md`、`uipanel-patterns.md`、`brick-mapping.md` 被两种模式的 Stage 共享，但各 stage 只读取自己需要的章节。两个 stage 都在必读列表中显式列出这些文件，不属于"跨 stage 读取"。UGUI 模式的 Stage 不读取 `references/uitk/` 子目录下的文件（`uss-reference.md`、`uxml-brick-composition.md`、`uss-completeness-example.md`、`functional-control-styles.md`，这些是 UI Toolkit 专属）。

> 当调用方预产出 `brick-mapping.md`（见 `shared/contracts.md` 步骤 2）时，Stage 01 subagent 跳过 Step 1，无需读取 `references/brick-mapping.md`。其余 references 按上表「备注」列边写边查。

## 阶段路由规则

> 阶段派发通用规则见主 SKILL.md「⛔ 阶段派发守卫」。

**阶段路由按 `ui_mode` 分流**——UGUI 模式走 `01-ugui-prefab` → `02-ugui-csharp`，UI Toolkit 模式走 `01-uxml-uss` → `02-csharp-integration`。阶段间通过 `ui-panel-builder-state.json` 传递状态和元素清单。派发每个 stage subagent 时，按上方阶段表 `required_skills` 列在 task prompt 中写明技能激活要求（规则见主 SKILL.md「⛔ 技能激活传递」）。详细派发流程、会话恢复和失败处理规则见 `shared/contracts.md`「阶段路由规则」章节。

## 模块目录规范

```
modules/ui-panel-builder/
├── SKILL.md                              # 本文件（薄路由）
├── shared/
│   └── contracts.md                      # 跨阶段共享契约
├── stages/
│   ├── 01-ugui-prefab/
│   │   └── SKILL.md                     # UGUI Stage 1: Canvas Prefab 构建（Steps 1-8）
│   ├── 01-uxml-uss/
│   │   └── SKILL.md                     # UI Toolkit Stage 1: UXML/USS 构建（Steps 1-9）
│   ├── 02-ugui-csharp/
│   │   └── SKILL.md                     # UGUI Stage 2: C# 脚本+集成（Steps 10-12）
│   └── 02-csharp-integration/
│       └── SKILL.md                     # UI Toolkit Stage 2: C# 脚本+集成（Steps 10-12）
├── references/                           # 参考手册（归属见上方「Stage References 矩阵」）
│   ├── brick-mapping.md                  # 共享（含 UGUI + UI Toolkit 映射）
│   ├── game-doctor-checks.md            # 共享（含 UGUI + UI Toolkit 检查项）
│   ├── UIFrame-API.md                   # 共享（含 UGUI + UI Toolkit API）
│   ├── uipanel-patterns.md              # 共享（含 UGUI + UI Toolkit 模式）
│   ├── ugui/                            # UGUI 模式专属
│   │   └── ugui-prefab-construction.md  # UGUI Prefab 构建总则
│   └── uitk/                            # UI Toolkit 模式专属
│       ├── uss-reference.md             # USS 编写规范
│       ├── uxml-brick-composition.md    # UXML Brick 组合规则
│       ├── uss-completeness-example.md   # USS 完整性校验示例
│       └── functional-control-styles.md  # 功能控件 USS/C# 修复
└── scripts/                             # 库维护者回归脚本（不属 skill 流水线）：test_ui_animation.cs（UIAnimationHelper 源稿，已移植为包内 TJAnimationRegressionTests，两份须同步维护）；其余脚本均已部署为包内编译工具（见下注）
```

> **部署型工具索引**（`cn.tuanjie.create.ui` 包 `Editor/UIBuildTools`，均经 `execute_csharp_script` 一行调用）：
> - `Create.GameFramework.TJStateHelper.Show/SetStage/SetTask` — state 文件 typed 一行调用（`SetStage` 可选 `timestamps: true` 自动写 `started_at`/`completed_at`，系统时钟；禁止手改 JSON 与手写时钟）
> - `Create.GameFramework.TJValidateAssets.Images()`（图片完整性，两模式共用）/ `.UguiPrefabs()`（UGUI Panel Prefab 完整性 + uiv-validation.json）/ `.ReimportUss()`（UI Toolkit USS touch + 强制重导入）
> - 回归工具：`Create.GameFramework.TJBrickAssemblyTests.RunAll()`（改 ugui brick 定义/compositionPolicy 后运行，须无 `BRICK_ASSEMBLY_TEST_FAILED`）/ `Create.GameFramework.TJAnimationRegressionTests.RunAll()`（动效库回归 UIAnimationHelper，须无 `UI_ANIMATION_TEST_FAILED`）

各 reference 的归属 Stage 见上方「Stage References 矩阵」，script 的归属 Stage 见 `shared/contracts.md`「Scripts 索引」。

## 完成检查

### UGUI 模式

- [ ] Tree Prefab 已部署到 `Assets/Games/{GameName}/UI/Prefabs/`（panel 树 + list_item 模板树）
- [ ] Panel 脚本已部署到 `Assets/Games/{GameName}/Scripts/`（继承 `UGUIPanelBase`/`UGUIGameControllerBase`）
- [ ] PanelNames.cs 已生成
- [ ] UIRoot.prefab 已创建（Canvas + CanvasScaler + GraphicRaycaster）
- [ ] UIGameInstaller.cs 已生成（调用 `UIFrame.InitializeUGUI()`）
- [ ] PanelRegistry.asset 已生成
- [ ] `ui-panel-builder-state.json` 所有阶段 `completed`

### UI Toolkit 模式

- [ ] UXML/USS 已写入 context 并部署到 `Assets/Games/{GameName}/UI/`
- [ ] PanelSettings.asset 已创建
- [ ] Panel 脚本已部署到 `Assets/Games/{GameName}/Scripts/`（继承 `UIPanelBase`/`GameUIControllerBase`）
- [ ] PanelNames.cs 已生成
- [ ] UIRoot.prefab 已创建（UIDocument + PanelSettings）
- [ ] UIGameInstaller.cs 已生成（调用 `UIFrame.Initialize()`）
- [ ] PanelRegistry.asset 已生成
- [ ] UIV.ValidateStructured 通过
- [ ] `ui-panel-builder-state.json` 所有阶段 `completed`
