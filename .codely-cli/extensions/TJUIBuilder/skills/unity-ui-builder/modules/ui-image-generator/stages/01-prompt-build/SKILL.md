---
stage: 01-prompt-build
required_skills: []  # 纯文档读写 + execute_csharp_script 运行包内部署型工具（写 context JSON，无 Editor 资产写操作；显式声明，防止臆测）
input: image_requirements.json (prompt 为 null)
output: 更新后的 image_requirements.json（Precompute 工具写入 generation_path/size/aspect_ratio/is_segmentation；本阶段写入 prompt）
reads: 本 SKILL.md + shared/contracts.md + image_requirements.json + references/prompt-engineering.md + references/text-to-image-prompt-rules.md + references/sprite_sheet_prompt_rules.md（三份 references 为**分章必读**：按当前条目路径查阅对应章节，禁止预读全文——章节路由见「起草优先」第 1 条；该读的章节必须真正读到，禁止跳过）
runs: Create.GameFramework.TJImageGenPipeline.Precompute("{GameName}")（成功 marker [PRECOMPUTE_CHECK] PASS）+ TJStateHelper.SetStage（完成后 state 更新）——包 Editor/UIBuildTools，execute_csharp_script 一行调用；只调用不读源码，FAIL 时才按日志排查
optional_reads: shared/adjudication-tables.md（Precompute 工具 FAIL 按日志排查 / 对输出存疑需人工裁决 / 完成后尺寸抽查核对时读取）
---

# Stage 01：构建 AI 提示词与尺寸配置

运行部署型工具 `Create.GameFramework.TJImageGenPipeline.Precompute("{GameName}")`（包 `Editor/UIBuildTools`）完成路径/预设尺寸/宽高比/透明背景的机械判定，随后为每条目构建完整 prompt 并**脚本合并**写回 JSON，供 Stage 02 消费。

> ⚠️ **Stage 01 是 `prompt` 字段的唯一权威**：`image_requirements.json` 由 `ui-doc-builder` Step 3 产出时，所有条目的 `prompt` 字段为 `null`。Stage 01 **必须**为每个 `source == "ai_generate"` 的条目按 `references/text-to-image-prompt-rules.md` §1 公式构建完整 prompt 并覆盖写入。禁止保留 `null` 或使用上游预写值（上游不写 prompt）。`size`、`aspect_ratio`、`is_segmentation`、`generation_path` 由步骤 1 的 Precompute 工具机械确定。

> **分章必读**：构建 Path D 文生图 prompt 前必读 `references/text-to-image-prompt-rules.md` §1.0/§1/§2；构建 Path C 图集 prompt 前必读 `references/sprite_sheet_prompt_rules.md` §2/§3；图生图条目必读 `references/prompt-engineering.md` §2.1（章节路由见「起草优先」第 1 条——该读的章节必须真正读到，禁止跳过）。三张机械判定表（路径/预设尺寸/`is_segmentation`）已由部署型工具 `TJImageGenPipeline.Precompute` 内置机械执行并写入 JSON——规格与仲裁依据见 `shared/adjudication-tables.md`，仅工具 FAIL、人工裁决或尺寸抽查核对时读取。

## 起草优先（防超时硬约束）

> 实测教训：前置读完 3 份 references 全文再开始推演全部 prompt，是本阶段 subagent 超预算零产出死亡的首要模式。

1. **最小必读即可动笔**：运行步骤 1 的 Precompute 工具 + 读 `shared/contracts.md` + 本 SKILL.md 即可开始构建。3 份 references 是**边写边查的工具书**，按当前条目的路径分章查阅（禁止预读全文）：
   - Path D 文生图条目 → `text-to-image-prompt-rules.md` 的 §1.0/§1（公式+净化）、§2.1（style_baseline 过滤）、§5（背景类注意）、§6（art_text 条目专用公式）
   - Path C 图集组 → `sprite_sheet_prompt_rules.md` 的 §2（prompt 公式）、§3（元素描述拼接 + 长度预算）
   - MISSING_PIXEL_PREVENTION 常量与 text_suffix → `prompt-engineering.md` 的 §1.1 / §2.1
