# 共享契约（两阶段共用）

本文件定义跨阶段共享的硬约束、路径守卫、核心规则、状态文件合同和阶段路由规则。每个阶段的 `stages/XX-xxx/SKILL.md` 均依赖本文件——执行任何阶段前应先读取本文件。

## 框架守卫

- **UI 模式守卫**：由 `UI需求.md` Step 0 主动询问用户选择 `ui_mode`，全程不可更改。两种模式不可混用：
  - **UGUI 模式**（默认推荐）：Canvas + CanvasScaler + GraphicRaycaster + UGUI 组件（Image/Text/Button/Slider 等），面板继承 `UGUIPanelBase`/`UGUIGameControllerBase`，数据绑定用 `Bind()`/`BindButton()`（UGUI 版），元素查询用 `SafeFind<T>(path)`
  - **UI Toolkit 模式**（可选）：UXML + USS + UIDocument + PanelSettings，面板继承 `UIPanelBase`/`GameUIControllerBase`，数据绑定用 `Bind()`/`BindButton()`，元素查询用 `SafeQ<T>(name)`
- **UI 框架**：Create.GameFramework UI 框架（`Packages/cn.tuanjie.create.ui/Runtime/`）。两种模式共享 `UIFrame`（含 `SetGameState`/`StateChanged` 状态契约）、`UIManager`、`DIContainer`、`BindableProperty`、`PanelRegistry`

## 路径守卫

- 宿主项目须采用 `context/games/{GameName}/` 目录约定。所有写入操作在目标目录不存在时**必须自动创建**（`write_file` 自动创建父目录；`execute_csharp_script` 中用 `Directory.CreateDirectory`）
- ✅ UXML/USS 文件（UI Toolkit 模式） → `context/games/{GameName}/UI/`（先写入 context，后部署到 Assets）
- ✅ UGUI trees.json → `context/games/{GameName}/ui-panel-builder/trees/{InterfaceName}.trees.json`（`write_file` 声明式构建源，含多棵 brick 树，供修改/重建/中断恢复）
- ✅ UGUI Panel Prefab（UGUI 模式） → 直接创建到 `Assets/Games/{GameName}/UI/Prefabs/`（由 `execute_csharp_script` 固定 runner 调用 `BrickPanelBuilder.BuildBrickTrees` 从 trees.json 产出；Prefab 是二进制资产无需 context 中间层）
- ✅ PanelSettings（UI Toolkit 模式） → `Assets/Games/{GameName}/UI/`（ScriptableObject，通过 execute_csharp_script 直接创建）
- ✅ UI Panel 脚本 → `context/games/{GameName}/GameScripts/`（先验证，后部署到 Assets）
- ⛔ 禁止将 `.cs` 文件直接写入 `Assets/` 目录（部署阶段除外）
- ⛔ 禁止将 UXML/USS 文件直接写入 `Assets/` 目录（部署阶段除外）
- ⛔ UGUI 模式不创建 UXML/USS/PanelSettings 文件（这些是 UI Toolkit 模式专属产物）

## UIGameInstaller 归属硬门禁

> UIGameInstaller.cs 是 Stage 2（02-csharp-integration / 02-ugui-csharp）步骤 12b 的专属产出物，**禁止归入 Job 2a（csharp-code-writer）**。

原因：UIGameInstaller 依赖步骤 10 产出的 PanelNames 常量类、步骤 12a 产出的 PanelRegistry 资产、以及（UI Toolkit 模式）DropdownField popup USS 修复代码——全部在本技能工作流内闭环。若交给 2a 提前编写，此时 PanelNames/PanelRegistry 尚不存在，只能产出空壳 stub。

