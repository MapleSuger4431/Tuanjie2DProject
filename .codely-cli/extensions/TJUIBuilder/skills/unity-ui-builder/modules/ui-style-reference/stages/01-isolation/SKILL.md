# Stage 01: 图生图隔离

Steps 2-4：关键色补边 → 图生图隔离 UI（三步：3a 移除文字 ∥ 3c 提取纯文字 → 3b 移除背景） → 验证隔离图。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `UIReferences/{PanelName}.{ext}`（已由 orchestrator 采集或 ui-mockup-generator 移交） |
| 输出 | `source_square.png`、`text_removed_2048.png`、`ui_only_2048.png`、`text_only_2048.png`、`transform.json`、`validation.json`、`pipeline-state.json`（含 `panel_provenance.step3_isolation`） |
| 前置门禁 | `UIReferences/{PanelName}.{ext}` 已存在 |
| 完成判据 | `validation.json` 三态门禁通过（pass 免视觉 / review 经视觉仲裁通过） |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：图生图执行规则、溯源记录、目录布局、核心硬约束见 `shared/contracts.md`。执行本阶段前应已读取。

## 2. 准备关键色补边方形输入

```text
{PYTHON} scripts/prepare_reference_square.py \
  {originalImage} \
  {processingDir}/source_square.png \
  {processingDir}/transform.json
```

检查 `transform.json`：左/上补边均为 0；`background_mode` 为 `"key_color"`；`key_color` 字段存在且含 `name`/`rgb`/`hsv`；`square_size` 等于 `max(原图宽, 原图高)`；`scaled_content_rect` 在 2048 画布内且尺寸大于 0。失败时停止讨论。

## 3. 图生图隔离 UI（三步法，仅用于几何检测）

隔离分三步执行，依赖关系：

- **3a 移除普通文字** ← `source_square.png`，产出 `text_removed_2048.png`
- **3c 提取纯文字图** ← `source_square.png`，产出 `text_only_2048.png`
- **3b 移除全屏背景** ← `text_removed_2048.png`（3a 产出），产出 `ui_only_2048.png`

3a 和 3c 均以 `source_square.png` 为输入、互相独立，由同一个 subagent 在同一批次内提交（仅 2 个任务，低于安全并发数 3）。3b 只依赖 3a（不依赖 3c），须等 3a 完成后再提交。3c 的产出 `text_only_2048.png` 只在 Stage 02 Step 5.5 消费，不影响 3b 和 Step 4 验证。

> **重试隔离（硬约束）**：3a 和 3c 虽并行提交，但各自独立验证和重试。某一步验证失败时，**仅重试该步**——已通过验证的步骤保留原结果，禁止重新提交。例如 3c 验证失败时，只重新提交 3c，不得连带重新提交已通过的 3a。

<HARD-GATE>Step 3a、3b 和 3c 是三个独立的 img2img 调用，禁止合并或跳过任何一个。3a 和 3c 均以 `source_square.png` 为输入，互相独立，可在同一批次内提交（仅 2 个任务，低于安全并发数 3）。3b 的输入**必须是** 3a 产出的 `text_removed_2048.png`（不是 `source_square.png`），因此 3b 必须等 3a 完成后才能提交。3b 不依赖 3c。跳过 3a 直接做 3b 会导致文字残留在隔离图中，下游 OpenCV 组件提取和语义分类全部不可靠。跳过 3c 会导致文字标签缺少精确几何坐标，Stage 02 Step 5.5 将降级为原图 OpenCV 两步检测（`detect_text_on_original.py`，见 Stage 02 §5.5-fallback）——几何仍来自像素检测，但色通道干扰下低对比度文字可能「未检出」、需 04.5A 画板用户补框。不存在任何"task constraint"允许跳过 3a 或 3c——若认为某步骤不必要，必须用 `ask_user` 确认后才能调整，禁止自行决定跳过。</HARD-GATE>

**读** `shared/contracts.md` 的图生图执行规则。

### 3a. 移除普通文字（保留艺术字）

参数：`generate_image`（TJGenerators MCP）——`provider: seedream_pro`、`image_urls: <source_square.png 按 mcp-image-generation.md §2 上传的 CDN URL>`、`image_size: {"width":2048,"height":2048}`、`is_segmentation: false`、`confirm_cost: true`。

prompt：

```text
移除所有UI元素上的普通字体的文字和数字，保留艺术字。确保所有普通文字都被移除，不要保留任何普通文字。
```

