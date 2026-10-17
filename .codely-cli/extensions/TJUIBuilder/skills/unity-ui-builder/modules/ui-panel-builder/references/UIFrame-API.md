# UIFrame API 速查手册

> 给 csharp-code-writer / unity-developer 读的 API 参考。框架源码在 `Packages/cn.tuanjie.create.ui/Runtime/`（命名空间 `Create.GameFramework`）。
>
> **UI 框架修改规则**：默认禁止修改 Package 源码（`Packages/cn.tuanjie.create.ui/Runtime/`），游戏级需求靠扩展解决，避免污染共享工具库。遇到框架不支持或存在 Bug 时：
> 1. **在游戏代码中扩展**：通过 partial class、扩展方法、子类化扩展框架能力，不改动 Package 源文件
> 2. **新增文件到游戏目录**：在 `Assets/Games/{GameName}/Scripts/` 下新建扩展文件（如 `CustomBinding.cs`），不往 Package 里加文件
> 3. **⛔ 禁止在游戏目录复制框架同名类**（如本地 `UIAnimationHelper.cs`）：同命名空间遮蔽副本会静默屏蔽框架升级，一律 `using Create.GameFramework;` 直接引用包内类
> 4. **扩展不可行时报告**：框架缺少某类 UI 或存在 Bug，向调用方报告
>
> ⚠️ 静态入口类名为 `UIFrame`（非 `UIFramework`），以避免与同名命名空间碰撞。
>
> ⚠️ **UI 状态契约**：`GameStates`（状态名常量）与 `GameStateChangedEvent`（事件载荷）来自 `Runtime/Core/UIStateChannel.cs`；UI 状态的唯一驱动入口是 `UIFrame.SetGameState(string)`（广播 `UIFrame.StateChanged`，HUD 基类/UIPanelRouter 自动订阅联动）。本包不定义状态机/事件总线——游戏侧（游戏编排 extension 或胶水脚本）负责在合适的时机调用 `SetGameState`。
>
> 🎯 **准星（FPS/TPS）不在此框架**：用 `CrosshairFactory`（矢量绘制、7 风格），见 `Packages/cn.tuanjie.create.ui/Runtime/Widgets/CrosshairFactory.cs`；把返回的 overlay `Add` 到任意 HUD 根或 UIDocument rootVisualElement 即可。
>
> 📌 **权威源声明**：框架源码（`Packages/cn.tuanjie.create.ui/Runtime/`）是 API 签名的唯一权威源。本文件是 UI 面板开发的补充教程，包含代码示例、常见模式和陷阱说明，但不保证与源码实时同步。如遇签名不一致，以源码为准。

---

## 快速开始

### 1. 游戏启动时初始化（UIGameInstaller，AI 编写）

> ⚠️ **UIGameInstaller 必须使用与 GameName 一致的 namespace**。在同一个 Unity 工程中多个游戏可能都有叫 `UIGameInstaller` 的类，不加 namespace 会导致 `AddComponent<UIGameInstaller>()` 误挂其他游戏的 Installer。UIFrame.Initialize() 会在运行时检测此问题并报错。
>
> ⚠️ **UIGameInstaller 用普通 `MonoBehaviour`**。重复初始化防护由 UIFrame 幂等承担：同一 UIDocument/UIRoot 重复初始化仅 LogWarning 跳过，换 UIDocument/UIRoot（切场景）自动 `Reset()` 后重新初始化。需要跨场景常驻去重时，在游戏代码中自行维护静态实例。

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using Create.GameFramework;

namespace MyGame  // ← 必须使用 {GameName} 作为 namespace
{
    public class UIGameInstaller : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;
        [SerializeField] PanelRegistry panelRegistry;

        void Start() {
            // UIFrame 幂等：同一 UIDocument 重复初始化仅 LogWarning 跳过；
            // 换 UIDocument（切场景）自动 Reset 后重新初始化
            UIFrame.Initialize(uiDocument, gameId: "MyGame", registry: panelRegistry);

            // 注册游戏服务到 DI
            UIFrame.DI.RegisterSingleton<IGameService>(new GameService());

            // 可选：订阅面板生命周期事件（音效/打点/转场）
            var uiManager = UIFrame.GetUIManager();
            uiManager.OnPanelPushed += panel => Debug.Log($"[UI] Pushed: {panel.PanelName}");
            uiManager.OnPanelPopped += panel => Debug.Log($"[UI] Popped: {panel.PanelName}");

            // 显示第一个面板
            uiManager.Push("MainMenu");
        }
    }
}
```

### 2. 编写面板（AI 编写，每个面板约 5-15 行）

```csharp
using Create.GameFramework;
using UnityEngine.UIElements;

