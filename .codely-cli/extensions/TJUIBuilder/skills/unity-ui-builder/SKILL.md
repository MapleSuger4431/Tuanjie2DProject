---
name: unity-ui-builder
description: Unity UI 界面构建技能（默认 UGUI，可选 UI Toolkit）。涵盖 UI 面板构建（UGUI Canvas Prefab 或 UXML/USS/Panel 脚本）、UI 图片生成（AI 生成按钮/图标/背景）、UI 风格参考提取（从参考截图提取布局与图生图配置）、AI 参考图生成（生成设计分辨率 UI 参考图，用户确认后移交 ui-style-reference 完成组件提取）、UI 画板布局设计（通过拉起 LayoutIntentBoard 自由设计）。使用此技能当：需要为 Unity 游戏创建 UI 界面（HUD、菜单、弹窗等）、需要创建 UGUI Canvas Prefab 或 UXML/USS 文件、需要编写 UI Panel 脚本、需要生成 UI 图片、用户提供了 UI 参考截图/设计稿、需要生成按钮背景/图标/弹窗背景/全屏背景。即使只是说"帮我生成一个按钮图片""生成游戏背景图""需要UI图标""按这个风格做UI""参考这张图做UI""先看UI效果图"，也应使用本技能。
---

# Unity UI Builder

> 执行本文及子模块中的 `{PYTHON}` 命令前，读取 `references/python-contract.md` 的"跨平台 Python 解析"合同。

UI 构建技能是一个**模块索引中心**，包含六个独立、自包含的 UI 子模块。每个模块都有自己的 references/scripts/prompts，通过本索引文档统一导航。

> **独立 extension**：本技能独立运行，不依赖特定编排器。核心入口是 `UI需求.md`（由 `ui-doc-builder` 的 Step 0 生成或补全）。`创意方向.md` 仅作为可选增强输入。`cn.tuanjie.create.ui`（含 UI 框架、UIBrick、UIV、LayoutIntentBoard 等）已随本 extension 内置，无需额外安装。

> **不进行场景搭建与 Play Mode 测试**：本技能创建 UI 资产（UGUI 模式：Tree Prefab（panel 树 + list_item 模板树）+ UGUIPanelBase 脚本 + UIRoot prefab；UI Toolkit 模式：UXML/USS/Panel 脚本/UIRoot prefab）但不负责场景搭建、编译验证和 Play Mode 运行时验证，由调用方或宿主编排器负责。

## 依赖

| 依赖 | 来源 | 说明 |
|------|------|------|
| `cn.tuanjie.create.ui` | 随本 extension | UI 框架、UIBrick、UIV、LayoutIntentBoard 等 |
| `csharp-code-writer`、`unity-operator` | 随本 extension | C# 脚本编写、Unity Editor 写操作 |
| TJGenerators MCP Server | 需配置 | AI 图片生成（ui-image-generator、ui-mockup-generator、ui-style-reference 需要），见下方环境自检 |
| Python 3.x + Pillow + OpenCV + numpy | 自动安装 | 仅 `ui-style-reference` 图像处理管线（15 个 OpenCV/像素级脚本）需要 |
| Python 3.8+ 标准库 | 需可用 | 仅剩余 `{PYTHON}` 脚本（ui-style-reference 图像管线与 `setup_env.py`）需要；流水线主干工具为 `Editor/UIBuildTools` 编译型工具（`execute_csharp_script` 一行调用），见下方环境自检 |

## ⚡ 环境自检

### TJGenerators MCP Server 检查（ui-image-generator、ui-mockup-generator、ui-style-reference 模块需要）

以上模块依赖 **TJGenerators MCP Server** 提供 AI 图片生成能力。进入这些模块前，**首先**检查 MCP Server 是否已配置：

1. 读取 `.codely-cli/settings.json`（项目级）和 `~/.codely-cli/settings.json`（用户级），检查 `mcpServers.TJGenerators` 是否存在
2. 如果不存在，提示用户添加以下配置并**重启 Codely CLI 会话**后重试，然后停止：
   ```json
   { "mcpServers": { "TJGenerators": { "httpUrl": "https://ai-generator.tuanjie.cn/mcp" } } }
   ```
3. 如果已配置，继续执行。MCP 运行时错误按 `modules/shared/references/mcp-image-generation.md` 错误处理表处理

