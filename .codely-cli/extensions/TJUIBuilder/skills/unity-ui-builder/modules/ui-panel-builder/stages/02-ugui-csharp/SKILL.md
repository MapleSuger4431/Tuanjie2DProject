# Stage 02: UGUI C# 脚本 + 集成

> ⛔ **模式守卫**：本 Stage 仅适用于 `ui_mode == "ugui"`。`ui_mode == "ui_toolkit"` 时走 `02-csharp-integration`。

Steps 10-12：Panel 脚本编写 → 部署 → UIRoot Prefab 装配 + UIGameInstaller。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `ui-panel-builder-state.json`（Stage 1 产出）、`brick-mapping.md`、UI设计文档.md §6/§8/§9 |
| 输出 | Panel 脚本（`.cs`）、`{GameName}PanelNames.cs`、`UIGameInstaller.cs`、`UIRoot.prefab`、`PanelRegistry.asset` |
| 前置门禁 | `ui-panel-builder-state.json` 中 `01-ugui-prefab.status == completed` |
| 完成判据 | UIRoot prefab 创建完成 + UIGameInstaller 挂载 + ui-panel-builder-state.json 更新为 completed |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」UGUI 模式 Stage 02 部分 |
| 必须激活技能 | csharp-code-writer + unity-operator——C# 脚本编写走 csharp-code-writer 闭环；UIRoot.prefab/PanelRegistry 经 execute_csharp_script；subagent 第 0 步先激活再读文档 |

> **共享契约**：框架守卫、路径守卫、UIGameInstaller 门禁、gameId namespace、Panel 注册名约定、状态文件 schema、阶段路由规则见 `shared/contracts.md`。

## 本阶段核心规则

1. **面板必须继承 `UGUIPanelBase`** 并标注 `[Panel("name", gameId: "GameName")]`。`is_hud == true` 的面板继承 `UGUIGameControllerBase`。从 state file `ui_mode` 字段确认模式为 `ugui`
2. **PanelNames 常量类生成**：所有 Push/Switch 调用引用常量而非裸字符串
3. **UGUI 模式 UIRoot**：由 `BrickPanelBuilder.BuildUIRoot(prefabPath, w, h)` 一行构建（内置 Canvas + CanvasScaler + GraphicRaycaster + sortingOrder=0 + 防黑屏修复 + [VERIFY]）。无 UIDocument、无 PanelSettings、无 UIPanelRouter。skill 不手写 GameObject/组件/防黑屏代码

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
| 12 | UIRoot Prefab + UIGameInstaller | Panel 脚本, Panel Prefabs, PanelRegistry | UIRoot.prefab, UIGameInstaller.cs |

### 步骤10：编写 UI Panel 脚本

UGUI Panel 是 C# 脚本，必须继承 `UGUIPanelBase`。脚本先写入 context，再部署到 Assets。

#### 10.0 读取状态文件

1. 读取 `ui-panel-builder-state.json`
2. 验证 `01-ugui-prefab.status == completed`，否则报错中止
3. 提取 `interfaces[]` 数组：每个界面的 `panel_name`、`panel_class`、`panel_type`、`is_hud`、`has_dropdown`、`trees[]`——每棵树含 `prefab_assets_path` 与完整元素清单 `elements[]`（含 `name`、`path`、`type`、`functional_type`）
4. 提取 `artifacts.brick_mapping` 路径，读取 brick-mapping.md 获取 `bindingPropsSchema`

#### 10.1 编写 Panel 脚本

1. 使用 `write_file` 将脚本写入 `context/games/{GameName}/GameScripts/{PanelName}.cs`
2. 遍历 `interfaces[]`，对每个界面编写一个 Panel 脚本。脚本内容要点：
   - using `UnityEngine`、`UnityEngine.UI`、`TMPro`（若用 TMP）、`Create.GameFramework`（不需要 `UnityEngine.UIElements`）
   - 标注 `[Panel("PanelName", gameId: "{GameName}")]` 属性
   - 继承 `UGUIPanelBase`。对于 `is_hud == true` 的面板，改用 `UGUIGameControllerBase`
   - 生命周期重写规则：
     - 菜单/弹窗面板：重写 `OnBind()`/`OnShow(param)`/`OnHide()`
     - `UGUIGameControllerBase` 子类：重写 `QueryElements()`/`UpdateHud()`/`OnSubscribe()`/`OnUnsubscribe()`
   - 在 `OnBind()` 中使用 `Bind()`/`BindButton()` 声明数据绑定
   - 使用 `SafeFind<T>(path)` 查询 UGUI 元素（path 从 state file `elements[].path` 读取）
   - 使用 `Resolve<T>()` 获取 DI 注册的游戏服务
   - 使用 `Push()`/`Pop()`/`Switch()` 进行面板导航