- job-plan.yaml 中 UIGameInstaller 必须出现在 Job 8 的交付物列表中，不得出现在任何 2a 子 Job 中
- UI设计文档.md §16 场景挂载说明可列出 UIGameInstaller 行（供本技能读契约），但其 Job Plan 输入摘要的 2a 分组表禁止包含它
- 2b（全局编译部署）的 `blocked_by` 不含 UIGameInstaller 的 2a Job（因为不存在）；UIGameInstaller 的编译验证随 Job 8-0 统一做
- **UGUI 模式差异**：UGUI 模式 UIGameInstaller 调用 `UIFrame.InitializeUGUI(uiRootTransform, gameId, registry)` 而非 `UIFrame.Initialize(uiDocument, gameId, registry)`；无 UIDocument、无 PanelSettings、无 UIPanelRouter、无 DropdownField popup 修复代码；UIRoot Prefab 由 `BrickPanelBuilder.BuildUIRoot(prefabPath, refWidth, refHeight)` 一行构建（内置 Canvas 三件套 + sortingOrder=0 + 防黑屏修复 + [VERIFY]），skill 不手写 GameObject/组件/防黑屏代码

## 跨阶段核心规则

以下规则两阶段均需遵守：

### gameId namespace 一致性

Panel 必须使用 gameId 隔离。`UIGameInstaller` 和所有游戏脚本的 namespace 必须与 gameId 完全一致——`UIFrame.Initialize(gameId: "MyGame")` 中的 gameId 是什么字符串，namespace 就必须是什么（如 gameId=`"MyGame"` → `namespace MyGame`）。`DIContainer` 是实例类型，通过 `UIFrame.DI` 属性访问。DI 注册写 `UIFrame.DI.RegisterSingleton(...)`；裸 `DI.RegisterSingleton` 会被编译器当作未定义的静态类，CS0103。Panel 脚本也必须使用与 gameId 一致的 namespace。

### Panel 注册名约定

`[Panel("name", gameId: "GameName")]` 的注册名统一使用**类名去掉 `Panel` 后缀**的值。例：`MainMenuPanel` → `[Panel("MainMenu", gameId: "...")]`。所有 `Push("name")` / `Switch("name")` 调用必须引用此注册名。

- Stage 1 用注册名命名 UXML 文件（`{PanelName}Panel.uxml`，PanelName = 注册名）
- Stage 2 用注册名写 `[Panel]` 属性和 `Push`/`Switch` 调用

## 状态文件：ui-panel-builder-state.json

### 位置

`context/games/{GameName}/ui-panel-builder/ui-panel-builder-state.json`

### 创建时机

由调用方在派发 Stage 1 前创建，所有阶段初始化为 `pending`。**阶段状态流转（`in_progress`/`completed`/`failed`）一律经部署型工具 `Create.GameFramework.TJStateHelper.SetStage`（包 `Editor/UIBuildTools`，`execute_csharp_script` 一行调用）写入，禁止手改 JSON 与手写时钟值**：创建后立即 `SetStage("<state文件>", "<本模式 Stage 1 名>", "in_progress", timestamps: true)` 置首阶段 `in_progress` 并自动写入 `started_at`（系统时钟）；派发 Stage 2 前同样将该阶段置 `in_progress`。

### Schema

