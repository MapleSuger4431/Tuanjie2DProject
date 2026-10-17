---
name: ui-mockup-generator
description: AI UI 参考图生成模块 — 根据 UI需求.md（可选增强输入：创意方向.md）生成设计分辨率大小的 UI 参考图，用户确认后移交 ui-style-reference 完成组件提取与 UI参考图分析.md 产出。步骤 2（图片生成）可委派 subagent，步骤 1/3（用户交互）由调用方执行。
---

# AI UI 参考图生成

根据 `UI需求.md` 生成设计分辨率宽高比的 UI 参考图供用户确认视觉方向（可选增强输入：`创意方向.md` 用于补充风格关键词），确认后移交 `ui-style-reference` 完成组件提取全流程并产出 `UI参考图分析.md`。

## 核心原则

1. **设计分辨率参考图**：为每个界面生成一张设计分辨率宽高比的 UI 参考图。API 不支持精确分辨率时取宽高比最接近的预设尺寸。
2. **参考图是设计参考，不是最终 UI 资产**：给用户确认视觉方向（风格/构图/层级），确认后移交 ui-style-reference 做组件提取。
3. **确认后移交 ui-style-reference**：用户确认满意后，将参考图作为 ui-style-reference 的输入，由其完成关键色图生图→OpenCV 组件树→递归拆分→最终图片生成→写 `UI参考图分析.md` 的全流程。

## 硬性约束

- **禁止实现操作**：仅生成参考图，不编写代码、不搭建项目
- **步骤 1/3 由调用方亲自执行**：读取创意方向、`ask_user` 确认界面清单、展示参考图、`ask_user` 确认参考图均由调用方直接完成
- **步骤 2 委派 subagent 执行**：图片生成通过 subagent 完成，提交/等待/收取/下载流程详见 `modules/shared/references/mcp-image-generation.md`
- **生成通过 TJGenerators MCP 的 `generate_image` 工具**（`provider: seedream`）：参考图为文生图，无参考图输入，无需 file_upload 上传
- **禁止在后台执行期间调 `ask_user`**：步骤 2 图片生成若通过后台 `task` 执行，`ask_user` 必须在后台执行派发前或完成后进行
- **禁止用 `analyze_multimedia` 展示参考图**：步骤 3 展示参考图时**必须**用 `unity_screenshot capture_ui_toolkit` 按 `modules/shared/references/image-preview.md` 工作流将图片内联渲染给用户看

## 交互规范

- 风格/确认交互必须用 `ask_user`，禁止纯文本提问
- 优先使用 `type: "choice"`，给 2-4 个选项
- 每个调用只问一个问题
- 选项 `label` 简洁（≤10 字），`description` 说明含义和影响

## 入口

- 用户直接要求先看 AI UI 效果图
- `ui-doc-builder` Step 0 判定需要走 AI 效果图路径并激活本模块
- 调用方可以是任意编排器或直接使用，不是本模块前提

## 工作流程

### 步骤 1：读取 UI 需求并推导界面清单

1. 优先读取 `context/games/{GameName}/UI需求.md`，提取 GameName、面板清单、面板用途、设计分辨率、UI 风格意图
2. 若存在 `创意方向.md`，将其作为**可选增强输入**补充美术风格、核心玩法、视觉关键词
3. 根据 `UI需求.md` 的面板清单与用途推导需要生成参考图的面板列表
4. `ask_user` 确认界面清单（仅当信息缺失或存在歧义时）

### 步骤 2：委派 subagent 生成设计分辨率参考图

当前调用方构建 prompt 和参数后，通过 subagent 完成图片生成全流程。subagent 内部完成：提交 generate_image → 等待完成通知 → 移动图片到 UIMockups/。

#### 2.1 调用方构建 prompt

调用方在步骤 1 确认界面清单后，为每个面板构建生成 prompt，连同参数一起传给 subagent。

