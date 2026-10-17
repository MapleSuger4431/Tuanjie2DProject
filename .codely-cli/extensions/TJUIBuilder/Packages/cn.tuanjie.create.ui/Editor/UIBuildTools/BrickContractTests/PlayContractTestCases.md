# Play 交互契约测试项目（TJBrickContractPlayTests 用例真源）

> 创建于 2026-09-12。本文档是 `TJBrickContractPlayTests`（Phase 1 Play 交互契约回归）的**唯一用例真源**：
> 覆盖范围（Phase 1：Controls 10 prefab；Phase 2：Container/Layout 7 brick——2026-09-14 扩围，见 §2.9；
> Phase 3：动态布局专项——2026-09-17 增补，见 §2.10）、
> 每条用例的断言要点、headless 不可测项的三级处置（排除 / 半测 / 全测）、
> 以及执行 harness 总则全部以本文档为准。
>
> **维护声明（强制）**：后续所有对测试项的修改、补充、删除、状态变更（如半测升级为全测、排除项解除），
> 都必须同步更新本文档——先改本文档、再改测试代码。测试代码与本文档不一致时以本文档为准并视为实现缺陷。

## 0. 背景

编辑态契约测试（`TJBrickContractTests`）锁"参数写进了组件"；uGUI **运行时内部逻辑**驱动的行为
（Slider 锚点驱动、Dropdown 展开链路、InputField caret、布局链传播）编辑态不可测，须进 Play Mode。
入口：`Create.GameFramework.TJBrickContractPlayTests.RunAll()`（marker `BRICK_CONTRACT_PLAY_TEST_FAILED`，
前缀 `[BrickContractPlay]`），与 `TJBrickAssemblyTests.RunAll()` 互不调用、代码互不引用。

## 1. Harness 总则

- **最小参数实例**：遍历 Controls 10 prefab 逐个建最基础实例——label / value_text / button / toggle /
  dropdown / input_field / slider / progress_bar 传 `{}` 零参数（模板默认已可观测：label='Label'、
  value_text='0'、button='Button'、toggle isOn=true、dropdown 内置 3 options 可直接展开）；icon / sprite
  传一个 `spritePath`（用包内 `Icons/RoundedRectBorder`）。
- **挂载**：专用测试 Canvas + EventSystem，brick 经 `CreateWithParams` 直挂 `canvas.transform`
  （**勿走 AssemblePanel**——它会包 Canvas+CanvasScaler 造成双重缩放）；几何断言一律世界坐标、
  以各 brick root 为参照（分辨率无关），容差 0.5px。
- **交互驱动**：全部用 `ExecuteEvents.Execute` 合成 EventSystem 级指针事件
  （pointerDown / pointerUp / pointerClick / pointerEnter / pointerExit / beginDrag / drag / endDrag /
  scroll，PointerEventData.position 用世界→屏幕坐标换算）。
- **时序类统一"等稳定/帧采样"**：ColorTint fade、Toggle Fade、Dropdown AlphaFadeList、
  ButtonScaleEffect tween 都是异步渐变——Verdict 前等若干帧/等目标值收敛再读终值，帧数上限防挂起。
- **帧驱动（2026-09-12 定论，修正两轮误判）**：帧冻结的**真因是 Error Pause**——Console 的
  出错即暂停开关：游戏自身或测试的任何 LogError（甚至 Verdict 输出失败）都会置 isPaused=true，
  冻结一切帧推进行为（协程 tween、Dropdown.Hide 的 DelayedDestroyDropdownList、Destroy 帧尾冲刷、
  caret 帧内隐藏、scaled Time），而 bridge 的 editor tick 与 canvas 侧 CrossFade 照常——于是同步
  路径用例全过、帧依赖用例全冻，表象酷似"编辑器饿帧"（第一轮误判）。**对策四件套**：①失败只缓冲、
  Verdict 统一输出（避免中途 LogError 触发暂停）；②RunAll 武装时清 isPaused；③ArmSequence 开场
  清 isPaused；④每个等待 tick 清 isPaused（中途触发也只损失一拍）。引擎侧事实：本引擎
  `QueuePlayerLoopUpdate()` 被魔改为仅 `SetSceneRepaintDirty()`（不跑 player loop）；
  `EditorApplication.Step()` 无效。门禁协议 = agent 编排四步：`RunAll()`（武装+进 Play）→
  **in-flight** `return ArmSequence();`（runtime script 工具）→ `return Verdict();`（返回结果
  字符串）→ `ExitPlay()`；ArmSequence 结束日志会输出序列期间帧数（frames during sequence）供诊断。
- **断言形态**：一律组件状态断言（Canvas.enabled 判可见、activeInHierarchy、anchors/sizeDelta、
  canvasRenderer alpha/color、事件计数），**不用截图**（用户经 Web 端可视化测试）。
- **命名与清理**：参数化实例命名沿用 `TJCC_` 前缀；Play 内创建的运行时对象退出 Play 即销毁，
  不 SaveScene、零场景污染。
- **入口形态**（2026-09-12 拍板 #5，按帧驱动实测定稿）：`RunAll()` 武装+进 Play；**EnterPlay / ArmSequence /
  Verdict / ExitPlay 四步各自独立封装为 public 静态入口**，支持 agent 分步调用编排；完整门禁协议 =
  RunAll → in-flight ArmSequence（保持帧全速）→ Verdict（返回结果字符串）→ ExitPlay（见上"帧驱动"条）。
- **Dropdown 纪律**：禁止在编辑模式调用 `Dropdown.Show()`（运行时对象会序列化进场景产生损坏引用）；
  展开必须由 pointerClick 驱动（**严禁脚本直调 Show()**——m_AlreadyClicked 会卡死第二次展开）；
  展开列表断言只读活跃克隆（`Item N: <选项文本>`，挂 Viewport/Content 下），**勿读失活模板**
  （模板 Item 被 SetupTemplate 有意 SetActive(false)、其 ItemLabel 文本恒空）。

## 2. 用例清单

编号规则：`<brick 首字母>-<序号>`；Container/Layout（§2.9，Phase 2）因单字母冲突（S 已被 slider 占用）
改用两字母前缀：SV=stack_vertical / SH=stack_horizontal / SC=scroll_view / GR=grid / MD=modal /
PN=panel / LI=list_item。来源标注：〔罗列〕= 用户罗列（2026-09-12 Controls 轮 / 2026-09-14
Container-Layout 轮）；〔背书〕= 用户 2026-09-11 预先罗列的行为目标；〔补充〕= 摸底/顾问补充；
〔拍板〕= 用户拍板结论衍生；〔纪律〕= 执行/排序纪律固化。

### 2.1 ugui.label / ugui.value_text

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| L1 | 〔罗列〕 | 默认文本对背景图上下居中（左对齐） | MiddleLeft 默认已落地（2026-09-12）。开背景变体（`backgroundEnabled:true`）断言 `|centerY(Text) − centerY(root)| < 0.5px`；默认无背景态同断言（Background GO active 但 Image.enabled=false 是模板默认，不影响文本定位） |
| L2 | 〔补充〕 | 单行与换行两态均保持垂直居中 | Wrap 模式两行文本 `|centerY(Text) − centerY(root)| < 0.5px` |
| L3 | 〔补充〕 | 文本渲染 sanity | 激活布局后 `Text.cachedTextGenerator.vertexCount > 0`（防 font 丢失静默不渲染——模板烘 LegacyRuntime） |
| L4 | 〔补充〕 | 大 fontSize 不溢出根 rect | fontSize 放大变体断言 Text worldRect ⊆ root worldRect（Truncate 溢出被裁是预期，越界渲染不是） |
| L5 | 〔补充〕 | value_text 默认内容 '0' 同套 | L1-L4 全套对 value_text 复跑（结构同构，内容默认值不同） |

### 2.2 ugui.button

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| B1 | 〔罗列〕 | 无 icon 时文本居中 | `centerX/Y(Text) == centerX/Y(root)`（根 UGUIFlexLayoutGroup MiddleCenter + Label flexW=1，Icon 默认失活不占位——已实测成立） |
| B2 | 〔补充〕 | 有 icon 变体 | icon.active:true + spritePath：label 中心右移 ≈ (iconW+spacing)/2；渲染序 Background siblingIndex=0 < Icon（Background 恒最底层） |
| B3 | 〔补充〕 | icon 陷阱固化 | icon.active:true **不配** spritePath：Icon Image.enabled=false（PostProcess 无 sprite 守卫），不渲染白块——历史坑固化为用例 |
| B4 | 〔补充〕 | onClick 触发 | pointerClick → listener 计数 +1 |
| B5 | 〔补充〕 | ColorTint 四态 | pointerEnter→highlightedColor、pointerDown→pressedColor、pointerUp→selectedColor；读 `canvasRenderer.GetColor()` 比对（乘 Image.color 基色），等 fadeDuration 稳定 |
| B6 | 〔补充〕 | ButtonScaleEffect opt-in + clickScale tween | **模板烘焙 ButtonScaleEffect=禁用**（SetScaleEffect handler 仅在传 clickScale/hoverScale 参数时隐式启用——2026-09-12 实测发现）。断言：默认 button 实例 BSE enabled=false（opt-in 契约）；icon_scale 实例（clickScale/hoverScale 参数）BSE enabled=true 且 press→(0.8,0.8,1)、up→hover(1.1,1.1,1)、exit→回 (1,1,1) 多帧采样收敛；OnDisable 停 tween 并还原 |
| B7 | 〔补充〕 | interactable=false | 无 onClick、disabledColor 应用 |

