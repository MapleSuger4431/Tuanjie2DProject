# UI Panel 脚本模式参考

> 本文件是 unity-ui-builder SKILL.md 的配套参考，包含 UI Panel 脚本的详细模式和适配器规范。

## 1. UIGameInstaller 适配器规范

### ⚠️ 命名空间要求（强制）
- **UIGameInstaller 必须使用 `{GameName}` 作为 namespace**（如 `namespace MyGame { public class UIGameInstaller : MonoBehaviour { ... } }`）
- **Panel 脚本也必须使用与 gameId 一致的 namespace**：虽然 `[Panel]` 属性匹配不检查 namespace，但 `DI.Resolve<T>()` 按 Type 解析（含 namespace 限定），不同 namespace 会导致 DI 找不到类型
- 原因：同一工程中多个游戏可能有同名 `UIGameInstaller` 类，不加 namespace 会导致 `AddComponent<UIGameInstaller>()` 误挂其他游戏的 Installer
- UIFrame.Initialize() 会在运行时检测跨游戏误挂并报错中止
- 同理，所有游戏级脚本（GameManager、GameController 等）也应放在 `{GameName}` namespace 下

### 适配器初始化模式
- **优先使用 `FindObjectOfType<T>()`** 获取游戏管理器引用，而非序列化字段
- 原因：序列化字段依赖场景手动接线，容易遗漏；`FindObjectOfType` 自动查找，零配置
- 如果管理器在场景中唯一（如 GameManager、ScoreManager），直接使用 `FindObjectOfType<T>()`

### 游戏事件 → UI 面板触发
- UIGameInstaller 的 `Start()` 中**必须**订阅所有触发面板切换的游戏事件
- 常见事件映射：`OnGameOver` → `Push("GameOver")`，`OnLevelComplete` → `Push("LevelComplete")`

### 面板导航去重规则（极易出错）
- **原则：每个 Push/Pop 只有一个触发源**。禁止同一操作在 BindButton 和 UIGameInstaller 事件中重复触发 Push/Pop
- **错误模式**：`BindButton("pause-btn")` 中调用 `PauseGame()` + `Push("Pause")`，同时 UIGameInstaller 订阅 `OnGamePaused` 也调用 `Push("Pause")` → 双重 Push 导致 Pop 无法正确恢复
- **正确模式**：
  - BindButton 只调用游戏逻辑方法（如 `gameService.PauseGame()`），**不调用 Push/Pop/Switch**
  - 导航触发源二选一（勿混用）：
    - **事件订阅形态**：UIGameInstaller 订阅 `UIFrame.StateChanged` 游戏状态后 Push/Pop/Switch（见 Stage 02 12b 模板）
    - **服务方法形态**：游戏服务方法内部完成状态变更 + 导航（如 `PauseGame()` 内 `Push("Pause")`、`GoToMainMenu()` 内 `Switch("MainMenu")`），BindButton 只调服务方法
- **例外**：如果面板切换纯粹是 UI 层行为（如从设置页返回暂停页），不涉及游戏状态变更，则可以直接在 BindButton 中调用 `Push()`/`Switch()`；弹窗返回**优先用 `Switch(host)` 确定性切回**（见下节），仅短会话栈可信时可用 `Pop()`

### 导航语义与确定性返回（全屏互斥 vs 弹窗）

| 面板类型 | 进入方式 | 返回方式 | 原因 |
|----------|---------|---------|------|
| 全屏互斥面板（主菜单/关卡选择/HUD/结算） | `Switch`（排他，清栈） | `Switch` 回目标面板 | 两个 sortingOrder 相同的全屏面板会同时渲染；`Switch` 已清空栈，**返回禁止 `Pop`（空栈黑屏）** |
| 弹窗（暂停/设置） | `Push`（叠加） | **`Switch(host)`**（推荐） | 长会话/混合导航后栈状态不可靠，`Pop` 恢复到错误面板会黑屏；`Switch(host)` 行为确定 |

**弹窗确定性返回模式（Push 传宿主 + Switch 切回）**：

```csharp
// 调用方：Push 时把来源面板注册名作为 param 传入
BindButton("btn_settings", () => Push(PanelNames.Settings, PanelNames.HUD));

// 弹窗侧：OnShow 记录宿主，返回按钮 Switch 回宿主（不依赖栈恢复）
private string _hostPanel;
protected override void OnShow(object param)
{
    if (param is string host && !string.IsNullOrEmpty(host)) _hostPanel = host;
}
protected override void OnBind()
{
    BindButton("btn_back", () =>
        Switch(string.IsNullOrEmpty(_hostPanel) ? PanelNames.MainMenu : _hostPanel));
}
```