2. **连续 3 轮只读不写 = 过度准备**，立即动笔
3. **逐条构建逐条落盘**：每构建完 2-3 条 prompt 立即执行一次脚本合并写回（或分段构建 `$map` 分批写入），禁止先在 thinking 中推演全部条目再一次性输出

## 步骤 1：运行 precompute 工具（机械判定：路径 / 预设尺寸 / 宽高比 / 透明背景）

尺寸不匹配 → 图片只覆盖部分区域。判定规则（`shared/adjudication-tables.md` 的权威路径判定表、API 预设尺寸选择含逐项偏离度计算与极端宽高比正方形画布回退、`is_segmentation` 判定表、Path C 豁免）已**内置实现**于部署型工具 `TJImageGenPipeline.Precompute`——本步骤执行工具，**不再人工逐项计算**。

**执行方式**：

1. 通过 `execute_csharp_script` 一行调用：`Create.GameFramework.TJImageGenPipeline.Precompute("{GameName}");`（读写 `context/games/{GameName}/image_requirements.json`）
2. 工具对每个条目写入机械判定结果：
   - `generation_path`：`A` / `B` / `C` / `D` / `skip`（含前置转换：`functional_type` 非空 / `quality=contaminated` → `source` 强制 `ai_generate`）
   - `size`：API 预设尺寸（Path C → `sprite_sheet_preset` 固定 `2048x2048`；Path D → 最小偏离度预设，同比例选像素 ≥ 目标的最小档，deviation > 2.0 回退正方形画布 `1024x1024`/`2048x2048`）
   - `aspect_ratio`：目标宽高比最简整数比（如 `10:3`；Path C 成员供元素级 `aspect_ratio_description` 使用）
   - `is_segmentation`：按判定表得出最终值
   - 日志含 Path D 的 `[SIZE_CHECK] name=target_ratio:selected_preset:deviation` 逐行审计输出
3. 确认日志末尾 `[PRECOMPUTE_CHECK] PASS`。**FAIL 时**按日志明细修复 `image_requirements.json` 的上游字段（`width`/`height`/`source`/`crop_path`/`sprite_sheet_group` 等）后重跑工具，禁止手改 `size`/`is_segmentation` 等计算结果绕过自检；仍存疑时**选读** `shared/adjudication-tables.md` 对应判定表章节人工裁决

> ⚠️ **正方形画布策略的影响**：当 deviation > 2.0 时工具选 1:1 正方形预设，AI 在正方形画布中生成目标元素，Stage 03 的 `TrimTransparentBorders` 自动裁剪到内容边界后缩放到目标尺寸。prompt 中的 `{width}x{height} pixels` 仍写目标尺寸不变，`aspect_ratio` 字段也仍是目标尺寸的宽高比——两者共同引导 AI 理解目标内容形状，实际画布由 `size` 字段（API 预设）决定。

> ⚠️ **Path C 豁免**：`sprite_sheet_group` 非空的条目跳过预设尺寸计算——`size` 直接使用 `sprite_sheet_preset` 字段值（固定 `2048x2048`），`is_segmentation` 固定为 `true`。Path C 的 prompt 使用 `sprite_sheet_prompt_rules.md` §2 公式构建，不使用 `{aspect_ratio}` 全局前置规则（元素级宽高比描述仍读取 `aspect_ratio` 字段）。

如果判定为纯 USS（开发者明确要求），从 `image_requirements` 中移除该项，标注 `uss_substitute: true`、`skip_reason: "developer_request"`。

## 步骤 2：prompt 构建

### 路径读取

直接读取每条目的 `generation_path` 字段（Precompute 工具产出：`A`/`B`/`C`/`D`/`skip`；判定规则与 `shared/adjudication-tables.md` 权威路径判定表一致，无需人工判定）。

### 各路径 prompt 构建

**路径 D（单张生成，回退路径）**：


> ⚠️ Path D 仅用于不满足 Sprite Sheet 条件（§7.1）的文生图元素，或图集切片失败后的逐张回退。满足条件的元素应已在 ui-doc-builder Step 3（无参考图路径由工具内置；有参考图路径 §3.2.4）被分入 `sprite_sheet_group` 走 Path C。