### 2.3 ugui.dropdown

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| D1 | 〔罗列〕 | 展开列表宽度与 dropdown 对齐 | "Dropdown List" `width == root width`、左缘对齐（世界坐标；List 挂 root 下、Template 锚 stretch-x 复制） |
| D2 | 〔罗列〕 | 少量选项全部可见 | 默认 3 项：引擎 extraSpace 收缩（List 高 ≈ 3×20+边距，非模板 150）；每活跃 item clone worldRect ⊆ Viewport worldRect |
| D3 | 〔罗列+拍板3〕 | 大子项数（≥8 项）overflow 变体 | 运行时注入 options（definition 无 options 参数）：Scrollbar 激活（ScrollRect AutoHideAndExpandViewport）、视口内缩 ≈17px（宽20−间距3）、`verticalNormalizedPosition` 可滚、滚到 0/1 极值时首/尾项进入视口。不做"自动滚到选中项"（拍板：引擎无此行为，不要求） |
| D4 | 〔补充〕 | 展开链路结构 | pointerClick 驱动展开（禁 Show() 直调）；"Dropdown List" 为 root 子物体、Canvas sort=30000；"Blocker" sort=29999、全透明 Image、Button(onClick=Hide) |
| D5 | 〔补充〕 | 选中回填 | 点 item clone → value 更新、caption Label 文本 == 选项文本、onValueChanged 触发、List+Blocker 销毁 |
| D6 | 〔补充〕 | Blocker 拦截与关闭 | 列表打开时 pointerClick 其他按钮 → 该按钮不触发；点击 Blocker → 列表关闭且 Blocker 销毁 |
| D7 | 〔补充〕 | 贴底翻转 | 实例锚在 canvas 底缘 → 展开列表翻转向上（引擎 FlipLayoutOnAxis 行为固化） |
| D8 | 〔补充〕 | itemHeight×itemFontSize 配对约束 | 22pt 变体必须配 itemHeight ≥ 29（fontSize×1.15+3≈28.3；Settings 实证组合为 22pt+itemHeight 30）——历史坑固化：17px 可用高里 22pt 文本整体被裁 |
| D9 | 〔补充〕 | 0 options fail-soft | 空选项点击不崩：列表可开可关，无活跃 item |
| D10 | 〔纪律〕 | 只读活跃克隆 | 断言枚举 Viewport/Content 下活跃克隆；模板 Item 失活、ItemLabel 文本恒空——读模板当 bug 证据视为用例错误 |
| D11 | 〔补充〕 | 只调 dropdown 本身（height/fontSize）→ 展开行自动适配 | PostProcess 派生契约（2026-09-16）：height=64→行高 60（box−4，20px 下限）、fontSize=24→行字号 24；断言行高 60/行字号 24/行宽==box 宽/列表宽==box 宽且左缘对齐/列表高 188=3×60+2×4 边距（extraSpace 收缩）/滚动条隐藏（模板窗自动抬升至 3×60+12=192）/caption 24。行宽无需代码——行 stretch 锚天然跟随 box（D1 同机制），派生只补行高/字号 |
| D12 | 〔补充〕 | 显式行参数优先 | 同 box 参数（200×64/fontSize 24）+ itemHeight 40 + itemFontSize 16：行 40/字 16 落值而非派生 60/24；模板窗未抬升（3×40+12=132<150，Template 保持 150）；列表高 128=3×40+8、滚动条隐藏、行全可见 |
| D13 | 〔补充〕 | 无尺寸/字体参数 → 默认行几何不受派生影响 | 负对照：默认实例 Template 150/Content 28 原值、行高 20/字 14（DefaultControls 复刻）；派生仅在 height/fontSize 显式传入且对应行参数缺省时发生 |

### 2.4 ugui.input_field

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| I1 | 〔罗列〕 | caret 可见（不被遮挡） | 结构断言：激活后 caret 名 `<root> Input Caret`、parent=Text Area、`Input Area/Text Area siblingIndex > Input Area/Background siblingIndex`（"Text Area 层不可拆"回归点——caret 随 Text 挂 Text Area 内，渲染序在 Background 之后；2026-09-14 起 Background/Text Area 收纳于 Input Area 包装层（hint label 动态布局），层级加深一层、断言路径同步） |
| I2 | 〔罗列〕 | 文本对背景图上下居中 | `|centerY(Text) − centerY(root)| < 0.55px`（textAlignment default MiddleLeft 已烘模板；DefaultControls 对 input 文本 6px/7px 上下内边距是有意设计，0.5px 非对称属模板复刻，容差 0.55） |
| I3 | 〔补充〕 | 指针激活 | pointerDown/Up → `EventSystem.currentSelectedObject == inputField`、caret 出现 |
| I4 | 〔补充〕 | placeholder 显隐 | 空文本时 Placeholder 可见；赋文本后隐藏 |
| I5 | 〔补充〕 | 文本事件 | onValueChanged / onEndEdit 触发计数与值序列 |
| I6 | 〔补充·半测〕 | ContentType 过滤（partial） | 反射调 `protected KeyPressed(Event)` + `Event.KeyboardEvent("5"/"a")`：整数字段拒收字母、characterLimit 逐键截断、caret 位移前进。**标注：覆盖过滤逻辑层，不含键盘队列派发层**；反射取不到方法自动 SKIP（不算 FAIL） |
| I7 | 〔补充〕 | 失焦 | 选中移到其他对象后 caret 不可见（销毁/失活/alpha→0 三态谓词）。**引擎发现（2026-09-12 活体实证）：本引擎 DeactivateInputField 缺 standard uGUI 的 `m_CaretVisible=false` 重置，deselect 后 caret quad 持续绘制（alpha 1.00、1500+ 帧不隐藏）——期望契约记 SOFT（非门禁），引擎补上该行后自动转 PASS** |
| I8 | 〔补充〕 | interactable=false | pointerDown 无可见 caret、无激活、notFocused。**同 I7 引擎发现（caret 不隐藏路径同源），记 SOFT** |
| I9 | 〔排除〕 | 键盘队列派发层 / IME 组字流 | 见 §3 排除清单 |
| I10 | 〔排除〕 | caret blink 相位 | 见 §3——结构断言（I1）承载契约，blink 是纯引擎时序振荡 |

### 2.5 ugui.slider

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| S1 | 〔补充〕 | value → fill 渲染 | Fill 是 `Image.Type=Filled` → 引擎驱动 **fillAmount**：设 value=0.37 → `fill.fillAmount ≈ 0.37`（读 fillRect anchor 测不到——Filled 分支不走 anchor，这是引擎行为要点） |
| S2 | 〔补充〕 | handle 锚点跟随 | `handleRect.anchorMin.x == anchorMax.x == normalizedValue`、垂直锚 (0,0)-(0,1)；**handle 高度 == HandleArea 高度**（handle.height 参数由 HandleArea 承载的特例——竖椭圆历史回归） |
| S3 | 〔补充〕 | 极值 0/1 | fillAmount 0/1、handle 中心在轨道两端——**用锚点断言，禁 containment**（handle 半宽出界是 uGUI 默认行为） |
| S4 | 〔补充〕 | 拖动交互 | pointerDown@handle → drag → up：value 按指针 x 世界坐标比例变化；wholeNumbers 吸附；onValueChanged 值序列 |
| S5 | 〔补充〕 | interactable=false | 拖动不改 value |
| S6 | 〔补充·可选〕 | direction=RightToLeft | fill/handle 反向（低优先，可选） |

### 2.6 ugui.progress_bar

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| P1 | 〔补充〕 | value → fill 渲染 | 同 S1（fillAmount==normalizedValue） |
| P2 | 〔补充〕 | 默认无柄 | `HandleArea.activeSelf == false`（handle.active default:false，模板已实测） |
| P3 | 〔拍板2〕 | 默认 interactable=True 契约固化 | 轨道 pointerDown/拖动可改 value（引擎 Slider 支持轨道操作）——**按现状断言，不改默认** |
| P4 | 〔补充〕 | handle.active:true 变体 | 激活后 handle 几何随 value（同 S2） |

### 2.7 ugui.toggle

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| T1 | 〔罗列〕 | 勾选框不与文本重叠 | `Label.left(≈23px) > Background.right(≈20px)`（世界坐标；3px 间隙为模板默认） |
| T2 | 〔罗列〕 | 勾选框与文本同一水平线 | `|centerY(Background) − centerY(Label)| < 0.5px`，且两者中心都落在 root 中线 |
| T3 | 〔补充〕 | Fade 显隐 | 翻转后 checkmark `canvasRenderer alpha → 1/0`（等 fade 稳定再读，ttrans=Fade） |
| T4 | 〔补充〕 | 点文本区域也能翻转 | Toggle 挂根、Label raycastTarget=True——pointerClick@Label 位置同样翻转 isOn |
| T5 | 〔补充〕 | onValueChanged | 计数与 isOn 值同步翻转 |
| T6 | 〔注意〕 | isOn 初态 | isOn 无 default、模板烘 True——最小实例初态是 on，断言翻转方向从 on→off 开始 |

### 2.8 ugui.icon / ugui.sprite

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| X1 | 〔补充〕 | 最小实例渲染 | 一个 spritePath（Icons/RoundedRectBorder）：Image.sprite 非空、enabled=true |
| X2 | 〔补充〕 | raycastTarget 开关 | true/false 两态 RaycastAll 命中数 ±1 |
| X3 | 〔补充〕 | 可点击性差异 | Icon 有 Button（pointerClick → onClick 计数）；Sprite 无 Button（点击无 Selectable 反应且无异常） |
| X4 | 〔补充〕 | UIAudioFeedback 安全 | clip 为 null 时 pointerDown/Enter 无 Console Error；（可选）带 clip → AudioSource.isPlaying |
| X5 | 〔排除→SOFT〕 | preserveAspect 视觉 | 图像缩放比例不可状态断言——记 SOFT 或排除（见 §3 说明） |

### 2.9 Container/Layout · Phase 2（2026-09-14 扩围起草；Layout 5 组 GR/SV/SH/SC/MD 于 2026-09-14 实现全绿；**PN 组于 2026-09-15 按拍板实现落地**——PN2 并入 PN1、PN4 升级双臂行为契约、PN6 新增、panel-in-panel 深层点击链不单列（PN1 落点 + PN3 路由已覆盖本质）；实现基线见 §4 变更记录；LI 两小节保持"已起草、未实现"——按实现范围拍板，勿视为缺陷）

扩围对象：Container 3（panel / list_item / modal）+ Layout 4（grid / scroll_view / stack_horizontal /
stack_vertical）。（modal 于 2026-09-15 由 Layout 迁至 Container/assemblyOnly，brickId 不变。）原 §2.9"范围外"条目（scroll_view / modal / stack·grid）自本节起草起全部转入覆盖；
残余范围外项收敛至 §2.9.7。

#### 2.9.0 夹具与判据总则（叠加在 §1 之上，Container/Layout 用例共用）

- **有效组合矩阵（2026-09-14 磁盘实测；`CanNest` 双向 AND：子 ∈ 父.allowedChildren ∧ 父 ∈ 子.canNestUnder）**：

| 父实例 | 有效可挂子项 | 备注 |
|---|---|---|
| panel（allowedChildren="any"） | **16**：stack_v / stack_h / grid / scroll_view / modal / panel / Controls×10 | "any" 须按子侧 canNestUnder 收敛；**list_item 挂不进 panel**（其 canNestUnder 仅 scroll_view） |
| stack_v / stack_h | **15**：stacks×2 / grid / scroll_view / Controls×10 / panel | 声明集即有效集 |
| grid | **13**：stacks×2 / Controls×10 / panel | **勘误**：不含 scroll_view（交接文档 §5.2"stacks+scroll_view+…"描述有误，以磁盘为准——grid 可被 scroll_view 嵌、不能反向嵌） |
| scroll_view | **5**：stack_v / stack_h / grid / list_item / panel | **零 Controls**——"挂控件"类断言对 scroll_view 不成立，子项全是布局/容器类 |
| modal | **5**：stack_v / stack_h / button / label / panel | dropdown 等不在列，勿挂 |
| list_item | **none** | 无可挂子项；其自身仅可挂 scroll_view（唯一组合通道，用例见 SC3/LI6） |

- **夹具只构造合法组合**：以 allowedChildren / canNestUnder 双侧校验为准，未声明的不强行测试；
  违规组合的负向断言（编辑态构建仅 warning）归装配回归/声明面契约两层，Play 不重复。