> `Switch(name)` 语义：清空栈并压入新面板（见 `UIFrame-API.md`）。凡 Switch 进入的面板，其"返回"必须同为 Switch。

### UI 操作 → 游戏状态重置
- 当 UI 面板的操作导致游戏"回退"（如 GameOver → MainMenu → 重新开始），**必须**同时重置游戏状态
- 模式：在服务接口中添加对应方法（如 `IGameService.GoToMainMenu()`），UI Panel 调用服务方法后再切换面板
- **禁止**只切换 UI 面板而不重置游戏状态

### HUD 数据同步机制
- HUD 的 BindableProperty 必须有明确的同步来源：
  - **事件驱动**：游戏事件触发时更新（如 `OnBoostStarted` → `BoostActive.Value = true`）
  - **轮询驱动**：时间类数据需要每帧同步，在 UIGameInstaller（MonoBehaviour）中实现 `SyncTimers()` 方法，由 UIGameInstaller 的 `Update()` 调用
- 同步时机：Panel 的 `OnShow()` 中调用初始同步；UIGameInstaller 的 `Update()` 中持续同步时间类数据（UIPanelBase 不是 MonoBehaviour，没有 `Update()`）

## 2. Slider 双向绑定模式

当需要 Slider（音量、速度等）与 BindableProperty 双向同步时，Create.GameFramework 没有内置的 Slider 绑定 API。必须手动实现双向绑定，使用 `Track()` 管理回调生命周期：

```csharp
private bool _updating; // 防止循环触发

protected override void OnBind() {
    var audio = Resolve<IAudioService>();

    // Property → Slider（数据驱动显示）
    Bind(audio.MusicVolumeProperty).ToAction(OnMusicVolumeChanged);

    // Slider → Property（用户交互驱动数据）
    var musicSlider = SafeQ<Slider>("music-slider");
    if (musicSlider != null) {
        musicSlider.RegisterValueChangedCallback(OnMusicSliderChanged);
        Track(() => musicSlider.UnregisterValueChangedCallback(OnMusicSliderChanged));
    }
}

private void OnMusicVolumeChanged(float volume) {
    if (_updating) return;
    _updating = true;
    var slider = SafeQ<Slider>("music-slider");
    if (slider != null) slider.value = volume;
    _updating = false;
}
private void OnMusicSliderChanged(ChangeEvent<float> evt) {
    if (_updating) return;
    _updating = true;
    Resolve<IAudioService>().MusicVolumeProperty.Value = evt.newValue;
    _updating = false;
}
```

**关键要点**：
- `_updating` 标志防止 Property→Slider→Property 循环触发
- `Track()` 确保 `UnregisterValueChangedCallback` 在面板销毁时自动调用
- **禁止**在 `RegisterValueChangedCallback` 之外直接修改 `BindableProperty.Value`
- **tracker 宽度同步（必做）**：上述双向绑定仅同步 Slider.value 与 BindableProperty。Tuanjie 2022.3 的 tracker 元素宽度不会自动随 value 变化，不额外写代码则 tracker 始终 100% 填充。必须在 OnBind() 中额外注册 `RegisterValueChangedCallback` 手动设 `tracker.style.width` 为百分比，代码模板见 `references/uitk/functional-control-styles.md`「fill 宽度必须手动同步」章节。

## 3. 文本格式化绑定规则

`ToText<Label>()` 仅执行 `value.ToString()` 赋值，**不会**添加前缀/后缀/格式化。需要格式化的场景**必须**使用 `ToAction`：

| 场景 | 错误方式 | 正确方式 |
|------|---------|---------|
| "最高分: 1200" | `Bind(HighScore).ToText<Label>("highscore-label")` → 只显示 "1200" | `Bind(HighScore).ToAction(v => SafeQ<Label>("highscore-label").text = $"最高分: {v}")` |
| "15 km/h" | `Bind(Speed).ToText<Label>("speed-label")` → 只显示 "15" | `Bind(Speed).ToAction(v => SafeQ<Label>("speed-label").text = $"{(int)v} km/h")` |
| "0.0s" | `Bind(Timer).ToText<Label>("timer-label")` → 显示 "0" | `Bind(Timer).ToAction(v => SafeQ<Label>("timer-label").text = $"{v:F1}s")` |
| 纯数字（无格式化需求） | `Bind(Score).ToText<Label>("score-label")` → "1200" ✅ | 正确 |

