---
name: ui-doc-builder
description: UI 文档构建模块 — 从 UI需求.md 生成 UI设计文档.md。负责 UI 输入采集（Step 0 intake/normalize）、UI风格处理、UI设计文档产出。核心必需输入是 UI需求.md，创意方向.md 为可选增强输入。UI需求.md 信息不全时允许先生成带占位章节的 UI设计文档.md。使用此模块当：需要生成 UI设计文档.md、需要从 UI需求.md 构建 UI 设计规格、需要补全 UI需求.md。
---

# UI Doc Builder

从 `UI需求.md` 生成 `UI设计文档.md` 的核心模块。是 `unity-ui-builder` extension 的文档入口。

## 执行模式（硬约束）

本模块**由主 agent 直接执行，禁止派 subagent**（含 Step 0 与 Step 1-4 全部步骤）。本模块不适用主 SKILL.md「⛔ 阶段派发守卫」的 subagent 派发要求。

## 输入与输出

### 核心必需输入
- `context/games/{GameName}/UI需求.md`
  > 路径固定。是正式中间输入文档：由 Step 0 生成或补全后保留在 context 中，不删除。后续 UI 方向变化时优先更新 `UI需求.md`，再由 `UI需求.md` 重生成 `UI设计文档.md`。

### 可选增强输入
- `context/games/{GameName}/创意方向.md`
- 用户上传参考图或 AI 生成参考图（经 `ui-style-reference` 产出 `UI参考图分析.md`）
- 用户在 LayoutIntentBoard 中确认的布局摘要（经 `ui-layout-intent-designer` 产出 `UI布局意图.md`）

### 产出
- `context/games/{GameName}/UI设计文档.md`
- `context/games/{GameName}/image_requirements.json`

> `image_requirements.json` 由本模块 Step 3 生成。它是 UI 图片需求的权威清单，供 `ui-image-generator` 消费（Stage 01-03）和 `compile_job_plan.py` 编译时派生 `ui_image` 隐式 Job。

## 允许/禁止依赖

### 允许依赖
- `cn.tuanjie.create.ui`
- `unity-ui-builder` 内部模块的产出

### 禁止依赖
- `unity-game-planner` 内部规则文件
- 外部编排器流程前提
- `csharp-code-writer` / `unity-operator` 规则文件

---

## 工作流程

### Step 0：Intake / Normalize — 生成或补全 `UI需求.md`

#### 0.1 场景判定

采用**调用方显式标记优先，文档信号兜底**：

| 模式 | 判据 | Step 0 行为 |
|------|------|------------|
| **全新构建** | 项目中无 UXML/USS/UI Panel 脚本/UIDocument（通过仓库探查确认） | 完整信息采集：UI 模式 → 视觉输入方式 → 界面清单 → UI 风格 → 设计分辨率 → 落盘 UI需求.md |
| **增量构建** | 项目中已有 UI 资产，或已有 UI需求.md | 探查现有 UI 资产 → 仅对新增/修改面板采集缺失信息 → 追加到 UI需求.md |

#### 0.2 仓库探查边界

只允许探查 **UI 直接相关资产**：
1. 现有 UI 文件（UXML / USS / UI Prefab / Canvas Prefab）
2. 场景中的 UIDocument / UIRoot / Canvas / PanelSettings
3. 与 UI 状态 / 绑定直接相关的脚本（仅命名明显相关者）
4. 已存在的 `GameName` / 分辨率 / 入口场景信号 / UI 模式信号

禁止扩张为全项目规划扫描。

#### 0.3 信息采集与补全

> 若 `UI需求.md` 已存在且必填字段齐全**且用户请求的面板已涵盖在面板清单中**，**必须跳过 Step 0**，直接读取新增的增强输入（如 `创意方向.md`）进入 Step 1/2。若 UI需求.md 新增了绑定字段 / 服务接口 / 挂载要求，仅做补强，不回退为重新 intake。若用户请求的新面板不在面板清单中，不可跳过 Step 0。

1. 读取 `创意方向.md`（如存在），提取 `GameName`、游戏类型、美术方向等非 UI 基础上下文
2. 从 `UI需求.md`（若已存在）读取绑定字段、服务接口、挂载要求、事件名等技术字段；UI需求.md 信息不全时，绑定字段/服务接口/挂载要求等字段留占位说明，待 UI需求.md 更新后由 Step 1-2 补强
3. 读取 `UI参考图分析.md`（如存在），提取面板清单、布局参数、图生图配置；参考图（用户上传或 AI 生成）经 ui-style-reference 处理后含 OpenCV 组件树字段时，同步保留节点 ID、父组件 / 父 UXML 元素、深度、渲染模式、shell 路径、相对父级 X/Y/W/H% 和仅诊断用根 X/Y/W/H%；`text_type=label` 的文字标签节点还需同步保留 `font_color`、`font_size`、`font_weight`、`font_style` 字段，写入 §11 元素列表的 `字体颜色`/`字体大小`/`字体粗细` 列
4. 执行 0.2 仓库探查，用 `ask_user` 确认所有必填字段（详见 Step 0.4 询问策略）；新面板须额外确认视觉输入方式、界面元素等
4a. **采集 UI 模式**：确认 `ui_mode` 字段（`ugui` 默认 / `ui_toolkit` 可选）。该字段写入 `UI需求.md` 基本信息，全程不可更改

5. 从以上信息**自动整理** `UI需求.md` 的全部必填字段和可填的可选字段；需要哪些 UI 界面、UI 风格、视觉输入方式等 UI 维度由本步骤自行归纳，不依赖上游固定字段
6. 所有必填字段均须通过 `ask_user` 逐一询问用户确认（详见 Step 0.4 询问策略），已从文档推导出的字段也需询问以确保用户知情
7. 落盘 `UI需求.md`（新面板信息追加到已有内容）

#### 0.4 询问策略

- 必须全问；且按照顺序一个个来问，不能一起询问
- `ask_user` 优先用 `type: "choice"`，给 2-4 个选项
- 可根据项目信息适当调整每个问题询问内容

