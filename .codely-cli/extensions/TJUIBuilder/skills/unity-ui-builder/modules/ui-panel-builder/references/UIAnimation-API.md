# UIAnimationHelper API（UGUI 动效库）

> 位置：`Packages/cn.tuanjie.create.ui/Runtime/Panel/UIAnimationHelper.cs`（namespace `Create.GameFramework`）。
> Panel 脚本已 `using Create.GameFramework`，直接 `UIAnimationHelper.Xxx(...)` 调用即可。

## 全局语义

| 规则 | 说明 |
|------|------|
| **暂停安全** | 所有动画走 `Time.unscaledDeltaTime` —— `Time.timeScale = 0`（暂停面板弹出）时照常播放 |
| **显隐手动停** | UGUI 面板显隐走 `Canvas.enabled`（GameObject 常驻激活，**非** SetActive）：协程不随隐藏自动停止，`OnShow` 重入会叠加协程——在 `OnHide()` 中 `StopAllCoroutines()` 停掉入场/循环动效（`OnShow` 会重新启动需要的动画） |
| **无限循环类** | `IdleFloat`/`TitleGlow` 无限循环且不随显隐自动停（见上行）；target 被销毁时自动退出，不抛异常 |
| **按钮特效一次性** | `AttachButtonEffect` 在 `OnBind()` 挂载（只跑一次）；幂等——重复调用直接返回 |
| **中断防漂移** | 位移类动效（`PanelFadeIn` / `StaggerIn` 滑入分支）自动挂 `UIPosBaseline` 组件锚定基准位置：动画被中断（隐藏/Kill 半途）后再次播放**回到基准位而非漂移位**；干净状态下播放会**自动刷新基准**（允许设计期移动元素）。基准为序列化字段，域重载不丢 |
| **录制时序** | `record_game_view` 类录制开始早于脚本首帧：动效依赖的首帧初始态（alpha=0）在协程第一行同步设置即可；若录制仍吃掉前几帧，在 `OnShow` 首行 `StartCoroutine` 前手动设置初始态并 `yield return new WaitForSeconds(0.5f)` 等待录制稳定 |

## LayoutGroup 安全矩阵（核心规则）

LayoutGroup（Horizontal/Vertical/Grid）**每帧覆写子元素的 `anchoredPosition`** —— 位移动画会被布局吞掉。

| 动画类型 | 面板根（Canvas 根） | 锚定子元素（无 LayoutGroup 父级） | LayoutGroup 子元素 |
|----------|:---:|:---:|:---:|
| 位移（`anchoredPosition`）：`PanelFadeIn` slide / `StaggerEntrance` | ✅ | ✅ | ❌ **禁止**，改用 `StaggerFadeIn` |
| alpha（`CanvasGroup`）：`FadeIn` / `StaggerFadeIn` | ✅ | ✅ | ✅ LayoutGroup 安全 |
| 缩放（`localScale`）：`LogoBounceIn` / `PopIn` / `AttachButtonEffect` | ✅ | ✅ | ✅ LayoutGroup 安全 |
| `IdleFloat`（`localPosition`） | — | ✅ | ❌ **禁止** |
| 数值类：`SmoothSlider` / `ScoreRoll` / `TitleGlow` | ✅ | ✅ | ✅ 不涉及布局属性 |

判断口诀：**要动位置先问父级有没有 LayoutGroup；alpha 和 scale 永远安全**。推荐入口 `StaggerIn` **自动检测**容器（有 LayoutGroup → alpha 模式，无 → 滑入），无需人工查表——查表仅在显式使用 `StaggerEntrance`/host 版时需要。

## API 一览

