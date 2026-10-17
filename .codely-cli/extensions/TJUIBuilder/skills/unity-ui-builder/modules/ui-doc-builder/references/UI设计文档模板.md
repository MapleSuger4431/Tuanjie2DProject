# UI 设计文档

> UI 默认使用 Create.GameFramework UI 框架（UIPanelBase/UGUIPanelBase + DIContainer + 声明式绑定）。UGUI 模式与 UI Toolkit 模式不可混用。

## 1. Create.GameFramework UI 框架适用性评估

| 评估项 | 是否涉及 | 说明 |
|--------|---------|------|
| 世界空间 UI（敌人头顶血条、飘字伤害、3D 场景内嵌 UI） | ☐ 是 / ☐ 否 | UI Toolkit 模式：UIPanelBase 不支持世界坐标对齐；UGUI 模式：Canvas World Space 原生支持 |
| 拖拽交互（背包拖放排序、拖拽合成） | ☐ 是 / ☐ 否 | UI Toolkit 模式：拖拽状态管理超出 BindableProperty 范畴；UGUI 模式：可通过 IDragHandler 等接口扩展 |
| 自定义渲染管线 UI（RenderTexture 实时预览、后处理效果） | ☐ 是 / ☐ 否 | 需要直接控制渲染流程，两种模式均需自定义 |
| 分屏多人独立 UI 上下文 | ☐ 是 / ☐ 否 | Create.GameFramework UI 框架的单导航栈不适用，两种模式均受影响 |

**决策结果**: {使用框架 / 扩展框架 / 回退原生}
**理由**: {...}

## 2. UIBrick 实现候选

| 界面 | Brick 覆盖 | 可用 Brick（标注层级） |
|------|----------|----------------------|
| {主菜单} | {full / partial / none} | {UGUI 模式: {container brick}（背景容器）, {layout brick}（按钮组布局）, {text brick}（标题）, {input brick} ×3（按钮）, {image brick}（音量）/ UI Toolkit 模式: primitive.text.label（标题）, primitive.input.button_base ×3（按钮）, primitive.image.icon（音量）} |
| {HUD} | {full / partial / none} | {（列出可用 Brick，无覆盖标"无——手写 UXML"）} |

> UGUI 模式下，§2 应同时列出交互控件和 Layout/Container 级 Brick。Layout Brick（`category=layout`）和 Container Brick（`category=container`）用于组织面板内部的布局结构。具体可用 Brick 以 `BrickIndex.ugui.json` 为准。

### 2.1 assetPropsSchema 候选元素

> 仅视觉参考模式下填写。

| 组件名 | 对应 Brick | assetPropsSchema 槽位 | 裁剪图路径 |
|--------|-----------|----------------------|-----------|
| {如 播放按钮} | primitive.input.button_base | iconAsset | {裁剪图路径} |

## 3. UI 总体设计
- **UI 框架**: Create.GameFramework UI 框架（UIPanelBase/UGUIPanelBase + DIContainer + 声明式绑定）
- **UI 模式**: {ugui|ui_toolkit}
- **UI 架构**: {填写当前 UI 模式的架构描述}
- **UI 层级**: {HUD / 菜单 / 弹窗}
- **UI 风格**: {与游戏美术风格一致的描述}，关键词: {英文视觉关键词串——必须内嵌在本行值内供 Step 3 提取 style_baseline，写成独立行不会被提取}
- **PanelSettings**（仅 UI Toolkit 模式）: 使用默认 PanelSettings，缩放模式为 Scale With Screen Size，设计分辨率: {1920×1080}
- **CanvasScaler**（仅 UGUI 模式）: Scale With Screen Size，参考分辨率: {1920×1080}
- 当存在 MainMenu 时，游戏状态机须包含预游戏状态（如 Ready），MainMenu 开始按钮触发 Ready → Running。禁止游戏直接从 Running 状态启动

### 3.1 布局参数