[Panel("HUD", gameId: "MyGame")]
public class HUDPanel : UIPanelBase {
    protected override void OnBind() {
        var game = Resolve<IGameService>();
        Bind(game.Score).ToText<Label>("score");
        Bind(game.Health).ToVisibility("low-health", true);
        BindButton("pause", () => game.PauseGame()); // UIGameInstaller 订阅 OnGamePaused 事件 Push("PauseMenu")
    }
}
```

---

## API 参考

### UIPanelBase（抽象基类，所有面板必须继承）

| 方法 | 类型 | 说明 |
|------|------|------|
| `OnBind()` | **abstract** | 声明所有数据绑定，仅在面板首次显示时调用一次 |
| `OnShow(object param)` | virtual | 每次面板显示时调用，param 来自 Push() 的第二个参数 |
| `OnHide()` | virtual | 每次面板隐藏时调用 |
| `Resolve<T>()` | protected | 从 DI 容器获取服务/ViewModel |
| `Bind<T>(BindableProperty<T>)` | protected | 创建数据绑定，返回 BindingBuilder |
| `BindButton(string name, Action)` | protected | 绑定按钮点击，自动注册/注销 |
| `Push(string panelName, object param)` | protected | 打开新面板（当前面板隐藏） |
| `Pop()` | protected | 关闭当前面板（显示栈中上一个面板） |
| `Switch(string panelName, object param)` | protected | 清空栈并压入新面板（用于"返回主菜单"） |
| `BindClick(string name, Action)` | protected | 绑定非 Button 元素的点击事件（如 VisualElement）。Button 用 `BindButton`，其他元素用 `BindClick` |
| `Track(Action cleanup)` | protected | 注册清理回调，面板销毁时自动执行。用于手动订阅的 C# 事件取消订阅 |
| `SafeQ<T>(string name)` | protected | 安全查找 UXML 元素，找不到时 Warn 而非 NPE |
| `SetupSliderFill(Slider)` | protected | 设置 Slider 的 fill 纹理防拉伸：注入固定宽度子元素到 tracker（`overflow: hidden` 遮罩），同步 tracker 宽度到 value，自动清理。在 OnBind() 中设初始值后调用一次 |
| `SetupProgressBarFill(ProgressBar)` | protected | 设置 ProgressBar 的 fill 纹理防拉伸：注入固定宽度子元素到 `__progress`（`overflow: hidden` 遮罩），同步宽度到进度条宽度，自动清理。在 OnBind() 中调用一次。fill 纯色无图案时可跳过 |
| `NavigatePush/NavigatePop/NavigateSwitch` | public | ⚠️ 框架内部使用（预留给 UIBrick Adapter），游戏代码应使用 `Push`/`Pop`/`Switch` |

### [Panel("name", gameId: "XXX")] 属性

```csharp
[Panel("HUD", gameId: "MyGame")]  // gameId 用于多游戏项目隔离
public class HUDPanel : UIPanelBase { ... }
```

- name 在同一 gameId 范围内必须唯一
- `gameId` 可选，用于多游戏共存的项目（如 `Assets/Games/MyGame/` 和 `Assets/Games/AnotherGame/`）
- `UIFrame.Initialize(uiDocument, gameId: "MyGame")` 初始化后：
  - **面板发现**：注册 gameId 匹配的面板 + 无 gameId 的面板（向后兼容），跳过其他 gameId 的面板
  - **UXML 加载**：通过 `PanelRegistry` 查找面板对应的 VisualTreeAsset。Registry 持有序列化的 VTA 引用，Build 后不依赖 AssetDatabase。同一 panelName 存在多个条目时，优先匹配 gameId 一致的条目

### BindingBuilder（Bind() 返回的链式构建器）

> ⚠️ **`Bind()` 在 property 为 null 时返回 null**，后续链式调用会抛 `NullReferenceException`。安全模式：`Bind(game.Score)?.ToText<Label>("score-label")`（使用 `?.` 运算符跳过空值）。详见 `references/uipanel-patterns.md` §5 安全绑定模式。

| 方法/属性 | 说明 |
|----------|------|
| `.CurrentProperty` | 只读访问绑定的 BindableProperty，用于扩展方法自定义绑定；绑定 `IReadOnlyBindableProperty` 时为 null |
| `.ReadOnlyProperty` | 只读属性访问（`IReadOnlyBindableProperty<T>`，恒非 null），外部扩展方法优先使用 |
| `.ToText<TElement>(string name)` | 将值绑定为文本（支持 Label、Button），**立即刷新**。⚠️ `TElement` 必须是 `Label` 或 `Button`，传入其他类型会 Warning 但不生效 |
| `.ToVisibility(string name, T matchValue)` | 值=matchValue 时显示，否则隐藏 |
| `.ToEnabled(string name, T matchValue)` | 值=matchValue 时启用，否则禁用 |
| `.ToAction(Action<T> callback)` | 值变化时调用回调，**立即触发一次** |
| `.ToActionOnChange(Action<T> callback)` | 值变化时调用回调，**不触发初始值** |
| `.ToProgress(string name)` | 绑定 ProgressBar 的 value（仅 `BindableProperty<int>`） |

**扩展方法示例**：利用 `ReadOnlyProperty` / `CurrentProperty` 在外部程序集创建自定义绑定。⚠️ `builder.Panel` / `Panel.Bag` / `SafeQ` 是包内 internal/protected 成员，外部程序集不可访问——外部扩展只能基于 public 属性访问器构建，元素查找与清理（`Track`）由调用方的面板代码完成

```csharp
// 用户代码（Assembly-CSharp）——只使用 public 成员
public static class BindingBuilderColorExtensions {
    // 元素由调用方传入（面板代码里 SafeQ 查好）；
    // 返回 IDisposable，由调用方在面板内用 Track() 注册清理
    public static IDisposable ToColor(
        this BindingBuilder<bool> builder,
        VisualElement element,
        Color whenTrue,
        Color whenFalse) {
        if (builder == null || element == null) return null;
        return builder.ReadOnlyProperty.SubscribeWithCurrent(v =>
            element.style.backgroundColor = v ? whenTrue : whenFalse);
    }
}