- **挂载点落点断言 + 零告警门**：每个被挂子项断言 `child.parent == 预期挂载点`（stack/grid → 根；
  scroll_view → `Viewport/Content`；panel → 根；modal → 显式槽路径）；序列期零 `[BrickFactory]` 告警
  照旧是门——挂载推理对找不到的挂载点是 fail-soft（warning + 静默回退默认容器），落点断言是唯一防线。
- **"完整显示 / 不遮挡"判据分两套**：
  - 布局组驱动的父（stack / grid、scroll_view+contentLayout）：断言子项**被驱动后** worldRect 两两
    不相交（容差 0.5px）且落在父内容界内；grid 另断言子项 rect == 所在 cell rect。
  - 无布局组的挂载点（panel 根、modal 三槽、未传 contentLayout 的 scroll_view Content）：子项 rect
    不被驱动、多子会先天互叠——夹具必须显式给非重叠布局（或嵌 stack），断言转为"挂载后 rect == 夹具
    期望"（挂载未破坏几何）。
- **截断例外仅限启用的滚动方向**：scroll_view 横向未启用时，Content 的横向溢出是被 Viewport Mask
  真实裁掉的——按截断 FAIL，不算"可滑动显示"；纵向同理。
- **"默认值实例"的边界**：结构性前提参数允许显式传——实例自身 width/height（stack/grid 默认尺寸摸底
  前先显式传，否则显示断言无意义）、触发滚动的子项数量、grid cellSize 装不下某子项默认内容时调大、
  modal header/footer 槽高度；"默认值"指不传装饰/换肤类参数。
- **摸底三件套先行**（复刻 Phase 1 附录 A 形态：默认激活态 / 组件清单 / 场景态几何基线）：grid 默认
  cellSize、stack 默认 spacing/padding 与 childControl 实效、scroll_view 默认 Content 高与滚动条默认
  参数、modal 槽默认高度与 overlay raycastTarget——基线数直接进断言容差；摸底细节沉淀进本文件
  **§2.9.8 摸底矩阵**（TODO.md 只记拍板结论——Phase 1 细节丢失的教训）。
- **组序纪律**：有全局拦截副作用的组排最后——dropdown 组既有理由照旧；MD 组（全屏 overlay）排其后
  最后跑。混挂实例内做交互冒烟时，拦截性子项（modal overlay、展开期 dropdown 的 Blocker）须先失活 /
  等销毁稳定再测后续子项。

#### 2.9.1 ugui.panel（PN）——2026-09-15 按拍板实现落地（摸底实证见 §2.9.8 E）

夹具（BuildHarness 内常驻）：`pn_host`（**280×184** 显式 RT 写入——panel definition **无 width/height 参数**，
传参必触发零告警门，尺寸/子项布局全部夹具直写）+ 15 个非 modal 子项按 4×4 网格（**70×46 格、子项 60×40、
第 16 格(3,3)有意留空**）+ `pn_modal` 子项（装配默认态）+ PN4 专用 `pn4_btn`/`pn4_cov`（180×60 盖板）+
PN6 五态横排条（110×80）。**摆放（run 1/2 两轮教训）**：BuildHarness 一次性建全部夹具（构建序≠用例
执行序），任何后建夹具都会从构建刻起截胡**先跑用例**的点击——大面板三次摆放连环踩雷（540×370@(-0.14,
0.05) 杀 SH7、400×264@(0.32,0.36) 杀 GR7 的 gm_slider 交互格），终态 **280×184@frac(0.36,-0.38)**：gm
全部交互点 x≤170、sv_mixed 全部交互点 x=166，均在左缘 257.8 之外；下方只与无点击断言夹具重叠（sv_hug
按钮、sc_plain、sc_layout_h/g、sv_mixed 底部子项）；pn4@frac(0.40,-0.24)（run 2 已验证）；pn6 面
raycastTarget=false 对一切 raycast 隐形；空 stack/grid 子项根无 Image、对 raycast 透明（覆盖
dropdown_bottom 无害——D7 全程 ClickDirect+blocker，与 raycast 无关）。

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| PN1 | 〔罗列·吸收原 PN2〕 | 16 类混挂 + 几何中立性 | 15 个非 modal 子项逐项：落点 `parent == panel 根`、rect == 夹具期望（挂载未破坏几何——panel 根无 LayoutGroup、rect 不被驱动）、两两不相交（0.5px）、`Inside(panel rect)`、全部 activeInHierarchy、零 `[BrickFactory]` 告警；modal 子项按**装配默认摸底值**断言（stretch(0,0)-(1,1)/sd 0 → worldRect == host rect，探针 §2.9.8 E），**排除出 pairwise 集合**（overlay-by-design 填满宿主），断言完即 SetActive(false)（overlay raycastTarget=true 组序卫生） |
| PN3 | 〔罗列〕 | 可交互子项事件路由冒烟 | 混挂实例中 button/toggle/slider/progress_bar/input_field/dropdown 各一次合成交互成功（onClick 计数 / isOn 翻转 / DragAt value 变化 / PressAt track 跳值 / caret 出现 / OpenDropdown→选 Item 1→List+Blocker 销毁稳定）——容器不拦截/不吞子项事件；dropdown 排组内最后（展开期 Blocker 是全局拦截面） |
| PN4 | 〔补充·双臂行为契约〕 | raycastTarget 穿透/拦截 | ①默认臂：`raycastTarget==false`（definition 默认经完整装配路径落地，探针实证）且白板 cover 盖住按钮后 `ClickAt` 仍达按钮（cover 不入 RaycastAll 结果）；②拦截臂：置 `true` → RaycastAll 中 **canvas 序 500（harness 画布）内的首命中 == cover**——MD 夹具的全屏 overlay canvas（600/100）自 BuildHarness 起恒在 RaycastAll 头/尾（run 1 实测 hits=10），须过滤 sortingOrder==500 后断言。**helper 语义限制（探针实证）**：`RaycastHandlerAt<T>` 跳过无 handler 的命中继续下找——拦截臂不能断言"按钮收不到合成点击"，只能断言首命中变化（真实 StandaloneInputModule 只处理首命中链，行为上确实拦截）；夹具窗口化（180×60）防全局拦截，测完还原 false |
| PN6 | 〔补充〕 | 底图承载 mesh sanity | 判据 `canvasRenderer.GetMesh().vertexCount`（**引擎 CanvasRenderer 无 vertexCount 成员**，探针 §2.9.8 E；L3 文本 vertexCount 先例的等价通道）：①装配默认=UISprite(border10)+Sliced → 36 verts（3×3 九块，blanket 回退补图——"null-sprite 白板"态实际不存在，见 §2.9.8 E）；②`sprite` 参数（RRB border16）→ 36；③`fillCenter:false` → 32（去中块）；④borderless+Sliced → **4 verts 全幅 UV 塌缩单 quad，正常渲染**（与 2026-09-04 "Sliced+无 border 空渲染"观察不同——当前引擎基线如此，历史注记留档；borderless sprite 用运行时 Sprite.Create，工厂 sprite 参数路径归声明面契约，此处锁渲染行为）；⑤borderless+`type:Simple` → 4 |
| PN5 | 〔排除〕 | visible 绑定 | bindingProps 由游戏侧绑定系统消费，harness 无消费方——见 §3 增行与 §2.9.7 |

#### 2.9.2 ugui.list_item（LI）

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| LI1 | 〔背书〕 | 默认 flex 几何 | 实高 40px（世界坐标）；Label/Value LE flexW=1 平分剩余宽、childAlignment MiddleCenter、行宽 fill 满排、根 spacing 默认 8（DefaultIconMargin，摸底复核） |
| LI2 | 〔补充〕 | 双列对齐配方变体 | `icon.active + iconSprite` 变体 + `icon.margin + childAlignment:MiddleLeft + label.width:150 + label.flexibleWidth:0 + value.flexibleWidth:0 + textAlignment:MiddleLeft` → icon 段 / label 固定段 / value 贴右各列世界坐标断言（LE pin 通道回归；Settings 58/222 实锚仅作历史参照，不复刻数值） |
| LI3 | 〔补充〕 | icon.active 无 sprite 占位 | B3 同模式固化：Image.enabled=false 不渲染白块、flex 占位宽度保留 |
| LI4 | 〔补充〕 | 根 Button clicked | pointerClick → clicked 事件计数 +1（list_item 根是 Button） |
| LI5 | 〔补充〕 | value.active / value.text 落点 | value 显隐两态 + 文本渲染（value_text 同构断言） |
| LI6 | 〔罗列〕 | 作为 scroll_view 子项的行布局 | 唯一合法父、唯一组合通道——用例本体在 SC3，此处登记防漏 |

#### 2.9.3 ugui.grid（GR）

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| GR1 | 〔罗列〕 | 混挂 13 类有效子项（每类一个） | 根 GridLayoutGroup 驱动：逐项落点 `parent == grid 根`；每子项 worldRect == 所在 cell rect（cell 强制驱动）；相邻 cell 子项不重叠；零告警。默认 cellSize 摸底——某类默认内容溢出 cell 时调大 cellSize 属结构性前提（本用例锁几何，不锁内容像素级） |
| GR2 | 〔补充〕 | constraint=FixedColumnCount + constraintCount 变体 | N 项 → count 列 × ceil(N/count) 行；逐项位置按 cellSize+spacing 世界坐标断言（模板默认 constraint 值摸底后补默认态断言） |
| GR3 | 〔补充〕 | startCorner / startAxis 变体 | 排布起点角与填充轴方向变体几何（默认值摸底后定基线） |
| GR4 | 〔补充〕 | padding / childAlignment 变体 | padding 四向内缩几何 + 子项在 cell 内对齐落点 |
| GR5 | 〔背书〕 | preferred 尺寸传播 | grid 挂入 stack_v：stack hug 高 == grid preferred 高（行数×cellSize.y + (行−1)×spacing.y + padding 上下）——尺寸逐级传播链（与 SV2 呼应） |
| GR6 | 〔补充〕 | 子项激活/失活重排 | 失活中段一个 cell 子项 → 其后子项前移补位（GridLayoutGroup 跳过 inactive）；重新激活 → 排布恢复 |
| GR7 | 〔罗列〕 | 可交互子项事件路由冒烟 | 混挂实例中 6 类交互子项各一次合成事件成功（PN3 同式；拦截性子项前置处理同 §2.9.0 组序纪律） |