> **Path D 基础公式（内联，无需读外部文件）**：
> ```
> "{style_baseline}, {panel_type_prefix}, {aspect_ratio} {visual_subject}, {width}x{height} pixels, {style_from_gdd}, {color_from_gdd}, {text_directive}, {negative_ui_suffix}, no watermark, game UI, high quality, detailed texture"
> ```
> - `style_baseline`：从 `style_guidance.style_baseline` 读取。移除文字相关关键词（`text`/`typography`/`font` 等）除非 `text_type=art_text`
> - `aspect_ratio`：从条目的 `aspect_ratio` 字段读取（Precompute 工具产出；如 `9:16`、`16:9`、`4:3`、`1:1`）。**必须直接前置于 `{visual_subject}`（组件名称）之前**，禁止放在 prompt 末尾或使用"宽高比为 X:Y"后缀式表达。AI 模型对 prompt 开头部分权重更高，前置式（如 `9:16 popup panel`）能让模型在构图阶段就锁定目标比例，后缀式（如 `popup panel, aspect ratio 9:16`）容易被模型忽略导致内容不铺满画布
> - `panel_type_prefix`：从 `style_guidance.panel_type_styles[面板类型]` 读取（抑制规则见 `text-to-image-prompt-rules.md` §2.2）
> - `visual_subject`：从 image_requirements 该条目的视觉主体描述读取
> - `text_directive`：`text_type=art_text` 时读 `references/text-to-image-prompt-rules.md` §6 获取艺术字专用公式；否则 `no text`
> - `negative_ui_suffix`：按 `text-to-image-prompt-rules.md` §1.0 根据 `component_type` 选取
> - 全屏背景/弹窗背景/面板背景类**必须追加**缺失像素预防关键词：`fully rendered scene from foreground to background, complete composition with no blank or white areas, every pixel filled with detailed content, rich textures throughout`

**路径 D（图生图）**：prompt 只写"这是什么"，不写颜色/质感/形状/风格。读 `references/prompt-engineering.md` §2.1 获取完整公式及 suffix 构建规则，§2.5 容器背景特殊处理，§2.6 `contaminated` leaf 验证与重试。功能控件子部件（`functional_type` 非空）也走此路径，参考 `prompt-engineering.md` §3 获取子部件独立生成的特殊规则。

**路径 B（复用）**：跳过 AI 生成，直接引用代表组件的同一张图片文件（不复制文件，USS 中共用路径）。处理细节（拓扑排序、引用规则）见 Stage 02 §2.0。

**路径 A（直接使用）**：从 `crop_path` 读取透明 PNG 复制到 Images/，设置 TextureImporter。不提交 AI 生成任务。ui-doc-builder Step 3 §3.2.3 已处理。

**路径 C（Sprite Sheet，默认路径）**：同一 `sprite_sheet_group` 的成员合并为 1 次 `generate_image` 调用。包括透明 UI 元素（图标/按钮/徽章/显示框等）和功能控件子部件（`functional_type` 非空，同一父组件的子部件分入同一图集组）。`crop_path` 为正常路径时走 img2img（传入父级透明 PNG），`text2img_fallback` 时走文生图。读 `references/sprite_sheet_prompt_rules.md` 获取 prompt 构建、生成参数、切片规则。**满足图集条件的元素必须走此路径**，Path D 仅用于不满足条件或图集切片失败的回退。

> **Path C prompt 存储**：Stage 01 按 `sprite_sheet_prompt_rules.md` §2 公式为每个 `sprite_sheet_group` 构建一个图集 prompt，写入该组内**所有成员**的 `prompt` 字段（同一图集的成员 prompt 相同）。`size` 写入 `sprite_sheet_preset` 值（`2048x2048`），`is_segmentation` 写入 `true`。Stage 02 读取时，同一 `sprite_sheet_group` 的成员只提交 1 次生成调用（取第一个成员的 prompt）。

## 步骤 3：脚本合并写回 image_requirements.json

prompt 构建完成后，**必须用脚本合并写回**，禁止 `read_file` 读入全文后 `write_file` 全量重写——多条目 × 长 prompt 的全文手抄输出是本阶段 subagent 超预算的首要原因。

**推荐方式（PowerShell，Windows）**：

