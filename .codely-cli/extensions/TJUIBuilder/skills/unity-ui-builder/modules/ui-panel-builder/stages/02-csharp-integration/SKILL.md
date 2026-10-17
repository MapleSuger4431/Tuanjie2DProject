# Stage 02: C# 脚本 + 集成（UI Toolkit 模式）

> ⛔ **模式守卫**：本 Stage 仅适用于 `ui_mode == "ui_toolkit"`。`ui_mode == "ugui"` 时走 `stages/02-ugui-csharp/SKILL.md`。

Steps 10-12：Panel 脚本编写 → 部署 → UIRoot Prefab 装配 + UIGameInstaller。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `ui-panel-builder-state.json`（Stage 1 产出）、`brick-mapping.md`、UI设计文档.md §6/§8/§9 |
| 输出 | Panel 脚本（`.cs`）、`{GameName}PanelNames.cs`、`UIGameInstaller.cs`、`UIRoot.prefab`、`PanelRegistry.asset` |
| 前置门禁 | `ui-panel-builder-state.json` 中 `01-uxml-uss.status == completed` 且 `uiv_validation_passed == true` |
| 完成判据 | UIRoot prefab 创建完成 + UIGameInstaller 挂载 + ui-panel-builder-state.json 更新为 completed |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |
| 必须激活技能 | csharp-code-writer + unity-operator——C# 脚本编写走 csharp-code-writer 闭环；UIRoot.prefab/PanelRegistry 经 execute_csharp_script；subagent 第 0 步先激活再读文档 |

> **共享契约**：框架守卫、路径守卫、UIGameInstaller 门禁、gameId namespace、Panel 注册名约定、状态文件 schema、阶段路由规则见 `shared/contracts.md`。执行本阶段前应已读取。

## 本阶段核心规则

1. **面板必须继承 `UIPanelBase`（UI Toolkit 模式）或 `UGUIPanelBase`（UGUI 模式）** 并标注 `[Panel("name", gameId: "GameName")]`，数据绑定用 `Bind(property).ToText/ToVisibility/ToEnabled()`。`GameUIControllerBase`/`UGUIGameControllerBase` 继承各自基类，用于刷新频率高的 UI（如 HUD）。从 state file `ui_mode` 字段判定模式。**本 Stage 仅处理 `ui_mode == "ui_toolkit"` 的面板；`ui_mode == "ugui"` 走 `02-ugui-csharp` Stage。**
2. **PanelNames 常量类生成**：编写 Panel 脚本时，必须同时生成一个 `{GameName}PanelNames.cs` 常量类，将所有面板注册名定义为 `public const string`，所有 Push/Switch 调用引用常量而非裸字符串（拼错即编译失败）：
   ```csharp
   public static class MyGamePanelNames
   {
       public const string MainMenu = "MainMenu";
       public const string HUD = "HUD";
       public const string Pause = "Pause";
       public const string GameOver = "GameOver";
   }
   ```
3. **UIRoot 状态路由模式**：当各界面是独立 `[Panel]` + 独立 UXML（本技能默认结构）时，`UIPanelRouter.routingTarget` 必须为 `UIFramePanels`，`allPanels`/rules 必须使用 `[Panel("...")]` 注册名（如 `MainMenu/HUD/GameOver`），禁止填 UXML 内部根节点名（如 `MainMenuRoot`）。仅"一个宿主 UXML 同时内含全部状态根容器"时才用 `VisualElements`

## 中断恢复

| 检查项 | 产出文件 | 存在则跳到 |
|--------|---------|-----------|
| 步骤 10/11 完成 | `context/games/{GameName}/GameScripts/*.cs` + `Assets/Games/{GameName}/Scripts/*.cs` | 步骤 12 |
| 步骤 12 完成 | `Assets/Games/{GameName}/Prefabs/UIRoot.prefab` | 完成（更新 state file） |

---

## 步骤索引

| 步骤 | 名称 | 输入 | 产出 |
|------|------|------|------|
| 10 | 编写 Panel 脚本 | state file 元素清单, brick-mapping.md, UI设计文档.md §6/§8 | {PanelName}.cs + {GameName}PanelNames.cs |
| 11 | 部署 Panel 脚本 | context 侧 .cs | Assets 侧 .cs |
| 12 | UIRoot Prefab + UIGameInstaller | Panel 脚本, PanelSettings, PanelRegistry | UIRoot.prefab, UIGameInstaller.cs |

### 步骤10：编写 UI Panel 脚本