```jsonc
{
  "game_name": "MyGame",
  "ui_mode": "ui_toolkit",            // "ui_toolkit" | "ugui" — from UI需求.md
  "design_resolution": { "width": 1920, "height": 1080 },
  "stages": {
    // UI Toolkit 模式：01-uxml-uss / 02-csharp-integration
    // UGUI 模式：01-ugui-prefab / 02-ugui-csharp
    "01-uxml-uss": {               // UGUI 模式下为 "01-ugui-prefab"
      "status": "completed",           // pending | in_progress | completed | failed
      "started_at": "2026-08-01T10:00:00",
      "completed_at": "2026-08-01T10:15:00",
      "uiv_validation_passed": true,   // UI Toolkit: UIV.Validate 结果；UGUI: Prefab 完整性检查结果
      "uiv_errors": [],
      "uiv_input_sha256": "<sha256 of sorted Assets UXML/USS paths and bytes>"  // UGUI 模式不使用
    },
    "02-csharp-integration": {     // UGUI 模式下为 "02-ugui-csharp"
      "status": "pending",
      "started_at": null,
      "completed_at": null
    }
  },
  "artifacts": {
    "brick_mapping": "context/games/{GameName}/ui-panel-builder/brick-mapping.md",
    // ── UI Toolkit 模式 ──
    "panel_settings": "Assets/Games/{GameName}/UI/GamePanelSettings.asset",  // 仅 ui_toolkit
    "uiv_validation": "context/games/{GameName}/ui-panel-builder/uiv-validation.json",  // 仅 ui_toolkit
    // ── UGUI 模式 ──
    "panel_prefabs_dir": "Assets/Games/{GameName}/UI/Prefabs/",  // 仅 ugui
    "trees_dir": "context/games/{GameName}/ui-panel-builder/trees/"  // 仅 ugui，{InterfaceName}.trees.json（BrickTreeSpecJson 存档，供重建/修改）
  },
  "interfaces": [
    {
      "panel_name": "MainMenu",          // [Panel] 注册名，去 Panel 后缀
      "panel_class": "MainMenuPanel",     // C# 类名
      "panel_type": "full_screen",        // full_screen | overlay
      "is_hud": false,                    // 是否 HUD（GameUIControllerBase）
      "has_dropdown": false,             // 是否含 DropdownField（UI Toolkit）/ Dropdown（UGUI）（Stage 2 决定是否写 popup USS 修复——仅 UI Toolkit 模式）
      // ── UI Toolkit 模式字段（ui_mode == "ui_toolkit" 时使用） ──
      "uxml_context_path": "context/games/{GameName}/UI/MainMenuPanel.uxml",
      "uxml_assets_path": "Assets/Games/{GameName}/UI/MainMenuPanel.uxml",
      "uss_context_path": "context/games/{GameName}/UI/MainMenuPanel.uss",
      "uss_assets_path": "Assets/Games/{GameName}/UI/MainMenuPanel.uss",
      // ── UGUI 模式字段（ui_mode == "ugui" 时使用）：一个界面含多棵 brick 树（panel 树 + list_item 模板树） ──
      "trees": [
        {
          "name": "MainMenuPanel",           // 树名 = Prefab 文件名 + 根 GameObject 名（panel 树）
          "root_brickId": "ugui.panel",      // 根节点 brick：panel 树为 ugui.panel，list_item 树为 ugui.list_item
          "prefab_assets_path": "Assets/Games/{GameName}/UI/Prefabs/MainMenuPanel.prefab",
          "has_canvas": true,                 // panel 树为 true（Canvas + CanvasScaler + GraphicRaycaster），list_item 树为 false
          "sortingOrder": 10,                 // Canvas 层级（仅 has_canvas 为 true 的树有效）
          "elements": [
            {
              "name": "play_button",          // 元素名称（UI Toolkit: UXML name 属性值；UGUI: GameObject name）
              "path": "CenterContent/PlayButton", // UGUI 模式专用：Transform 路径供 SafeFind<T>(path) 使用。UI Toolkit 模式不使用此字段
              "type": "Button",              // Button | Label | Slider | Toggle | DropdownField | TextField | VisualElement | Instance
              "functional_type": null         // 从 brick-mapping.md 带入；slider/toggle/dropdown/text_field/progress_bar/other/null
            },
            {
              "name": "volume_slider",
              "type": "Slider",
              "functional_type": "slider"
            }
          ]
        },
        {
          "name": "MainMenuItemTemplate",    // list_item 模板树（ListView 运行时克隆用，按需）
          "root_brickId": "ugui.list_item",
          "prefab_assets_path": "Assets/Games/{GameName}/UI/Prefabs/MainMenuItemTemplate.prefab",
          "has_canvas": false,
          "sortingOrder": 0,
          "elements": [
            {
              "name": "Label",
              "path": "Label",
              "type": "Label",
              "functional_type": null
            }
          ]
        }
      ]
    }
  ]
}
```