## 4. 不适用框架的场景

以下场景 Create.GameFramework 目前不支持，但应优先通过扩展框架解决：
- 世界空间 UI（敌人头顶血条、飘字伤害）→ 可扩展 UIPanelBase 支持世界空间渲染
- 拖拽交互（背包拖放排序）→ 可扩展 BindDrag 等绑定 API
- 需要自定义渲染管线的 UI → 可扩展面板渲染管线
- 分屏多人独立 UI 上下文 → 可扩展 UIManager 支持多上下文

**决策流程**与**一致性原则**：默认禁止修改 Package 源码（`Packages/cn.tuanjie.create.ui/Runtime/`），游戏级需求靠扩展解决。遇到框架不支持或存在 Bug 时：在游戏代码中扩展（partial class / 扩展方法 / 子类化），新增文件到 `Assets/Games/{GameName}/Scripts/`；扩展不可行时报告调用方。

## 5. Bind() 安全绑定模式

`Bind()` 在传入 null property 时返回 `null`，直接链式调用 `.ToText()` / `.ToVisibility()` 会抛 `NullReferenceException`。这通常发生在游戏服务尚未初始化、或 BindableProperty 尚未创建时。

**安全模式 — 使用 `?.` 运算符**：

```csharp
protected override void OnBind() {
    var game = Resolve<IGameService>();
    
    // ✅ 安全：property 为 null 时跳过整个绑定链
    Bind(game.Score)?.ToText<Label>("score-label");
    Bind(game.Health)?.ToVisibility("low-health-warning", true);
    
    // ❌ 危险：property 为 null 时 NPE
    Bind(game.Score).ToText<Label>("score-label");
}
```

**何时使用 `?.`**：
- 不确定 BindableProperty 是否已初始化时（如依赖外部服务延迟创建的属性）
- 游戏服务可能在面板绑定时尚未完全就绪时

**何时可以省略 `?.`**：
- BindableProperty 在构造函数中就初始化了（如 `public BindableProperty<int> Score { get; } = new(0);`）
- 确认 Resolve<T>() 返回的服务中所有绑定属性都非 null

**批量绑定的安全模式**：

```csharp
protected override void OnBind() {
    var game = Resolve<IGameService>();
    // 集中检查，一次防护
    if (game == null) return;
    
    Bind(game.Score)?.ToText<Label>("score-label");
    Bind(game.Health)?.ToProgress("health-bar");
    BindButton("pause", () => game.PauseGame());
}
```

## 6. bindingPropsSchema → 绑定代码映射

步骤 1 中读取的 `definition.json` 的 `bindingPropsSchema` 声明了砖块的可绑定字段及 `runtimeType`，按以下规则转化为 Panel 脚本中的绑定代码：

| `bindingPropsSchema` 字段 | `runtimeType` | 对应绑定 API |
|---------------------------|---------------|-------------|
| `text` | `string` | `Bind(prop)?.ToText<Label>("name")` |
| `visible` | `bool` | `Bind(prop)?.ToVisibility("name", true)` |
| `enabled` | `bool` | `Bind(prop)?.ToEnabled("name", true)` |
| `activeIndex` | `number` | `Bind(prop)?.ToAction(v => SafeQ<T>("name").value = v)` |
| `value` | `number` | `Bind(prop)?.ToProgress("name")`（仅 `BindableProperty<int>`） |
| 其他字段 | — | 暂不支持自动绑定，在 Panel 脚本的 `OnShow()` 中手写逻辑处理 |

> 字段名对应 UXML 中 Brick 元素的 `name` 属性。`runtimeType` 帮助确定 `BindableProperty<T>` 的泛型参数 `T`（`number` → `int` 或 `float`，`bool` → `bool`，`string` → `string`）。

---

## UGUI 模式面板模式

> 以下模式仅适用于 `ui_mode == "ugui"`。UI Toolkit 模式使用上方 §1-§6 规则。

## 7. UGUI SafeFind 路径约定

UGUI 模式使用 `SafeFind<T>(path)` 查询元素，path 是相对于面板根的 Transform 路径：

```csharp
// 路径格式：用 "/" 分隔的 GameObject 名（不含根节点名）
SafeFind<Button>("CenterContent/PlayButton");    // 查找根/CenterContent/PlayButton
SafeFind<Text>("TopBar/ScoreText");               // 查找根/TopBar/ScoreText
SafeFind<Slider>("Settings/VolumeSlider");        // 查找根/Settings/VolumeSlider
```