> 此步只移除普通文字，不动背景。"艺术字"指具有装饰性、风格化外观的文字（如立体描边标题、特效文字），这些是 UI 视觉的一部分需保留；"普通文字"指按钮标签、列表项文字等纯信息性文字，这些由 UXML text 属性渲染，不需要保留在图片中。

收到 waiter 返回的 `status=completed` 后，用 `output_url` 下载保存为 `text_removed_2048.png`（`curl.exe -sS -L -o "{processingDir}/text_removed_2048.png" "<output_url>"`），校验非零字节。

**文字移除验证**：用 `analyze_multimedia` 分析 `text_removed_2048.png`（单图，不与原图对比）。`analyze_multimedia` 工具只接受单张图片，因此验证 prompt 面向单图分析。prompt（逐字使用）：

```text
分析这张经过文字移除处理的UI图片。普通文字（按钮标签、数字、列表项文字）是否已被移除？艺术字（具有立体描边、特效装饰等风格化外观的文字）应保留。返回JSON：{"plain_text_removed": true/false, "art_text_preserved": true/false, "residual_text_description": "描述残留的普通文字，无则留空"}。
```

判定与处理：
- `plain_text_removed=true && art_text_preserved=true` → ✅ 通过，继续 3b（3b 依赖 3a，不依赖 3c）
- `plain_text_removed=false` 且 `residual_text_description` 中仅含极少量零碎残留（如单个数字、图标下的小标记） → ✅ 视为通过，继续 3b
- `plain_text_removed=false` 且残留面积较大（整行/整段文字未移除） → ❌ 重试 3a（保留前次结果为 `text_removed_2048_r{N}.png`），最多重试 2 次。重试时 prompt 追加强调：`移除所有UI元素上的普通字体的文字和数字，保留艺术字。确保所有普通文字都被移除，不要保留任何普通文字。`
- `art_text_preserved=false` → ⚠️ 艺术字被误移除，重试 3a，prompt 追加：`移除所有UI元素上的普通字体的文字和数字，保留艺术字。注意保留所有具有装饰性、风格化外观的艺术字。`
- 2 次重试仍失败 → 保留最优结果继续 3b

> **溯源记录**：将此步的 `task_id` 和 `generator_id` 写入 `pipeline-state.json` 的 `panel_provenance.step3_isolation.text_removal` 字段。

### 3b. 移除全屏背景（关键色填充）

> **前置检查（硬门禁）**：确认 `text_removed_2048.png` 已存在。若不存在，说明 3a 被跳过，**立即返回执行 3a**，禁止用 `source_square.png` 替代 3b 的输入。

参数：`generate_image`（TJGenerators MCP）——`provider: seedream_pro`、`image_urls: <text_removed_2048.png 按 mcp-image-generation.md §2 重新上传的 CDN URL>`、`image_size: {"width":2048,"height":2048}`、`is_segmentation: false`、`confirm_cost: true`。

> ⚠️ **关键色从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 字段读取**，在构建 prompt 时将 `{key_color}` 替换为颜色名称+十六进制（如 `magenta (#FF00FF)`，hex 从 rgb 的 r/g/b 转大写拼接）。禁止硬编码颜色。

prompt：

```text
去除全屏背景和非UI的角色立绘，去除的部分用{key_color}填充。保留所有UI元素的原始外观，不要在UI元素周围添加任何边框、轮廓或描边。
```

> 此步基于已移除文字的图，专注移除最底层全屏背景层和角色立绘。用"全屏背景"精确指代最底层全屏背景层；"角色立绘"指中央展示的游戏角色/宠物/跟随物等非 UI 控件的视觉内容——它们在 Stage 05 背景提取时从原图保留。

**prompt 调整方向**：元素丢失 -> 明确保留（如 `保留所有UI元素、图标和角色立绘`）；元素残留 -> 追加移除（如 `去除背景场景中的角色`）。

收到 waiter 返回的 `status=completed` 后，用 `output_url` 下载保存为 `ui_only_2048.png`（`curl.exe -sS -L -o "{processingDir}/ui_only_2048.png" "<output_url>"`），校验非零字节。此图仅用于 OpenCV 几何检测，不用于组件图片提取。