#### 2.9.4 ugui.stack_vertical / ugui.stack_horizontal（SV / SH）

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| SV1 | 〔罗列〕 | 混挂 15 类有效子项（每类一个） | 根 VLG 驱动：逐项落点 `parent == stack 根`；子项沿主轴顺序排布、worldRect 两两不相交（0.5px）、交叉轴按 childAlignment；零告警 |
| SV2 | 〔背书〕 | preferred 尺寸逐级传播 | 子项显式尺寸 → 组 hug 高 == Σ子高 + spacing×(n−1) + padding 上下；嵌套链 stack_v > stack_h > button：孙项 preferred → 子组 hug → 父组 hug 逐级上传（"父组压扁/展开"的断言化） |
| SV3 | 〔补充〕 | spacing / padding / childAlignment 变体 | 各参数一变体，世界坐标几何断言 |
| SV4 | 〔补充〕 | childControlW/H 默认 true 实效 | 子项交叉轴宽被驱动 == 组内宽 − padding；childForceExpandW/H 默认 true 的主轴多余空间分配行为（摸底后固化基线；与游戏侧 UGUIFlexLayoutGroup 须显式 false 的历史条目是两回事） |
| SV5 | 〔补充〕 | reverseArrangement 变体 | 排布序反转：视觉首项 == sibling 末项（参数落点 + 几何双向断言） |
| SV6 | 〔补充〕 | 子项激活/失活重排 + hug 重算 | 失活中段子项 → 其后上移、组 hug 高 −(该项高+spacing)；重新激活 → 恢复 |
| SV7 | 〔罗列〕 | 可交互子项事件路由冒烟 | 混挂实例中 6 类交互子项各一次合成事件成功（PN3 同式） |
| SV8 | 〔背书〕 | layoutEnabled:false 纯定位容器 | 组组件 `isActiveAndEnabled==false`；两个 label 子项的**作者锚点/anchoredPosition 在 Play 布局 pass 跑过之后原值保持**（引擎 `LayoutGroup.SetChildAlongAxisWithScale` 无条件把子锚点驱动为 (0,1)+顺序堆叠，childControl=false 救不了——组件禁用是唯一闸门，2026-09-16 MainLobby 百分比布局塌陷陷阱的断言化）；该 stack 不再向父级报告 preferred |
| SH1–SH8 | 〔补充〕 | 同套镜像 | 与 SV1–SV8 同构，轴向交换（主轴横、交叉轴纵）；实现按 brickId 参数化复用同一 case 体，勿复制两份漂移 |

#### 2.9.5 ugui.scroll_view（SC）

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| SC1 | 〔背书〕 | contentLayout 三模式 | vertical / horizontal / grid 三变体：Content 上挂对应 LayoutGroup + ContentSizeFitter（vertical→verticalFit=Preferred；horizontal→horizontalFit=Preferred；grid→verticalFit=Preferred）；未传 contentLayout 时 Content 无组（结构断言）——即混挂多子须先传 contentLayout（§2.9.0 判据规则） |
| SC2 | 〔罗列〕 | 混挂 5 类有效子项 | 逐项落点 `parent == Viewport/Content`；contentLayout:vertical 驱动下 Content 总高 == Σ行 preferred（实测 146 = stackv40+stackh40+grid40+list_item16+**panel10**，panel 的 preferredHeight 实测 10——run 2 诊断）；行 worldRect 两两不相交；零告警 |
| SC3 | 〔罗列〕 | list_item 行布局（唯一组合通道） | 行序、contentSpacing 变体间距 5、行高 16、**行宽 fill 实测视口宽**（引擎 AutoHide 内缩后行宽跟随视口，勿写死 400）、Label/Value 落位（LI1/LI2 断言在滚动容器内复跑）；行点击计数（LI4 在容器内仍成立） |
| SC4 | 〔背书〕 | AutoHide 两态 + 内缩几何 | 内容 ≤ 视口 → 滚动条隐藏 + Viewport 满宽；内容 > 视口 → 显示 + 内缩 17px/轴（300→283 实测）；显隐按**实例化当刻视口**判定且**引擎会激活工厂预建 inactive 的滚动条**（见 §2.9.8 D） |
| SC5 | 〔补充〕 | 截断例外精确化 | 仅**启用的滚动方向**溢出可滚入视口：**向上拖**（手指 y 增）→ position 减小、滚到底后末行 worldRect 进入视口（run 3 实测拖动物理，勿用向下拖）；**未启用方向**（horizontal:false）断言 sr.horizontal==false |
| SC6 | 〔补充〕 | 滚动条三条驱动路径 | **handle↔position 双向同步**（驱动 position 后 handle.value 逐点跟随——直拖 handle 环境敏感不可作为门禁，run 4 实测，见 §2.9.8 D）；viewport drag（**从底部向下拖** position 增大，run 3 实测方向）；滚轮（负 y=向下滚）；两端极值 0/1 可达（movementType Clamped 硬 clamp） |
| SC7 | 〔补充〕 | 滚动条分离仍可滚 | 引擎 AutoHide 会 SetActive 激活溢出滚动条（`scrollbar.active:false` 夹具构造不出"未激活+溢出"态）→ 改为 `ScrollRect.verticalScrollbar=null` + 条 GO SetActive(false) 的**分离断言**：拖/滚 position 变化不受滚动条影响；Play 中 Destroy prefab 实例子物体抛 InvalidOperationException（run 3），分离是 Play 合法的等价操作 |
| SC8 | 〔补充〕 | AutoHide 可逆翻转（动态子项集） | 运行时失活部分子项 → Content 高度回缩 ≤ 视口 → 滚动条隐藏 + viewport 回满；反向再激活 → 内缩重现——显隐状态可逆 |
| SC9 | 〔补充·排除视觉〕 | viewportShowMaskGraphic + Mask stencil | 参数落点断言（Mask.showMaskGraphic 两态 + viewportSprite/color 落点）；"遮罩后面渲不渲染"像素级留用户 Web 端裁决（§3 增行）——契约由 SC2/SC5 几何判据承载 |
| SC10 | 〔补充〕 | 滚动后交互坐标 | **向上拖**一段后合成点击：目标行按**滚动后世界坐标**取点（safePoint 从画布世界矩形推算纪律）→ 点击计数命中正确行（滚动前后行↔计数对应关系断言） |

#### 2.9.6 ugui.modal（MD）

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| MD1 | 〔罗列〕 | 三槽显式挂载 | header/content/footer 各挂一子项（显式槽路径 `ModalPanel/ModalHeaderSlot` 等）→ 逐项落槽断言；**挂载推理对 modal 落 else-self（modal 根）**——不传槽/传错槽即落点 FAIL（fail-soft 风险最高点）；header/footer 槽默认高度摸底，为零则配 headerHeight/footerHeight（结构性前提，否则"完整显示"是夹具自伤） |
| MD2 | 〔罗列〕 | allowedChildren 5 类混挂（分槽） | stacks×2 / button / label / panel 按槽分配：落点 + 显示 + 槽间/槽内子项不相互遮挡（ModalPanel 内几何） |
| MD3 | 〔背书〕 | overrideSorting 层级压制 | 嵌套 canvas overrideSorting == true 生效；sortingOrder 参数落点与压制关系（与测试 canvas 内既有对象的渲染序对比；**getter 语义坑：先读 overrideSorting 再读 sortingOrder**；夹具注意 TJCC_PlayCanvas sortingOrder=500，modal 须设 >500 才可断言压制） |
| MD4 | 〔补充〕 | 三槽几何 | headerHeight / footerHeight / panelPadding → 槽高与 Content 槽让位；panelWidth / panelHeight → ModalPanel rect 世界坐标断言 |
| MD5 | 〔补充〕 | overlay 上子项交互 | overlayColor 遮罩之上 button pointerClick 成功（overlay Image raycastTarget 摸底：若拦截子项点击即缺陷回归）；label 正常显示 |
| MD6 | 〔补充·摸底〕 | 根 CanvasScaler 嵌套缩放 | modal 根自带 CanvasScaler，在 TJCC_PlayCanvas 下的嵌套缩放行为摸底（历史 defect 记录曾报 modal 树根问题，definition 现已含 CanvasScaler，以实跑为准）；结论直接定 MD 组几何容差，异常记 SOFT/缺陷上报 |
| MD7 | 〔纪律〕 | 组序最后 | 全屏 overlay 具全局拦截副作用 → MD 组排在 dropdown 组之后最后跑（与既有"Blocker 拦截合成点击"排序理由同源） |

#### 2.9.7 Phase 2 范围外（收敛残余）

- `visible` 绑定（panel / scroll_view 的 bindingProps.visible）：bindingProps 由游戏侧绑定系统在运行时
  消费，harness 无 UIManager/绑定系统，无消费方——排除（§3 增行）。
- CanNest 违规组合负向用例：编辑态构建仅 warning，归装配回归/声明面契约两层——Play 不构造违规夹具。
- Mask/stencil 像素级视觉：几何判据承载契约，视觉裁决留用户 Web 端（§1 断言形态纪律）。
- Sliced"2×border > 消费方最小边"的中段反转态（2026-09-03 经验项）：PN6 五态断言面不含此态（需另立
  小尺寸夹具 + 反转几何/UV 探针）；borderless Sliced 的当前引擎基线已入 PN6（§2.9.8 E-5 历史注记）。

#### 2.9.8 Layout 5 摸底矩阵（2026-09-14 场景态实测；探针=TJCC_ 前缀场景态实例 + ForceRebuildLayoutImmediate + finally DestroyImmediate，编辑态零 Play）

**A. 管线与引擎级发现（写用例前必读）**

1. **CreateWithParams 单体路径不做挂载点推理**（推理仅 trees 装配路径有）——夹具必须显式传挂载点
   transform：stack/grid 子项 → 容器根；scroll_view 子项 → `Viewport/Content`；modal 子项 →
   `ModalPanel/Modal{Header,Content,Footer}Slot`。探针实证：传 sv.transform 时子项落 root 而非 Content。
2. **引擎 gate 实锤 + 管线缺口已修**：嵌套 canvas `overrideSorting=false` 时 `Canvas.sortingOrder`
   **setter 静默丢弃**写入（写 600 读 0；开 override 后写 700 读 700）。原管线 PostProcess 在
   ApplyParameters 之后才开 override → 嵌套 modal 的 `sortingOrder` 参数恒被丢、保持模板烘焙值 100；
   standalone root canvas 无 gate 不受影响（契约层因此未暴露）。**修复**：BrickFactory.CreateWithParams
   在 ApplyParameters 前对 root Canvas 无条件预设 overrideSorting=true（与 PostProcess 语义一致、仅提前；
   root canvas 拒绝该 flag 无害）；修复后嵌套 modal sortingOrder 参数实测生效（600 落值）。
   **2026-09-15 追补**：modal 迁 Container/assemblyOnly 后，CreateInternal 的 assemblyOnly 分支在
   应用 definition 默认前同样预升 overrideSorting——嵌套 modal 的 definition 默认 sortingOrder=100
   也落值（此前会被 gate 丢弃归 0）。
   注意 T14a 断言的是 AssemblePanel 的 panel root canvas（parent=null，root canvas 无 gate），与本 gate 无关。
3. **AutoHide 显隐 / Viewport 17px 内缩编辑态不跑**（ScrollRect 运行时逻辑，编辑模式无 Update 驱动；
   场景态探针 Viewport 恒满宽、滚动条恒为参数激活态）→ SC4/SC6/SC8 全部 Play 断言；读数以
   `CreateWithParams` 新实例为准（勿沿用旧面板烘焙内缩基线）。