每个面板的 prompt 生成一张 UI 参考图，按设计分辨率宽高比展示完整界面布局：

```
"game UI design, {panel_type} interface for a {game_genre} game, {style_description}. {layout_description}. Placeholder text in Simplified Chinese (中文), all visible text must be Chinese characters. {aspect_ratio} aspect ratio. Clean, high-detail UI mockup."
```

**变量说明**：
- `panel_type`：`main menu` / `HUD` / `game over popup` / `pause popup` / `settings popup`
- `game_genre`：从 `UI需求.md` 或创意方向提取（如 `arcade snake`、`puzzle`、`racing`）
- `style_description`：优先从 `UI需求.md` 的 UI 风格偏好提取，缺失时再结合创意方向美术风格补足
- `layout_description`：按面板类型描述布局意图（如 `centered title at top, two buttons stacked vertically in center, decorative grid background`）
- `aspect_ratio`：从设计分辨率推导（1920×1080 → `16:9`，1080×1920 → `9:16`）

**prompt 规则**：
- 必须包含 `placeholder text in Simplified Chinese (中文)`（参考图可以有中文占位文字展示内容布局，所有可见文字必须是简体中文）
- 必须包含 `{aspect_ratio} aspect ratio`（参考图须与设计分辨率宽高比一致）
- 风格描述从创意方向提取，不自行编造
- 不指定透明背景（参考图不透明，`is_segmentation: false`）
- 不包含任何组件区、分色背景、双区布局描述——参考图是单张完整 UI 画面

#### 2.2 API 预设尺寸选择

按设计分辨率宽高比选择最接近的 SeeDream 预设尺寸，提交时以 `image_size: {"width":W,"height":H}` 传入。遇到尺寸被拒时，换宽高比最接近的已验证档位并记录：

| 设计分辨率宽高比 | 推荐 size | 说明 |
|---|---|---|
| 16:9（如 1920×1080） | `2848x1600`（尺寸白名单见 `modules/ui-image-generator/shared/adjudication-tables.md`） | 横屏宽屏 |
| 9:16（如 1080×1920） | `1600x2848` | 竖屏移动端 |
| 4:3（如 1024×768） | `2304x1728` | 横向 |
| 3:4（如 768×1024） | `1728x2304` | 竖向 |
| 1:1（如 1080×1080） | `2048x2048` | 正方形 |
| 其他宽高比 | 取最接近的预设 | 3:2→`2304x1728`；2:3→`1728x2304`；21:9→`3136x1344` |

**选择逻辑**：
1. 从 `UI需求.md` 读取设计分辨率 `{targetW}×{targetH}`
2. 计算 `aspect = targetW / targetH`
3. 遍历 5 种预设尺寸，选 `|预设aspect - aspect|` 最小者
4. 记录选择的 size 和对应的宽高比标签

#### 2.3 委派 subagent 生成

**恢复预检**：委派 subagent 前，检查 `context/games/{GameName}/UIMockups/` 目录下是否已存在 `{PanelName}_reference.png` 文件。已存在的面板跳过生成，仅对缺失的面板提交生成任务。用户可能已手动删除部分文件——删除的文件需要重新生成。

当前调用方构建完整 prompt 列表后，通过 subagent 发起任务：

**task 调用模板**：