> **溯源记录**：将此步的 `task_id` 和 `generator_id` 写入 `pipeline-state.json` 的 `panel_provenance.step3_isolation.background_removal` 字段（见 `shared/contracts.md` 核心硬约束·img2img 溯源记录）。Stage 02 构建 `component-tree.json` 时从此字段读取并写入树顶层。

### 3c. 提取纯文字图（关键色背景 + 仅普通文字）

参数：`generate_image`（TJGenerators MCP）——`provider: seedream_pro`、`image_urls: <source_square.png 按 mcp-image-generation.md §2 上传的 CDN URL>`、`image_size: {"width":2048,"height":2048}`、`is_segmentation: false`、`confirm_cost: true`。

> ⚠️ **`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 字段读取**，在构建 prompt 时将 `{key_color}` 替换为颜色名称+十六进制（如 `magenta (#FF00FF)`，hex 从 rgb 的 r/g/b 转大写拼接）。禁止硬编码颜色。

prompt：

```text
提取所有UI元素上的普通字体的文字和数字，移除所有UI元素、按钮、图标、面板、背景、角色立绘和艺术字。被移除的部分用{key_color}填充。只保留普通文字，保持文字的颜色、原始位置、大小不变。必须逐字保留参考图中已有的文字及其位置，严禁添加、改写或虚构参考图中不存在的任何文字。
```

> 此步产出 `text_only_2048.png`，**唯一目的是供 Stage 02 Step 5.5 用 OpenCV 提取文字标签的 `root_box` 几何坐标（位置 + 大小）**。OpenCV 的 `foreground_mask` 检测所有非关键色像素，文字像素无论颜色都能被检测到。"普通文字"指按钮标签、列表项文字、设置项名称等纯信息性文字；"艺术字"指具有装饰性、风格化外观的文字（如立体描边标题、特效文字），需移除。

收到 waiter 返回的 `status=completed` 后，用 `output_url` 下载保存为 `text_only_2048.png`（`curl.exe -sS -L -o "{processingDir}/text_only_2048.png" "<output_url>"`），校验非零字节。

**文字提取验证（三态门禁）**：先跑脚本，按三分支执行（门禁契约见 `shared/contracts.md`「三态门禁」节）：

```text
{PYTHON} scripts/validate_text_only.py \
  {processingDir}/text_only_2048.png \
  {processingDir}/transform.json \
  --json-output {processingDir}/text-only-validation.json
```
- 退出码 0（pass：关键色覆盖达标且仅细小连通域）→ ✅ 免视觉通过（3c 独立于 3a/3b，产出就绪供 Stage 02 使用）
- 退出码 1（fail：覆盖不达标，或存在大块均匀非关键色区域——AI 返回了普通截图/整面板未移除，机械确定）→ ❌ 直接重试 3c（保留前次结果为 `text_only_2048_r{N}.png`），无需视觉确认机械结论。重试 prompt 追加强调：`只保留普通字体的文字和数字。移除所有UI元素、按钮、图标、面板、背景、角色立绘和艺术字。确保只保留普通文字。`
- 退出码 2（review：存在大块非均匀连通域——残留 UI 元素/艺术字嫌疑，密集文字段落也可能）→ 视觉仲裁：将脚本 `reasons` 作补充证据，对 `text_only_2048.png` 执行下方三问 prompt
- 审计：`text-only-validation.json` 必须落盘，免视觉判定留痕
- 2 次重试仍失败 → 保留最优结果（`text_only_2048.png` 仍保留）。Stage 02 Step 5.5 **必须使用 OpenCV `extract_text_labels.py` 对该文件提取文字标签几何坐标**，不阻塞 Stage 01 完成。OpenCV 只检测非关键色像素的 bounding box，不依赖图片纯净度——即使残留少量 UI 元素或艺术字，仍可正确提取文字标签的位置和大小

**review 仲裁 prompt**（逐字使用，`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 读取，替换为颜色名称+十六进制，如 `magenta (#FF00FF)`）：

```text
这是一张关键色背景图片，应仅保留普通文字。背景应填充为{key_color}，所有UI元素（按钮、图标、面板、背景、角色立绘、艺术字）应已移除。仅普通文字应可见。分析这张图片并回答：（1）是否仅可见普通文字（无UI元素、无背景、无艺术字）？是/否（2）背景是否填充为{key_color}？是/否（3）是否有未移除的UI元素或艺术字？是/否，描述它们。返回JSON：{"plain_text_only": true/false, "background_filled": true/false, "residual_description": "..."}。
```

**仲裁判定与处理**：
- `plain_text_only=true && background_filled=true` → ✅ 通过
- `plain_text_only=false` → ❌ 重试 3c（保留前次结果为 `text_only_2048_r{N}.png`），最多重试 2 次（与 fail 分支共享重试次数上限）

> **溯源记录**：将此步的 `task_id` 和 `generator_id` 写入 `pipeline-state.json` 的 `panel_provenance.step3_isolation.text_extraction` 字段。

## 4. 验证隔离图（三态门禁）

```text
{PYTHON} scripts/validate_ui_only_reference.py \
  {processingDir}/ui_only_2048.png \
  {processingDir}/transform.json \
  --baseline {processingDir}/text_removed_2048.png \
  --json-output {processingDir}/validation.json
```

脚本检查项：图片 2048x2048；内容矩形有效；填充区关键色覆盖率达阈值；内容区非全关键色（关键色像素面积占非填充区 >=15%，同时涵盖"独立轮廓 >=1"）；**最大前景轮廓 <= 内容区 95%**（背景未移除会形成一个吞掉全内容的巨轮廓）；内容区大块均匀非关键色区域（底板残留嫌疑）；与 3a 基线的"UI 元素带"连通块保留率比较（过度移除嫌疑——大块立绘/底板不进比较分母，无需识别立绘）。失败时保留诊断并可重试。绝不退回 AI 坐标。

三分支（门禁契约见 `shared/contracts.md`「三态门禁」节）：
- 退出码 0（pass）→ ✅ 免视觉：隔离图合格，直接进入 Stage 02
- 退出码 1（fail：机制性硬失败——尺寸/内容矩形/填充区覆盖率/前景覆盖率/最大轮廓超标，机械确定）→ ❌ 调整 prompt 重试 3b，无需视觉确认机械结论
- 退出码 2（review：脚本标记大块均匀非关键色区域（关键色化不足嫌疑）或带内元素保留率过低（过度移除嫌疑））→ 视觉仲裁：将脚本 `reasons` 作补充证据，对 `ui_only_2048.png` 执行下方两问 prompt
- 审计：`validation.json` 必须落盘，免视觉判定留痕

**review 仲裁 prompt**（逐字使用，`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 读取，替换为颜色名称+十六进制，如 `magenta (#FF00FF)`）：
```text
这是一张关键色隔离的UI图片。背景应填充为{key_color}，所有UI元素（按钮、面板、文字、图标、弹窗内容）应保留。分析这张图片并回答：（1）全屏背景是否已被成功替换为{key_color}？是/否（2）是否有UI元素被错误移除或替换为{key_color}？是/否，描述哪些。返回JSON：{"background_replaced": true/false, "ui_elements_lost": true/false, "lost_description": "..."}。
```
**仲裁判定与处理**：
- `background_replaced=true && ui_elements_lost=false` → ✅ **通过**：隔离图合格，进入 Stage 02
- `background_replaced=false` → ❌ **关键色化不足**：背景未移除，调整 prompt 重试 3b
- `ui_elements_lost=true` → ❌ **关键色化过度**：UI 内容被吞，调整 prompt 重试 3b

retry（fail 或 review 确认异常）最多尝试 2 次，若最终失败则先进入下一步，因为可能这张UI图片是全屏弹框。

## 完成后

1. **文件清单检查（逐项确认存在）**：
   - ✅ `source_square.png`
   - ✅ `text_removed_2048.png`（3a 产出，缺失 = 3a 被跳过，返回补做）
   - ✅ `ui_only_2048.png`（3b 产出）
   - ✅ `text_only_2048.png`（3c 产出，缺失 = 3c 被跳过，返回补做）
   - ✅ `transform.json`
   - ✅ `validation.json`
2. **pipeline-state.json 溯源字段检查**：`panel_provenance.step3_isolation` 必须包含三个子字段，缺一不可——字段为 `text_removal`、`background_removal`、`text_extraction`（权威全文见 `shared/contracts.md`）。若缺少 `text_removal`/`background_removal` 子字段（仅有 flat `task_id`），说明 3a 被跳过，返回补做。若缺少 `text_extraction` 子字段，说明 3c 被跳过，返回补做。
3. 更新 `pipeline-state.json`：
   - `01-isolation` → `completed`
   - `02-root-extraction` → `in_progress`
4. orchestrator 派发下一阶段 subagent（`stages/02-root-extraction/SKILL.md`）