4. **modal 嵌套缩放定论（MD6）**：嵌套（父 canvas ConstantPixelSize）下自身 CanvasScaler 不生效
   （scaleFactor 继承 root canvas=1）、lossyScale=1、root rt stretch 填满父 canvas——**几何断言直接用
   父 canvas 本地坐标 1:1，无缩放失真**。standalone 时 modal=root canvas：rect 铺满 Game View、
   CanvasScaler ScaleWithScreenSize(1920x1080) 生效（scaleFactor 随 Game View 尺寸实算）。
   overrideSorting：嵌套=true（PostProcess 无条件开）、独立=false（root canvas 被引擎重置，预期无害）。
5. **overlay Image raycastTarget=True**（默认色黑 alpha 0.8）——PN3/MD5 的"拦截性子项前置失活"纪律的
   实证依据；modal 自身子项在 overlay 之上、raycast 先于父，overlay 上子项点击正常。

**B. 逐 brick 基线（断言锚）**

- **grid**（模板 root 160x30；组件 RectTransform+GridLayoutGroup+CanvasGroup）：GLG 默认全与
  definition 一致——cellSize[100,100] spacing[4,4] startCorner=UpperLeft startAxis=Horizontal
  constraint=Flexible constraintCount=2 childAlignment=UpperLeft padding 全 0。cell 强制驱动：子项
  sd==cellSize、位置=padding+cell 中心逐格推进（同行 x 间隔 cell.w+spacing.x；行间 y 间隔
  cell.h+spacing.y）；GLG.preferredHeight=行数×cell.h+(行数−1)×spacing.y+padding 上下（5 项 400 宽
  cell60 sp10 → 1 行 prefH=30；FixedColumnCount/2 → 2 列 3 行实测正确）。失活中段子项 → 其后前移补位、
  再激活恢复（GLG 跳过 inactive）。
- **stack_v / stack_h**（模板 root 160x30；组件 RectTransform+{V,H}LG+CanvasGroup）：默认全与
  definition 一致——spacing 4、padding 0、childAlignment UpperLeft、childControlW/H=true、
  childForceExpandW/H=true、childScaleW/H=false、reverseArrangement=false。preferred 公式精确：
  VLG.prefH=Σ子 pref+spacing×(n−1)+padding 上下（20/30/40+2×10=110 ✓ 失活中段 →70 ✓；嵌套链
  sv>sh>2btn：HLG.prefW=60+80+5=145 ✓、外层 VLG.prefH=40+8+8=56 ✓）。**childControl=true 实效**：
  子项 rect 完全由组驱动——主轴按 min/preferred、空间不足按 preferred 比例压缩（root 高 30 内
  h20/30/40 → 2.2/3.3/4.4）、childForceExpand=true 把主轴剩余空间均分给全部子项（HLG 160 内
  w50/70 → 65/85）。**hug=preferred 语义**：root rect 不自动 hug（恒模板 30 或显式值）——"组 hug 高"
  断言用 LayoutGroup.preferredHeight（或父组消费后的 rect），勿断言 root.sizeDelta。
  reverseArrangement=true → 主轴排布序反转（参数落点+几何双向验证）。
- **scroll_view**（模板 root 160x30；组件 RectTransform+ScrollRect+CanvasGroup）：ScrollRect 默认
  horizontal=false vertical=true movementType=Clamped elasticity=0.1 inertia=true decel=0.135
  scrollSensitivity=1；滚动条 visibility=AutoHideAndExpandViewport、scrollbarSpacing=-3
  （WireBrickScrollbar 接线、非 definition 参数）。滚动条 optional 默认 inactive（宽 20、Handle
  active=true）。Viewport：拉伸锚(0,0)-(1,1) sd[0,0] pivot(0,1)、Image.enabled=true
  sprite=RoundedRectBorder、Mask.showMaskGraphic=true。Content：锚(0,1)-(1,1) sd[0,300] pivot(0,1)。
  contentLayout:vertical → Content 挂 VLG（**childControlHeight=true**、childForceExpandHeight=false、
  spacing=0）+CSF（verticalFit=Preferred、horizontalFit=Unconstrained）；Content 高=Σ行高+spacing×(n−1)
  精确成立（3 行 48、10 行 160）。**夹具行数：超过 300 视口需 ≥19 行（19×16=304）**。
- **list_item（SC3 行基线）**（模板 root 160x30；组件
  RectTransform+CanvasRenderer+Image+Button+CanvasGroup+UGUIFlexLayoutGroup）：flex 默认
  verticalAxis=false、spacing=8（DefaultIconMargin）、childAlignment=MiddleCenter、
  childForceExpandW/H=false（显式 false 已落模板）。子物体默认态：Icon inactive
  LE(prefW20,flexW0,prefH20)、Label active text='Item' align=MiddleCenter LE(prefW=-1,flexW=1)、
  Value inactive LE(prefW=0,flexW=0)（**空文本退出布局态**）。**默认行高 16px**（fontSize 14 的 Text
  行高；flex.prefH=max(子 prefH)，icon.active 时 20）——历史"40px"是合金弹头 Settings 大字体配置
  下的值、非默认；行宽=Content 宽（fill 满排）、行序按行高+spacing 堆叠。双列配方
  （MiddleLeft+label.width150+全 flexW=0+icon.active）：Icon 段 20 → sp8 → Label 段 150 → sp8 →
  Value 段（Text preferred）全从左起、剩余空间空置。
- **modal**（装配默认；2026-09-15 起 assemblyOnly 无模板，结构=definition childStructure）：**三槽默认全 stretch(0,0)-(1,1) sd[0,0] 互相重叠**——MD1/MD2/MD4
  夹具必传 headerHeight/footerHeight/panelPadding（结构性前提，否则"完整显示"是夹具自伤）；槽内
  子项 rect 不被驱动（槽为纯 RectTransform 无 LayoutGroup）→ 走"无组挂载点"判据（子项 rect ==
  夹具期望）。sortingOrder 默认 100（definition 默认）；嵌套+参数写入经 A.2 修复后生效。挂槽路径
  `ModalPanel/Modal{Header,Content,Footer}Slot` 显式传 transform 实测可达。

**C. 摸底发现的草案冲突登记（PN/LI 启用时裁决，本次不实现不改写）**

- LI1"实高 40px" ↔ 默认实例现实 16px（fontSize 决定）：LI 启用时按"行高=max(子 prefH)、由
  fontSize 决定"改写断言（40 仅作大字体变体参考值）。
- LI3"Image.enabled=false 不渲染白块" ↔ 实测 icon.active:true 无 iconSprite 时 Icon
  **Image.enabled=true 且 sprite=null**（2026-09-02 的"无 sprite Image disable"处理不覆盖 list_item）：
  LI 启用时裁决（夹具传 iconSprite，或按缺陷上报）。

**D. 实现轮次（run 1-5，2026-09-14）实测修正——已按此实现并全绿，与上文冲突处以本节为准**

1. **GLG 的 childAlignment 是生效的**（修正本节 B grid 条目中"子项 rect==cell 强制驱动"的绝对化表述）：
   引擎 `GetStartOffset` 按 childAlignment 把 **requiredSpace 整块**在剩余空间内对齐（MiddleCenter →
   居中偏移 (可用−内容)/2，run 2 GR4 实测 x+20/y+44）。B 节的 cell 公式仅在"内容恰好占满"或默认
   UpperLeft 时成立；startCorner/startAxis 的引擎源码语义（PackageCache GridLayoutGroup.cs L269-339）：
   `positionX/positionY` 索引翻转——**单列时 cornerX 翻转无效、列贴 childAlignment 对齐边**（GR3
   LowerRight+Vertical 实测=左列从底向上，勿按标准直觉写"贴右"）。
2. **uGUI 嵌套链 preferred 的交叉轴语义**：垂直组的 preferredWidth = **max(子 prefW) + padding**（交叉轴
   取最宽子，非求和）；只有主轴求和。SH2 镜像链实测 inner VLG prefW=25（max(20,25)）、outer HLG
   prefW=45（25+pad20）——"同套镜像"断言的**数值**因轴向而异（SV2 的 145/56 是主轴求和）。
   另：**根上有 width/height 参数的子组**——参数经 HandleSetSizeDelta 写根 LE（priority 1）**遮蔽组自身
   的 preferred 报告**（GR5：父组看到的是 LE 值 100 而非 GLG prefH 85）——嵌套链夹具的内层组勿传
   与断言轴同向的尺寸参数，或按 LayoutUtility（取 priority 最高）断言传播。
3. **assemblyOnly panel 的 preferredHeight 实测 10**（SC2 诊断：40+40+40+16+10=146）——SC 组混挂
   Content 总和公式含此项。
4. **引擎 AutoHideAndExpandViewport 会无条件 SetActive 激活工厂预建 inactive 的滚动条**（内容超视口时），
   并同步施加 17px 内缩——"scrollbar.active:false 构造未激活溢出态"不可行（SC7 改分离断言）；
   相应地 sc 类夹具的行宽跟随**实测视口宽**（SC3 勿写死 400）。
5. **ScrollRect 拖动物理**（方向纪律，勿再写反）：手指**向上**拖（终点 y 增）→ position 减小（露出下方
   内容）；**向下**拖 → position 增大；滚轮 **y 负=向下滚**（position 减小）。夹具拖动前先 pin 到有
   空间的极端位。**DragAt 合成必须含 beginDrag/endDrag**——ScrollRect.OnDrag 以 m_Dragging 门控、
   只有 OnBeginDrag 置位（Scrollbar 无此门控，故 handle 拖动在缺 beginDrag 时仍"看似正常"，run 1 教训）。
6. **Play 中 Object.Destroy prefab 实例子物体抛 InvalidOperationException**（"Destroying a GameObject
   inside a Prefab instance is not allowed"）——运行时改造夹具用 SetActive(false)/引用置 null/SetParent(null)。
7. **直拖 Scrollbar handle 环境敏感**（run 2 在夹具原位有效、run 4 移位后静默 no-op，raycast/遍历分析
   无果）——SC6 的 handle 断言锁定 handle.value↔position **双向同步契约**（API 驱动 position、断言
   handle 跟随），拖动物理由 viewport drag + 极值拖动承载。若后续复测 handle 直拖恢复稳定可升级断言。
8. **夹具摆放纪律**（run 1-3 的遮挡连环教训）：**Layout 夹具的带 handler 子项（button/toggle/list_item
   行/滚动条）是 raycast 截胡源**——新夹具与 Controls 既有交互实例、以及 Layout 组之间的**交互点击点**
   必须两两错开；纯断言夹具（无点击需求）可任意叠放。混挂交互点击一律取**元素中心**（progress track
   勿取 75% 点——全宽行的偏右点击点极易落入相邻夹具）。

**E. PN 组摸底矩阵（2026-09-15 Play 态实测；探针=TJCC_ProbeCanvas 临时画布 + CreateWithParams + 双帧渲染后读值 + finally Destroy）**