1. **UI 模式**（第一个问）
   - 用 choice 让用户选择：
     - 「UGUI（推荐）」→ `ui_mode: "ugui"`，使用 Canvas + Image/Text/Button 等 UGUI 组件，面板继承 `UGUIPanelBase`
     - 「UI Toolkit」→ `ui_mode: "ui_toolkit"`，使用 UXML + USS + UIDocument，面板继承 `UIPanelBase`
   - 默认推荐 UGUI（选择「UGUI（推荐）」时直接确认，不需额外说明）
   - 产出写回 `UI需求.md` 的「UI 模式」字段
   - 此选择全程不可更改，后续所有步骤（图片生成、面板构建、C# 脚本）均按此模式分流

2. **视觉输入方式**
   - 用 choice 让用户选择：
     - 「AI 直接规划」
     - 「上传参考图」
     - 「AI 效果图定稿」
     - 「画板手动设计」
   - 这一步不是被动记录，而是**主动路由信号**：
     - 选「AI 直接规划」→ 写回 `UI需求.md` （0.5 落盘完成）后，交还调用方
     - 选「上传参考图」→ 写回 `UI需求.md` （0.5 落盘完成）后，由当前调用方立即激活 `ui-style-reference`，禁止用`ask_user` 询问参考图，由 `ui-style-reference` 负责后续采集
     - 选「AI 效果图定稿」→ 写回 `UI需求.md`（0.5 落盘完成） 后，由当前调用方立即激活 `ui-mockup-generator`（生成参考图 → 用户确认 → 移交 ui-style-reference）
     - 选「画板手动设计」→ 写回 `UI需求.md` 后，立即激活 `ui-layout-intent-designer`；在同一多画布项目中逐界面设计并整体确认，生成 `UI布局意图.md` 后回到本模块 Step 1/2
   - ⚠️ **分支激活统一发生在 0.5 落盘之后**：以上路由描述的是最终去向；选择后仍须依次完成第 3-5 问（界面清单、UI 风格偏好、设计分辨率）并执行 0.5 落盘，之后才激活对应分支
   - 不在本步骤直接索要图片文件；若走参考图路径，由 `ui-style-reference` 负责后续采集
   - **2.1 参考图布局校正**（条件询问，默认关闭）
     - 仅当视觉输入方式选择「上传参考图」或「AI 效果图定稿」时询问；其他视觉输入方式不得显示此问题
     - 用 choice 提供：
       - 「关闭（默认）」→ `参考图布局校正: 关闭`
       - 「开启」→ `参考图布局校正: 开启`
     - 用户未主动选择开启、旧 `UI需求.md` 缺少该字段、或调用方无法确定值时，一律按关闭处理
     - 本问题只在 Step 0.4 询问一次；`ui-style-reference` 完成后禁止再次询问
     - 将「视觉输入方式」和「参考图布局校正」同时写回 `UI需求.md`，作为会话恢复后的权威路由信号
     - **分支完成后的回流规则**：`ui-style-reference` 全流程（含校正开启时的 Stage 04.5）完成后，无论校正开关状态，都直接回到本模块 Step 1/2。校正开关的决策已在 Step 0.4 落盘、由 `ui-style-reference` 在 Stage 04 完成时读取并路由；**本模块禁止在 `ui-style-reference` 完成后再次激活布局 skill**。`UI参考图分析.md` 中的标准布局表（含校正开启时的最终布局几何与 `layout_resolution` 审计元数据）即为最终权威，不再引用任何后置布局校正区块。

3. **界面清单**
   - 先给两种选择：
     - 「推荐方案」→ AI 按游戏类型推荐界面清单
     - 「我自己选」→ 用 choice 的"其他/自定义"输入框收集
   - 产出写回 `UI需求.md` 的「面板清单」

4. **UI 风格偏好**
   - 先给两种选择：
     - 「推荐风格」→ AI 按游戏类型和美术方向补齐
     - 「我自己描述」→ 用 choice 的"其他/自定义"输入框收集
   - 若用户信息不足，AI 按已确认的界面逐个补齐风格与布局描述
   - 视觉输入方式选「上传参考图」时，询问时应说明：颜色、质感、风格以参考图为准，此处描述仅作为文生图回退风格参数（§13），不会覆盖参考图
   - 产出写回 `UI需求.md` 的「UI 风格偏好」

5. **设计分辨率**
   - 询问用户：
     - 「横屏」→ `1920×1080`
     - 「竖屏」→ `1080×1920`
     - 「我不懂，你帮我定」→ 按游戏类型推荐
     - 「自定义分辨率」
   - 允许用"其他/自定义"输入框填写自定义分辨率
   - 视觉输入方式选「上传参考图」时，可结合参考图朝向交叉验证（如参考图为竖屏时确认是否 `1080×1920`）
   - 产出写回 `UI需求.md` 的「设计分辨率」

#### 0.5 落盘

将整理好的 `UI需求.md` 写入 `context/games/{GameName}/UI需求.md`（若目录不存在先创建）。格式参照 `references/UI需求模板.md`。

> `UI需求.md` 生成后保留在 context 中，不删除。后续 UI 方向、UI需求.md 或独立项目需求变化时，优先更新 `UI需求.md`，再由 `UI需求.md` 重新生成 `UI设计文档.md`。

---

### Step 1：读取 UI需求.md + 可选增强输入

> ⚠️ **UI 风格数据源优先级（硬约束）**：`UI需求.md` 的「UI 风格偏好」章节（美术风格、配色方案、按钮风格、图标风格、UI 风格关键词）是 `style_baseline` 和 `panel_type_styles` 的**唯一数据源**。**禁止从 `创意方向.md` 补充 UI 风格维度**——`UI需求.md` 本身已由 Step 0 从创意方向+UI需求.md 提炼完成，到 Step 1-3 时是 UI 侧的唯一权威。`创意方向.md` 仅用于补全 `GameName` 等非风格元信息。例如 `UI需求.md` 写「卡通可爱」时，`style_baseline` 必须包含 `cute`/`adorable` 等表达"可爱"的视觉关键词，不得用 UI需求.md 的「赛璐璐动画风」替代或遗漏。