### 设计原则

1. **Stage 2 不解析 UXML / Prefab** — 所有 `name`/`type`/`functional_type`（及 UGUI 模式的 `path`）由 Stage 1 从 UXML + brick-mapping.md（UI Toolkit）或 Prefab 层级 + brick-mapping.md（UGUI）提取写入 state file。Stage 2 纯消费。
2. **brick-mapping.md 仍为独立文件** — Stage 2 读它获取 `bindingPropsSchema`（brick 可绑定字段 → C# 绑定代码映射），但不从中提取元素 name（元素 name 从 state file 读）。
3. **`has_dropdown` 是 Stage 2 的关键开关** — 决定 UIGameInstaller 是否需要 DropdownField popup USS 修复代码（Step 12b §5）。⚠️ 仅 UI Toolkit 模式需要 popup USS 修复；UGUI 模式的 Dropdown 组件不需要此修复。
4. **外部输入不进 state file** — UI设计文档.md §8（导航表）、§9（事件映射）、§6（绑定关系）由 Stage 2 直接读取，不属于 Stage 1 产出。
5. **UI Job 完成硬门禁** — `kind: ui` 完成时 Hook 会读取固定路径的 `uiv-validation.json`（UI Toolkit 模式），重新派生 UIV 状态，并核对每个面板的 UXML root、USS closure、`uiv_input_sha256` 与 context↔Assets 内容一致性；缺失、失败或陈旧产物均阻止完成。UGUI 模式下 UIV 不适用，改为 Prefab 完整性检查（组件存在性、Sprite 引用、Canvas 配置），验证结果写入同一 `uiv-validation.json` 路径但使用 UGUI schema。
6. **UGUI 模式 elements[].path** — UGUI 模式下每个元素需记录 Transform 路径（如 `"ButtonGroup/PlayButton"`），供 Stage 2 `SafeFind<T>(path)` 使用。路径相对于树根节点（panel 树或 list_item 树的根 GameObject）。UI Toolkit 模式不使用 `path` 字段（使用 `name` + `SafeQ<T>(name)`）。
7. **`started_at` / `completed_at` 一律由 `TJStateHelper.SetStage` 的 `timestamps: true` 按系统时钟自动写入**（`in_progress` → `started_at`，`completed` → `completed_at`；`pending`/`failed` 不写时钟）——LLM 无法得知真实时钟，禁止手写时间戳值。Stage 1 组装 state file 为全量重写，**必须原样透传既有 `status`/时钟字段**（透传 ≠ 新造），否则调用方派发时写入的 `started_at` 会被清掉。

### 元素类型 → C# SafeQ/SafeFind 映射

Stage 2 根据元素清单中的 `type` 字段选择查询方法。UI Toolkit 模式使用 `SafeQ<T>(name)` 在 VisualElement 树中查找；UGUI 模式使用 `SafeFind<T>(path)` 在 Transform 层级中查找。