UI Panel 是 C# 脚本，必须继承 `UIPanelBase`。脚本先写入 context，再部署到 Assets，编译验证由调用方安排的后续 C# 闭环统一执行。

#### 10.0 读取状态文件

1. 读取 `ui-panel-builder-state.json`
2. 验证 `01-uxml-uss.status == completed` 且 `uiv_validation_passed == true`，否则报错中止
3. 提取 `interfaces[]` 数组：每个面板的 `panel_name`、`panel_class`、`panel_type`、`is_hud`、`has_dropdown`、`elements[]`
4. 提取 `artifacts.brick_mapping` 路径，读取 brick-mapping.md 获取 `bindingPropsSchema`

#### 10.1 编写 Panel 脚本

1. 使用 `write_file` 将脚本写入 `context/games/{GameName}/GameScripts/{PanelName}.cs`
2. 遍历 `interfaces[]`，对每个面板编写一个 Panel 脚本。脚本内容要点：
   - 必须引用 `using UnityEngine;`、`using UnityEngine.UIElements;` 和 `using Create.GameFramework;`
   - 标注 `[Panel("PanelName", gameId: "{GameName}")]` 属性（`PanelName` 从 state file `panel_name` 字段读取）
   - 继承 `UIPanelBase`。对于 `is_hud == true` 的面板，改用 `GameUIControllerBase`（`UIPanelBase` 子类，额外加了 UIFrame.StateChanged 订阅 + 每帧 `UpdateHud()` 协程）
   - 生命周期重写规则：菜单/弹窗面板只重写 `OnBind()`/`OnShow(param)`/`OnHide()`。`GameUIControllerBase` 子类重写 `QueryElements()`/`UpdateHud()`/`OnSubscribe()`/`OnUnsubscribe()`
   - 在 `OnBind()` 中使用 `Bind()`/`BindButton()` 声明数据绑定。`Bind()` 在 property 为 null 时返回 null，链式调用会 NPE——使用 `?.` 运算符：`Bind(game.Score)?.ToText<Label>("score-label")`
   - 使用 `SafeQ<T>("name")` 查询 UXML 中的命名元素。`SafeQ` 在找不到元素时 Warn 而非 NPE
   - 使用 `Resolve<T>()` 获取 DI 注册的游戏服务
   - 使用 `Push()`/`Pop()`/`Switch()` 进行面板导航。`Push()` 推入栈顶（弹窗），`Switch()` 替换栈顶（主画面切换），`Show()` 仅控制已在栈中的面板可见性（不能让未入栈的面板出现）
   - 面板间导航使用 `Push("name")`/`Pop()`，禁止面板间直接引用

#### 10.2 元素清单 → 绑定代码映射

从 state file `interfaces[].elements[]` 读取每个元素，按 `type` 和 `functional_type` 生成对应的 C# 代码。基础 `SafeQ<T>` 泛型参数选择见 `shared/contracts.md` 元素类型 → C# SafeQ 映射表，以下为需要额外代码的执行级细节：

| state file `type` | 额外代码 |
|-------------------|---------|
| Button | `BindButton("name")` 绑定点击回调 |
| Slider | 双向绑定模式（读 `references/uipanel-patterns.md` §2）+ `SetupSliderFill(slider)` 同步 tracker 宽度与 fill 纹理防拉伸（读 `references/uitk/functional-control-styles.md`「fill 宽度必须手动同步」） |
| ProgressBar | `SetupProgressBarFill(bar)` 注入 fill 纹理子元素防拉伸 + `__progress` 高度 C# 同步（Tuanjie 2022.3 引擎缺陷，USS `height:100%` 无法覆盖 `__container` 硬编码 24px，必须 C# 设像素值 + `GeometryChangedEvent` 监听，读 `references/uitk/functional-control-styles.md` ProgressBar 章节） |

**functional_type 非 null 的元素**：读 `references/uipanel-patterns.md` 对应模式（如 Slider 双向绑定 §2）。`functional_type` 从 state file 元素清单读取，不从 brick-mapping.md 重新推断。

**bindingPropsSchema → 绑定代码映射**：从 brick-mapping.md 读取 `bindingPropsSchema`（brick 可绑定字段及 `runtimeType`），映射规则读 `references/uipanel-patterns.md` §6

#### 10.3 框架 API 按需查

签名不确定时查 `references/UIFrame-API.md`（含 Bind/BindButton/SafeQ/Push/Pop/Switch/Resolve/Track 等方法签名）。仍找不到再查框架源码 `Packages/cn.tuanjie.create.ui/Runtime/`（`Panel/UIPanelBase.cs`、`UIFrame.cs` 等）。Slider 双向绑定、文本格式化、UIGameInstaller 适配器等模式读 `references/uipanel-patterns.md`（按需查对应小节）