1. 读取 `context/games/{GameName}/UI需求.md`
2. 读取可选增强输入（如存在）：
   - `创意方向.md` → 补全 `GameName` 等非风格元信息（⚠️ 不得用于 UI 风格）
   - `UI参考图分析.md` → 补充图生图配置、布局参数、组件裁剪映射
   - `UI布局意图.md` → 补充用户确认的组件树、几何百分比、组件类型、绑定和导航意图
3. **UIBrick 资产盘点（延后到 Step 2 §2 起草时执行）**：本步骤**不再预读 BrickIndex 全文**——每面板的 Brick 覆盖评估统一由 Step 2 §2 起草时的 Coverage 一行调用机械完成（见 Step 2 第 2 条）。仅当 Coverage 工具 FAIL 或输出需人工裁决时，才按下方物理路径说明回退读索引：UGUI 模式读 `.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/BrickIndex.ugui.json`（`ugui.*` brickId），UI Toolkit 模式读 `.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/BrickIndex.json`（`primitive.*`/`layout.*` brickId）

   > ⚠️ **Brick 物理路径（硬约束，读文件前必读）**：`cn.tuanjie.create.ui` 通过 `Packages/manifest.json` 的 `file:../` 本地引用接入，**文件系统里 `Packages/` 目录下没有这个包**——用 `read_file`/`glob` 直接读 `Packages/cn.tuanjie.create.ui/...` 必然失败，且 glob 默认跳过 `.codely-cli` 隐藏目录，盲目全项目搜索只会浪费轮次。物理路径固定为 `.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Runtime/UIBrick/`；若该路径不存在，读 `Packages/manifest.json` 解析 `cn.tuanjie.create.ui` 的 `file:` 引用得到真实目录，禁止全项目盲搜。⚠️ C# 脚本中的 `AssetDatabase.LoadAssetAtPath` 仍使用 Unity 虚拟路径 `Packages/cn.tuanjie.create.ui/...`，两者不可混用（详见 `modules/ui-panel-builder/references/brick-mapping.md`）
   > ⚠️ **UIBrick 随 `cn.tuanjie.create.ui` 包内置，必然存在，无需检查**。完整路径（`{pkg}` = 上述物理路径）：
   > - UI Toolkit Brick：`{pkg}/Runtime/UIBrick/`（USS/UXML/Definitions 三件套，索引 `BrickIndex.json`）
   > - UGUI Brick：`{pkg}/Runtime/UIBrick/UGUI/`（Definitions + Prefabs，索引 `BrickIndex.ugui.json`；速查卡 `BrickCheatsheet.ugui.md` 在 `{pkg}/Runtime/UIBrick/` 下）
   >
   > §2 UIBrick 实现候选表禁止写"未安装/无 Brick"——UIBrick 随包内置必然存在。覆盖率为 `full`/`partial`/`none`，如实评估当前 Brick 库对面板的覆盖情况。误写"未安装"会导致下游 panel-builder 手写所有组件、跳过 `br-toggle-base--switch` 等 variant，引发 toggle 不滑动等 bug。
   > ⚠️ **UGUI 模式 §2 brickId 必须使用 `ugui.*` 前缀**（如 `ugui.button` 而非 `primitive.input.button_base`）。UGUI Brick 使用独立的索引和定义体系。
   > ⚠️ **UGUI 模式 §2 应同时列出交互控件和 Layout/Container 级 Brick**。UGUI Brick 按 `category` 字段分类（如 layout、container、input、text、image 等）。评估面板覆盖时不仅看交互控件（category=input/text/image 等）是否有对应 Brick，还应规划布局结构使用哪些 layout/container 类 Brick。读取 `BrickCheatsheet.ugui.md` 的 `compositionPolicy`（allowedChildren/canNestUnder）获取合法嵌套关系，作为布局规划依据

### Step 2：生成 UI设计文档.md

> ## 🚦 起草优先（硬约束，违反必超时）
>
> 读完输入后下一个动作**必须是 `Copy-Item` 模板到目标路径**（得到骨架），然后逐章 `replace` 填充。禁止脑中预演全文再 `write_file`——连续多轮纯 thinking 不发工具调用会导致流式超时、零产出。
>
> 1. **骨架来自 copy**：`Copy-Item` 模板 → 立即 `read_file` 一次（replace 工具要求编辑前有读取记录）→ 逐章 `replace`
> 2. **逐章顺序**：§1 框架评估 → **§3-§11 结构功能 → §2 UIBrick 候选**（§2 延后到 §11 之后：其覆盖表由 `TJBrickMappingDraft.Coverage` 工具从 §4/§11 机械产出，§11 未就绪时工具无输入）→ §12-§14 图生图（如适用）→ §15 资产清单 → §16 挂载
> 3. **收尾**：运行部署型工具一行调用 `Create.GameFramework.TJDocChecks.CheckDocPlaceholders("{GameName}");`（包 `Editor/UIBuildTools`，经 `execute_csharp_script`），确认 `[DOC_PLACEHOLDER_CHECK] PASS`（残留占位符 + 章节完整性 + UI 模式条件化检查）
> 4. **限制 replace 批次**：每轮 thinking 最多规划并执行 **2-3 章**的 replace，执行完立即进入下一轮；禁止单轮 thinking 跨多章预演完整内容后再批量 replace

读取 `references/UI设计文档模板.md` 作为骨架，按以下顺序填充：