### Python 环境自检（仅剩余 {PYTHON} 脚本需要）

> **流水线主干为 C# 编译型工具**：`cn.tuanjie.create.ui` 包 `Editor/UIBuildTools` 提供全部流水线主干工具（`execute_csharp_script` 一行调用，无需 Python）。`{PYTHON}` 仅服务 ui-style-reference 的图像处理脚本（Pillow/OpenCV/numpy）。

仅 `ui-style-reference` 需要额外依赖（Pillow、opencv-python 含 numpy）：使用该模块时**首先**通过 `run_shell_command` 执行环境自检脚本，自动检查并安装：

- 所有平台：`{PYTHON} .codely-cli/extensions/TJUIBuilder/skills/unity-ui-builder/scripts/setup_env.py`

脚本输出 `ENV_READY` 表示环境就绪。

## ⛔ 框架守卫（全模块通用）

> 权威全文见 `modules/ui-panel-builder/shared/contracts.md`「框架守卫」。以下为摘要：

- **UI 模式守卫**：由 `UI需求.md` Step 0 主动询问用户选择 `ui_mode`，全程不可更改。两种模式不可混用：
  - **UGUI 模式**（默认推荐）：Canvas + UGUI 组件，面板继承 `UGUIPanelBase`/`UGUIGameControllerBase`，元素查询用 `SafeFind<T>(path)`
  - **UI Toolkit 模式**（可选）：UXML + USS + UIDocument + PanelSettings，面板继承 `UIPanelBase`/`GameUIControllerBase`，元素查询用 `SafeQ<T>(name)`
- **USS 编写规范**（仅 UI Toolkit 模式）：统一参考 `modules/ui-panel-builder/references/uitk/uss-reference.md`。UGUI 模式不生成 USS

## ⛔ AI 图片生成并发守卫（全模块通用）

> 完整规则（上传/提交/等待/收取/下载/错误/恢复）见 `modules/shared/references/mcp-image-generation.md`。

生成通过 TJGenerators MCP 的 `generate_image` 提交（异步，返回 task_id），安全并发数 = 3（服务端账户级上限 5，留 2 个余量）。逐个提交，在途数以 state 文件统计为准；每个任务派 1 个后台 waiter task 等待，`check_task` 确认终态后下载到最终路径。`ui-style-reference` 用 seedream_pro（携带 `confirm_cost: true`），其余模块用 seedream。

## ⛔ 路径守卫（全模块通用）

> 完整路径约定和查询门禁见 `references/project-layout.md`。

- 宿主项目须采用 `context/games/{GameName}/` 目录约定。写入操作在目录不存在时**必须自动创建**
- ⛔ 禁止将 `.cs`/UXML/USS 文件直接写入 `Assets/` 目录（部署阶段除外）
- ⛔ UGUI 模式不创建 UXML/USS/PanelSettings 文件（UI Toolkit 专属）

---

## ⛔ 项目定位守卫（目标游戏确定）

在进入任何模块之前，**必须先确定目标游戏 `{GameName}`**。这是后续所有路径和文件定位的前提。

> **强制确认**：无论 `context/games/` 目录下内容如何（不存在、为空、只有一个子目录、有多个子目录），**都必须通过 `ask_user` 向用户确认本次流程的游戏名**。不得自动选择或跳过确认。

| 条件 | `ask_user` 提供的选项 |
|------|----------------------|
| 用户在请求中明确指定了游戏名 | 将该名称作为默认选项，同时列出 `context/games/` 下的现有游戏目录名（如有）供选择 |
| `context/games/` 目录下有一个或多个游戏子目录 | 列出全部目录名供选择，并允许用户输入新名称 |
| `context/games/` 目录不存在或为空 | 从用户请求中提取可能的名称作为默认选项，允许用户修改或新建 |

确定 `{GameName}` 后，后续所有模块的路径均以 `context/games/{GameName}/` 为根。

## ⛔ 新增面板守卫（入口路由）

`{GameName}` 确定后，用户请求创建 UI 面板时，**必须先判定是「全新项目首次建 UI」还是「已有项目新增面板」**，两者入口不同：