1. **panel 装配默认态（blanket 回退补图，definition 单独不表达）**：root sizeDelta **160×30**（BuildFromDefinition
   100×100 → ApplyUnityDefaults blanket 分支改写）、中心锚点；Image `sprite=builtin UISprite（border 10）`、
   `type=Sliced`、`fillCenter=true`、`raycastTarget=false`、`color=白`——即"null-sprite 白板"态**不存在**：
   sprite==null 的 Image 被 blanket 无条件填 UISprite（ApplyUnityDefaults L685-701），definition 的
   raycastTarget/color/type/fillCenter 默认在其后应用（sprite 无 default、不被覆盖）。**panel definition 无
   width/height 参数**（对照 stack/grid/scroll_view 2026-09-08 增补项）——夹具尺寸必须 RT 直写，传参必报
   unknown-parameter 告警。
2. **modal 子项装配默认（挂在 panel 内，PN1 语义）**：root stretch(0,0)-(1,1)/sd[0,0] → worldRect ==
   宿主 panel rect；Canvas overrideSorting=true、sortingOrder=100（definition 默认，经 CreateInternal
   assemblyOnly 分支预升 flag 后落值）；CanvasScaler ScaleWithScreenSize（嵌套惰性、lossyScale=1，同
   MD6）；overlay Image `raycastTarget=true`、sprite=UISprite、色 #000000CC——**PN1 断言后必须失活**
   （组序卫生；实证它其实不吞 canvas-500 内容的点击——RaycastAll 按 canvas sortingOrder 降序，嵌套
   modal(100) 排在宿主画布(500)命中之后，但失活纪律照旧执行）。
3. **PN4 raycast 双臂语义（探针 canvas 30000 实测；harness 500 有 run 1 修正）**：cover `raycastTarget=false`
   时**不入 RaycastAll 结果**（同一位置的按钮 Background/Label 命中、handler 链达按钮）；置 `true` 后
   **canvas 序 500 内首命中 == cover**（同画布后建 sibling 序在上）。**harness 修正（run 1 FAIL 实测）**：
   MD 夹具（md=600 / md2=100 嵌套 canvas）自 BuildHarness 起恒在 RaycastAll 头/尾（hits=10、裸首命中=md
   overlay）——canvas 序过滤后才可断言同序首命中；其余组用 RaycastHandlerAt 找 handler（md overlay 无
   handler 被跳过）故不受影响，仅裸 RaycastAll 断言暴露。**helper 语义限制**：`RaycastHandlerAt<T>` 逐命中
   找 handler、会跳过无 handler 的 cover 继续下找——合成点击仍会到达按钮，拦截臂只可断言首命中变化。
   RaycastAll 排序 = canvas sortingOrder 降序（嵌套 canvas 命中按其 overrideSorting 后的 sortingOrder
   参与全局排序：30000 内容 → modal(100) → 游戏画布）。
4. **PN6 判据通道：引擎 CanvasRenderer 无 `vertexCount` 成员**（CS1061；成员面实测：hasPopInstruction/
   materialCount/absoluteDepth/cullTransparentMesh/hasRectClipping/relativeDepth/cull/isMask + GetMesh(0)
   等方法）——等价通道 = `canvasRenderer.GetMesh().vertexCount`（Mesh 标准 API）。
5. **PN6 五态顶点基线**（110×80 消费尺寸、双帧渲染后读）：UISprite(10)/Sliced=**36 verts / 54 idx**；
   RRB(16)/Sliced=**36**；RRB+Sliced+`fillCenter:false`=**32 / 48**（9 块去中）；borderless(0)/Sliced=
   **4 verts 全幅 UV(0,0)-(1,1) 全幅几何**——3×3 塌缩单 quad、**正常渲染**；borderless/Simple=**4**。
   Text sanity 同通道 20 verts。**历史注记**：2026-09-04 合金弹头曾实证"Sliced+无 border sprite 渲染为
   空"，当前引擎（Tuanjie 1.10 / uGUI 1.0.0 现态）borderless Sliced 渲染满幅——按现基线锁定，若视觉再
   现异常按 SOFT 上报。"2×border > 最小边"的中段反转态（2026-09-03 经验）未入 PN6 断言面（残余范围外，
   见 §2.9.7）。
6. **夹具布局基数（run 3 终态）**：Game View 实测 **1105×977**（ScreenSpaceOverlay 无 CanvasScaler，
   canvas rect == 视口，半宽 552.5/半高 488.5）；pn_host@frac(0.36,-0.38) **280×184**、pn4@frac(0.40,-0.24)、
   pn6 条@frac(0.14,-0.42) pitch 115px，PN 内部点击点两两错开且全部界内（col3 子项右缘 533<552.5）。
   **run 1/2 教训（SH7/GR7 两轮回归）**：夹具交互点错开须按"夹具存在期"而非"用例执行期"校验——
   BuildHarness 构建序≠GuardCase 执行序，后建夹具从构建刻起即截胡先跑用例；大面板（540×370→400×264）
   在拥挤画布上手工找零冲突区不可靠，**缩尺寸（280×184）+ 右下断言区**是终解；空 stack/grid 子项
   （根无 Image）对 raycast 透明，pn6 面（raycastTarget=false）对一切 raycast 隐形——这两类重叠无害。

### 2.10 动态布局专项（UGUIFlexLayoutGroup）· Phase 3（2026-09-17 增补）

来源：用户 2026-09-17 罗列三条——「所有挂载动态布局脚本组件的控件，分别测试其：
(1) 上下左右布局是否生效 (2) inactive/active 其中一个子控件，观察另一个是否自动居中 (3) 间距调整是否正常」。

**挂载矩阵（definition + `BrickFactory.AttachDynamicLayoutGroups` 磁盘实测）**：

| brick | flex 宿主 | 挂载路径 | 固定/弹性子 | 方向参数 | 间距参数 | 子物体显隐参数 |
|---|---|---|---|---|---|---|
| ugui.button | 根 | 结构推断（activatable Icon + 直挂 Text Label） | Icon 固定（LE pref 20×20 默认）/ Label flexW=1 | `icon.position`（left/right/top/center；**无 bottom**） | `icon.margin`→flex.spacing（默认 8） | `icon.active` |
| ugui.toggle | 根 | 声明 `dynamicLayout`（spacing 3） | Background 固定 20×20 / Label flexW=1 | `checkbox.position`（left/right/top/center；**无 bottom**） | `checkbox.margin`→flex.spacing | 无 → 运行时 SetActive |
| ugui.input_field | 根 | 结构推断（label.active 使 hint Label 兼具 activatable+直挂 Text） | Label hug（prefW/H=-1、flex 0）/ Input Area 吃剩余（flexW/H=1） | `label.position`（**left/right/top/bottom 全支持**，走独立 `ApplyInputLabelPosition`） | `label.spacing`→flex.spacing（默认 8） | `label.active`（default false） |
| ugui.dropdown | Caption Area | 声明 `dynamicLayout`（parent:"Caption Area"，spacing 5） | Label flexW=1 / Arrow 固定 20×20 | 无参数面 → 组件驱动（verticalAxis/reverseOrder） | 无参数面 → 组件驱动（flex.spacing） | 无 → 运行时 SetActive |
| ugui.list_item | 根 | 结构推断（icon.active/value.active + 直挂 Text×2） | Icon 固定 / Label flexW=1 / Value flexW=1 | 无参数面 → 组件驱动 | `icon.margin`→flex.spacing（相邻子项间 uniform） | `icon.active` / `value.active` |

**范围外（不挂动态脚本，勿再排查）**：label / value_text（子物体无 SetActive 参数面 → 无 activatable 子）；
slider / progress_bar（Label 经包装层隔离 Text 直挂——2026-09-14 反锚点驱动设计，**有意不挂**）；icon / sprite（无 Text 子）；
stack / grid / scroll_view（原生 LayoutGroup，非动态脚本）。

**夹具与纪律**：
- 14 个 `fl_*` 夹具两排置于画布上带（frac y=0.44 / 0.36）；list_item 根 RT 直写 260×120（definition 无
  width/height 参数）；上下向变体夹具统一传 height 64 / 56（垂直块 = 固定子高 + margin + 文本行高，需容纳空间）。
- **全组夹具构建后统一置 raycastTarget=false**（pn6「对 raycast 隐形」同款纪律）：本组零合成点击，透明夹具不可能
  截胡任何既有用例的射线点 → 规避 BuildHarness 摆放连环坑（§2.9.8 E-6），画布拥挤区可安全落位。
- 夹具构建置于 LayoutWarnSink 注册之后；首例 F0 以「零 `[BrickFactory]` 告警」先行把关（此时列表仅含
  BuildHarness 段——既有夹具基线零告警，非零即新夹具管线漂移）。
- 空文本陷阱：Text 子物体空文本 → `ApplyLabelAutoInactive` 置 GO 失活退出布局——夹具一律非空文本
  （list_item 必传 `value.text`）。
- 运行时改造纪律：仅 SetActive / 组件属性写（Play 中 Destroy prefab 子物体非法，§2.9.8 D-6）；任何属性写后
  `yield return null` 一帧再读（GR6 同式）；用例结束恢复夹具默认态（组内后续用例依赖）。
- 「自动居中」判据 = 幸存子 worldRect 双轴中心 == 宿主中心（±0.5px）——弹性子独占时叠加「宽==宿主宽」断言，
  固定子幸存时由组 childAlignment(MiddleCenter) 居中。