```
task(
  subagent_name: "unity_developer",
  description: "生成 UI 参考图",
  prompt: """
    激活 unity-ui-builder 技能，读取 modules/ui-mockup-generator/SKILL.md 步骤 2 的生成指令，
    并读取 modules/shared/references/mcp-image-generation.md（MCP 生成工作流权威源）。
    执行以下图片生成任务：

    ## 面板列表
    {panels_json}
    每个面板含: name, prompt, size

    ## 生成参数
    - generate_image: provider="seedream"、is_segmentation=false
    - 每个面板的 image_size 从面板列表的 size 读取（"WxH" → {"width":W,"height":H}，按设计分辨率宽高比选择，不统一）
    - 下载目录: context/games/{GameName}/UIMockups/
    - 文件命名: {PanelName}_reference.png

    ## 执行步骤
    1. 分批提交面板：在途 <3 时逐个调用 generate_image（每次调用即 1 次提交），提交后立即记录 task_id
    2. 每提交 1 个任务，按 mcp-image-generation.md §5.2 派发 1 个后台 waiter task（执行该任务返回的 poll_command_powershell → check_task 恰好一次）
    3. waiter 返回 completed 后，用 output_url 下载：curl.exe -sS -L -o "context/games/{GameName}/UIMockups/{PanelName}_reference.png" "<output_url>"，并校验文件存在且非零字节
    4. waiter 异常/超时时 check_task 一次确认终态，禁止在确认 failed 前重新提交

    ## 返回结果
    列出每个面板的: name, reference_path（参考图在 context/games/{GameName}/UIMockups/ 下的绝对路径）, status（completed/failed）
  """
)
```

**并发限制**：安全并发数 = 3（服务端账户级上限 5，留 2 余量）；提交流程/在途统计/`TaskLimitExceeded` 重试细节以 `modules/shared/references/mcp-image-generation.md` §4 为唯一权威。

⚠️ `prompt` 中如含双引号须转义为 `\"`

#### 2.4 调用方接收结果

调用方收到 subagent 的 task 完成通知后，从返回结果中提取每个面板的 reference_path，进入步骤 3 用户确认循环。

### 步骤 3：用户确认循环

1. <HARD-GATE>**展示参考图**：逐面板将参考图展示给用户（必须一张一张展示，至少间隔5秒展示下一张; 如果需要对图片重命名则必须重命名完成才能展示）。**必须**先读取 `modules/shared/references/image-preview.md` 工作流，用 `unity_screenshot capture_ui_toolkit` 将图片内联渲染到 CLI。**禁止**用 `analyze_multimedia` 做文字描述——用户需要直接看图自行判断，AI 描述既浪费 token 又无决策价值</HARD-GATE>
2. **`ask_user` 确认**：
   - 「满意，继续」→ 进入步骤 4（移交 ui-style-reference）
   - 「修改风格」→ `ask_user` 确认修改范围：
     - 「全局风格调整」（整体配色/质感/装饰变化）→ **所有面板重新生成**，已满意的面板作废。`ask_user` 收集修改意见（`type: "text"`），回到步骤 2.1 调整 prompt 后重新委派 subagent 生成
     - 「局部面板调整」（只改某个面板的按钮风格等）→ **只重生指定面板**，已满意的面板固定不变。`ask_user` 收集修改意见（`type: "text"`），回到步骤 2.1 仅调整指定面板的 prompt 后重新委派 subagent 生成
   - 「重新生成」→ 保持相同 prompt 重新委派 subagent 生成（AI 生成有随机性）
3. **逐面板确认**：满意的面板固定，不满意的重生，直到全部满意
4. 持续循环直到用户对所有面板满意确认

### 步骤 4：移交 ui-style-reference

参考图确认后，将参考图作为 ui-style-reference 的输入，激活并执行 ui-style-reference 全流程。

#### 4.1 复制参考图到 UIReferences/

将确认后的参考图从 `UIMockups/` 复制到 `UIReferences/`（ui-style-reference 的输入目录）：

- `UIMockups/{PanelName}_reference.png` → `UIReferences/{PanelName}.png`

> 多面板时，每个面板的参考图分别复制到 `UIReferences/{PanelName}.png`。

#### 4.2 激活 ui-style-reference

当前调用方激活 `ui-style-reference` 模块，以 `UIReferences/{PanelName}.png` 作为参考图输入。ui-style-reference 从其 Step 2 开始执行（跳过 Step 1 用户上传采集——参考图已就位），按其自身 SKILL.md 完成全流程并产出 `UI参考图分析.md`。多面板时各面板独立进入 ui-style-reference 流程。