| state file `type` | UI Toolkit: `SafeQ<T>` | UGUI: `SafeFind<T>` | UXML/UGUI 元素 |
|-------------------|------------------------|----------------------|----------------|
| Button | `SafeQ<Button>` | `SafeFind<Button>` | `<ui:Button>` / `UnityEngine.UI.Button` |
| Label | `SafeQ<Label>` | `SafeFind<Text>` | `<ui:Label>` / `UnityEngine.UI.Text` |
| Slider | `SafeQ<Slider>` | `SafeFind<Slider>` | `<ui:Slider>` / `UnityEngine.UI.Slider` |
| Toggle | `SafeQ<Toggle>` | `SafeFind<Toggle>` | `<ui:Toggle>` / `UnityEngine.UI.Toggle` |
| DropdownField | `SafeQ<DropdownField>` | `SafeFind<Dropdown>` | `<ui:DropdownField>` / `UnityEngine.UI.Dropdown` |
| TextField | `SafeQ<TextField>` | `SafeFind<InputField>` | `<ui:TextField>` / `UnityEngine.UI.InputField` |
| ListView | `SafeQ<ListView>` | `SafeFind<ScrollRect>` | `<ui:ListView>` / `UnityEngine.UI.ScrollRect` |
| ListItem | — | `SafeFind<Button>` | UGUI 专用：列表条目（Button+Image+Text） |
| BackgroundBar | — | `SafeFind<Image>` | UGUI 专用：背景条/装饰条（9-slice Image） |
| Modal | `SafeQ<VisualElement>` | `SafeFind<Canvas>` | UGUI 专用：弹窗壳体（Canvas+Image+Slots） |
| VisualElement | `SafeQ<VisualElement>` | `SafeFind<Transform>` | `<ui:VisualElement>` / `GameObject` |
| Instance | `SafeQ<VisualElement>` | `SafeFind<Transform>` | `<ui:Instance>` / `GameObject` |

## 阶段路由规则

### 派发流程

```
1. 调用方确认前置条件：
   - UI设计文档.md 存在
   - image_requirements.json 存在
   - UI 图片已生成（ui-image-generator 完成）

2. 创建 ui-panel-builder-state.json（所有阶段初始化为 pending）。同时产出 `context/games/{GameName}/ui-panel-builder/brick-mapping.md`——调用方运行部署型工具一行调用 `Create.GameFramework.TJBrickMappingDraft.Mapping("{GameName}");`（包 `Editor/UIBuildTools`，经 `execute_csharp_script`）生成草稿：类型→brickId 候选、prefabPath/definitionPath 校验、image_requirements 关联（uxml_element/expanded_from，含共享风格图候选提示）、functional_type 判定均由工具完成；调用方仅审核「人工复核」行（多候选/无候选）并补充 Layout/Container 级 Brick——无需读取 BrickIndex 全文。这样 subagent 跳过 Step 1 的全部文档读取，从 Step 2 开始执行——避免 subagent 因文档读取量过大而超时。派发时在 prompt 中告知 brick-mapping.md 已存在。

3. 读取 state file 的 ui_mode 字段，按模式选择 Stage：

   ── ui_mode == "ui_toolkit" ──
   3a. 派发 Stage 01-uxml-uss subagent：
       - 传入 shared/contracts.md + stages/01-uxml-uss/SKILL.md 路径
       - 传入必读 references 路径
       - 传入 ui-panel-builder-state.json 路径
       - subagent 执行 Steps 1-9，完成后写 state file（组装透传既有 `status`/时钟字段、新写结构与 `uiv_*` 字段；最终 `status` 与 `completed_at` 以 `TJStateHelper.SetStage(..., "completed", timestamps: true)` 收尾，见该 Stage SKILL.md 步骤 9）

   3b. Stage 01 完成（state file 中 01-uxml-uss.status == completed）：
       派发 Stage 02-csharp-integration subagent：
       - 调用方派发前先 `Create.GameFramework.TJStateHelper.SetStage("<state文件>", "02-csharp-integration", "in_progress", timestamps: true)`（自动写 `started_at`）
       - 传入 shared/contracts.md + stages/02-csharp-integration/SKILL.md 路径
       - 传入必读 references 路径
       - 传入 ui-panel-builder-state.json 路径
       - subagent 执行 Steps 10-12，完成后用 `Create.GameFramework.TJStateHelper.SetStage("<state文件>", "<Stage 2 名>", "completed", timestamps: true)` 更新 state file（自动写 `completed_at`，禁止手改 JSON 与手写时钟）

   ── ui_mode == "ugui" ──
   3c. 派发 Stage 01-ugui-prefab subagent：
       - 传入 shared/contracts.md + stages/01-ugui-prefab/SKILL.md 路径
       - 传入必读 references 路径
       - 传入 ui-panel-builder-state.json 路径
       - subagent 执行 Steps 1-8（每界面写 trees.json → `BrickPanelBuilder.BuildBrickTrees` 产出 Prefab），完成后写 state file（组装透传既有 `status`/时钟字段、新写结构与 `uiv_*` 字段；最终 `status` 与 `completed_at` 以 `TJStateHelper.SetStage(..., "completed", timestamps: true)` 收尾，见该 Stage SKILL.md 步骤 8）

   3d. Stage 01 完成（state file 中 01-ugui-prefab.status == completed）：
       派发 Stage 02-ugui-csharp subagent：
       - 调用方派发前先 `Create.GameFramework.TJStateHelper.SetStage("<state文件>", "02-ugui-csharp", "in_progress", timestamps: true)`（自动写 `started_at`）
       - 传入 shared/contracts.md + stages/02-ugui-csharp/SKILL.md 路径
       - 传入必读 references 路径
       - 传入 ui-panel-builder-state.json 路径
       - subagent 执行 Steps 10-12，完成后用 `Create.GameFramework.TJStateHelper.SetStage("<state文件>", "<Stage 2 名>", "completed", timestamps: true)` 更新 state file（自动写 `completed_at`，禁止手改 JSON 与手写时钟）

4. Stage 2 完成：ui-panel-builder 整体完成
```