| ID | 来源 | 用例 | 断言要点 |
|---|---|---|---|
| F0 | 〔纪律〕 | flex 夹具就绪 + 零工厂告警 | 14 实例全部存在；`_layoutWarns` 为空（BuildHarness 段全量） |
| B8 | 〔罗列1〕 | button `icon.position` 左/右/上 | 三变体参数驱动：left→Icon 贴左缘+gap==margin(12)+flex(verticalAxis=false,reverseOrder=false)+纵向共中线；right→reverseOrder=true+Label 贴左+Icon 贴右+gap==margin；top→verticalAxis=true+UpperCenter+Icon 顶格+垂直 gap==margin+Icon 水平居中 |
| B9 | 〔罗列2〕 | button Icon 运行时失活 → Label 自动居中 | SetActive(Icon,false)+1 帧→Label 双轴中心==根中心+宽==根宽（flexW=1 独占）；激活→gap==12 复现 |
| B10 | 〔罗列3〕 | button `icon.margin` 间距 | flex.spacing==12 读数 + 几何 gap==12（±0.5px） |
| B11 | 〔补充·SOFT〕 | button `icon.position:"bottom"` | 期望：Icon 移到 Label 下方——**`ApplyLayoutIconPosition` 无 bottom 分支（left/right/top/center），传 bottom 静默 no-op 保持横向左置**；库补齐 bottom 后自动转 PASS |
| T7 | 〔罗列1〕 | toggle `checkbox.position` 左/右/上 | 同 B8 三变体（固定子=Background）；left=声明默认态、right=reverseOrder、top=verticalAxis+UpperCenter+Background 顶格 |
| T8 | 〔罗列2〕 | toggle 子物体运行时失活 → 另一自动居中（双向） | Background 失活→Label 独占根（中心==根中心+宽==根宽）；Label 失活→Background 20×20 固定块被组居中（双轴中心==根中心）；恢复→gap==18 复现 |
| T9 | 〔罗列3〕 | toggle `checkbox.margin` 间距 | flex.spacing==18 读数 + 几何 gap==18（声明默认 3 仅作对照，不单测） |
| T10 | 〔补充·SOFT〕 | toggle `checkbox.position:"bottom"` | 同 B11 缺口（无 bottom 分支，no-op） |
| I11 | 〔罗列1〕 | input_field `label.position` 四向全测 | 四变体参数驱动：left→Label 贴左（hug 宽）+gap==22+Input Area 贴右；right→Label 贴右+Input Area 贴左；top→verticalAxis+UpperLeft+Label 顶格+垂直 gap；bottom→LowerLeft+Label 底格+垂直 gap（**唯一 bottom 全支持控件**） |
| I12 | 〔罗列2〕 | input_field hint Label 运行时失活 → Input Area 自动居中 | SetActive(Label,false)+1 帧→Input Area 独占根（中心==根中心+宽==根宽）；激活→gap==22 复现 |
| I13 | 〔罗列3〕 | input_field `label.spacing` 间距 | flex.spacing==22 读数 + 横向 gap（left 夹具）与垂直 gap（top 夹具）双==22 |
| D14 | 〔罗列1〕 | dropdown Caption Area flex 四向（组件驱动） | 无方向参数面→驱动 verticalAxis/reverseOrder：默认→Label 左/Arrow 贴 Caption 右缘+gap==声明 spacing 5；reverseOrder→Arrow 贴左+Label 贴右；verticalAxis→Label 上/Arrow 下（垂直 gap==spacing）；+reverseOrder→Arrow 上/Label 下；恢复默认态 |
| D15 | 〔罗列2〕 | dropdown caption 子物体运行时失活 → 另一自动居中（双向） | Arrow 失活→Label 独占 Caption（中心==Caption 中心+宽==Caption 宽）；Label 失活→Arrow 20×20 固定块居中；恢复 |
| D16 | 〔罗列3〕 | dropdown caption 间距（组件驱动） | 无参数面→flex.spacing=15 写入+1 帧→几何 gap==15；恢复声明默认 5 |
| LI7 | 〔罗列1〕 | list_item flex 四向（组件驱动） | 无方向参数面→驱动：默认→Icon 贴左+Icon<Label<Value 视觉序；reverseOrder→Icon 贴右+Value 贴左；verticalAxis→Icon 上/Label 中/Value 下（两垂直 gap==icon.margin 16）；+reverseOrder→Value 上/Icon 下；恢复 |
| LI8 | 〔罗列2〕 | list_item 子物体运行时失活 → 其余重排/末位自动居中 | Icon 失活→Label 贴左缘+Label↔Value gap==16（双 flexW=1 重排）；再 Value 失活→Label 独占整行（中心==根中心+宽==根宽）；全恢复→Icon 贴左+gap==16 原列复现 |
| LI9 | 〔罗列3〕 | list_item `icon.margin` 间距（相邻 uniform） | flex.spacing==16 读数 + Icon↔Label gap==16 且 Label↔Value gap==16（flex spacing 对全部相邻子项 uniform——多子控件特有断言）+ Label/Value **均分剩余空间 surplus**（双 flexW=1：各自 preferred 宽之上均分盈余、宽差==pref 差——2026-09-17 run1 实测 113.5/94.5；§2.9.8 B「平分剩余宽度」据此精化为 surplus 平分、非等宽）+ Value 贴右缘（span 满排） |

## 3. headless 不可测项三级处置（2026-09-12 拍板 A）

三级形态：**真排除**（无注入通道）/ **半测**（反射注入走真实过滤代码，标注 partial）/ **全测**（默认）。
半测与排除均**非门禁**（SKIP/SOFT），FAIL 才是门禁——每一条 FAIL 都必须是真回归。

| 条目 | 级别 | 原因（引擎源码实证 2026-09-12） | 重审条件 |
|---|---|---|---|
| 键盘事件队列派发层（Event.PopEvent → OnUpdateSelected → KeyPressed 管线） | excluded | 键盘字符来自引擎全局 Event 队列（InputField.cs L2036 `while (Event.PopEvent(...))`），队列由平台输入管线填充，C# 无公开注入 API；ExecuteEvents 只能合成 EventSystem 级事件 | 引擎/ugui 包升级时复查是否出现可注入通道 |
| IME 组字流（compositionString 显示/落字） | excluded | 读 `Input.compositionString`（L335-337），平台依赖，headless 无法产生组字 | 同上 |
| 真实硬件输入特性（多点触控/压力/硬件滚轮惯性） | excluded | 平台输入管线依赖 | 同上 |
| ContentType 过滤 / characterLimit 逐键截断 / caret 键入位移 | **partial（半测）** | `protected EditState KeyPressed(Event evt)`（L1821）可反射直调 + `Event.KeyboardEvent("5")` 合成按键——绕过队列但走真实 Append/Validate 路径；覆盖过滤逻辑层，不含派发层；反射缺失自动 SKIP | 引擎/ugui 包升级时核 KeyPressed 签名 |
| caret blink 相位 | excluded | CaretBlink() 协程按 unscaledTime 半周期翻转 alpha（L1194-1217）——随机帧采样是掷硬币，跨帧断言证明的是引擎行为且天然 flaky；brick 契约由 I1 结构断言承载 | 不重审（无契约价值） |
| preserveAspect 视觉比例 | SOFT 或 excluded | 图像 letterbox 缩放无可回读状态（CanvasRenderer 不暴露 quad 几何） | 引擎提供可回读 API 时升格 |
| Mask/RectMask2D 像素级裁剪视觉（scroll_view Viewport 遮罩后渲不渲染） | excluded | 组件状态探针对 stencil 渲染系统性失明（2026-09-07 实证：enabled/alpha/active 全正常也照常隐形；无裁剪结果回读通道） | 引擎提供可回读 API 时升格；契约由几何判据（子项 worldRect ⊆ 视口界 + 参数落点）承载，视觉留用户 Web 端 |
| visible 绑定开关（panel/scroll_view 的 bindingProps.visible） | excluded | bindingProps 由游戏侧绑定系统在运行时消费；Play harness 无 UIManager/绑定系统，无消费方可触发 | harness 接入绑定系统或转为消费方集成测试时升格 |

**手动验证建议**（Web 端真实 Play 测试点，排除项的兜底）：input_field 用真实键盘输入——整数字段拒收字母、
超 characterLimit 截断、中文输入法组字显示正常、caret 闪烁无异常常灭/常亮；此层回归由引擎版本锁
（Tuanjie 1.10 / uGUI 1.0.0）与手测共同承担。

## 4. 变更记录

- 2026-09-12：初版。用户罗列 6 条场景（L1/B1/D1-D3/I1-I2/T1-T2）+ 摸底补充（其余条目）+ 拍板 A
  三级处置，经 Controls 10 prefab 摸底（引擎 Dropdown 手动布局机制、模板激活态矩阵、Filled fillAmount
  驱动路径）成文。
- 2026-09-12（实现落地）：`Editor/UIBuildTools/TJBrickContractPlayTests.cs` 实现全部用例并实跑全绿，
  基线 **48 passed / 2 soft / 0 skipped / 0 failed**（判据：日志无 `BRICK_CONTRACT_PLAY_TEST_FAILED`）。
  实现过程关键事实：①**引擎发现（SOFT I7/I8）**：本引擎 DeactivateInputField 缺 standard uGUI 的
  `m_CaretVisible=false` 重置——deselect 后 caret quad 持续绘制（活体实证 1500+ 帧 alpha=1.00）；期望契约
  记 SOFT，引擎补行后自动转 PASS。②B6 契约修正：ButtonScaleEffect 模板烘焙为禁用、SetScaleEffect 参数隐式
  启用——opt-in 契约（默认禁用断言 + icon_scale 实例 clickScale tween 实测）。③I2 容差 0.55px
  （DefaultControls input 文本 6/7px 上下内边距的 0.5px 有意非对称）。④caret 不可见断言用三态谓词
  （销毁/失活/alpha→0）。⑤执行协议见 §1 帧驱动：Error Pause 是帧冻结真因（三层 unpause 防御）；
  safePoint 必须从画布世界矩形推算（裸局部坐标导致 raycast 落空、blocker 关闭全 no-op）；列表销毁后
  捕获的 Transform 引用 fake-null，判定须在销毁前捕获布尔。⑥引擎 Dropdown 销毁时间线实测：
  item 点击/Hide 后 ≈0.17s（alphaFadeSpeed 0.15s + 1 帧）。
- 2026-09-12（重命名）：`Play交互契约测试项目.md` → `PlayContractTestCases.md`——对齐子仓英文 PascalCase
  文档命名风格（DeployedTools.md / PanelRegistryBuilder.md / ReleaseNote.md），随契约测试子功能文件夹
  （`Editor/UIBuildTools/BrickContractTests/`）统一管理；内容不变，.meta 同步改名（GUID 保持）。
- 2026-09-14（Phase 2 扩围起草）：Container/Layout 7 brick 用例起草（§2.9：PN/LI/GR/SV/SH/SC/MD，
  编号改两字母前缀避 slider 冲突）。来源三部分：用户 2026-09-14 三条挂载要求（尽量默认值实例 + 按有效
  组合矩阵混挂全部子类、完整显示不遮挡；以 allowedChildren 双向 AND 为准、未声明不强行测；滚动条功能）、
  2026-09-11 已背书三目标（scroll_view AutoHide/内缩/contentLayout 三模式、modal overrideSorting、
  stack/grid preferred 传播）、顾问补充（挂载点落点断言+零告警门、动态子项集重排、事件路由冒烟、modal
  三槽、截断例外精确化至"启用的滚动方向"、布局参数变体几何）。**勘误**：grid allowedChildren 实为
  13 项不含 scroll_view（磁盘为准；交接文档 §5.2 待同步修正）。**状态：仅文档，未实现**——摸底三件套
  （§2.9.0）先行后按四步协议实现。
- 2026-09-14（Phase 2 声明面纳入 + Layout 5 摸底 + sortingOrder gate 修复）：①`TJBrickContractTests`
  扫描数组加入 `Container/Definitions`——panel 6 项 / list_item 37 项首轮全 PASS，契约基线
  **793 passed / 7 soft / 0 skipped / 0 failed**（零 FAIL、既有 15 brick 数字零扰动）；交接文档预警的
  icon.margin flex 分支 / SetFlexTextProp 伴随注入 / value.text 伴随均被现有机制自然覆盖（enablers
  SetActive 全注入 + list_item Label 默认文本 'Item' 非空使 pin 存活）。②Layout 5 brick（GR/SV/SH/SC/MD）
  场景态摸底矩阵沉淀进 **§2.9.8**（基线数=断言锚；§2.9.0 沉淀指向同步更正为本文件）。③摸底发现
  **引擎 gate**：嵌套 canvas overrideSorting=false 时 sortingOrder setter 静默丢弃写入 → 嵌套 modal
  的 sortingOrder 参数管线缺口（PostProcess 开 flag 晚于参数应用）——已修
  `BrickFactory.CreateWithParams`（  ApplyParameters 前预设 overrideSorting，与 PostProcess 语义一致仅
  提前）；修复后装配 127/0 + 契约 793/7/0 复跑零扰动，嵌套 modal sortingOrder 参数实测落值。