1. **§1 框架适用性评估**：根据 UI需求.md 的面板清单和交互需求评估
2. **§2 UIBrick 实现候选**：**任意时点（含首次起草）优先运行部署型工具一行调用 `Create.GameFramework.TJBrickMappingDraft.Coverage("{GameName}");`（包 `Editor/UIBuildTools`）直接产出 §2 覆盖表草稿——免通读 BrickIndex 全文**。工具从 §4 面板清单 + §11 元素清单机械生成每面板的 Brick 覆盖（full/partial/none）与候选清单（前置条件：§4/§11 已就绪，故本章在 §11 之后起草）。审核工具输出的「人工复核」行（多候选/无候选）并补充 Layout/Container 级 Brick；仅当工具 FAIL 或输出不足以裁决时，才回退读 `BrickIndex.ugui.json`（UGUI）/`BrickIndex.json`（UI Toolkit）人工评估。UGUI 模式下应同时列出交互控件和 Layout/Container 级 Brick，标注每个面板使用了哪些 layout/container 类 Brick 组织布局结构
2. **§3-§11 结构与功能**：从 UI需求.md 的面板清单、面板用途、导航骨架、基础状态映射、绑定字段、服务接口、挂载要求、事件名等字段填充
   - `UI布局意图.md` 存在时，UXML/UGUI 层级和几何以其中记录的用户画板结果为准；参考图只补充风格和图片来源，AI 只补充未明确字段
   - 当画布 `布局来源=reference_analysis` 时，通过「来源参考节点 ID + 来源参考面板」关联 `UI参考图分析.md` 的裁剪、文字、风格与生成配置；确认后的画板区域集合是布局权威，参考图中未出现在画板结果里的节点视为用户删除，不得重新补回；来源字段为空的区域视为用户新增
   - ⚠️ **§11 元素列表 `元素类型` 列**：UI Toolkit 模式映射到 UXML 标签（`<ui:Button>`, `<ui:Label>` 等），元素 name = UXML `name` 属性值。UGUI 模式映射到 UGUI 组件（`Button`, `Text`, `Image` 等），元素 name = GameObject name（同时用于 SafeFind 路径，面板内必须唯一）
   - ⚠️ **§11 元素列表 `text_type` 列**（无参考图模式下必填）：每个元素必须标注 `text_type`——`art_text`（文字已烘焙进图片，如艺术字标题）、`label`（纯文字标签）、`none`（无文字，如图标按钮）。参考图路线从 §12.2 的 `text_type` 列读取；AI 直接规划路线由 ui-doc-builder 根据图片是否含文字自行标注。标注 `art_text` 的元素在 §15 资产清单中对应的图片 `text_type` 字段也必须为 `art_text`
   - ⚠️ **§15 资产清单按 UI 模式条件化**：UI Toolkit 模式列出 UXML、USS、PanelSettings；UGUI 模式列出 Panel Prefab（每面板一个），不列 UXML/USS/PanelSettings
   - ⚠️ **§16 场景挂载按 UI 模式条件化**：UI Toolkit 模式描述 UIDocument + PanelSettings + UIPanelRouter + `UIFrame.Initialize()`；UGUI 模式描述 Canvas + CanvasScaler + GraphicRaycaster + `UIFrame.InitializeUGUI()`
4. **§12-§14 图生图配置**（条件章节）：仅当 `UI参考图分析.md` 存在时填写，从其吸收图生图配置、裁剪映射表、文生图回退风格参数（⚠️ `quality=contaminated`（leaf）不需要文生图回退风格参数——图生图以透明 PNG 为输入，风格由透明 PNG 决定）。§12.2 组件裁剪映射表中原样传递 `functional_type` 列（来自 ui-style-reference 语义分类），供下游 ui-image-generator 独立生成功能控件子部件、ui-panel-builder 映射到交互 Brick。⚠️ `functional_type` 是路径判定的**最高优先级**——当 `functional_type` 非 `—` 时，不论 `source` 是什么（即使 `direct_use`），都必须走 Path C/D 拆解子部件，禁止直接复制整图
5. **§15 UI 资产清单**：列出所有需要 AI 生成的 UI 图片。⚠️ **Slider 子部件强制清单**：每个 `functional_type: slider` 的控件**必须**包含 3 个子部件——`track`（固定背景条）、`fill`（随 value 缩放的填充条）、`handle`（旋钮）。缺 `fill` 图会导致滑块无进度反馈，用户看不出当前值在多少。⚠️ **fill 不可降级为 `uss_only`**：`functional_type` 非空时 `source` 被前置转换强制设为 `ai_generate`（前置转换规格见 `../ui-image-generator/shared/adjudication-tables.md` 路径判定表），`uss_only` 不适用于功能控件子部件。fill 的正确处理路径：参考图中 fill 可见 → Path C sprite sheet（默认）或 Path D img2img（回退）；参考图中 fill 不可见（如截图时 slider 值为 0）→ Path D 文生图（`visible_in_reference: false`、`crop_path: text2img_fallback`，prompt 从同组件可见部件推导风格）；fill 与 track 视觉完全相同（同色同材质）→ 不作为独立层提取，不走 `uss_only`。Toggle 子部件按设计意图确定（滑动开关式：`track` + `knob`；复选框式：可选只有 `knob`）。⚠️ **Dropdown 子部件强制清单**：每个 `functional_type: dropdown` 的控件**必须**包含 2 个子部件——`collapsed_bg`（收起态显示框背景）和 `expanded_panel_bg`（展开态弹出列表面板背景）。展开态面板通常比收起态更大、带边框装饰、可能有半透明遮罩，视觉上与收起态不同，禁止复用同一张图。⚠️ **Dropdown expanded_panel_bg prompt 约束**：展开态面板高度不固定（取决于选项数量），必须通过 USS `scale-to-fit` 或 9-slice 拉伸。其 prompt 必须描述为**简洁可拉伸矩形**（simple flat rectangular panel, uniform border, no decorative patterns, stretchable），禁止复杂花纹或装饰性边框——这些在拉伸时会变形。⚠️ **art_text 图片行约定**：说明列需追加 `art_text_style: <英文艺术字风格>`（如 `art_text_style: cartoon fantasy bold rounded letters, golden gradient fill with warm glow`）——Step 3 工具据此填充 JSON 的 `art_text_style` 字段，缺失会 FAIL。`display_text` 由 §11 显示文字列自动读取，无需在 §15 重复。⚠️ **§15 图片行说明列必填两要素**：① **W×H 目标尺寸**（如 `1920×1080`，Step 3 工具按此解析 width/height，缺失会 FAIL）；② **面板标识**（多面板文档必须写明界面名或 PanelName，如「设置面板 SettingsPanel 的 …」——工具按此匹配所属面板，缺失会 FAIL）。⚠️ **art_text 图片行与 §11 元素不同名时**：说明列必须显式写 `text_type: art_text` 或包含对应 §11 元素名——否则 text_type 落为 none、art_text_style 丢失。
6. **§16 场景挂载说明**：UIRoot prefab 装配说明
7. **收尾**：运行部署型工具 `Create.GameFramework.TJDocChecks.CheckDocPlaceholders("{GameName}");`，确认 `[DOC_PLACEHOLDER_CHECK] PASS`（残留 `{占位符}` + 章节完整性 + UGUI/UI Toolkit 模式条件化检查，替代人工 grep）