> 仅视觉参考模式下填写。

- 信息密度: {sparse|moderate|dense}
- 间距风格: {tight|comfortable|generous}
- 垂直分布: {equal_spacing|justify|top_clustered|bottom_clustered|sparse_distributed}
- 水平分布: {equal_spacing|center|left_aligned|right_aligned|justify}
- 圆角尺寸: {如 12px}
- 对齐方式: {如 居中对齐, 网格布局}

### 3.2 面板布局表

> 仅视觉参考模式下填写。背景层和内容层必须分行列出。

| 面板 | 背景层 | 内容层 | 布局结构（仅 UGUI 模式） |
|------|--------|--------|------------------------|
| {如 MainMenuPanel} | {如 全屏横屏（天蓝+草地+放射阳光线）} | {如 中央深绿竖条面板+胶囊按钮} | {如 Canvas → {container brick}(背景) → {layout brick}(内容区) → [{input brick} ×3]} |
| {如 HUDPanel} | {如 全屏游戏区域} | {如 底部D-Pad+顶部信息栏} | {如 Canvas → {layout brick}(底部栏) → [{image brick}, {text brick}]} |

## 4. 界面清单
| 界面名称 | Panel 名称 | 树类型 | 树根 brickId | 类型 | 触发条件 | 包含元素 |
|---------|-----------|--------|------------|------|---------|---------|
| {HUD} | {HUDPanel} | 主面板 | ugui.panel | {常驻} | {游戏开始} | {分数显示、生命值显示、计时器} |
| {主菜单} | {MainMenuPanel} | 主面板 | ugui.panel | {全屏} | {游戏启动} | {开始按钮、设置按钮} |
| {主菜单} | {MainMenuItemTemplate} | 列表模板 | ugui.list_item | {子组件} | {运行时克隆} | {图标、标签、值} |
| {游戏结束} | {GameOverPanel} | 主面板 | ugui.panel | {弹窗} | {失败/胜利} | {最终分数、重试按钮、返回主菜单按钮} |

> 一个界面可包含多棵 brick 树：主面板（`ugui.panel` 根）+ 列表条目模板（`ugui.list_item` 根）等。每棵树对应一个独立 Prefab。

## 5. UI Panel 脚本清单
> 每个 UI Panel 须继承 `UIPanelBase`（UI Toolkit 模式）或 `UGUIPanelBase`（UGUI 模式），标注 `[Panel("PanelName")]`，在 `OnBind()` 中声明数据绑定

| Panel 脚本名 | [Panel] 名称 | 基类 | 绑定的 BindableProperty | BindButton 列表 | 导航目标 |
|-------------|-------------|------|------------------------|-----------------|---------|
| {HUDPanel.cs} | {HUD} | {UIPanelBase|UGUIPanelBase} | {Score, Lives, TimeLeft} | {PauseBtn → gameService.PauseGame()} | {PausePanel} |
| {GameOverPanel.cs} | {GameOver} | {UIPanelBase|UGUIPanelBase} | {FinalScore, IsWin} | {RetryBtn → gameService.RestartGame()} | {-} |

## 6. 数据绑定关系
> 描述游戏逻辑的 BindableProperty / 事件 如何通过 `Bind()`/`BindButton()` 绑定到 UI Panel

| 游戏逻辑脚本 | BindableProperty / 事件 | UI Panel | 绑定方式 | UI 元素 |
|------------|------------------------|---------|---------|---------|
| {GameManager} | {ScoreProperty} | {HUDPanel} | {UI Toolkit: `Bind().ToText<Label>("score-label")` / UGUI: `Bind()?.ToTextUGUI("TopBar/ScoreLabel")`} | {score-label} |
| {GameManager} | {IsGameOver} | {HUDPanel} | {UI Toolkit: `Bind().ToVisibility("gameover-btn", true)` / UGUI: `Bind()?.ToVisibilityUGUI("GameOverBtn", true)`} | {gameover-btn} |
| {GameManager} | {-} | {HUDPanel} | {UI Toolkit: `BindButton("pause-btn", ...)` / UGUI: `BindButton("TopBar/PauseBtn", ...)`} | {pause-btn} |