| 场景 | 判据 | 入口模块 |
|------|------|---------|
| **全新项目首次建 UI** | `context/games/{GameName}/UI需求.md` 不存在 | `ui-doc-builder` Step 0（完整信息采集） |
| **已有项目新增面板** | `context/games/{GameName}/UI需求.md` 已存在且必填字段齐全，但用户请求的面板**不在**现有面板清单中 | `ui-doc-builder` Step 0 **不可跳过**；仅针对新面板执行 0.3 信息采集（**必须询问视觉输入方式，含画板手动设计**），将新面板信息追加到 `UI需求.md` |
| **已有项目重建/修改现有面板** | `context/games/{GameName}/UI需求.md` 已存在且用户请求的面板**已在**面板清单中 | 根据用户需求自行判断 |

> ⚠️ **禁止跳过 `ui-doc-builder` Step 0 直接进入 `ui-panel-builder`**。`ui-panel-builder` 依赖 `UI设计文档.md` 作为输入，而 `UI设计文档.md` 由 `ui-doc-builder` 从 `UI需求.md` 生成。新增面板时如果不先走 Step 0 采集视觉输入方式（AI 直接规划 / 上传参考图 / AI 效果图 / 画板手动设计），会导致后续图片生成阶段缺失方向、用户无法选择视觉参考路径。

## ⛔ 阶段派发守卫（全模块通用）

本技能多个模块内部包含多阶段/多步骤流水线（如 `ui-style-reference` 的 6 阶段、`ui-panel-builder` 的步骤、`ui-image-generator` 的阶段）。无论由何种调用方驱动，均须遵守：

<HARD-GATE>模块内部的多阶段/多步骤流水线**每阶段必须由独立 subagent 执行**，禁止将多个阶段合并到同一 subagent。合并会导致单 subagent 认知超载（需同时读入多套规则文档 + 上下文）→ 超预算 → 零产出。阶段间状态通过中间产物（`pipeline-state.json`、`component-tree.json`、磁盘文件等）传递，调用方逐阶段派发、逐阶段验证产出后再进入下一阶段</HARD-GATE>

- 适用模块：`ui-style-reference`（6 阶段流水线）、`ui-panel-builder`（多步骤构建）、`ui-image-generator`（多阶段生成）
- ⛔ **`ui-doc-builder` 例外**：由主 agent 直接执行，禁止派 subagent
- 每阶段 subagent 仅读取当前阶段的 SKILL.md + 必读 references + 共享契约，不读取其他阶段的文档
- 调用方负责阶段间状态传递和产物验证

**Stage References 矩阵通用规则**（ui-image-generator、ui-panel-builder、ui-style-reference 共用）：
- 每个模块的 SKILL.md 中「Stage References 矩阵」列出的全部 references 文件**必须逐个 `read_file` 读取**——阅读时机按「起草优先」原则：在对应步骤需要其规则时查阅（该读的内容必须真正读到、不得跳过，保证流程不因漏读出错），不强制在阶段开始前批量预读全文
- ⛔ **禁止读取非本 stage 列出的 references 文件**——每个 stage 保持解耦
- ⛔ **subagent 必须自行读取模块 SKILL.md 的「Stage References 矩阵」**，据此读取对应 stage 的全部必读 references。禁止仅依赖调用方在 task prompt 中转述——调用方可能遗漏或过时。subagent 执行的第一步是读模块 SKILL.md → 找到当前 stage 的必读 references → 逐个读取
- ⛔ **禁止一个 subagent 执行多个阶段**。阶段间状态通过 state 文件 + 磁盘工件传递，不通过对话历史

### ⛔ 起草优先（防超时通用原则，全模块通用）

> 源自 01-ugui-prefab 的实测经验：subagent 超时的根因是「前置读完所有 references 再动手」+「读完长思考不落盘」。以下原则适用于所有模块的阶段执行（subagent 与主 agent 接管同规则）。

1. **最小必读即可动笔**：只需读 `shared/contracts.md` + 当前阶段的 `stages/XX/SKILL.md` 即可开始产出。`references/` 下的文件是**边写边查的工具书**，在对应步骤执行时按需查阅，不需要预读全文
2. **连续 3 轮只读不写 = 过度准备**，立即动笔
3. **每构建 1 条立即落盘**：每构建 1 条 prompt / 1 棵 trees.json 树 / 1 个文档章节 / 1 个函数就立即写入文件（`write_file` 主体 + `replace` 追加），禁止先在 thinking 中推演全部内容再一次性输出
4. **拿不准的先写**：按最合理写法写，工具 marker 自检 + 阶段验证兜底