#### UI需求.md 信息不全时的占位规则

当 `UI需求.md` 信息不全时（全新构建模式）：
- `UI设计文档.md` 仍允许生成
- 绑定字段 / 服务接口 / 挂载要求 / 具体事件名等章节允许先写 **占位说明**（如"待脚本/状态机确认后回填"）
- 待后续脚本、状态机或用户确认补齐后再回填

### Step 3：生成 image_requirements.json

UI设计文档.md 生成后，从 §4/§11/§15 构建 `image_requirements.json`。这是 UI 图片需求的权威清单，供 `ui-image-generator` Stage 01-03 消费和 `compile_job_plan.py` 编译时派生 `ui_image` 隐式 Job。

> ⚠️ **编译器自动派生 ui_image Job**：`compile_job_plan.py` 会从 `image_requirements.json` 自动派生 `ui_image` 隐式 Job，覆盖全部 UI 图片生成。源 `job-plan.yaml` 中**禁止**为 UI 图片手动创建 `sprite_asset`/`image_asset`/`ui_image` Job——会导致同一批图片被生成两次。UI 重建的 `job-plan.yaml` 只需包含 `ui`、`ui_verify`、`verify_before_play`、`verify_in_play`、`publish_thumbnail`、`publish` Job。

#### 3.0 执行方式判定（先读，二选一）

按 UI设计文档.md 是否含 §12 判定路径。**两条路径的小节完全隔离：只执行所选路径的小节，另一条路径的小节一律跳过不读**。

| 路径 | 判据 | 执行的小节 |
|------|------|-----------|
| **无参考图路径**（默认） | 视觉输入方式=AI 直接规划；UI设计文档.md 无 §12 | 仅 §3.1.1-§3.1.2（运行部署型工具 + 主 agent 校验）。§3.1.3-§3.1.4 是工具已内置的规格说明，仅在校验/FAIL 裁决时查阅 |
| **有参考图路径** | UI设计文档.md 含 §12.2 组件裁剪映射表 | 仅 §3.2.1-§3.2.5（手动执行全部小节）。执行前必读 `../ui-image-generator/shared/contracts.md`（字段速查表）+ `../ui-image-generator/shared/adjudication-tables.md`（路径判定表、`is_segmentation` 判定表） |

> ⚠️ **小节编号规则**：§3.1.x 全部属于无参考图路径，§3.2.x 全部属于有参考图路径——序号不跨路径连续，禁止把两条路径的小节当成一个线性流程依次读下去。
> ⚠️ **两条路径共用的硬约束**（先记住，无论选哪条都适用）：
> - 所有条目的 `prompt` 字段一律写 `null`——最终 prompt 由 `ui-image-generator` Stage 01 按 `text-to-image-prompt-rules.md` §1 公式构建并覆盖写入；`ui-doc-builder` 不构建最终 prompt。
> - `style_guidance`（`style_baseline` + `panel_type_styles`）只能包含**视觉关键词**（配色、质感、画风、材质、光影），禁止功能语义词/交互元素词/UI 容器词，也禁止 `icons`/`buttons`/`badges`/`pills`/`bars`/`labels`/`toggles` 等组件类型特有词（转为通用描述词，如 "rounded solid shapes" 而非 "pill-shaped buttons"）。完整禁止词清单和替换示例见 `../ui-image-generator/references/text-to-image-prompt-rules.md` §1.0/§2.1。
> - 无参考图路径**禁止全文必读** `contracts.md` / `../ui-image-generator/shared/adjudication-tables.md` / `sprite_sheet_prompt_rules.md` / `text-to-image-prompt-rules.md`——判定逻辑已内置于部署型工具；仅在工具 FAIL 需人工裁决时按需查阅对应章节。有参考图路径必读 `contracts.md` + `adjudication-tables.md`，其余两份按 §3.2.x 指引查阅。

---

#### 3.1 无参考图路径（工具化）

##### 3.1.1 工具调用（已部署为编译型 Editor 工具）

1. 通过 `execute_csharp_script` **一行调用**（GAME_NAME 作方法参数；工具已编译进 `cn.tuanjie.create.ui` 包 `Editor/UIBuildTools`，无需读取/重发工具源码）：
   ```csharp
   Create.GameFramework.TJBuildImageRequirements.Run("{GameName}");
   ```
   > 免激活：该工具仅写 `context/` 文件（UI设计文档.md §15 回写 + image_requirements.json，零 AssetDatabase/Assets 写），按主 SKILL.md 权威映射表「部署型工具豁免」条款免激活 unity-operator。
2. 工具自动完成（行为规格见 §3.1.3/§3.1.4）：
   - **清单构建**：从 §15 解析图片行（尺寸、is_segmentation、text_type），从 §4 解析面板映射，从 §11 匹配 uxml_element/display_text，从 §3 提取 style_baseline——⚠️ 提取规则：`关键词:` **必须内嵌在 §3 `**UI 风格**` 行的值内（同一行）**，正确格式 `- **UI 风格**: <中文风格描述>，关键词: <英文视觉关键词串>`；写成独立行/独立段落**不会**被提取
   - **功能控件子部件扫描**：§11 中 Slider/Toggle/Dropdown/TextField/ProgressBar → 生成子部件条目（track/fill/handle/knob/collapsed_bg/expanded_panel_bg/bg，默认尺寸见工具源码注释）并幂等回写 §15（重跑不重复追加）
   - **排除与去重**：HUD 根背景标记 skip_generation 并排除；说明列含 skip_generation/uss_only 的行排除；同名图片去重
   - **Sprite Sheet 分组**：按 `../ui-image-generator/references/sprite_sheet_prompt_rules.md` §7.1/§7.2 分组，写入 sprite_sheet_group/slot/total/orientation/preset
   - **自检**：输出 `[IMAGE_REQ_CHECK] PASS` 或逐项 FAIL 明细（含 style_baseline/panel_type_styles 禁止词检查、art_text 必填检查）