## 7. DI 注册清单
> 描述 UIGameInstaller 中需要注册的服务和接口

| 注册类型 | 注册方式 | 说明 |
|---------|---------|------|
| {IGameService → GameManager} | {Singleton} | {游戏核心服务} |
| {IScoreService → ScoreManager} | {Singleton} | {分数服务} |

## 8. 面板导航关系
> 描述面板间的导航关系（**意图级**：表达"从哪到哪、由什么触发"，用 Push/Pop/Switch 词汇示意）。落地语义由 ui-panel-builder 换算——全屏互斥面板互转一律 `Switch`；弹窗 `Push` 进入、`Switch(来源面板)` 返回——本节字面不被照抄，见 ui-panel-builder `references/uipanel-patterns.md` §1

```
MainMenuPanel → Push("Gameplay") → GameplayPanel
GameplayPanel → Push("Pause") → PausePanel → Pop()
GameplayPanel → Push("GameOver") → GameOverPanel → Pop()
```

## 9. 游戏事件与 UI 面板触发映射
> 游戏逻辑事件如何触发 UI 面板的显示/隐藏

| 游戏事件 | 所属脚本 | 触发的 UI 操作 | 传递参数 | 说明 |
|---------|---------|---------------|---------|------|
| {OnGameOver} | {GameManager} | {Push("GameOver")} | {最终分数} | {游戏结束时显示 GameOver 面板} |
| {OnLevelComplete} | {GameManager} | {Push("LevelComplete")} | {-} | {关卡完成时显示结算面板} |

> 多关卡游戏末关结算面板的"下一关"按钮须改为"返回主菜单"或展示通关画面，不得导向不存在的关卡。面板上任一按钮点击后须能到达有效状态。

## 10. UI 操作与游戏状态重置映射
> UI 面板的操作如何影响游戏状态

| UI 操作 | 所属面板 | 游戏状态变更 | 服务方法 | 说明 |
|---------|---------|------------|---------|------|
| {"主菜单"按钮} | {GameOverPanel} | {重置为 Ready 状态} | {IGameService.GoToMainMenu()} | {返回主菜单时必须重置游戏状态} |
| {"重新开始"按钮} | {GameOverPanel} | {重置游戏并开始} | {IGameService.RestartGame()} | {重新开始时必须完全重置} |

## 11. UI 元素设计
> 每个界面的详细元素定义、布局说明、交互行为描述。
>
> `显示文字` 列：有文字的按钮填实际文字，图标按钮和背景/容器留空。`componentType: Label` 的元素为纯文字标签，不生成图片。
> `字体颜色`/`字体大小`/`字体粗细` 列：仅 `componentType: Label` 填写。

#### {界面名称1}
- **布局**: {顶部/居中/底部对齐方式}
- **布局 Brick**（仅 UGUI 模式）: {如 {layout brick}（按钮组布局）, {container brick}（背景容器）}
- **元素列表**:

> `元素类型` 列：UI Toolkit 模式映射到 UXML 标签（`Label`, `Button`, `VisualElement` 等）；UGUI 模式映射到 UGUI 组件（`Text`, `Button`, `Image` 等）。

  | 元素名称 | 元素类型 | 用途 | 显示文字 | text_type | 字体颜色 | 字体大小 | 字体粗细 | 绑定方式 |
  |---------|---------|------|---------|-----------|---------|---------|---------|---------|
  | {score-label} | {UI Toolkit: Label / UGUI: Text} | {显示分数} | {如 "分数"} | {label} | {如 #D4A84B} | {如 14} | {normal\|bold} | {UI Toolkit: Bind(scoreProperty).ToText<Label>() / UGUI: Bind(scoreProperty).ToTextUGUI()} |
  | {pause-btn} | {Button} | {暂停游戏} | {如 "暂停"} | {label} | — | — | — | {BindButton("pause-btn", action)} |
  | {art-title} | {UI Toolkit: VisualElement / UGUI: Image} | {游戏标题艺术字} | {如 "GameTitle"} | {art_text} | — | — | — | {无（静态图片）} |
  | {icon-btn} | {Button} | {设置图标} | {空} | {none} | — | — | — | {BindButton("icon-btn", action)} |

## 12. 图生图配置

> 仅视觉参考模式下存在。无参考图时删除 §12-§14。

### 12.1 参考图路径映射

| 参考图编号 | 面板映射 | 文件路径（绝对路径） |
|-----------|---------|--------------------------------------------------------|
| 1 | {如 MainMenuPanel} | {如 E:/.../context/games/{GameName}/UIReferences/MainUI.jpg} |
| 2 | {如 HUDPanel} | {如 ...} |

### 12.2 组件裁剪映射

| 节点 ID | 父组件 / 父级元素 | 深度 | 渲染模式 | shell 路径 | 组件名 | display_text | 组件类型 | text_type | art_text_style | font_color | font_size | font_weight | font_style | 裁剪图路径（相对 game context） | source | quality | contamination_description | functional_type | 来源参考图编号 | 保真度 | override_dimensions | 相对父 X% | 相对父 Y% | 相对父宽% | 相对父高% | 根 X%（诊断） | 根 Y%（诊断） | 根宽%（诊断） | 根高%（诊断） | style_group | reuse_from | exclude_overlays | dynamic_content | generation_hint |
|--------|------------------------|------|----------|-----------|--------|-------------|---------|---------|---------|---------|---------|---------|---------|---------------------|--------|--------------|--------------------------|--------|-------------------|------------|------------|--------------|--------------|--------------|--------------|----------------|----------------|-------------|------------|-----------------|---------------|----------------|
| {如 node-13} | {如 menu-content} | 2 | direct | {如 MainMenuRoot/menu-shell/menu-content/help-button} | {如 说明按钮} | 说明 | button | — | — | — | — | — | — | reuse_from:播放按钮 | reuse_from:播放按钮 | clean | — | — | 1 | exact | — | {如 12.5} | {如 60.0} | {如 75.0} | {如 16.0} | {如 36.2} | {如 61.7} | {如 27.3} | {如 13.1} | button_style_1 | 播放按钮 | — | — | — |
| {如 node-14} | {如 menu-content} | 2 | text | {如 MainMenuRoot/menu-shell/menu-content/music-label} | {如 音乐标签} | 音乐 | label | label | — | {如 #FFFFFF} | {如 24} | normal | — | skip_generation | uss_only | clean | — | — | 1 | — | — | {如 5.0} | {如 10.0} | {如 25.0} | {如 8.0} | {如 21.6} | {如 15.2} | {如 7.7} | {如 4.2} | — | — | — | — | — |
| {如 node-15} | {如 MainMenuRoot} | 1 | shell_background | {如 MainMenuRoot/background} | {如 fullscreen_background} | | background | — | — | — | — | — | — | {整图路径} | ai_generate | clean | — | — | 2 | exact | — | 0 | 0 | 100 | 100 | 0 | 0 | 100 | 100 | — | — | {如 声音按钮,游戏标题,播放按钮} | — | — |
| {如 node-16} | {如 badge-shell} | 2 | direct | {如 HUDRoot/badge-shell/tiny-badge-icon} | {如 tiny_badge_icon} | | icon | — | — | — | — | — | — | text2img_fallback | ai_generate | clean | — | — | 1 | exact | — | {如 5.0} | {如 5.0} | {如 90.0} | {如 90.0} | {如 0.3} | {如 0.5} | {如 0.3} | {如 0.6} | — | — | — | — | — |

**列说明**：

| 列名 | 含义 | 值格式 |
|------|------|--------|
| `节点 ID` / `父组件 / 父级元素` / `深度` / `渲染模式` / `shell 路径` | 组件树的结构信息 | 旧文档可为空；存在时据此组合容器、shell/background 与 content layer |
| `相对父 X%/Y%/宽%/高%` | 子元素布局坐标 | 相对直接父级百分比 |
| `根 X%/Y%/宽%/高%（诊断）` | 相对参考图根的诊断坐标 | 仅 overlay/debug，不替代子元素相对父布局 |
| `display_text` | 组件上实际渲染的文字 | 实际文字（如"播放"），无文字组件为空 |
| `style_group` | 同风格组件组名 | `{componentType}_style_{N}`，独立组件为 `—` |
| `reuse_from` | 复用的代表组件名 | 代表组件名，代表组件和独立组件为 `—` |
| `exclude_overlays` | 背景图上叠加的内容层组件名列表 | 逗号分隔，非背景组件为 `—` |
| `dynamic_content` | 组件内的动态内容类型和描述 | `{type}:{description}`，无动态内容为 `—` |
| `generation_hint` | 图片生成时排除动态内容的提示 | 自然语言提示，无时为 `—` |
| `functional_type` | 功能性控件类型标记 | `slider`/`toggle`/`dropdown`/`text_field`/`progress_bar`/`other`/`—` |

### 12.3 元素尺寸比例

> 数据来源为组件树的相对父级几何，均非 AI 估算。

- 参考图原图尺寸: {如 3024×1964}（仅用于百分比计算）
- 元素占比表:

| 元素 | 屏宽占比 | 屏高占比 | 估算像素尺寸 | 说明 |
|------|---------|---------|-------------|------|
| {如 主按钮} | {如 15%} | {如 6%} | {如 290×65px} | {如 圆角矩形按钮} |
| {如 全屏背景} | 100% | 100% | {如 1920×1080px} | 铺满 |

- 元素大小层级: {如"标题文字是正文的 2 倍大"、"图标约为按钮宽度的 1/3"}

## 13. 文生图回退风格参数

> 仅当存在以下任一情况时才包含：
> 1. 裁剪失败组件（crop_path = `text2img_fallback`）
> 2. 用户指定了维度调整（`partial_override`）
> 3. 参考图缺少某些 UI 元素需要文生图生成
>
> 若所有组件均可图生图且保真度为 `exact`，则删除此章节。

### 回退组件风格表

| 组件名 | 回退原因 | 所需风格维度 | 提取值 |
|--------|---------|-------------|--------|
| {如 tiny_badge_icon} | 裁剪区域 < 8×8 | 图标风格 + 配色 | 图标: flat icon, bold silhouette; 主色 ≈ #1A1A2E, 强调色 ≈ #E94560 |
| {如 settings_button} | partial_override: 颜色改蓝 | 按钮样式 + 配色 | 按钮: 药丸形圆角 12px 实心; override 后颜色: 蓝色 |

### 图片生成风格指引

#### 通用视觉基线

所有面板类型共享的视觉关键词。

- 通用视觉基线: {如 "vibrant cartoon style, high saturation colors, bold black outlines"}

#### 按面板类型风格组

> 仅列出包含文生图回退组件的面板类型。

| 面板类型 | 适用面板 | 风格前缀 | 类别覆盖（仅覆盖含回退组件的类别） |
|---------|---------|---------|----------------|
| {如 MainMenu} | {适用面板名列表} | {面板类型独有的视觉关键词} | 按钮背景: {...}<br>图标: {...} |

## 14. 无参考图面板派生样式

> 仅视觉参考模式下，当存在参考图未覆盖的面板（如暂停、游戏结束、设置等弹窗类面板）时才包含。
> 无视觉参考时删除此章节。

### 通用视觉风格描述

从参考图中提取的通用视觉语言，以文生图 prompt 友好的自然语言描述：

- 通用视觉基线: {如 "vibrant cartoon style, rounded corners, soft shadows, high saturation colors"}
- 配色方案: {如 "主色调深蓝(#1A1A2E)，强调色金黄(#FFD700)，背景色深灰半透明"}
- 质感/纹理: {如 "磨砂玻璃质感，轻微噪点纹理，柔和渐变"}
- 装饰风格: {如 "圆角药丸形按钮，卡片有柔和投影，边缘有微光效果"}
- 圆角风格: {如 "大圆角(12-16px)，按钮为药丸形全圆角"}
- 整体氛围: {如 "温暖明亮，卡通风格，适合休闲游戏"}

### 无参考图面板文生图风格指引

| 面板 | 面板类型 | 文生图风格描述 | 需生成图片的组件 |
|------|---------|--------------|----------------|
| {如 PausePanel} | popup | {如 "dark semi-transparent glass panel, rounded corners, soft shadow, cartoon style, gold accent buttons"} | {如 暂停面板背景, 继续按钮背景, 退出按钮背景} |
| {如 GameOverPanel} | popup | {如 "dark semi-transparent glass panel, rounded corners, soft shadow, cartoon style, large title area"} | {如 结算面板背景, 再玩一次按钮背景, 返回主菜单按钮背景} |

## 15. UI 资产清单

> ⚠️ 图片行「说明」列必填三要素：① **W×H 目标尺寸**（如 `1920×1080`，Step 3 工具按此解析 width/height）；② **面板标识**（多面板文档须含界面名或 PanelName，工具按此匹配所属面板）；③ art_text 图片行须含 `art_text_style: <英文风格>`，且与 §11 元素不同名时另写 `text_type: art_text` 或包含 §11 元素名。缺①②会被 Step 3 工具 FAIL 拦截。

| 资产类型 | 文件名 | 路径 | 说明 |
|---------|--------|------|------|
| 图片 | {artText_gameTitle.png} | {Assets/Games/{GameName}/UI/} | {主菜单游戏标题，is_segmentation: true} |
| 图片 | {bg_mainmenu.png} | {Assets/Games/{GameName}/UI/} | {主菜单全屏背景，视觉描述，is_segmentation: false} |
| 图片 | {btn-primary.png} | {Assets/Games/{GameName}/UI/} | {主要按钮底图，视觉描述，is_segmentation: true} |
| 图片 | {panel-gameover.png} | {Assets/Games/{GameName}/UI/} | {游戏结束弹窗背景，视觉描述，is_segmentation: true} |
| 图片 | {icon-pause.png} | {Assets/Games/{GameName}/UI/} | {暂停图标，视觉描述，is_segmentation: true} |
| 图片 | {settings-container.png} | {Assets/Games/{GameName}/UI/} | {设置控件区壳图，视觉描述，is_segmentation: true} |
| ── UI Toolkit 模式 ── | | | |
| UXML | {HUD.uxml} | {Assets/Games/{GameName}/UI/} | {游戏 HUD 布局} |
| USS | {HUD.uss} | {Assets/Games/{GameName}/UI/} | {游戏 HUD 样式表} |
| PanelSettings | {GamePanelSettings.asset} | {Assets/Games/{GameName}/UI/} | {UI 面板设置} |
| ── UGUI 模式 ── | | | |
| Panel Prefab | {MainMenuPanel.prefab} | {Assets/Games/{GameName}/UI/Prefabs/} | {主菜单面板 Prefab（Canvas + UGUI 组件层级）} |
| Panel Prefab | {HUDPanel.prefab} | {Assets/Games/{GameName}/UI/Prefabs/} | {HUD 面板 Prefab} |
| ── 共通 ── | | | |
| 脚本 | {HUDPanel.cs} | {Assets/Games/{GameName}/Scripts/} | {HUD Panel（UI Toolkit: 继承 UIPanelBase / UGUI: 继承 UGUIPanelBase）} |
| 脚本 | {UIGameInstaller.cs} | {Assets/Games/{GameName}/Scripts/} | {DI 注册安装器} |
| PanelRegistry | {PanelRegistry.asset} | {Assets/Games/{GameName}/UI/} | {面板注册表（UI Toolkit: VTA 引用 / UGUI: Prefab 引用）} |
| **Prefab** | **{UIRoot.prefab}** | **{Assets/Games/{GameName}/Prefabs/}** | **{自包含 UI 根 prefab：UI Toolkit 模式 UIDocument+PanelSettings+UIGameInstaller / UGUI 模式 Canvas+CanvasScaler+UIGameInstaller}** |

> 一个界面可包含多棵 brick 树，每棵树对应 1 个 Panel Prefab（panel 树）或 1 个模板 Prefab（list_item 树）。

## 16. 场景挂载说明
> 描述 Create.GameFramework UI 框架初始化和 UIRoot 组件如何挂载到场景中

### UI Toolkit 模式
- **框架初始化**: `UIRoot.prefab`（含 UIDocument + PanelSettings + PanelRegistry + UIGameInstaller），场景搭建阶段实例化即可。UIGameInstaller 负责 `UIFrame.Initialize(uiDocument, gameId, registry)` + DI 注册 + 首次 `Push("MainMenu")` + 游戏状态→面板路由
- **PanelSettings 引用**: {GamePanelSettings.asset}
- **UIDocument 挂载**: UIRoot.prefab 内已挂载 UIDocument 组件并引用 PanelSettings。不设置 visualTreeAsset（由 UIManager 从 PanelRegistry 加载各面板 UXML）

### UGUI 模式
- **框架初始化**: `UIRoot.prefab`（含 Canvas + CanvasScaler + GraphicRaycaster + PanelRegistry + UIGameInstaller），场景搭建阶段实例化即可。UIGameInstaller 负责 `UIFrame.InitializeUGUI(transform, gameId, registry)` + DI 注册 + 首次 `Push("MainMenu")` + 游戏状态→面板路由
- **Canvas 配置**: Canvas（Screen Space - Overlay）+ CanvasScaler（参考分辨率: {1920×1080}）+ GraphicRaycaster。不创建 PanelSettings
- **Panel Prefab 加载**: UIManager 从 PanelRegistry 读取 Prefab 引用，在 Push() 时按 [Panel] 属性实例化对应 Prefab

### 共通
- **Sort Order**: {不同 UI 层级的排序规则}

| 对象 | 挂载的脚本 | UIRoot 组件 | PanelRegistry | sortOrder |
|------|----------|------------|---------------|-----------|
| {UIRoot} | {UIGameInstaller.cs}（已挂 prefab） | {UI Toolkit: UIDocument+PanelSettings / UGUI: Canvas+CanvasScaler+GraphicRaycaster} | {PanelRegistry.asset} | {0} |

- **架构说明**: UI Toolkit 模式单 UIDocument，面板 UXML 由框架按 [Panel] 属性动态加载。UGUI 模式 UIRoot 为 Canvas Transform，面板 Prefab 由 UIManager 按 [Panel] 属性动态实例化
- **显隐驱动**: 各面板显隐由 §9 的游戏状态驱动（游戏侧调 `UIFrame.SetGameState`），§10 的 UI 操作触发游戏状态重置。导航触发源两种合法形态（见 ui-panel-builder `references/uipanel-patterns.md` §1）：事件订阅形态（UIGameInstaller 订阅 `UIFrame.StateChanged` 后导航）或服务方法形态（服务方法内完成状态变更 + 导航）
- **场景实例化**: 场景搭建阶段 `PrefabUtility.InstantiatePrefab(UIRoot.prefab)` 即可，无需重复装配