### ⛔ 技能激活传递（派发硬约束）

阶段派发不仅传递「读什么文档」，还必须传递「激活什么技能」。subagent 不自动继承主 agent 已激活的技能，也看不到只存在于其他技能文件内部的激活边界条款（如 unity-operator 的「主 agent 和所有 subagent 均须先激活」只写在其自身 SKILL.md 里）——不显式传递，subagent 就不会激活，Editor 写操作门禁对 subagent 失效。

**操作类型 → 必须激活的技能（权威映射表）**：

| 该阶段将执行的操作 | 必须激活 | 原因 |
|------|------|------|
| 任何 `execute_csharp_script` 写操作（Prefab 构建 / 资产导入 / TextureImporter / 场景 / .meta / SO / PanelRegistry / Brick Prefab 改写） | `unity-operator` | 其激活边界对 subagent 同样生效，但规则只存在于该技能文件内部，不传递即失效 |
| 编写或修改 C# 脚本（Panel / Installer / HUD / 游戏↔UI 胶水脚本，含改 bug / 调参 / 加功能） | `csharp-code-writer` | 「context↔Assets 双写 → 部署 → 编译验证 → Play 冒烟」闭环只在该技能中定义 |
| 纯文档读写、纯 MCP 生成提交/下载（无 Editor 写操作） | 无需激活 | 在 prompt 中显式写明「无需激活」，防止 subagent 臆测 |

> **部署型工具豁免**：`execute_csharp_script` 一行调用部署型工具、且工具仅写 `context/` 文件（零 `AssetDatabase`、零 `Assets/` 写——如 `TJBuildImageRequirements.Run`、`TJImageGenPipeline.Precompute/PlanSubmissions/BuildResult`、`TJStateHelper` 写 state）时免激活，与 shell 直写 context 文件（Stage 01 脚本合并写回）等价。工具触碰 `Assets/` 或 Editor 状态（切片/缩放/TextureImporter/`ImportAsset`/Prefab 构建等）时仍须激活，无例外。

- **调用方义务**：派发每个 stage subagent 时，task prompt 必须按上表写明该阶段的激活要求（含「无需激活」的显式声明），并注明「subagent 第 0 步先 `activate_skill`，再读 Stage References 矩阵」
- **subagent 义务**：第 0 步按 task prompt 激活指定技能；若 prompt 遗漏，以 stage SKILL.md 头部的 `required_skills` 声明（ui-image-generator 为 frontmatter 字段，ui-panel-builder 为「步骤契约」表的「必须激活技能」行）自查补激活，然后才读矩阵与 references
- **接管联动**：subagent 失败后主 agent 接管时，重新激活该 subagent 曾被要求激活的全部技能（对应 TJUIBuilder CODELY.md 接管规则）

> 各模块 SKILL.md 的阶段路由表标注每个 stage 的 `required_skills`，stage SKILL.md 头部同步声明；两者与上表冲突时以上表为准，并向调用方回报修正。

### ⛔ 调用方与工具调用纪律（派发硬约束）

1. **工具源码阅读纪律**：能一行调用的部署型工具一律「先一行调用 → 读 marker 输出（PASS/FAIL 与明细）→ 仅 FAIL、或文档与报错矛盾需 tie-break 时才读工具源码」。工具运行正常（marker PASS）时**禁止预读工具源码**；工具签名不确定时查 API 卡（`unity-operator/assets/generated-api/`），不预读实现
2. **派发 prompt 最小必读清单**：必读只列 `shared/contracts.md` + 阶段 SKILL.md + 该阶段产出直接依赖的项目文件；Stage References 矩阵的 references 在派发 prompt 中**仍须全部列出**（不可省略——漏读导致流程错误的代价远大于多读），但显式标注「边写边查、禁止前置批量预读」并注明各 reference 的查阅时机（如「步骤 5 构建 trees.json 时查 ugui-prefab-construction.md」）
3. **落盘节奏要求**：派发 prompt 要求 subagent 遵守「起草优先」第 3 条——每构建 1 条立即落盘

## 如何使用

1. **确定目标游戏**：按上方「⛔ 项目定位守卫」确定 `{GameName}`
2. **判定入口**：按上方「⛔ 新增面板守卫」判定是全新项目还是已有项目新增面板，确定入口模块
3. **读取模块文档**：读取匹配模块文件夹下的 `SKILL.md`，获取详细指引
4. **执行模块逻辑**：严格按照模块文档中的步骤操作