##### 3.1.2 主 agent 校验（必做）

- 确认日志末尾为 `[IMAGE_REQ_CHECK] PASS`；FAIL 时按日志明细修复 UI设计文档.md 后重跑工具，**禁止手改 JSON 绕过自检**。⚠️ 工具 FAIL 时**不写出 JSON**（既有文件保持原样属预期），修复重跑到 PASS 才刷新；重跑自动继承既有 `prompt`/`generation_path`/`aspect_ratio`，不毁下游成果
- 抽查 JSON：条目数与 §15 图片行（含子部件行）一致、`prompt` 全部为 null、width/height 与 §15 一致；子部件默认尺寸不合理时在 §11 元素行补标注尺寸后重跑
- `style_baseline` 为空或缺配色时：主 agent 直接补写 JSON 的 `style_baseline`（英文视觉关键词，禁止词约束见 §3.0 共用硬约束）；可按 UI需求.md「按钮风格」「图标风格」补写 `panel_type_styles`（`style_prefix`/`category_overrides`，英文视觉关键词——组件类型特有词转为通用描述词）

##### 3.1.3 规格：功能控件子部件扫描（工具已内置，供校验/FAIL 裁决参考）

当无 §12 时，§15 资产清单通常只列了背景和按钮图片，**不会包含 Slider/Toggle/Dropdown 等功能控件的子部件**。工具据此扫描 §11 元素类型并补齐：
- `Slider`（UI Toolkit: `<ui:Slider>` / UGUI: `UnityEngine.UI.Slider`） → track、fill、handle
- `Toggle`（UI Toolkit: `<ui:Toggle>` / UGUI: `UnityEngine.UI.Toggle`） → track、knob
- `DropdownField`（UI Toolkit: `<ui:DropdownField>` / UGUI: `UnityEngine.UI.Dropdown`） → collapsed_bg、expanded_panel_bg
- `TextField`（UI Toolkit: `<ui:TextField>` / UGUI: `UnityEngine.UI.InputField`） → bg
- `ProgressBar`（UI Toolkit: `<ui:ProgressBar>` / UGUI: Slider readonly） → track、fill、handle
- 子部件条目含 `sub_part`、`functional_type`、`expanded_from`、`sub_parts_summary` 字段；`prompt` 写 null（Stage 01 覆盖写入）。视觉外观由 Stage 01 从 `style_baseline` + 控件类型推导
- ⚠️ **slider/progress_bar 子部件默认矩形平端**：track 与 fill 的视觉描述默认为**矩形、左右两端平直**的条（英文 rectangular with flat squared-off vertical ends），**禁止默认写入** rounded ends / tapered / pointed / capsule 等端形描述——文生图模型对 8:1 以上扁长条有柳叶形/锥形先验，且 fill 在 USS 中随 value 按宽度伸缩、track 可能 9-slice 拉伸，矩形平端两者都不变形，端形还必须 track/fill 一致。仅当用户风格需求**明确要求**其他端形时才覆盖此默认（且 track 与 fill 同步覆盖）。toggle 的胶囊形轨道**不受此默认约束**（滑动开关本来就是胶囊形）。ui-image-generator Stage 01 的增强关键词（`text-to-image-prompt-rules.md` §3）会按此默认注入端形约束
- **§15 回写**：工具自动将子部件图片行追加到 §15 资产清单，每个 `sub_part` 一行，文件名 `{element_name}-{sub_part}.png`（幂等，重跑不重复追加）
- ⚠️ **功能控件子部件不可降级为 `uss_only`**：`functional_type` 非空时 `source` 被前置转换强制设为 `ai_generate`（前置转换规格见 `../ui-image-generator/shared/adjudication-tables.md` 路径判定表），`uss_only` 仅适用于纯文字 Label（`skip_generation`）。即便用户明确要求画面品质目标为"标准"，也必须生成图片

##### 3.1.4 规格：文生图默认值与字段约定（工具已内置）

当视觉输入方式为"AI 直接规划"（无 §12）时，以下字段使用默认值：

| 字段 | 默认值 |
|------|--------|
| `source` | `"ai_generate"` |
| `crop_path` | `"text2img_fallback"` |
| `functional_type` / `quality` / `style_group` / `reuse_from` / `exclude_overlays` / `dynamic_content` / `generation_hint` / `sub_part` / `component_crops` | `null` |

必须逐个填写的核心字段：`name`、`size`、`is_segmentation`、`width`、`height`、`text_type`、`display_text`、`art_text_style`、`component_type`、`panel_name`、`panel_type`、`uxml_element`

> ⚠️ 有 §12（有参考图路径）时**不适用**此默认值——每张图的 `source`/`crop_path`/`functional_type`/`quality` 等字段必须从 §12.2 逐行读取（见 §3.2.1）。

---

#### 3.2 有参考图路径（手动执行）

> 执行 §3.2.1 前必读 `../ui-image-generator/shared/contracts.md`（字段速查表）和 `../ui-image-generator/shared/adjudication-tables.md`（路径判定表、`is_segmentation` 判定表）。有 §12 时**不适用**无参考图默认值——每张图的 `source`/`crop_path`/`functional_type`/`quality` 等字段从 §12.2 逐行读取。

##### 3.2.1 构建图片清单