#### 10.2 UGUI 绑定代码示例

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Create.GameFramework;

namespace MyGame
{
    [Panel("MainMenu", gameId: "MyGame")]
    public class MainMenuPanel : UGUIPanelBase
    {
        protected override void OnBind()
        {
            var game = Resolve<IGameService>();
            Bind(game.Score)?.ToTextUGUI("CenterContent/ScoreLabel");
            Bind(game.Health)?.ToVisibilityUGUI("BottomBar/HealthBar", true);
            BindButton("CenterContent/PlayButton", () => game.StartGame());
            BindButton("CenterContent/SettingsButton", () => Push(MyGamePanelNames.Settings));
        }
    }
}
```

```csharp
// HUD 面板（继承 UGUIGameControllerBase）
using UnityEngine;
using UnityEngine.UI;
using Create.GameFramework;

namespace MyGame
{
    [Panel("HUD", gameId: "MyGame")]
    public class HUDPanel : UGUIGameControllerBase
    {
        private Text _scoreText;

        protected override void QueryElements()
        {
            _scoreText = SafeFind<Text>("TopBar/ScoreText");
        }

        protected override void UpdateHud()
        {
            var game = Resolve<IGameService>();
            _scoreText.text = game.Score.Value.ToString();
        }

        protected override void OnSubscribe()
        {
            var game = Resolve<IGameService>();
            game.Health.Subscribe(v =>
            {
                // 更新血条 Image fillAmount
                var healthBar = SafeFind<Image>("BottomBar/HealthFill");
                if (healthBar != null) healthBar.fillAmount = v / 100f;
            });
        }
    }
}
```

#### 10.3 UGUIBindingBuilder 方法映射

从 state file `interfaces[].trees[].elements[]` 读取每个元素，按 `type` 和 `functional_type` 生成对应的 C# 代码：

| state file `type` | SafeFind 泛型 | 额外代码 |
|-------------------|-------------|---------|
| Button | `SafeFind<Button>(path)` | `BindButton(path, action)` |
| Label | `SafeFind<Text>(path)`——路径须带 `/Text`（Text 挂在 label 实例的 "Text" 子节点） | `Bind(property)?.ToTextUGUI(path)` 或 `ToTMP(path)`——path 可指 brick 实例根，节点无 Text 时自动回退子树解析 |
| Slider | `SafeFind<Slider>(path)` | 双向绑定：`slider.onValueChanged.AddListener(...)` + `Track(() => slider.onValueChanged.RemoveAllListeners())`；**无需 SetupSliderFill**（UGUI 原生处理） |
| Toggle | `SafeFind<Toggle>(path)` | `toggle.onValueChanged.AddListener(...)` + `Track(...)` |
| DropdownField | `SafeFind<Dropdown>(path)` | `dropdown.onValueChanged.AddListener(...)` + `Track(...)`；**无 popup USS 修复**（UGUI 不需要） |
| TextField | `SafeFind<InputField>(path)` | `inputField.onEndEdit.AddListener(...)` + `Track(...)` |
| VisualElement | `SafeFind<Transform>(path)` | `Bind(property)?.ToVisibilityUGUI(path, matchValue)` |

⚠️ **UGUI 模式无 `SetupSliderFill` / `SetupProgressBarFill`**：UGUI Slider 的 fillRect 自动随 value 缩放，ProgressBar 用 Slider(readonly) 同理。不需要 C# 手动同步。

⚠️ **UGUI 模式无 DropdownField popup USS 修复**：UGUI Dropdown 自带 popup 模板，不需要 UI Toolkit 模式的 popup USS 选择器修复。

#### 10.4 契约 pass

编写前先列出每个 Panel 要绑定的 BindableProperty 名称和类型（名称从 UI设计文档.md §6 数据绑定关系获取，类型 runtimeType 从 brick-mapping.md 的 bindingPropsSchema 获取），防签名对不上。

#### 10.5 导航链完整性（必过）

编写 Panel 脚本前，从 UI设计文档.md §8 导航关系表提取所有 Push/Switch 调用，确保每条导航关系都有对应的代码实现。

⚠️ §8/§10 的 Push/Pop 是**意图级**描述，落地必须按 `references/uipanel-patterns.md` §1「导航语义与确定性返回」换算，禁止照抄字面：
- 全屏互斥面板（§4 界面清单「类型」列 = 全屏/常驻）互转一律 `Switch`——进入与返回都是。`Push` 进入会与同 sortingOrder 面板双渲染；`Pop` 返回作用在被 Switch 清空的栈上会黑屏
- 弹窗（§4「类型」列 = 弹窗）用 `Push` 进入并**传宿主面板注册名作 param**，返回按钮 `Switch(host)` 确定性切回（长会话栈状态不可靠，禁止依赖 `Pop`）
- §10 中 `PauseGame() + Push("Pause")` 这类「服务方法 + 导航」并列写法是**双触发源**，落地只保留一个（服务方法内导航 或 事件订阅，二选一）
- 落地后把最终导航语义**回写 UI设计文档.md §8/§10**——可复现要求：下次按提示词重建必须得到同一导航行为（合金弹头反例：实现已演化为 Switch/宿主返回，文档仍停留在 Push/Pop）

#### 10.6 生成 PanelNames 常量类

同时生成 `{GameName}PanelNames.cs` 常量类。从 state file `interfaces[].panel_name` 提取所有面板注册名。

#### 10.7 动效绑定（UIAnimationHelper）

API 全集见 `references/UIAnimation-API.md`。绑定规则——**安全性与样板均由 C# API 内建，无需记忆 LayoutGroup 矩阵**：

1. **OnBind()**：一行 `UIAnimationHelper.AttachButtonEffects(transform)` 批量挂载本面板所有激活按钮的 hover/press 特效（幂等）
2. **OnShow() 首行**：`StartCoroutine(UIAnimationHelper.PanelFadeIn(transform))` 面板级入场（全屏面板 slide=30；弹窗可用 slide=0 纯渐显）
3. **OnShow() 交错入场**：`StartCoroutine(UIAnimationHelper.StaggerIn(parent, ...))` —— parent 传子元素的**直接容器**（如 `ButtonGroup`、弹窗 `panel_bg` 根）；API 自动检测 LayoutGroup（有则纯 alpha 渐显、无则滑入），单协程驱动，**无需自查容器类型、无需传 host**
4. **数据回调（Bind 订阅内）**：`StartCoroutine(UIAnimationHelper.SmoothSliderTo(slider, v))` / `ScoreRollTo(label, v)` —— `from` 自动取当前显示值
5. **点缀动效**：弹窗根 `PopIn`；Logo/图标 `LogoBounceIn` + `IdleFloat`；标题 `TitleGlow`（`IdleFloat` 动 localPosition，仅限无 LayoutGroup 父级的锚定子元素；`TitleGlow` 仅动 color，LayoutGroup 子元素同样安全）
6. 显式逐元素控制时才用 `StaggerFadeIn`/`StaggerEntrance` 的 host 重载；旧 `RectTransform` 签名仅为兼容保留，禁止新代码使用
7. **OnHide() 必须清理**：面板显隐走 `Canvas.enabled`（GameObject 常驻激活，非 SetActive）——协程不随隐藏停止，OnShow 重入会叠加；`OnHide()` 中 `StopAllCoroutines()`，`OnShow()` 重启入场/循环动效

### 步骤11：部署 UI Panel 脚本

- 源目录：`context/games/{GameName}/GameScripts/`（仅 UI Panel 脚本）
- 目标目录：`Assets/Games/{GameName}/Scripts/`
- 使用 `run_shell_command` 复制文件

### 步骤12：UIRoot Prefab 装配 + UIGameInstaller

> namespace 必须与 gameId 一致、导航去重、Bootstrap 初始面板名禁止 [SerializeField]。

此步骤创建 UIRoot prefab 和 UIGameInstaller 脚本，不做编译验证、Play Mode 验证或场景搭建。

#### 12a. 创建 PanelRegistry 资产 + UIRoot

1. **创建 PanelRegistry 资产**：使用 `PanelRegistryBuilder.Create(path).ReplacePrefabEntries(...).Save()` 创建并配置 `Assets/Games/{GameName}/UI/PanelRegistry.asset`。每项传入 panelName、gameId 与 Prefab 引用（`PrefabEntrySpec`）。注册 `interfaces[].trees[]` 中所有 panel 型树的 Prefab：面板名从 state file `interfaces[].panel_name` 读取，Prefab 路径从对应 panel 型树的 `prefab_assets_path` 读取。list_item 模板树默认跳过不注册，除非确需单独注册（panel 型树 / list_item 模板树按根 brickId `ugui.panel` / `ugui.list_item` 区分，查 brick-mapping.md 树名表）。

```csharp
// PanelRegistry 创建（UGUI 模式用 ReplacePrefabEntries，注册所有 panel 型树）
// ⚠️ PanelRegistryBuilder 位于 Create.Utilities 命名空间（using Create.Utilities;）；
//    BrickPanelBuilder 位于 Create.GameFramework 命名空间
var specs = interfaces.SelectMany(i => i.trees
        .Where(t => t.root_brickId == "ugui.panel") // list_item 模板树默认跳过，除非需单独注册
        .Select(t => new PanelRegistryBuilder.PrefabEntrySpec(
            gameId, i.panel_name,
            AssetDatabase.LoadAssetAtPath<GameObject>(t.prefab_assets_path)
        )))
    .ToArray();