// 面板 OnBind() 内使用：
// var sub = Bind(game.IsLowHealth)?.ToColor(SafeQ<VisualElement>("hp-frame"), Color.red, Color.white);
// Track(() => sub?.Dispose());
```

### BindableProperty\<T\>（可观察属性）

```csharp
public class BindableProperty<T> {
    T Value { get; set; }        // 赋值自动触发变更通知
    IDisposable Subscribe(Action<T>);           // 订阅变更
    IDisposable SubscribeWithCurrent(Action<T>); // 订阅并立即推送当前值
}

// 只读视图（Bind() 重载可直接接受）：Value 只读 + Subscribe/SubscribeWithCurrent
public interface IReadOnlyBindableProperty<T> {
    T Value { get; }
    IDisposable Subscribe(Action<T>);
    IDisposable SubscribeWithCurrent(Action<T>);
}
```

使用方式（在游戏服务/ViewModel 中声明）：

```csharp
public class GameService : IGameService {
    public BindableProperty<int> Score { get; } = new BindableProperty<int>(0);
    public BindableProperty<bool> IsPaused { get; } = new BindableProperty<bool>(false);
    
    public void AddScore(int delta) => Score.Value += delta;
}
```

### DIContainer（依赖注入容器）

> 🚨 **DI 访问唯一路径 = `UIFrame.DI`**（`DIContainer` 是实例类型，无裸 `DI` 静态类）。`DI.RegisterSingleton(...)` → CS0103 编译失败（某游戏踩坑）。所有注册都写 `UIFrame.DI.Register...`。

| 方法（均经 `UIFrame.DI.` 调用） | 说明 |
|------|------|
| `UIFrame.DI.RegisterSingleton<T>(T instance)` | 注册单例实例 |
| `UIFrame.DI.RegisterFactory<T>(Func<T> factory)` | 注册瞬态工厂（每次 Resolve 新实例） |
| `UIFrame.DI.RegisterLazySingleton<T>(Func<T> factory)` | 注册懒加载单例（首次 Resolve 时创建并缓存） |
| `UIFrame.DI.Resolve<T>()` | 解析类型，循环依赖会抛异常（Panel 内可直接用继承来的 `Resolve<T>()` 简写） |

### UIManager（面板导航管理器）

| 方法/事件 | 说明 |
|----------|------|
| `Push(string name, object param)` | 压入面板 |
| `Pop()` | 弹出当前面板 |
| `Switch(string name, object param)` | 清空栈并压入新面板（用于"返回主菜单"） |
| `GetCurrentPanel()` | 获取栈顶面板 |
| `IsPanelVisible(string name)` | 指定面板是否在栈中 |
| `OnPanelPushed` (event) | 面板压入并显示后触发，用于转场/打点/音效 |
| `OnPanelPopped` (event) | 面板弹出并隐藏后触发，用于清理/打点 |

> **多游戏隔离**：当 `UIFrame.Initialize(uiDocument, gameId: "MyGame", registry: panelRegistry)` 传入 gameId 时，UIManager 会：
> 1. 仅自动发现 `[Panel]` 中 gameId 匹配或无 gameId 的面板类
> 2. 加载 UXML 时，通过 `PanelRegistry.Find(panelName, gameId)` 优先返回 gameId 匹配的 VisualTreeAsset
>
> 这确保了同一工程中多个游戏（如 MyGame、GameB、GameC）不会互相干扰。

### UIPanelRouter（状态驱动路由）

`UIPanelRouter` 有两种互斥目标模式，按 UI 所有权选择，禁止混用名称体系：

| 模式 | `allPanels` / rule 使用的名称 | 行为 |
|------|-------------------------------|------|
| `VisualElements` | 同一个宿主 UIDocument 内的 UXML 元素 `name` | 直接切换元素 `display`；适合一个 UXML 同时包含菜单/HUD/结算根容器 |
| `UIFramePanels` | `[Panel("...")]` 注册名 | 状态变化时调用 `UIManager.Switch()`；适合 UIFrame 为每个 Panel 独立加载 UXML 的标准结构 |

> ⚠️ UIFrame 标准面板结构必须选 `UIFramePanels`。不要把独立面板 UXML 的内部根元素名（如 `StartMenuRoot`）填进宿主 UIDocument 的 `VisualElements` 路由；这些元素并不存在于宿主文档，路由会解析 0 个面板，导致游戏已进入 Playing 但主菜单仍覆盖在上面。

### UIFramework（静态入口）

| 方法 | 说明 |
|------|------|
| `UIFrame.Initialize(UIDocument, string gameId = null, PanelRegistry registry = null)` | 初始化框架，传入场景中的 UIDocument、gameId（多游戏隔离）和 PanelRegistry（UXML 引用表）。⚠️ registry 为 null 时报 Error 并中止初始化。重复调用同 UIDocument 会被静默忽略（仅 LogWarning）；换 UIDocument（切场景）会自动 `Reset()` 后重新初始化 |
| `UIFrame.GetUIManager()` | 获取 UIManager 实例 |
| `UIFrame.DI` | 获取 DIContainer 实例 |
| `UIFrame.Reset()` | 重置框架（切换场景时使用） |
| `UIFrame.SetGameState(string)` | 设置 UI 状态并广播 `StateChanged`（载荷 `GameStateChangedEvent`）。游戏侧驱动状态 UI 的唯一入口；HUD 基类/UIPanelRouter 自动订阅联动 |
| `UIFrame.StateChanged` | `event Action<GameStateChangedEvent>`，订阅方须成对退订 |
| `UIFrame.GameState` / `UIFrame.HasGameState` | 当前 UI 状态（未设置过为 null）/ 是否已设置 |

> **命名说明**：静态入口类名为 `UIFrame`（非 `UIFramework`），以避免与同名命名空间 `Create.GameFramework` 碰撞。在 `using Create.GameFramework;` 的上下文中直接使用 `UIFrame.Initialize()`、`UIFrame.DI`、`UIFrame.GetUIManager()` 即可。

---

## 面板生命周期

```
游戏启动 → UIFrame.Initialize()
         → UIFrame.DI.Register...()
         → UIManager.Push("MainMenu")