1. **读取** `context/games/{GameName}/UI设计文档.md`
2. **提取图片清单**：从 §15 资产清单构建 `images` 列表。字段定义见 `contracts.md` 字段速查表
3. **读取风格指引**：`style_guidance` 从 §3 读取（`{ style_baseline, panel_type_styles }`）。无参考图面板（参考图未覆盖的弹窗等，见 §14）走文生图路径，风格描述来自 §3 `panel_type_styles` 或 §13 文生图回退风格参数。⚠️ 构建 `style_baseline` 时从 `UI需求.md`「UI 风格偏好」中提取**通用视觉关键词**（美术风格、配色方案、质感/光影、整体氛围），转化为英文视觉关键词纳入 `style_baseline`；⚠️ **`style_baseline` 禁止包含组件类型特有词**（icons, buttons, badges, pills, bars, labels, toggles 等名词形式）——「按钮风格」「图标风格」字段的视觉语义转为通用描述词（如 "rounded solid shapes" 而非 "pill-shaped buttons"；如 "bold flat silhouettes" 而非 "flat icons"），或放入 `panel_type_styles` 供特定组件类型按需注入
4. **纯文字组件排除**：§12.2 中 `裁剪图路径` 为 `skip_generation` 的组件不列入
5. **HUD 面板背景排除**：`panel_type == "hud"` 的根容器背景图标记 `skip_generation`。HUD 内的功能性元素仍正常生成
6. **图片复用规则**：多面板使用相同视觉背景时只列一条，`panel_name` 列出所有面板名。同风格组件去重（`style_group` / `reuse_from`）
7. **复合部件识别**：识别进度条、滑块、开关等复合 UI 部件，标记 `component_type` 和 `is_segmentation: true`

##### 3.2.2 functional_type 子部件展开

当 UI设计文档.md 含 §12.2 组件裁剪映射表时：
1. 逐行遍历 §12.2，检查每行的 `functional_type` 列
2. `functional_type` 值非 `—` 的组件 → 必须展开为子部件（track/handle/knob/fill/bg/expanded_panel_bg 等）。**子部件强制清单**：slider/progress_bar→track+fill+handle；toggle→track+knob（滑动开关式）或仅 knob（复选框式）；dropdown→collapsed_bg+expanded_panel_bg；text_field→bg。⚠️ **fill 不可降级为 `uss_only`**——`functional_type` 非空时 `source` 被前置转换强制设为 `ai_generate`（前置转换规格见 `../ui-image-generator/shared/adjudication-tables.md` 路径判定表）。fill 的处理路径：参考图中 fill 可见 → Path C sprite sheet（默认）或 Path D img2img（回退）；fill 不可见（如截图时 slider 值为 0）→ Path D 文生图（`visible_in_reference: false`、`crop_path: text2img_fallback`，prompt 从同组件可见部件推导风格）；fill 与 track 视觉完全相同（同色同材质）→ 不作为独立层提取，不走 `uss_only`
3. 按 `../ui-image-generator/references/prompt-engineering.md` §3.1 两步法分析透明 PNG（`final_transparent.png`）：先跑 `segment_control_layers.py` 产几何（`size_ratio` / `fills_parent` 一律取脚本值），再用 AI 只命名语义（每层 `layer_name`、`visual_description`、`visible_in_reference`；推算不可见部件如 `expanded_panel_bg`）。脚本输出 <2 层（`fallback: true`）时回退 AI-only 分析并标注「未经脚本几何」
4. `visible_in_reference: true` 的子部件：`crop_path` 指向父级透明 PNG。同一父组件的可见子部件**必须分入同一 `sprite_sheet_group`**，走 Path C（sprite sheet，img2img 模式，以父级透明 PNG 为参考图一次生成全部子部件再切片）。仅当可见子部件仅 1 个时走 Path D 独立生成。`visible_in_reference: false` 的子部件（如 dropdown 展开态面板）：`crop_path` 为 `text2img_fallback`，走 Path D 文生图（回退路径）或 Path C Sprite Sheet（若被分入 `sprite_sheet_group`）
5. **dropdown 展开态背景强制规则**：`functional_type=dropdown` 时，`expanded_panel_bg` 是强制必需层——即使 AI 未返回也必须手动添加。⚠️ **expanded_panel_bg prompt 约束**：展开态面板高度不固定（取决于选项数量），必须通过 USS `scale-to-fit` 或 9-slice 拉伸，prompt 须描述为简洁可拉伸矩形（simple flat rectangular panel, uniform border, no decorative patterns, stretchable），禁止复杂花纹或装饰性边框（拉伸时会变形）
6. **§15 资产清单回写（强制）**：将原始条目替换为展开后的子部件条目（每个 `sub_part` 一行），用 `replace` 工具（UTF-8 安全）
7. 详细规则参照 `../ui-image-generator/references/prompt-engineering.md` §3

##### 3.2.3 direct_use 条目（落位由 Stage 03 部署型工具完成）

若 §12.2 中存在 `source == "direct_use"` 且 `functional_type` 为 `—` 的组件，本步骤**不执行复制**：条目的 `crop_path` 已指向裁剪图，落位（File.Copy + ImportAsset + TextureImporter）由 ui-image-generator Stage 03 的部署型工具 `Create.GameFramework.TJImagePostprocess.DeployDirectUse` 统一完成（见路径判定表 Path A）。本模块保持零 Editor 写操作。

##### 3.2.4 Sprite Sheet 分组（强制，禁止跳过）

**必须执行**。遍历 `image_requirements.json` 中所有 `source == "ai_generate"` 的条目，按 `../ui-image-generator/references/sprite_sheet_prompt_rules.md` §7.1 条件筛选，满足条件的元素**必须**分入 sprite sheet（走 Path C，根据 `crop_path` 自动判定 img2img 或文生图）。仅当 eligible 元素仅 1 个时允许走 Path D 独立生成。

> ⚠️ **图集优先原则**：Sprite Sheet 是满足条件元素的默认路径，Path D 是回退路径。满足 §7.1 条件的元素不分入图集 = 违规。

##### 3.2.5 自检（禁止跳过，已工具化）