var report = PanelRegistryBuilder.Create($"Assets/Games/{gameName}/UI/PanelRegistry.asset")
    .ReplacePrefabEntries(specs)
    .Save();
// UIRoot 一行构建（替代原手工 GameObject + 组件 + 防黑屏修复）
var uiRootResult = BrickPanelBuilder.BuildUIRoot(
    $"Assets/Games/{gameName}/Prefabs/UIRoot.prefab", refWidth, refHeight);
if (!uiRootResult.success || uiRootResult.warnings.Count > 0)
    Debug.LogWarning($"[UIRoot] success={uiRootResult.success}: " + string.Join("; ", uiRootResult.warnings));
```

2. **UIRoot Prefab 由 `BrickPanelBuilder.BuildUIRoot` 直接产出**（一行调用，见上方代码块；分辨率来自 UI设计文档.md §1）。API 内置：RectTransform stretch/scale=1、Canvas（Screen Space - Overlay, sortingOrder=0）、CanvasScaler（ScaleWithScreenSize, match=0.5）、GraphicRaycaster、**不挂 UIDocument/UIPanelRouter**（UGUI 模式约定）、保存后防黑屏修复（SerializedObject 直修，⛔ 勿用 LoadPrefabContents）与 `[VERIFY]` 验证 —— **手工构建与防黑屏代码全部废弃**。

3. **将 Panel 脚本组件挂到各 Panel Prefab 上**：用 `execute_csharp_script` 给每个 Panel Prefab 添加对应的 Panel 脚本组件。要点：
   - 流程：`PrefabUtility.LoadPrefabContents(path)` → `AddComponent<T>()` → `SaveAsPrefabAsset` → `UnloadPrefabContents`，保存后 **readback 验证**组件已挂上
   - **幂等**：已挂同类型组件则跳过（重复运行不重复添加）
   - **list_item 模板树不挂 Panel 组件**（模板无面板语义）
   - trees.json 重建（BuildBrickTrees 覆盖保存）后组件会随重建丢失，须重挂并 readback（见 ugui-prefab-construction.md §5.5）
#### 12b. 创建 UIGameInstaller 脚本

UIGameInstaller 是 UGUI 模式的 UI 框架启动入口：

```csharp
using UnityEngine;
using UnityEngine.UI;
using Create.GameFramework;