⚠️ 路径从 state file `elements[].path` 读取，不手动推断。路径中的 GameObject 名必须与 Prefab 中的实际名一致。

> ⚠️ **value_text / label brick 的 Text 组件挂在名为 `Text` 的子节点上**（brick 结构），对应 SafeFind 路径带 `/Text` 后缀（如 `TopLeftBar/ammo_label/Text`）。paths.json / state file 的 `elements[].path` 已按此生成，照读即可；手写路径时勿漏后缀。

## 8. UGUIBindingBuilder 使用示例

```csharp
protected override void OnBind()
{
    var game = Resolve<IGameService>();

    // 文本绑定（UnityEngine.UI.Text）
    Bind(game.Score)?.ToTextUGUI("TopBar/ScoreText");

    // 文本绑定（TextMeshPro）
    Bind(game.Score)?.ToTMP("TopBar/ScoreTMP");

    // Sprite 绑定
    Bind(game.AvatarSprite)?.ToSpriteUGUI("TopBar/Avatar");

    // 可见性绑定
    Bind(game.IsPaused)?.ToVisibilityUGUI("PauseOverlay", true);

    // 可交互绑定
    Bind(game.CanRestart)?.ToEnabledUGUI("BottomBar/RestartButton", true);

    // 自定义回调
    Bind(game.Score)?.ToAction(v => {
        var slider = SafeFind<Slider>("BottomBar/ScoreSlider");
        if (slider != null) slider.value = v;
    });
}
```

### 8.1 UGUI 列表绑定（ToListView）

排行榜、好友列表等动态列表：`ugui.scroll_view`（`listMode: true`）作容器 + `ugui.list_item` 作条目，用 `ToListView` 把集合属性绑定到列表：

```csharp
[Panel("Leaderboard", gameId: "MyGame")]
public class LeaderboardPanel : UGUIPanelBase
{
    protected override void OnBind()
    {
        var game = Resolve<IGameService>();

        // 条目模板：trees.json 中预置一个隐藏的 ugui.list_item 节点（如 Templates/RankRowTemplate）
        var template = SafeFind<Transform>("Templates/RankRowTemplate")?.gameObject;

        // path 指向 ugui.scroll_view 实例（SafeFind<ScrollRect>），条目实例化到其 content 下
        Bind(game.RankEntries)?.ToListView("RankList", template, (go, item) =>
        {
            var entry = (RankEntry)item;
            go.GetComponentInChildren<Text>().text = entry.Name;
            go.transform.Find("Value")?.GetComponent<Text>().text = entry.Score.ToString();
        });
    }
}
```

要点：
- `ToListView(path, itemPrefab, bindItem)` 的 `path` 指向 ScrollRect；每次属性变化先清空 `content` 再按 `itemPrefab` 逐条实例化（`Instantiate` 场景模板同样有效，克隆体会挂到 content 下）
- 集合属性须实现 `IList` 或 `IEnumerable`；`bindItem` 回调负责填充单个条目的子元素
- 条目模板来源优先用面板内预置的隐藏节点（`SafeFind` 获取），避免序列化字段手工接线

## 9. UGUIGameControllerBase 模式（HUD 面板）

```csharp
[Panel("HUD", gameId: "MyGame")]
public class HUDPanel : UGUIGameControllerBase
{
    private Text _scoreText;
    private Image _healthBar;

    // QueryElements 在 OnBind() 中自动调用（基类实现）
    protected override void QueryElements()
    {
        _scoreText = SafeFind<Text>("TopBar/ScoreText");
        _healthBar = SafeFind<Image>("BottomBar/HealthFill");
    }

    // 每帧刷新（仅 Playing 状态时执行）
    protected override void UpdateHud()
    {
        var game = Resolve<IGameService>();
        _scoreText.text = game.Score.Value.ToString();
        _healthBar.fillAmount = game.Health.Value / 100f;
    }

    // 游戏事件订阅（在 OnEnable 时调用，OnDisable 时取消）
    protected override void OnSubscribe()
    {
        var game = Resolve<IGameService>();
        game.Health.Subscribe(v => _healthBar.fillAmount = v / 100f);
    }
}
```