`image_requirements.json` 构建完成后**运行部署型工具一行调用 `Create.GameFramework.TJDocChecks.ValidateImageRequirements("{GameName}");`**（包 `Editor/UIBuildTools`，经 `execute_csharp_script`；只读、不写 JSON），确认 `[IMAGE_REQ_CHECK] PASS`。工具逐项实现下列检查（FAIL 时按日志明细修复后重跑，禁止手改 JSON 绕过自检；下列条目同时是工具 FAIL 时的人工裁决规格）：
1. 遍历所有 `functional_type` 非空条目：**必须**有 `sub_part`、`expanded_from`、`sub_parts_summary` 字段
2. `functional_type` 非空且 `crop_path` 为正常文件路径的条目，`source` **必须**为 `ai_generate`
2.5. `functional_type` 非空的条目，`source` **不得**为 `uss_only`（前置转换会强制设为 `ai_generate`；`uss_only` 仅适用于纯文字 Label）
3. `functional_type` 非空的条目，若 AI 分析仅返回 1 个可见层，验证其确实只有 1 个 `visible_in_reference: true` 的子部件（数据校验；`component_crops` 缺失时跳过）
4. **§15 回写验证**：读取 UI设计文档.md §15，对每个 `functional_type` 非空的组件，确认 §15 已包含其全部 `sub_part` 对应的图片行
5. **dropdown 展开态背景检查**：`functional_type=dropdown` 的组件必须存在 `sub_part=expanded_panel_bg` 的子部件条目
6. **Sprite Sheet 分组验证**：若 eligible 文生图元素 ≥ 2 个且均未分入 `sprite_sheet_group` → 回退到 §3.2.4 执行分组。eligible 元素仅 1 个时允许走 Path D
7. 检查通过后输出 `[IMAGE_REQ_CHECK] PASS`
8. **style_guidance 功能语义净化检查**：读取 `style_guidance.style_baseline` 和 `style_guidance.panel_type_styles` 的所有值，逐个检查是否包含 `../ui-image-generator/references/text-to-image-prompt-rules.md` §1.0 禁止的功能语义词、交互元素词或 UI 容器词。发现任何禁止词时必须替换为纯视觉描述词后重新自检。⚠️ **`style_baseline` 额外检查组件类型特有词**（icons, buttons, badges, pills, bars, labels, toggles 等名词形式），发现时必须替换为通用视觉描述词（如 "bold flat silhouettes" 替代 "flat icons"）后重新自检。完整禁止词清单和替换示例见 §1.0

自检未通过时**禁止返回**。必须先修复对应问题，再重新自检直到 PASS。

### Step 4：返回

> "UI设计文档.md 已保存至 `context/games/{GameName}/UI设计文档.md`，`image_requirements.json` 已保存至 `context/games/{GameName}/image_requirements.json`。Panel {N} 个、需图片 {I} 个。"

---

## 中途换风格回退规则

用户在管线下游（ui-panel-builder / ui-image-generator 已在执行或完成后）提出"换风格"时，按以下三种粒度回退。**不做全自动增量更新**——改一个 padding 可能影响整个布局，全量重做对应步骤更安全。

### 粒度判定

| 粒度 | 典型措辞 | 回退到哪 | 重做什么 | 保留什么 |
|------|---------|---------|---------|---------|
| **只换图片** | "按钮颜色改成蓝色""背景图不好看" | ui-image-generator | 仅重生成受影响图片 | UXML 结构、USS 布局、Panel 脚本全保留 |
| **换视觉参考** | "这张参考图不好看，换一张""按新风格做" | ui-mockup-generator → ui-style-reference | `UI参考图分析.md` → `UI设计文档.md` §12/§13 → ui-image-generator → ui-panel-builder 步骤 5-9（USS 图片引用+颜色） | UXML 结构、Panel 脚本保留 |
| **换布局/面板结构** | "主菜单改成左右分栏""加一个设置面板" | ui-doc-builder Step 0 | `UI需求.md` → `UI设计文档.md` → 全链路重做 | 仅 `UI需求.md` 保留 |

### 清理规则（回退时必须删除受影响的中间产物）

| 回退粒度 | 删除文件 | 保留文件 |
|---------|---------|---------|
| 只换图片 | 无（ui-image-generator 覆盖同名文件） | 全部 |
| 换视觉参考 | `UI参考图分析.md`、`image_requirements.json`、`Assets/Games/{GameName}/UI/Images/*.png`、`UIReferences/Processing/{受影响面板}/` | `UI需求.md`、UXML、USS（布局部分）、Panel 脚本 |
| 换布局/面板结构 | `UI设计文档.md`、`UI参考图分析.md`、`image_requirements.json`、`Assets/Games/{GameName}/UI/Images/*.png`、`Assets/Games/{GameName}/UI/*.uxml`、`Assets/Games/{GameName}/UI/*.uss`、`UIReferences/Processing/{受影响面板}/` | `UI需求.md`（若面板清单变了则更新而非删除） |

> ⚠️ 清理时同步删除 context 和 Assets 两处的对应文件（遵守 UI 文件双写规则）。不清理会导致新旧文件混淆，ui-panel-builder 步骤 9 的 UIV.Validate 可能通过但运行时引用了旧 USS。

---

## 风格处理规则

UI 风格处理规则详见 `references/ui-style-handling.md`
### 视觉参考模式（UI参考图分析.md 存在时）

图生图为主路径——颜色、质感、形状、风格全部由参考图决定。`UI参考图分析.md` 的核心价值是布局信息、图生图配置、文生图回退风格参数。

### 无视觉参考模式

Agent 根据游戏整体美术风格自行规划 UI 视觉设计。**所有视觉性 UI 元素一律在 §15 和 `image_requirements.json` 中列出图片条目**，元素可选择独立 AI 生成（文生图）或通过 `reuse_from` 复用另一元素的生成图片（复用元素不单独生成、不复制文件，USS 直接引用代表组件图片路径），除非用户明确要求纯 USS。详见 `references/ui-style-handling.md` §6。

---

## UI设计文档.md UIBrick 候选信息（§2）

为每个界面输出 `uibrick_brick_coverage`（`full` / `partial` / `none`）。详细规则见 `references/UI设计文档模板.md` §2。

---

## 完成检查

- [ ] `UI需求.md` 已落盘（Step 0 产出，保留不删除）
- [ ] `UI设计文档.md` 已落盘
- [ ] `image_requirements.json` 已落盘且自检通过 `[IMAGE_REQ_CHECK] PASS`
- [ ] UI设计文档.md §2 包含 UIBrick 候选信息
- [ ] UI需求.md 信息不全时，绑定/接口/挂载章节有占位说明
- [ ] `UI需求.md` 路径为 `context/games/{GameName}/UI需求.md`