namespace MyGame
{
    public class UIGameInstaller : MonoBehaviour
    {
        [SerializeField] PanelRegistry panelRegistry;

        void Start()
        {
            // UGUI 模式初始化（替代 UIFrame.Initialize）
            // UIFrame 幂等：同一 UIRoot 重复初始化仅 LogWarning 跳过；切换场景（新 UIRoot）自动 Reset 重来
            UIFrame.InitializeUGUI(transform, gameId: "MyGame", registry: panelRegistry);

            // 注册 DI 服务
            UIFrame.DI.RegisterSingleton<IGameService>(new GameService());

            // 订阅 UI 状态变化做面板路由（游戏侧调 UIFrame.SetGameState 驱动）
            UIFrame.StateChanged += OnGameStateChanged;

            // 推入初始面板
            UIFrame.GetUIManager().Push(MyGamePanelNames.MainMenu);
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            var uiManager = UIFrame.GetUIManager();
            switch (e.NewState)
            {
                case GameStates.Menu:
                    uiManager.Switch(MyGamePanelNames.MainMenu);
                    break;
                case GameStates.Playing:
                    uiManager.Switch(MyGamePanelNames.HUD);
                    break;
                case GameStates.GameOver:
                    uiManager.Switch(MyGamePanelNames.GameOver);
                    break;
            }
        }