| API | 签名 | 典型挂载点 |
|-----|------|-----------|
| `PanelFadeIn` | `(Transform panel, float duration=0.35, float slide=30)` → IEnumerator | `OnShow` 首行，面板级入场惯例 |
| `StaggerIn` **推荐** | `(RectTransform parent, float delayPerItem=0.08, float itemDuration=0.3, float slide=40)` → IEnumerator | 交错入场默认入口：单协程驱动，**自动检测 LayoutGroup**（有则纯 alpha、无则滑入），parent 传子元素直接容器，无需 host |
| `AttachButtonEffects` **推荐** | `(Transform root, bool includeInactive=false)` → int | `OnBind` 一行批量挂载本面板所有按钮 hover/press（幂等，返回新挂数） |
| `SmoothSliderTo` **推荐** | `(Slider slider, float to, float duration=0.4)` → IEnumerator | 数据回调中数值条平滑，`from` 自动取 `slider.value` |
| `ScoreRollTo` **推荐** | `(Text label, int to, float duration=0.8)` → IEnumerator | 数据回调中分数滚动，`from` 自动解析 label 当前显示整数（非整数文本按 0 开始） |
| `FadeIn` | `(CanvasGroup cg, float duration=0.3, float delay=0)` → IEnumerator | 单元素渐显 / 自定义交错序列 |
| `StaggerFadeIn` host 版 | `(MonoBehaviour host, RectTransform parent, float delayPerItem=0.08, float itemDuration=0.3)` → IEnumerator | 逐元素显式控制时使用（LayoutGroup 安全，纯 alpha）；**host 传 `this`** |
| `StaggerFadeIn` 兼容版 | `(RectTransform parent, ...)` → IEnumerator | ⚠️ 旧签名：宿主取 parent 上任意 MonoBehaviour，纯容器静默 no-op |
| `StaggerEntrance` host 版 | `(MonoBehaviour host, RectTransform parent, float delayPerItem=0.08, float itemDuration=0.3, float slide=40)` → IEnumerator | 逐元素显式控制时使用（滑入，**仅锚定子元素**）；**host 传 `this`** |
| `StaggerEntrance` 兼容版 | `(RectTransform parent, ...)` → IEnumerator | 同上旧签名限制 |
| `LogoBounceIn` | `(Transform target, float duration=0.6)` → IEnumerator | Logo/图标弹性缩放入场（锚定子元素） |
| `PopIn` | `(Transform target, float duration=0.3, float overshoot=1.3)` → IEnumerator | 弹窗缩放弹入（Pause/Settings 的 panel_bg/panel_pause 根；scale 动画全场景安全） |
| `IdleFloat` | `(Transform target, float amplitude=6, float speed=1.5)` → IEnumerator | Logo 悬浮呼吸（锚定子元素，无限循环） |
| `SmoothSlider` | `(Slider slider, float from, float to, float duration=0.4)` → IEnumerator | 显式 from 版本（需精确控制起点时用） |
| `ScoreRoll` | `(Text label, int from, int to, float duration=0.8)` → IEnumerator | 显式 from 版本 |
| `TitleGlow` | `(Text label, Color baseColor, float pulseSpeed=2)` → IEnumerator | 标题文字呼吸高亮（无限循环） |
| `AttachButtonEffect` | `(Button btn, float hoverScale=1.08, float pressScale=0.92, float lerpSpeed=12)` → void | 单按钮挂载（批量用 `AttachButtonEffects`） |

## 挂载模式（生成代码模板）

```csharp
// OnBind()：一行批量挂载按钮交互（幂等）
UIAnimationHelper.AttachButtonEffects(transform);

// OnShow()：面板入场（首行惯例）
StartCoroutine(UIAnimationHelper.PanelFadeIn(transform, 0.35f, 30f));

// OnShow()：交错入场 —— StaggerIn 自动检测 LayoutGroup，无需二选一
//   LayoutGroup 容器（如 StatsGroup）子元素 → 自动纯 alpha：
StartCoroutine(UIAnimationHelper.StaggerIn(SafeFind<RectTransform>("ContentLayer/StatsGroup"), 0.08f, 0.3f));
//   弹窗锚定子节点 → 自动滑入：
StartCoroutine(UIAnimationHelper.StaggerIn(SafeFind<RectTransform>("ContentLayer/panel_pause/PauseContent"), 0.06f, 0.25f, 30f));

// 数据回调（Bind 订阅内）：To 版自动取当前显示值
Bind("hp", v => { if (_hpBar != null) StartCoroutine(UIAnimationHelper.SmoothSliderTo(_hpBar, v, 0.4f)); });
Bind("score", v => StartCoroutine(UIAnimationHelper.ScoreRollTo(_scoreLabel, v, 0.5f)));
```

## 兼容性说明

- 旧签名（`StaggerFadeIn(RectTransform, ...)` / `StaggerEntrance(RectTransform, ...)`）保留：宿主取 parent 上找到的**任意 MonoBehaviour**（Image/LayoutGroup/Button 均可充当）；纯 RectTransform 容器上**静默 no-op（不再添加 CanvasGroup）**。新代码一律用 `StaggerIn`（自动检测，无需 host）。
- 按钮文字为 UGUI `Text`；TMP 面板需自行扩展 `ScoreRoll`/`TitleGlow` 重载（当前无 TMP 用例，未内置）。