- 2026-09-14（Phase 2 实现落地）：`TJBrickContractPlayTests.cs` 实现 **Layout 5 组全部用例**
  （GR1-7 / SV1-7 / SH1-7（SV/SH 同一参数化 case 体按轴向注册两遍，杜绝镜像漂移）/ SC1-10 / MD1-7 组序
  纪律；PN/LI 按拍板不实现，§2.9.1/§2.9.2 保持"已起草、未实现"），四步协议第 5 轮实跑全绿——新基线
  **85 passed / 2 soft / 0 skipped / 0 failed**（2 soft 仍为 I7/I8 caret 引擎缺陷；48→85 新增 37 例）。
  五步全门禁链终态：装配 **127/0**、契约 **793/7/0**、Play **85/2/0**、UguiPrefabs **14/14**、gen 零漂移。
- 2026-09-15（modal 迁移 Container/assemblyOnly）：`ugui.modal` definition 迁至
  `UGUI/Container/Definitions/` 并标 `assemblyOnly:true`（layer→primitive、category→container），
  `ModalShell.prefab` 删除；`BrickFactory.ApplyControlDefaults` 新增 modal case（根 stretch /
  renderMode ScreenSpaceOverlay / CanvasScaler ScaleWithScreenSize 1920×1080 / 根与 ModalPanel
  UISprite Sliced——装配态与旧模板逐项等价，探针实证）+ CreateInternal assemblyOnly 分支在应用
  definition 默认前预升 overrideSorting（嵌套 definition 默认 sortingOrder=100 落值）。
  `TJBrickAssemblyTests` T14e-h 断言从模板资产改为装配实例；契约与 Play 测试**零功能改动**复跑——
  门禁链全绿：装配 **127/0**、契约 **793/7/0**（modal 现扫描为 Container 13/0/0/0）、Play **85/2/0**、
  UguiPrefabs **14/14**、gen 零漂移。游戏侧重建：风之小镇 ConfirmModal、合金弹头 UI/Bricks/ugui.modal
  （两处悬空源 GUID 引用清零）。
  实现过程实测修正沉淀于 **§2.9.8 D**（GLG childAlignment 生效性、交叉轴 max preferred 语义、panel
  prefH=10、引擎 AutoHide 自动激活滚动条、ScrollRect 拖动物理方向 + beginDrag 门控、Play 中 Destroy
  prefab 子物体限制、handle 直拖环境敏感、夹具摆放/中心点击纪律），SC 用例表格断言要点已按实测同步
  （SC2 总高 146 / SC3 行宽随实测视口 / SC6 handle 双向同步 / SC7 分离断言 / SC5、SC10 向上拖）。
- 2026-09-15（PN 组实现落地）：按用户拍板实现 ugui.panel 的 Play 交互契约 **4 例**（PN2 并入 PN1、
  PN4 升级双臂行为契约、PN6 新增底图 mesh sanity、panel-in-panel 深层点击链不单列——PN1 落点+PN3
  路由已覆盖本质；PN5 visible 维持排除）——`TJBrickContractPlayTests.cs` 增 BuildHarness PN 夹具块 +
  ArmSequence PN 注册（SC 后 MD 前）+ 四个 Body。四步协议 run 3 实跑全绿——新基线
  **89 passed / 2 soft / 0 skipped / 0 failed**（85→89；2 soft 仍为 I7/I8 caret 引擎缺陷）。
  ①**摸底先行（Play 态探针，§2.9.8 E）**：panel 装配默认 = UISprite(b10) Sliced 白 160×30 +
  raycastTarget=false（blanket 回退补图——"null-sprite 白板"态不存在）；modal 子项 = stretch 填宿主 +
  overlay raycastTarget=true（PN1 断言后失活）；引擎 CanvasRenderer 无 vertexCount → 判据通道
  GetMesh().vertexCount；五态顶点基线 36/36/32/4/4（borderless Sliced 塌缩单 quad 全幅 UV 正常渲染——
  2026-09-04 "Sliced 无 border 空渲染"观察在当前引擎不复现，历史注记留档）。
  ②**run 1/2 两轮夹具摆放回归**（SH7 btn/slider、GR7 gm_slider）：BuildHarness 一次性建全部夹具、
  构建序≠GuardCase 执行序——后建夹具从构建刻起截胡先跑用例的点击；540×370→400×264 两次摆放连踩，
  终解 = 缩 280×184 + 右下断言区（细节与"空 stack/grid 根无 Image 即 raycast 透明"结论见 §2.9.8 E-6）。
  ③**PN4 harness 修正（run 1 FAIL）**：MD 夹具 overlay canvas（600/100）自 BuildHarness 起恒居
  RaycastAll 头/尾——裸首命中断言必须过滤 canvas 序（harness=500）；`RaycastHandlerAt` 跳过无
  handler 命中的语义限制使拦截臂只可断言首命中变化（穿透臂 ClickAt 计数不受影响）。
  门禁说明：本次仅改测试代码与用例文档（未动 definition/BrickFactory/compositionPolicy），五步链其余
  门不触发；Play 门（日志无 `BRICK_CONTRACT_PLAY_TEST_FAILED`）run 3 实跑通过、退出后 Console 零残留。
- 2026-09-16（dropdown 展开列表自动适配）：`BrickFactory` 后处理新增行自动派生——参数只改 dropdown 本身
  （height/fontSize）而未显式传行参数（itemHeight/itemFontSize）时：行高=max(height−4, 20)（略小于 box，
  DefaultControls 20px 行下限防短 box 裁字）、行字号=fontSize；行宽无需代码（行 (0,0.5)-(1,0.5) stretch +
  Template 横向 stretch + AutoHideAndExpandViewport 视口回满宽，天然==box 宽，D1 同机制）。实现三件套：
  ①`_appliedParams` 字段（ApplyParameters 顶部记录原始参数字典、ResetCaches 清空）——PostProcess 判"显式
  传入与否"的真源；②`HandleSetSizeDelta` Item/y 特例同步 Content=行高+8——引擎 Show() 的堆叠边距由模板
  Content/Item 几何推算（offsetMin/offsetMax+localPosition），28px DefaultControls Content 里放大行使两侧
  边距转负、行上下各越界 (行高−28)/2 被裁；同步后任意行高保持 4px 列表内边距，显式 itemHeight 同样受益
  （风之物语 drp_quality itemHeight 56 的隐性上下裁切随面板重建即修复）；③Item/y 特例在 templateHeight
  缺省时把模板窗抬到 max(现值, 3×行高+12)——引擎 extraSpace 把展开列表收缩回内容高、抬窗零成本，显式
  templateHeight 恒优先。新增 D11/D12/D13（只调 box→行 60/字 24/行宽==box/列表 188/滚动条隐藏/caption 24；
  显式行参数 40/16 优先且模板窗不抬升（3×40+12=132<150）；无参数负对照 Template 150/Content 28/行 20/字 14
  原值）；T4 detail 增只读 RaycastAll 命中清单（"未命中"型失败否则事后不可诊断）。四步协议实跑全绿——
  新基线 **92 passed / 2 soft / 0 skipped / 0 failed**（89→92；2 soft 仍为 I7/I8 caret 引擎缺陷）。
  门禁链：gen 零漂移、装配 **133/0**、契约 **873/7/0**（7 soft 均为 wrapAround 引擎缺陷）、UguiPrefabs
  True（风之物语 6 tree prefabs / 0 warnings）。**测试环境注记**：本轮 Free Aspect 画布随用户 Web 端窗口
  变化（1284×934→1965×941），第 1/2 轮 T4/I3 出现"射线点击未命中"型偶发失败、干净生命周期重跑通过，
  且与本改动无关（D 组新夹具距失败点位 700px+、工厂改动仅触 dropdown 路径、三轮 D11-D13 恒绿）；
  T4/I3 对宽画布/活体会话的敏感性属既有 harness 脆弱性，留档观察。
- 2026-09-17（动态布局专项 Phase 3）：按用户三条罗列（方向 / 失活居中 / 间距）对**全部挂载 UGUIFlexLayoutGroup
  的 5 控件**（button / toggle / input_field / dropdown / list_item）成套补测 §2.10——18 例（F0 门禁 + B8-B11 +
  T7-T10 + I11-I13 + D14-D16 + LI7-LI9）。范围外判定：slider/progress_bar 因 Label 包装层有意不挂 flex、
  label/value_text 无 activatable 子、icon/sprite 无 Text 子。实现：`TJBrickContractPlayTests.cs` 增 BuildHarness
  fl_* 夹具块（置于 LayoutWarnSink 注册之后）+ ArmSequence flex 组注册（D 组后、GR 组前）+ 18 个 Body。夹具纪律：
  14 夹具全组构建后统一 raycastTarget=false（pn6 隐形同款——本组零合成点击，摆放免疫 §2.9.8 E-6 连环坑）+ 全部非空文本
  （ApplyLabelAutoInactive 空文本退出布局）+ 用例尾恢复默认态；dropdown/list_item 无方向/间距参数面 → 按既有 API 驱动
  先例（SC6 handle-sync）直驱 verticalAxis/reverseOrder/spacing。四步协议 run 2 全绿——新基线
  **108 passed / 4 soft / 0 skipped / 0 failed**（92/2→；新增 16 PASS + 2 SOFT）。B11/T10 为 **SOFT**：
  `ApplyLayoutIconPosition` 无 bottom 分支——button/toggle 的 position 参数面只到 left/right/top/center，传 bottom
  静默 no-op，库补齐后自动转 PASS；input_field `label.position` 走独立 `ApplyInputLabelPosition` 四向全支持。
  **run 1 断言修正沉淀（7 FAIL 全为新用例断言公式错，零管线缺陷；既有 94 例两轮均零扰动）**：
  ①垂直 gap 公式曾写 `上.yMax − 下.yMin`——量到的是整块跨距（52=24+12+16、54=20+18+16、56=块高），数值恰好证明
  纵向布局正确、块内相邻成立；正解 **`上.yMin − 下.yMax`**（B8/T7/I11 两臂/I13/D14 两臂/LI7 共 7 处）。
  ②LI9 flex 分配实测（113.5/94.5，宽差 19==两文本 pref 差）：引擎 flexible 分配为「各自 preferred 之上**均分剩余
  空间 surplus**」，非等宽平分——断言改 equal-surplus + Value 贴右 span，§2.9.8 B「平分剩余宽度」据此精化。
  门禁说明：本次仅改测试代码与用例文档（未动 definition/BrickFactory/compositionPolicy），其余四门不触发；
  Play 门 run 2 实跑通过（1090 帧、零冻结）。