#### 10.4 契约 pass（一次）

编写前先列出每个 Panel 要绑定的 BindableProperty 名称和类型（名称从 UI设计文档.md §6 数据绑定关系获取，类型 runtimeType 从 brick-mapping.md 的 bindingPropsSchema 获取），防签名对不上

#### 10.5 导航链完整性（必过）

编写 Panel 脚本前，从 UI设计文档.md §8 导航关系表提取所有 Push/Switch 调用，确保每条导航关系都有对应的代码实现。编写后逐条对照 §8 表格验证：
```
导航关系对照表（从 UI设计文档.md §8 提取）：
| 来源面板 | 触发 | 目标面板 | 操作 | 代码位置 | ✅/❌ |
|---------|------|---------|------|---------|-------|
| MainMenu | Play 按钮 | HUD | StartGame() → Switch(HUD) | BindButton: StartGame(); UIGameInstaller: Switch(HUD) | ✅ |
| HUD | GameOver 事件 | GameOver | Switch | UIGameInstaller: Switch("GameOver") | ❌ 缺失 |
```
导航去重规则（读 `references/uipanel-patterns.md` §1）：BindButton 只调用游戏逻辑方法（如 `StartGame()`），不调用 Push/Pop/Switch。导航触发源二选一：UIGameInstaller 订阅 `UIFrame.StateChanged` 状态后调用，或游戏服务方法内完成状态变更（`UIFrame.SetGameState`）+ 导航。例外：纯 UI 层切换（如打开说明页）可直接在 BindButton 中 Push。⚠️ §8 的 Push/Pop 是意图级描述，落地语义按 patterns §1「导航语义与确定性返回」换算（全屏互斥面板一律 Switch；弹窗 Push 进入、Switch(来源面板) 返回），落地后回写 UI设计文档.md §8

#### 10.6 生成 PanelNames 常量类

同时生成 `{GameName}PanelNames.cs` 常量类（核心规则 2）。从 state file `interfaces[].panel_name` 提取所有面板注册名。

### 步骤11：部署 UI Panel 脚本

- 源目录：`context/games/{GameName}/GameScripts/`（仅 UI Panel 脚本）
- 目标目录：`Assets/Games/{GameName}/Scripts/`
- 使用 `run_shell_command` 复制文件

### 步骤12：UIRoot Prefab 装配 + UIGameInstaller

> namespace 必须与 gameId 一致（`shared/contracts.md` gameId namespace 一致性）、导航去重（读 `references/uipanel-patterns.md` §1）、Bootstrap 初始面板名禁止 [SerializeField]。

此步骤创建 UIRoot prefab 和 UIGameInstaller 脚本，不做编译验证、Play Mode 验证或场景搭建。

**导航链完整性最终验证（必过）**：创建 UIRoot prefab 前，对照步骤 10 的导航关系对照表，逐条确认每条导航关系都有对应的代码实现。重点检查：
- MainMenu → Play 按钮 → BindButton 调 StartGame()，UIGameInstaller 订阅事件后 Switch(HUD)
- HUD → GameOver 事件 → UIGameInstaller Switch(GameOver)
- HUD → 暂停按钮 → BindButton 调 PauseGame()，UIGameInstaller 订阅事件后 Push(Pause) 或 Switch(Pause)
- GameOver → 再玩一次 → BindButton 调 RestartGame()，UIGameInstaller 订阅事件后 Switch(HUD)
- GameOver → 返回主菜单 → BindButton 调 ReturnToMenu()，UIGameInstaller 订阅事件后 Switch(MainMenu)
- 每个标准状态（Menu/Playing/Paused/GameOver）都有至少一个面板在该状态 Show/Switch

#### 12a. 创建 UIRoot GameObject

1. **创建 PanelRegistry 资产**：必须使用 `PanelRegistryBuilder.Create(path).ReplaceEntries(...).Save()` 创建并配置 `Assets/Games/{GameName}/UI/PanelRegistry.asset`，禁止通过 `SerializedObject.FindProperty("_entries")` 或反射依赖私有字段。每项传入 panelName、gameId 与 VisualTreeAsset；面板名和 UXML 路径从 state file `interfaces[].panel_name` 和 `interfaces[].uxml_assets_path` 读取。完整签名查 `unity-operator/assets/generated-api/PanelRegistryBuilder.md`，并消费 Save 的 fail-fast/readback 报告。
2. **创建 UIRoot GameObject**，挂载：
   - `UIDocument` 组件，引用 state file `artifacts.panel_settings` 路径对应的 `GamePanelSettings.asset`
   - 不设置 `visualTreeAsset`（由 UIManager 从 PanelRegistry 加载）
   - `UIPanelRouter`：默认独立 Panel UXML 结构必须设 `routingTarget=UIFramePanels`；`allPanels` 与每条 rule 填 PanelNames/`[Panel]` 注册名，不填 `*Root` 元素名