> ⚠️ **子类重写 OnBind/OnShow 时必须调用 base**：`base.OnBind()` 触发 `QueryElements()`（不调则元素缓存全为 null），`base.OnShow(param)` 启动 HUD 每帧刷新循环（不调则 `UpdateHud()` 不执行）。仅重写 `QueryElements`/`UpdateHud`/`OnSubscribe`/`OnUnsubscribe` 时无此要求。

## 10. UGUI 模式与 UI Toolkit 模式的关键差异

| 模式 | UI Toolkit | UGUI |
|------|-----------|------|
| 元素查询 | `SafeQ<T>(name)` — VisualElement name | `SafeFind<T>(path)` — Transform 路径 |
| 文本绑定 | `Bind(prop)?.ToText<Label>("name")` | `Bind(prop)?.ToTextUGUI("path")` 或 `ToTMP("path")`——path 可指 brick 实例根，节点无 Text 时自动回退子树解析（label/value_text 的 Text 挂 "Text" 子节点）；`SafeFind<Text>` 无回退，须带 `/Text` |
| 可见性 | `Bind(prop)?.ToVisibility("name", val)` | `Bind(prop)?.ToVisibilityUGUI("path", val)` |
| 可交互 | `Bind(prop)?.ToEnabled("name", val)` | `Bind(prop)?.ToEnabledUGUI("path", val)` |
| BindButton | `BindButton("name", action)` — name | `BindButton("path", action)` — path |
| 列表数据 | ListView + `itemsSource`（手写同步） | `Bind(prop)?.ToListView(path, itemPrefab, bindItem)` |
| 框架初始化 | `UIFrame.Initialize(uiDocument, gameId, registry)` | `UIFrame.InitializeUGUI(transform, gameId, registry)` |
| Slider fill | `SetupSliderFill(slider)` 手动同步 | UGUI 原生自动处理（无需调用） |
| ProgressBar fill | `SetupProgressBarFill(bar)` 手动同步 | UGUI 原生自动处理（无需调用） |
| Dropdown popup | 需要 USS popup 修复代码 | 不需要（UGUI Dropdown 自带模板） |

## 11. UGUI bindingPropsSchema → 绑定代码映射

| `bindingPropsSchema` 字段 | `runtimeType` | UGUI 绑定 API |
|---------------------------|---------------|-------------|
| `text` | `string` | `Bind(prop)?.ToTextUGUI("path")` 或 `ToTMP("path")`——path 可指 brick 实例根，节点无 Text 时自动回退子树解析 |
| `visible` | `bool` | `Bind(prop)?.ToVisibilityUGUI("path", true)` |
| `enabled` | `bool` | `Bind(prop)?.ToEnabledUGUI("path", true)` |
| `value` | `number` | `Bind(prop)?.ToAction(v => SafeFind<Slider>("path").value = v)` |
| 其他字段 | — | 在 Panel 脚本的 `OnShow()` 中手写逻辑处理 |

> UGUI 模式下字段名对应 GameObject 名（Transform 路径中的最后一段），通过 UGUI 定义文件的 `parameters` 字段确定目标属性。

## 12. 动效绑定模式（UIAnimationHelper）

API 全集与 LayoutGroup 安全矩阵见 `UIAnimation-API.md`。三类典型模板：

### 12.1 主菜单面板（Logo 弹入 + 按钮交错渐显）

```csharp
protected override void OnBind()
{
    // ... BindButton 声明 ...
    _logo = SafeFind<Image>("ContentLayer/Logo");   // 锚定子元素（无 LayoutGroup 父级）
    UIAnimationHelper.AttachButtonEffects(transform);   // 一行批量挂载全部激活按钮（幂等）
}

protected override void OnShow(object param)
{
    StartCoroutine(UIAnimationHelper.PanelFadeIn(transform, 0.35f, 30f));
    if (_logo != null)
    {
        StartCoroutine(UIAnimationHelper.LogoBounceIn(_logo.transform, 0.6f));
        StartCoroutine(UIAnimationHelper.IdleFloat(_logo.transform, 5f, 1.5f));
    }
    // 按钮是 ButtonGroup 根（VerticalLayoutGroup）子元素 —— StaggerIn 自动检测并降级纯 alpha
    StartCoroutine(UIAnimationHelper.StaggerIn(
        SafeFind<RectTransform>("ContentLayer/ButtonGroup"), 0.1f, 0.3f));
}
```

### 12.2 弹窗面板（Pause/Settings，弹入 + 锚定子节点滑入）