### 会话恢复

1. 读 `ui-panel-builder-state.json`
2. 找到第一个 `status != completed` 的 Stage
3. 该 Stage 内部按中断恢复表检查已有产出，从首个缺失步骤继续
4. 已完成 Stage 不重新执行

### 失败处理

| 场景 | 处理 |
|------|------|
| Stage 1 UIV 验证 7 次重试仍失败（UI Toolkit） | state file 中 `uiv_errors` 记录错误列表，Stage 1 经 `TJStateHelper.SetStage(..., "failed")` 标记 failed，调用方决定是否重试 |
| Stage 1 Prefab 完整性验证失败（UGUI） | state file 中 `uiv_errors` 记录错误列表（组件缺失、Sprite 引用 null 等），Stage 1 经 `TJStateHelper.SetStage(..., "failed")` 标记 failed，调用方决定是否重试；修复 = 修改 trees.json 后重跑 `BrickPanelBuilder.BuildBrickTrees`，⛔ 禁止手改 Prefab |
| Stage 1 缺失图片或 Importer 错误 | producer 返回稳定错误码并中止；回到 ui-image-generator 修复，禁止用 TODO 占位继续完成 UI |
| Stage 2 编译失败 | 不在此技能范围，由宿主 C# 编译部署闭环处理。state file 保持 in_progress |

## 完整性验证

### 模式差异

| 维度 | UI Toolkit 模式 | UGUI 模式 |
|------|----------------|-----------|
| 验证工具 | `UIV.Validate()` / `UIV.ValidateStructured()` | `execute_csharp_script` 内联检查（Prefab 组件完整性） |
| 验证对象 | UXML/USS 文件（closure、class 覆盖、`url()` 解析） | Tree Prefab（panel 树：Canvas/CanvasScaler/GraphicRaycaster + scale/anchors；list_item 树：仅组件接线；Image.sprite 引用、Button.targetGraphic、Slider.fillRect、handleRect 仅 HandleArea 存在时） |
| 产出文件 | `uiv-validation.json`（UIV 结构化输出） | `uiv-validation.json`（自定义结构，字段：`trees[]` 每树 `{tree_name, has_canvas, components_ok, sprites_ok, canvas_ok, errors[]}`） |
| 图片检查 | 部署型工具 `TJValidateAssets.Images()`（通用） | 部署型工具 `TJValidateAssets.Images()`（通用，相同） |