3. **保存为 `Assets/Games/{GameName}/Prefabs/UIRoot.prefab`**，保存后标记场景脏并保存场景：

```csharp
UnityEditor.PrefabUtility.SaveAsPrefabAsset(uiRootGo, prefabPath);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (string.IsNullOrEmpty(scene.path)) throw new System.InvalidOperationException("UIRoot assembly requires a path-backed scene");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scene.path);
```

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 主 UIDocument 显示灰色方块 | 空 rootVisualElement 默认渲染 | `Initialize()` 后设置 `_uiDocument.rootVisualElement.style.display = DisplayStyle.None` |

#### 12b. 创建 UIGameInstaller 脚本

UIGameInstaller 是 UI 框架的启动入口。它负责：
1. `UIFrame.Initialize(uiDocument, gameId, registry)` 初始化 UI 框架（registry 为步骤 12a 创建的 PanelRegistry）
2. 注册 DI 服务（IGameService 及 Panel 脚本中 `Resolve<T>()` 引用的所有游戏逻辑类型）
3. 订阅 `UIFrame.StateChanged` 做面板状态路由（Menu→MainMenu, Playing→HUD, Paused→Pause, GameOver→GameOver 等）；游戏侧在状态变化时调 `UIFrame.SetGameState(state)`
4. 推入初始面板（MainMenu）。初始面板名必须从 `PanelNames` 常量类读取（`private readonly string _initialPanel = MyGamePanelNames.MainMenu;`），禁止 `[SerializeField] private string _initialPanel = "MainMenu"`——场景保存的旧序列化值会覆盖代码修改后的默认值
5. DropdownField popup USS 修复：在 `OnPanelPushed` 回调中将面板 USS 添加到 `panel.visualTree`。代码模板读 `references/uitk/functional-control-styles.md` DropdownField 章节

**DropdownField 判定**：从 state file `interfaces[].has_dropdown` 读取。`has_dropdown == true` 的面板，UIGameInstaller 必须包含 DropdownField popup USS 修复代码。

**编写流程**：

1. **扫描 Resolve<T>() 调用**：grep 步骤 10 产出的全部 Panel 脚本中的 `Resolve<` 调用，收集所有被 Resolve 的类型
2. **读取状态→面板映射**：从 `UI设计文档.md` §14 读取非事件式跨 Job 调用合同，以 §14 职责/公共接口暴露锚点 + 框架状态契约，并结合 `UI设计文档.md` §9 映射确认每个 GameStates 值对应面板
3. **编写 UIGameInstaller.cs**（双写到 `context/games/{GameName}/GameScripts/` 和 `Assets/Games/{GameName}/Scripts/`）：
   - namespace 必须与 gameId 一致（`shared/contracts.md` gameId namespace 一致性）
   - 普通 `MonoBehaviour`（配置型启动器，`uiDocument` 由 Inspector 填写）。重复初始化防护由 UIFrame 幂等承担：同一 UIDocument 重复初始化仅 LogWarning 跳过，换 UIDocument（切场景）自动 `Reset()` 后重新初始化——`Start()` 中无需手写去重守卫
   - `Start()` 中依次执行：`UIFrame.Initialize(uiDocument, gameId, registry)` → `UIFrame.DI.RegisterSingleton` 注册所有 Resolve<T>() 扫描到的类型 → `UIFrame.StateChanged += OnGameStateChanged` → `UIFrame.GetUIManager().Push(初始面板)`
   - 必须包含 `[SerializeField] PanelRegistry panelRegistry` 字段，在 `Start()` 中传给 `UIFrame.Initialize`。缺 registry 会报 Error 并中止初始化
   - `OnGameStateChanged` 中按上述可用合同做面板路由
   - `OnDestroy()` 中 `UIFrame.StateChanged -= OnGameStateChanged` + 取消订阅 `OnPanelPushed`
4. **将 UIGameInstaller 挂到 UIRoot prefab**：用 `execute_csharp_script` 给 UIRoot prefab 添加 UIGameInstaller 组件，并设 `uiDocument` 和 `panelRegistry` 字段引用