Push("MainMenu"):
  创建 MainMenuPanel → Initialize(doc) → OnBind() [仅一次] → OnShow(null)
                                                                    ↓
Push("HUD"):                                              MainMenuPanel.Hide()
  创建 HUDPanel → Initialize(doc) → OnBind() → OnShow(param)
                                                     ↓
Push("PauseMenu"):                           HUDPanel.Hide()
  创建 PauseMenuPanel → Initialize → OnBind() → OnShow(null)
                                                     ↓
Pop():                                    PauseMenuPanel.Hide() + Cleanup()
                                          HUDPanel.OnShow(null) [恢复显示]
```

---

## 适用范围

| ✅ 适用 | ❌ 不适用 |
|--------|----------|
| 面板式 HUD（分数/血条/快捷栏） | 世界空间 UI（敌人头顶血条、飘字伤害） |
| 菜单导航（主菜单→游戏→暂停→设置） | 拖拽交互（背包拖放排序） |
| 数据驱动的文本/显隐更新 | UI 动画过渡（Push/Pop 是瞬切） |
| 弹窗/确认框 | 分屏多人（每人独立 UI 上下文） |

> ⚠️ **单面板栈限制**：`Push()` 会隐藏当前栈顶面板，因此**不支持多面板同时显示**（如 HUD + 暂停菜单叠加）。需要 HUD 始终可见时：
>
> **决策树**：
> ```
> HUD 是否需要在暂停/弹窗时保持可见？
>   ├─ 否 → 方案 A（HUD 元素内嵌在游戏面板 UXML 中，HUD 不作为独立 Panel）
>   └─ 是 → 方案 B（独立 UIDocument 渲染 HUD，脱离 UIFramework 管理，自行处理生命周期）
> ```
>
> - **方案 A（默认推荐）**：将 HUD 元素直接放在游戏面板的 UXML 中。HUD 不是独立导航目标，它是游戏面板的 overlay。大多数游戏暂停时 HUD 跟随游戏画面一起冻结/暗化，不需要在暂停菜单后面看到实时 HUD。
> - **方案 B（例外）**：使用独立 UIDocument 渲染 HUD，脱离 UIFramework 管理自行处理生命周期。适用于需要暂停时 HUD 仍可见的场景（如 RTS 暂停后仍需看资源面板）。
>
> **UIPanelBase 是 MonoBehaviour 但禁止重写 Unity 生命周期方法**（`OnEnable`/`OnDisable`/`Start`/`OnDestroy`）。面板生命周期由 UIManager 管理，重写这些方法会与框架冲突导致不可预期行为。唯一例外：`OnShow(param)`/`OnHide()` 可安全重写。

> **Create.GameFramework 一致性原则与不适用场景决策**：见 `references/uipanel-patterns.md` §4

---

## 常见模式

> UIGameInstaller 适配器规范、带参数弹窗、确认弹窗、ViewModel 模式、Slider 双向绑定、文本格式化等模式详见 `references/uipanel-patterns.md`

---

## 文件位置

框架源码：`Packages/cn.tuanjie.create.ui/Runtime/`
- `UIFrame.cs` — 静态入口（含 UI 状态契约：`SetGameState` / `StateChanged` / `GameState`）
- `BindableProperty.cs` — 可观察属性（含只读接口 `IReadOnlyBindableProperty<T>`，位于 Runtime 根目录）
- `Core/UIStateChannel.cs` — UI 状态契约（`GameStateChangedEvent` 载荷 + `GameStates` 状态名常量）
- `Core/DIContainer.cs` — 依赖注入容器
- `Core/DisposableBag.cs` — 订阅清理
- `Binding/BindingBuilder.cs` — 链式绑定构建器（UI Toolkit）
- `Binding/UGUIBindingBuilder.cs` — UGUI 链式绑定构建器
- `Panel/UIPanelBase.cs` — UI Toolkit 面板基类
- `Panel/UGUIPanelBase.cs` — UGUI 面板基类
- `Panel/UIManager.cs` — 面板导航栈管理
- `Panel/PanelAttribute.cs` — 面板标记特性
- `Panel/PanelRegistry.cs` — 面板注册表（ScriptableObject，持有 panelName → VTA/Prefab 序列化引用，Build 安全）
- `Panel/GameUIControllerBase.cs` — UI Toolkit HUD 面板基类（继承 UIPanelBase）
- `Panel/UGUIGameControllerBase.cs` — UGUI HUD 面板基类（继承 UGUIPanelBase）
- `Flow/UIPanelRouter.cs` — 状态驱动面板显隐映射器
- `Widgets/CrosshairFactory.cs` — 准星工厂（7 风格矢量绘制）
- `Widgets/UIFormat.cs` — UI 文本格式化工具

## 面板继承关系

### UI Toolkit 模式
```
UIPanelBase (菜单/弹窗/HUD 通用基类)
  └── GameUIControllerBase (HUD 专用，加了 UIFrame.StateChanged 订阅 + 每帧刷新)