### 直接验证（默认）
调用方通过 UIV.Validate + 部署型工具 `TJValidateAssets.Images()`（UI Toolkit 模式）或 execute_csharp_script Prefab 完整性检查 + `TJValidateAssets.Images()`（UGUI 模式）直接验证 UI 产出的完整性。

### 外部编排器 Gate（可选）

> ⚠️ **以下 Gate schema 描述适用于 UI Toolkit 模式**。UGUI 模式的 Gate 验证对象为 Panel Prefab 而非 UXML/USS，相关字段（`uxml`/`uss`/`panel_settings`/`uidocument_visual_tree_asset` 等）替换为 `panel_prefabs`/`canvas_config`/`panel_registry_prefab_entries` 等 UGUI 等价字段。UGUI 模式 Gate 的完整 schema 待后续补充。
若由外部编排器驱动，`ui_verify` 任务通过 `record_ui_gate_receipt.py` 固化机器可读 Gate receipt。生产格式唯一固定为 `schema: codely.ui-verify-receipt/v4`；旧 schema 不兼容。禁止 AI 创建或编辑 Gate 产物；producer 只消费 artifacts manifest、图片检查原文、`UIV.ValidateStructured` JSON 和只读 Unity integration probe，自行派生状态、自验并原子写入。

- `game`、`logical_id`、`image_requirements`、`expected_panels`
- `artifacts.ui_directory/images_directory/uxml/uss/panel_settings/panel_registry/ui_root` 的项目内相对路径
- `checks.validate_images.raw_output`：原样保存部署型工具 `Create.GameFramework.TJValidateAssets.Images()` 的输出且 `status: passed`
- `checks.static_ui`：原样保存 `JsonUtility.ToJson(UIV.ValidateStructured("Assets/Games/{GameName}/UI"), true)` 的结构化对象及 SHA-256；validator 重新派生每个 root 的 Template/UXML/USS closure、class 覆盖和汇总状态
- `checks.integration`：只读 Unity probe 的结构化结果，字段集合必须精确包含 `status`、UIRoot 上 `UIDocument/UIPanelRouter/UIGameInstaller`、`panel_settings_type`、`uidocument_panel_settings`、`uidocument_visual_tree_asset`、`installer_panel_registry`、`installer_uidocument_matches_host`、`router_uidocument_matches_host`、`router_target`、`router_configured_panels`、`registered_panels` 和 `panel_controllers`。`installer_uidocument_matches_host` 必须由 probe 通过序列化字段引用与宿主 `UIDocument` 的对象同一性派生为 true；`router_uidocument_matches_host` 必须由 `router.GetComponent<UIDocument>() == hostDocument` 派生为 true，不能读取或伪造 `UIPanelRouter` 的非序列化运行时缓存。独立 `[Panel]` UXML 架构要求 `router_target=UIFramePanels` 且 router 覆盖全部 expected panel。每个 expected panel 必须同时有路由项、注册项和运行控制器；宿主 `uidocument_visual_tree_asset` 必须为空
- `producer.ui_documents_sha256`：对声明的全部 UXML/USS 按项目相对路径排序后计算 `path + NUL + bytes` 的确定性哈希；完成端重算，UI 文档在 UIV 之后有任何变化均令 receipt 失效
- `producer.integration_artifacts_sha256`：对 UIRoot Prefab、PanelRegistry 与 PanelSettings 的路径和字节做确定性哈希；完成端重算，integration probe 后任一接线资产变化均令 receipt 失效
- 对 `image_requirements.json` 中 `is_segmentation: true` 的图片，validator 会复验每个实际 `background-image` 声明在同一 UXML 内联样式或 USS 规则内显式设置透明 `background-color`；缺失或不透明时以 `UI_SEGMENTED_IMAGE_OPAQUE_BACKGROUND` 阻止 `ui_verify` 完成，避免 Button 默认灰色背景从透明像素透出