> `UIFrame.StateChanged`（载荷 `GameStateChangedEvent`）是本包自有的 UI 状态契约，订阅即真实派发；游戏侧通过 `UIFrame.SetGameState(state)` 驱动，无状态机依赖。
>
> DI 注册遗漏是运行时 NPE 头号根因：Panel 中每个 `Resolve<T>()` 都必须在 UIGameInstaller 有对应的 `UIFrame.DI.RegisterSingleton<T>(instance)`。漏注册则运行时 `Type not registered in DI` 崩溃。

UIGameInstaller 适配器规范读 `references/uipanel-patterns.md` §1。

#### 12c. 更新状态文件

UIRoot prefab 创建完成后，用部署型工具更新 `ui-panel-builder-state.json`（禁止手改 JSON 与手写时钟）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "02-csharp-integration", "completed", timestamps: true);` —— 写入 `status: completed` + `completed_at`（系统时钟，与 schema 格式一致）

---

## Completion Checklist

**以下全部满足方可报告"Stage 2 完成"**：

1. [步骤10+11] Panel 脚本规范（UIPanelBase/gameId/Bind/using），已部署到 `Assets/Games/{GameName}/Scripts/`
2. [步骤10] 所有含 Slider 的面板已在 OnBind() 中调用 `SetupSliderFill(slider)`（UIPanelBase 基类方法，封装 tracker 宽度同步 + fill 纹理防拉伸），否则 tracker 始终 100% 填充且纹理被拉伸
2b. [步骤10] 所有含 ProgressBar 的面板已在 OnBind() 中调用 `SetupProgressBarFill(bar)`（fill 纹理防拉伸），并通过 `progress.style.height = bar.layout.height` + `GeometryChangedEvent` 同步 `__progress` 高度（Tuanjie 2022.3 引擎缺陷：`__container` 硬编码 24px，USS `height:100%` 无效），否则 fill 高度只有 24px 不撑满 track
3. [步骤10] PanelNames 常量类已生成（所有 Push/Switch 引用常量而非裸字符串）
4. [步骤10+12] 导航链完整（UI设计文档 §8 每条导航关系都有对应 Push/Switch 代码实现）
5. [步骤12] UIRoot prefab 创建完成（含 PanelRegistry 资产）
6. [步骤12] UIGameInstaller 脚本已创建并挂到 UIRoot prefab（含 `[SerializeField] PanelRegistry` 字段 + DI 注册全部 Resolve<T>() 类型 + 游戏状态→面板路由）
7. [步骤12] UIPanelRouter 为独立 Panel 结构时已设 `UIFramePanels`，规则名全部匹配 `[Panel]` 注册名
8. [步骤12] DropdownField popup USS 修复代码已包含（若 state file 中任一面板 `has_dropdown == true`）
9. [步骤12c] **ui-panel-builder-state.json 已更新**（`02-csharp-integration` 设为 `completed`）

**以下验证不在此阶段范围内，由调用方安排后续任务负责**：
- 编译验证 — 由宿主提供的 C# 编译部署闭环或调用方安排的后续任务统一做
- 场景搭建 — 由调用方安排的后续任务负责（实例化 UIRoot prefab 到场景）
- Play Mode 验证 — 由调用方安排的后续任务负责
- 视觉溢出检查 — 由调用方安排的后续任务负责
- UIDocument UXML 路径冲突检测 — 由调用方安排的后续任务负责

## 与其他模块的协作关系

### 依赖宿主 C# 闭环的产出
- UI Panel 脚本引用游戏逻辑脚本暴露的 BindableProperty 和公共属性

### 为场景搭建任务提供
- UIRoot prefab（步骤 12 产出，含 UIDocument + PanelSettings + UIGameInstaller + PanelRegistry 引用）
- PanelSettings 资产路径（供 UIDocument 组件引用，从 state file `artifacts.panel_settings` 获取）
- PanelRegistry 资产路径（供 UIGameInstaller 的 panelRegistry 字段引用）
- UI Panel 脚本已部署到 `Assets/Games/{GameName}/Scripts/`
- UI 图片已部署到 `Assets/Games/{GameName}/UI/Images/`（Stage 1 验证通过）
- 后续场景搭建由调用方安排的后续任务负责（实例化 UIRoot prefab、编译验证、Play Mode 测试）；本技能不进行场景搭建
- 多场景/关卡游戏：每次 `UIFrame.Initialize` 只能执行一次。切场景前必须调用 `UIFrame.Reset()` 清理旧状态，新场景的 UIGameInstaller `Start()` 中重新 `Initialize`。单场景游戏无需调用 `Reset()`