> ⚠️ **宿主环境注意**：含 `$` 变量的 PowerShell 代码**必须写成临时 `.ps1` 文件用 `-File` 执行**——宿主 `run_shell_command` 的内联命令中 `$` 变量会被外层 shell 吞掉；Windows PowerShell 5.1 不支持 `&&` 分隔符（用 `;`）。

```powershell
$p = "<projectRoot>/context/games/{GameName}/image_requirements.json"
$json = Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json
# 逐条目（或按 sprite_sheet_group 分组）构建 prompt 后写入 $map：
$map = @{}   # key = 条目 name，value = prompt 字符串
foreach ($img in $json.images) { if ($map.ContainsKey($img.name)) { $img.prompt = $map[$img.name] } }
[System.IO.File]::WriteAllText($p, ($json | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
```

**跨平台方式（Python）**：`json.load` → 逐条目设置 `prompt` → `json.dump(..., ensure_ascii=False, indent=2)`（UTF-8 无 BOM）。

**写回约束**：
- 仅更新 `prompt` 字段；`generation_path` / `size` / `aspect_ratio` / `is_segmentation` 保持 Precompute 工具产物不变
- Path C 成员写入所在组的**同一个**图集 prompt（同组成员 prompt 相同）
- 写回后自查：每条 `source == "ai_generate"` 条目 `prompt` 非 null 且 ≤1024 字符；`uss_only` / `direct_use` 条目 prompt 保持 `null`

## 完成后

1. 确认 `image_requirements.json` 中每条 `source == "ai_generate"` 的条目：`generation_path` / `size` / `aspect_ratio` / `is_segmentation` 来自 Precompute 工具（步骤 1 日志 `[PRECOMPUTE_CHECK] PASS`），`prompt` 经步骤 3 脚本合并写入（非 null）。**Path C 特例**：同一 `sprite_sheet_group` 的所有成员写入相同的图集 prompt，`size` 为 `sprite_sheet_preset` 值，`is_segmentation` 为 `true`
2. 确认每条 **Path D** prompt 都按 `text-to-image-prompt-rules.md` §1 公式构建（含 `style_baseline`、`panel_type_prefix`（若适用）、`negative_ui_suffix`），已执行 §1.0 功能语义净化。**Path C prompt** 按 `sprite_sheet_prompt_rules.md` §2 公式构建，不适用此项检查
3. 确认每条 **Path D** prompt 都在组件名称（`{visual_subject}`）前包含 `{aspect_ratio}`（如 `9:16 popup panel`），而非放在末尾或使用后缀式表达。**Path C 不适用**（用 `aspect_ratio_description` 逐元素描述，无全局 `{aspect_ratio}` 前置）
4. 确认 `uss_only`/`direct_use` 条目的 `prompt` 保持 `null`（这些条目不走 AI 生成）
5. **尺寸选择自检**：逐条验证 `size` 字段——**Path C 条目**确认 `size` == `sprite_sheet_preset`（`2048x2048`）。**Path D 条目**引用 Precompute 工具日志中已有的 `[SIZE_CHECK] name=target_ratio:selected_preset:deviation` 行（无需人工重算），抽查 1-2 条与 `shared/adjudication-tables.md` 尺寸表核对一致（deviation ≤ 2.0，或已回退正方形画布 `1024x1024`/`2048x2048`）
6. **sprite_sheet_group 完整性自检**：对每条 `functional_type` 非空且 `crop_path` 为正常路径（非 `text2img_fallback`/`skip_generation`/`reuse_from:xxx`）的条目，检查 `sprite_sheet_group` 是否非空。为空时输出 `[SPRITE_GROUP_WARN] name=functional_type={type} missing sprite_sheet_group, will fallback to Path D instead of default Path C`。同样对 `crop_path == "text2img_fallback"` 且 `functional_type` 非空的条目检查（为空时输出 `[SPRITE_GROUP_WARN] name=functional_type={type} missing sprite_sheet_group, will fallback to Path D instead of default Path C`）。此警告不阻塞流程，但调用方应审查是否为上游 `ui-doc-builder` Step 3 遗漏
7. 用部署型工具更新 `ui-image-generator-state.json`（禁止手改 JSON）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "01-prompt-build", "completed", next: new[]{"02-generate","in_progress"});`（`next` 同时把 `current_stage` 置为 `02-generate`）
8. 调用方派发 Stage 02 subagent