producer 与 Job 完成端统一使用同一个 validator：producer 写入前自验，Hook 在 `completed` 边界再次验证输入 hash、context↔Assets UXML/USS 同步、图片集合、UIRoot/PanelRegistry 和面板覆盖。退出码非 0 时 `ui_verify` 不得完成。

```text
{PYTHON} .codely-cli/extensions/TJUIBuilder/tools/record_ui_gate_receipt.py \
  --game {GameName} --logical-id {logicalJobId} \
  --artifacts <artifacts-manifest.json> \
  --validate-images <validate-images-output.txt> \
  --static-ui <static-ui.json> --integration-probe <integration-probe.json> \
  --project-root .
```

`{PYTHON}` 按 `references/python-contract.md` 探测；Windows 优先 `python`、备用 `py -3`，macOS/Linux 优先 `python3`、备用 `python`。

producer/validator 将 `image_requirements.json` 作为预期图片与面板来源，并结合实际 UXML/USS/PanelSettings/PanelRegistry/UIRoot、部署型工具 `TJValidateAssets.Images()` 的检查结果、结构化静态 UI closure 与 Unity integration probe 交叉校验。对每个 `ai_generate` 条目通过 compiled semantic→batch 映射读取共享 source receipt，复验 semantic ID、primary artifact 位于当前游戏 `UI/Images`、图片真实性及 UXML/USS 实际引用；`direct_use` / `uss_only` / `user_import` 保持既有语义，不要求 Generator receipt。

## References 索引

每个 stage 的必读 references 清单见模块 SKILL.md「Stage References 矩阵」。该矩阵是权威来源，本文件不再重复维护。⛔ 禁止读取非本 stage 列出的 references 文件。

## Scripts 索引

| Script / 工具 | 归属 Stage | 用途 |
|------------|-----------|------|
| 部署型工具 `TJStateHelper.SetStage/SetTask/Show`（包 `Editor/UIBuildTools`） | 全阶段 + 调用方 | state 文件状态流转与时间戳（`SetStage` 可选 `timestamps: true` 自动写 `started_at`/`completed_at`），禁止手改 JSON 与手写时钟 |
| 部署型工具 `TJBrickMappingDraft.Mapping("<GameName>")` / `.Coverage("<GameName>")`（包 `Editor/UIBuildTools`） | 调用方（Step 1 预产出）/ Stage 1 回退 | brick-mapping.md 草稿：类型→brickId 候选、prefabPath/definitionPath 校验、image_requirements 关联、functional_type（Mapping）；§2 覆盖表（Coverage） |
| 部署型回归工具 `TJBrickAssemblyTests.RunAll()`（包 `Editor/UIBuildTools`） | 库维护回归 | 改 ugui brick 定义/compositionPolicy 后运行，须无 BRICK_ASSEMBLY_TEST_FAILED |
| 部署型回归工具 `TJAnimationRegressionTests.RunAll()`（包 `Editor/UIBuildTools`） | 库维护回归 | 动效库回归（UIAnimationHelper） |
| `scripts/test_ui_animation.cs`（模块 scripts，维护者源稿，非流水线） | 库维护回归 | 上一行的移植源稿——改其一须同步另一份（测试清单保持一致） |
| 部署型工具 `TJValidateAssets.Images()`（包 `Editor/UIBuildTools`） | Stage 1（两种模式共用） | Step 4 图片完整性校验 |
| 部署型工具 `TJValidateAssets.UguiPrefabs()`（包 `Editor/UIBuildTools`） | Stage 1（仅 UGUI） | Step 6 Panel Prefab 完整性验证（Canvas/CanvasScaler/GraphicRaycaster、Image.sprite、Button.targetGraphic、Slider.fillRect、handleRect 仅 HandleArea 存在时）+ uiv-validation.json |
| 部署型工具 `TJValidateAssets.ReimportUss()`（包 `Editor/UIBuildTools`） | Stage 1（仅 UI Toolkit） | Step 8 USS touch + 强制重新导入 |