```
- `UIPanelBase`：UI Toolkit 面板基类，提供 `SafeQ<T>(name)` 元素查询、`Bind()`/`BindButton()` 绑定、生命周期管理
- `GameUIControllerBase`：继承 `UIPanelBase`，额外提供 `UpdateHud()` 每帧刷新和 UIFrame 状态订阅集成

### UGUI 模式
```
UGUIPanelBase (菜单/弹窗/HUD 通用基类 — Canvas Prefab)
  └── UGUIGameControllerBase (HUD 专用 — UIFrame.StateChanged + 每帧刷新)
```
- `UGUIPanelBase`：UGUI 面板基类，提供 `SafeFind<T>(path)` 元素查询、`Bind()`/`BindButton()` 绑定、`Track()` 事件清理、生命周期管理。面板由 Prefab 实例化，显隐通过 `Canvas.enabled`（GameObject 常驻激活）
- `UGUIGameControllerBase`：继承 `UGUIPanelBase`，额外提供 `UpdateHud()` 每帧刷新和 UIFrame 状态订阅集成

### UGUIPanelBase API

| 方法 | 类型 | 说明 |
|------|------|------|
| `OnBind()` | abstract | 声明所有数据绑定，仅在面板首次显示时调用一次 |
| `OnShow(object param)` | virtual | 每次面板显示时调用 |
| `OnHide()` | virtual | 每次面板隐藏时调用 |
| `Resolve<T>()` | protected | 从 DI 容器获取服务 |
| `SafeFind<T>(string path)` | protected | 按 Transform 路径查找 UGUI 组件，找不到 Warn |
| `Bind<T>(BindableProperty<T>)` | protected | 创建 UGUI 数据绑定，返回 UGUIBindingBuilder |
| `BindButton(string path, Action)` | protected | 绑定 Button.onClick，自动 Track 清理 |
| `Push/Pop/Switch` | protected | 面板导航（共享 UIManager） |
| `Track(Action cleanup)` | protected | 注册清理回调 |

### UGUIBindingBuilder 方法

| 方法 | 说明 |
|------|------|
| `ToTextUGUI(path)` | 绑定到 Text.text；path 可指到 brick 实例根——节点自身无 Text 时自动回退子树解析（label/value_text 的 Text 挂在 "Text" 子节点），显式 `/Text` 路径同样有效；`SafeFind<Text>` 无此回退，须精确路径 |
| `ToTMP(path)` | 绑定到 TextMeshProUGUI.text；子树回退同 ToTextUGUI |
| `ToSpriteUGUI(path)` | 绑定到 Image.sprite |
| `ToVisibilityUGUI(path, matchValue)` | 值=matchValue 时 SetActive(true) |
| `ToEnabledUGUI(path, matchValue)` | 值=matchValue 时 interactable=true |
| `ToListView(path, itemPrefab, bindItem)` | 集合绑定到 ScrollRect 动态列表：属性变化时清空 content 并按 itemPrefab 实例化每个条目，bindItem 回调填充单条数据（配合 `ugui.scroll_view` listMode + `ugui.list_item`；集合须实现 IList/IEnumerable） |
| `ToAction(callback)` | 立即触发+后续变化 |
| `ToActionOnChange(callback)` | 仅后续变化 |

### UIFrame UGUI 初始化

```csharp
// UGUI 模式初始化（替代 UIFrame.Initialize）
UIFrame.InitializeUGUI(uiRootTransform, gameId: "MyGame", registry: panelRegistry);
```

| 方法 | 说明 |
|------|------|
| `UIFrame.InitializeUGUI(Transform, gameId, registry)` | UGUI 模式初始化，传入 UIRoot Transform |
| `UIFrame.UIMode` | 返回当前 UI 模式（UIToolkit / UGUI） |