        void OnDestroy()
        {
            UIFrame.StateChanged -= OnGameStateChanged;
        }
    }
}
```

**UGUI 模式与 UI Toolkit 模式的关键差异**：
- 调用 `UIFrame.InitializeUGUI(transform, gameId, registry)` 而非 `UIFrame.Initialize(uiDocument, gameId, registry)`
- `UIFrame.StateChanged`（载荷 `GameStateChangedEvent`）是本包自有的 UI 状态契约，订阅即真实派发；游戏侧通过 `UIFrame.SetGameState(state)` 驱动，无状态机依赖
- 传入 `transform`（UIRoot 的 Transform）而非 `uiDocument`（UIDocument）
- 无 `OnPanelPushed` 回调中的 DropdownField popup USS 修复代码
- 无 `[SerializeField] UIDocument uiDocument` 字段
- PanelRegistry 使用 Prefab 引用（已通过 `ReplacePrefabEntries` 配置）

**编写流程**：
1. 扫描 Resolve<T>() 调用，收集所有被 Resolve 的类型
2. 读取状态→面板映射
3. 编写 UIGameInstaller.cs（双写到 context 和 Assets）
4. 将 UIGameInstaller 挂到 UIRoot prefab

#### 12c. 更新状态文件

UIRoot prefab 创建完成后，用部署型工具更新 `ui-panel-builder-state.json`（禁止手改 JSON 与手写时钟）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "02-ugui-csharp", "completed", timestamps: true);` —— 写入 `status: completed` + `completed_at`（系统时钟，与 schema 格式一致）

---

## Completion Checklist

1. [步骤10+11] Panel 脚本规范（UGUIPanelBase/gameId/Bind/using），已部署到 `Assets/Games/{GameName}/Scripts/`
2. [步骤10] PanelNames 常量类已生成
3. [步骤10+12] 导航链完整（UI设计文档 §8 每条导航关系都有对应 Push/Switch 代码实现）
4. [步骤12] UIRoot prefab 创建完成（Canvas + CanvasScaler + GraphicRaycaster + UIGameInstaller）
5. [步骤12] PanelRegistry 资产已创建（使用 ReplacePrefabEntries，Prefab 引用）
6. [步骤12] Panel 脚本组件已挂到各 Panel Prefab 上
7. [步骤12] UIGameInstaller 调用 `UIFrame.InitializeUGUI()` 而非 `UIFrame.Initialize()`
8. [步骤12c] **ui-panel-builder-state.json 已更新**（`02-ugui-csharp` 设为 `completed`）
9. [步骤10.7] 动效使用推荐 API（`StaggerIn` 自动 LayoutGroup 检测、`AttachButtonEffects` 批量幂等、`SmoothSliderTo`/`ScoreRollTo` 自取 from；`IdleFloat` 仅锚定子元素，`TitleGlow` 色彩脉冲全场景安全）；`OnHide()` 已 `StopAllCoroutines()` 清理
10. [步骤10.5] 导航按 §4 面板类型换算落地（全屏 Switch / 弹窗 Push+Switch(host) 返回、单触发源），且最终导航语义已回写 UI设计文档.md §8/§10

**以下验证不在此阶段范围内**：
- 编译验证 — 由宿主提供的 C# 编译部署闭环统一做
- 场景搭建 — 由调用方负责
- Play Mode 验证 — 由调用方负责