```csharp
protected override void OnShow(object param)
{
    StartCoroutine(UIAnimationHelper.PanelFadeIn(transform, 0.2f, 0f));   // 弹窗背景纯渐显
    // 弹窗背景缩放弹入（scale 动画，不受 LayoutGroup 影响）
    if (_popupRoot != null)
    {
        StartCoroutine(UIAnimationHelper.PopIn(_popupRoot, 0.3f));
        // panel_pause 的直接子节点为锚定节点 —— StaggerIn 检测无 LayoutGroup，走滑入
        StartCoroutine(UIAnimationHelper.StaggerIn(
            SafeFind<RectTransform>("ContentLayer/panel_pause/PauseContent"), 0.06f, 0.25f, 30f));
    }
}
```

### 12.3 结算/HUD 面板（数据滚动 + 统计行交错）

```csharp
// 数据回调内：To 版自动取当前显示值作 from，平滑滚动到新值
Bind("hp", v => { if (_hpBar != null) StartCoroutine(UIAnimationHelper.SmoothSliderTo(_hpBar, v, 0.4f)); });
Bind("score", v => StartCoroutine(UIAnimationHelper.ScoreRollTo(_scoreLabel, v, 0.5f)));

// 统计行是 StatsGroup 根（LayoutGroup）子元素 —— StaggerIn 自动 alpha 交错
StartCoroutine(UIAnimationHelper.StaggerIn(
    SafeFind<RectTransform>("ContentLayer/StatsGroup"), 0.08f, 0.3f));

// 标题高亮（无限循环；OnHide 中 StopAllCoroutines() 停止，OnShow 重启）
StartCoroutine(UIAnimationHelper.TitleGlow(_titleLabel, new Color(1f, 0.82f, 0.25f), 2f));
```

> **速查**：交错入场默认用 `StaggerIn`（自动检测 LayoutGroup：有则纯 alpha、无则滑入）；需逐元素显式控制时才用 host 版 `StaggerFadeIn`/`StaggerEntrance`；按钮特效默认 `AttachButtonEffects(transform)` 批量；数据滚动默认 `SmoothSliderTo`/`ScoreRollTo` 自取 from。`IdleFloat` 仅用于锚定子元素（动 localPosition）；`TitleGlow` 仅动 color，LayoutGroup 子元素也安全。

## 13. UGUI 交互组件模式（Toggle / Dropdown / 占位控件）

### 13.1 Toggle 视觉反馈（设置项列表）

设置类 Toggle（音效/音乐/震动开关）推荐「颜色反馈」：开启时标签文字金色、checkmark 高亮，关闭时文字暗灰。**禁止改 label.text**（其文字是设置项名，非状态值）：

```csharp
private void SetupToggleVisual(Toggle toggle)
{
    if (toggle == null) return;
    var label = toggle.GetComponentInChildren<Text>();
    var checkmark = toggle.graphic as Image;

    void ApplyFeedback(bool isOn)
    {
        if (label != null)
            label.color = isOn ? new Color(1f, 0.82f, 0.25f) : new Color(0.55f, 0.53f, 0.45f);
        if (checkmark != null)
            checkmark.color = isOn ? new Color(1f, 0.92f, 0.55f) : Color.white;
    }

    ApplyFeedback(toggle.isOn);                                   // 初始态同步
    toggle.onValueChanged.AddListener(ApplyFeedback);
    Track(() => toggle.onValueChanged.RemoveListener(ApplyFeedback));  // 面板销毁自动注销
}
```

### 13.2 Dropdown 选项填充

UGUI Dropdown 自带模板（无需 USS/popup 修复），但**选项内容必须在 `OnBind()` 中填充**：

```csharp
var quality = SafeFind<Dropdown>(path + "/quality_dropdown");
if (quality != null)
{
    quality.ClearOptions();
    quality.AddOptions(new List<string> { "低", "中", "高" });
}
```

### 13.3 占位控件（无绑定逻辑，仅保证可交互）

设计稿要求但暂无业务逻辑的控件，保持默认值并确保可交互：

```csharp
var volume = SafeFind<Slider>(path + "/volume_row/volume_slider");
if (volume != null) volume.interactable = true;
```

### 13.4 ⚠️ 字段名不可与基类序列化字段重名

`UGUIPanelBase` 含 `_panelRoot` 等私有序列化字段；Panel 子类声明同名字段会触发引擎序列化器 duplicated field 错误。缓存弹窗根等 RectTransform 时换名（如 `_popupRoot`）。