### 完整流水线

流水线分为「视觉输入分支」和「主链路」两部分。`ui-doc-builder` Step 0 采集用户选择的视觉输入方式后，进入对应分支；分支完成后汇入主链路，主链路三步固定不变。

```
  UI需求.md（核心必需输入，由 ui-doc-builder Step 0 生成或补全）
      │
      ├── 上传参考图 ─────────────────────────────────────┐
      │                                                  ▼
      ├── AI 效果图 ──► ui-mockup-generator ──► ui-style-reference ──► UI参考图分析.md ──┐
      │                                                  │                              │
      │                             ┌─────── 校正开启 ────┴──── 校正关闭（默认）──────────┐
      │                             ▼                                                   │
      ├── 画板手动设计 ──► ui-layout-intent-designer ───────► UI布局意图.md ──────────────┤
      │                                                                                 │
      └── AI 直接规划 ──────────────────────────────────► （无 UI参考图分析.md）───────────
                                                                                        │
                                                                                        ▼
                                                                                ui-doc-builder
                                                                        （生成 UI设计文档.md + image_requirements.json）
                                                                                        │
                                                                                        ▼
                                                                                ui-image-generator
                                                                                (UI图片生成阶段)
                                                                                Stage 01-02-03
                                                                                → Assets/.../Images/
                                                                                        │
                                                                                        ▼
                                                                                ui-panel-builder  ← 最终产出模块
                                                                                ┌─ UGUI 模式（默认）─ Stage 01-ugui-prefab → Stage 02-ugui-csharp
                                                                                └─ UI Toolkit 模式 ── Stage 01-uxml-uss → Stage 02-csharp-integration
                                                                                （最终产出：Panel Prefab/UXML+USS + C# Panel 脚本 + UIRoot.prefab）
                                                                                        │
                                                                                        ▼
                                                                                场景搭建阶段集成（不在本技能范围内）
```

> `UI需求.md` 是核心入口。`创意方向.md` 作为可选增强输入由 `ui-doc-builder` Step 0 消费。

## 模块清单

| 模块 | 文件夹 | 简介 | 触发词 |
|------|--------|------|--------|
| 📄 **ui-doc-builder** UI 文档构建 | [`modules/ui-doc-builder/`](modules/ui-doc-builder/SKILL.md) | 从 `UI需求.md` 生成 `UI设计文档.md` 和 `image_requirements.json`；Step 0 负责生成或补全 `UI需求.md` | 生成 UI 设计文档、UI 需求整理 |
| 🎨 **ui-panel-builder** UI 面板构建 | [`modules/ui-panel-builder/`](modules/ui-panel-builder/SKILL.md) | 根据 ui_mode 创建 UGUI Canvas Prefab 或 UI Toolkit UXML/USS + C# Panel 脚本，引用 UIBrick 组件库。UGUI 模式（默认）：Canvas Prefab + UGUIPanelBase；UI Toolkit 模式：UXML/USS + UIPanelBase | 创建 UI 界面、HUD、菜单、弹窗、UGUI Canvas Prefab、UXML、USS、UI Panel 脚本 |
| 🖼️ **ui-image-generator** UI 图片生成 | [`modules/ui-image-generator/`](modules/ui-image-generator/SKILL.md) | 从 `image_requirements.json` 读取需求，AI 生成按钮背景/图标/弹窗背景/全屏背景等游戏 UI 图片 | 生成按钮图片、生成背景图、UI图标、图片质量不好要重新生成 |
| 📐 **ui-style-reference** 风格参考提取 | [`modules/ui-style-reference/`](modules/ui-style-reference/SKILL.md) | 从参考截图提取布局与图生图配置（保真度、组件裁剪、布局提取）。内部为 6 个常规阶段 + 仅在开启布局校正时执行的 Stage 04.5（先校正结构树再生成图片与分析文档）；**每个 AI 阶段由独立的单个 subagent 执行**，确定性 resolver 由模块内脚本执行 | 有参考图、按这个风格做、参考这张图做 UI |
| 🧩 **ui-mockup-generator** AI 参考图生成 | [`modules/ui-mockup-generator/`](modules/ui-mockup-generator/SKILL.md) | 根据 `UI需求.md`（可选增强输入：`创意方向.md`）生成设计分辨率 UI 参考图，用户确认后移交 ui-style-reference 完成组件提取 | AI 效果图、UI mockup、先看界面效果 |
| 🧱 **ui-layout-intent-designer** 画板布局设计 | [`modules/ui-layout-intent-designer/`](modules/ui-layout-intent-designer/SKILL.md) | 独立通过 LayoutIntentBoard 采集并验证布局，输出权威 `layout_intent.json`；路线 A 同步生成 `UI布局意图.md` 并回流 `ui-doc-builder`，路线 B 只向调用它的 Stage 04.5 返回正式 JSON | 画板手动设计、空白画板、布局意图、手动规划 UI 面板 |

