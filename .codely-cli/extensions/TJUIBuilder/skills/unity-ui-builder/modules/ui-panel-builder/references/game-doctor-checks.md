# GameDoctor UI 相关检查项清单

> 本文件汇总 ui-panel-builder 中引用的所有 GameDoctor 检查项，供快速查阅。GameDoctor 属外部"游戏编排" extension（本仓库不内置），未安装时相关检查跳过；已安装时聚合入口为 `GameDoctor.CheckInPlay()`（Play 模式）和 `GameDoctor.CheckBeforePlay()`（编辑模式）。

## 编辑模式检查（CheckBeforePlay）

| 检查方法 | 校验内容 | 常见失败原因 | 对应 ui-panel-builder 规则 |
|---------|---------|------------|----------------------|
| `CheckPanelNameConsistency` | Push/Switch 参数与 `[Panel]` 注册名一致 | 拼错面板名、注册名与类名不匹配 | 核心规则 4（Panel 注册名约定） |
| `CheckSerializedFieldDrift` | 场景序列化值匹配已注册面板名 | `[SerializeField]` 旧值覆盖代码默认值 | 其他规则（Bootstrap 初始面板名禁止 [SerializeField]） |
| `CheckOrphanShow` | 面板调了 Show() 但从未被 Push/Switch 入栈 | 只调 Show() 不先 Push/Switch | 其他规则（Panel 显示语义 Show vs Push/Switch） |
| `CheckPanelReachability` | 每个标准状态（Menu/Playing/Paused/GameOver）有可见面板 | 缺导航分支、某状态无面板 | 步骤 10 导航链完整性 |

## Play 模式检查（CheckInPlay）

| 检查方法 | 校验内容 | 常见失败原因 | 对应规则 |
|---------|---------|------------|---------|
| `CheckUIOcclusion` | UI 不遮挡游戏关键对象 | HUD 背景不透明、面板 background-image 遮挡画面 | uss-reference.md §4（HUD 叠加面板规则） |
| `CheckInstantDeath` | 开局不秒死 | 出生点/重力问题 | — |
| `CheckRuntime` | 运行时关键状态/组件正常 | 脚本未挂载、引用丢失 | — |

## 时机说明

- `CheckBeforePlay`：进 Play 前执行。改动涉及场景/Prefab 结构性接线（加删组件、改引用、配 Tag/Layer）时补跑
- `CheckInPlay`：进 Play、玩过受影响环节后执行。改 bug/调参/加功能后必跑
- `CheckUIOcclusion`/`CheckGameplayObjectsVisible`/`CheckInstantDeath` 须 Playing 态；`CheckRuntime` 静止段须 Menu 态——按报告提示切状态重跑

## UGUI 模式检查项

> 以下检查项仅适用于 `ui_mode == "ugui"`。通过 `execute_csharp_script` 在编辑模式执行。

### UGUI Prefab 完整性检查（替代 UIV.Validate）

| 检查项 | 校验内容 | 失败原因 | 修复方式 |
|--------|---------|---------|---------|
| 根节点 localScale | Panel Prefab 和 UIRoot Prefab 根节点 `localScale == (1,1,1)` | `AddComponent<RectTransform>` 后未显式设置 scale，导致默认 `(0,0,0)` → 运行时黑屏 | execute_csharp_script 设置 `transform.localScale = Vector3.one` + `SavePrefabAsset` |
| 根节点 RectTransform 尺寸 | Panel Prefab 根节点 `rect.width > 0 && rect.height > 0`（stretch fill 配置正确） | anchors 未设为 stretch（`anchorMin=(0,0), anchorMax=(1,1)`），导致 rect 宽高为 0 | execute_csharp_script 设置 anchors + offset + `SavePrefabAsset` |
| Canvas 完整性 | 每个 Panel Prefab 根节点有 Canvas + CanvasScaler + GraphicRaycaster | 组件缺失 | execute_csharp_script 补加组件 |
| CanvasScaler 配置 | referenceResolution 与 UI设计文档.md §1 设计分辨率一致 | 分辨率不匹配 | execute_csharp_script 修正 |
| Image.sprite 引用 | 有图片的 Image 组件 sprite 不为 null | 图片未赋值或路径错误 | 重新 LoadAssetAtPath 并赋值 |
| Button.targetGraphic | Button 组件 targetGraphic 不为 null | 缺少背景 Image | 设置 targetGraphic = GetComponent<Image>() |
| Slider.fillRect | Slider 的 fillRect 不为 null | 子结构缺失 | 创建 Fill 子 GameObject + Image |
| Slider.handleRect | 有 HandleArea 的 Slider handleRect 不为 null（progress_bar 默认无手柄，slider 有手柄） | 子结构缺失 | 创建 Handle 子 GameObject + Image |
| Dropdown.captionText | Dropdown 的 captionText 不为 null | Label 子 GO 缺失 | 创建 Label 子 GO + Text 组件 |
| Dropdown.template | Dropdown 的 template 不为 null | Template 子 GO 缺失 | 创建 Template 子 GO |
| Toggle.graphic | Toggle 的 graphic 不为 null | checkmark 子 GO 缺失 | 创建 Checkmark 子 GO + Image |
| 元素路径可达性 | state file 中每个 `elements[].path` 在 Prefab 中有对应 Transform | GameObject 名拼错或层级路径错误 | 修正 GameObject name 或路径 |
| Panel 脚本挂载 | 每个 Panel Prefab 挂了对应 Panel 脚本组件 | 脚本未挂载 | execute_csharp_script 添加组件 |

### UGUI 模式不适用的检查项

| 检查项 | 原因 |
|--------|------|
| UIV.Validate / UIV.ValidateStructured | UIV 仅适用于 UXML/USS（UI Toolkit 模式） |
| USS `url()` 资源解析 | UGUI 不使用 USS |
| UXML `<ui:Style>` 引用完整性 | UGUI 不使用 UXML |
| Brick `br-*` class 名使用 | UGUI 不使用 USS class 名 |
| DropdownField popup USS 选择器 | UGUI Dropdown 不需要 popup USS 修复 |
| SetupSliderFill / SetupProgressBarFill | UGUI Slider/ProgressBar 原生处理 fill，不需要 C# 同步 |

### UGUI 模式适用的共享检查项

| 检查项 | 说明 |
|--------|------|
| CheckPanelNameConsistency | 共享——Push/Switch 参数与 `[Panel]` 注册名一致 |
| CheckSerializedFieldDrift | 共享——场景序列化值匹配已注册面板名 |
| CheckOrphanShow | 共享——面板调了 Show() 但从未被 Push/Switch 入栈 |
| CheckPanelReachability | 共享——每个标准状态有可见面板 |
| CheckUIOcclusion | 共享——UGUI 模式下检查 Canvas 背景是否遮挡游戏画面（HUD 面板 Canvas 不应有不透明背景 Image） |