## 模块间协作关系

> 完整流程见上方「完整流水线」ASCII 图。以下仅列出图中未体现的协作细节。

**关键协作规则**：
- **上传参考图路径**：在 `ui-doc-builder` Step 0 选择后，立即转入 `ui-style-reference`，产出 `UI参考图分析.md`
- **AI 效果图路径**：在 `ui-doc-builder` Step 0 选择后，转入 `ui-mockup-generator` 生成参考图，用户确认后移交 `ui-style-reference` 产出 `UI参考图分析.md`
- **参考图布局校正**：仅上传参考图/AI 效果图路线可用，由 `ui-doc-builder` Step 0.4 条件询问并写入 `UI需求.md`，默认关闭。开启后由 `ui-style-reference` Stage 04.5（位于 Stage 04 之后、Stage 05 之前）调用独立的 `ui-layout-intent-designer`（路线 B），用户确认布局后由确定性 resolver 把检测树与用户布局解析为**最终 `component-tree.json`**；布局 skill 只返回用户布局权威 `layout_intent.json`，不修改参考分析或调用方状态。校正开启时**先校正结构树，再由 Stage 05 生成图片、Stage 06 一次性从最终树生成 `UI参考图分析.md`**，随后沿原主链进入 `ui-doc-builder`
- **AI 直接规划路径**：在 `ui-doc-builder` Step 0 选择后，继续当前文档链路，不产出 `UI参考图分析.md`
- **画板手动设计路径**：在 `ui-doc-builder` Step 0 选择后，一次打开包含全部目标界面的 LayoutIntentBoard 项目；用户通过顶部标签逐界面设计。全部确认后由 `ui-layout-intent-designer` 从内部权威文件 `LayoutIntent/layout_intent.json` 生成 `UI布局意图.md`，再回到 `ui-doc-builder` Step 1/2
- `ui-doc-builder` 读取 `UI参考图分析.md`（如有）吸收到 `UI设计文档.md`；`source` 列新增值（`direct_use`）和 `crop_path` 列新增值（`text2img_fallback` / `reuse_from:xxx`）由 `ui-image-generator` 路径判定消费
- `ui-doc-builder` 同时读取 `UI布局意图.md`（如有）；用户画板几何和父子层级优先于参考图布局与 AI 推断
- **functional_type 子部件处理时序**：① ui-style-reference Stage 05 §9a 透明裁剪 → `final_transparent.png`（`source=ai_generate`）；② ui-style-reference Stage 05 §9b shell 走 `composite_shell` → `shell_transparent.png`（`source` 保持 `ai_generate`）；③ ui-image-generator Path C/D 以 `final_transparent.png` 为参考生成各子部件（Path C sprite sheet 为默认，Path D 独立生成为回退）
- **术语约定**："sprite sheet" = Path C 小型元素图集；"图集"（ui-style-reference 中）= `generation_atlas.py` 的装箱/拆图流程（atlas）。两者含义不同

## 模块目录规范

每个模块自包含（有自己的 SKILL.md/references/scripts/prompts）；模块间只通过中间产物（md/json 磁盘文件）交接状态。字段级契约的共享权威：跨模块共享契约放 `modules/shared/references/`，被消费模块 `shared/` 目录下的契约文档允许被上游模块直读（如 ui-doc-builder 读 ui-image-generator 的 `shared/adjudication-tables.md`）；除此之外，模块间不直连彼此的 stages/references 内部文件。

```
modules/<module-name>/
├── SKILL.md            # 模块文档（必须）
├── references/         # 参考手册（按需）
├── scripts/            # 脚本（按需）
├── prompts/            # AI 分析 prompt 模板（按需）
```
