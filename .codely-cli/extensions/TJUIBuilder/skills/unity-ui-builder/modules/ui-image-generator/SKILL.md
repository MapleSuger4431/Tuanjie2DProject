---
name: ui-image-generator
description: Unity UI 图片生成模块 - 通过 TJGenerators MCP Server（generate_image，provider: seedream）为游戏生成高质量 UI 图片（按钮、图标、背景等），从 image_requirements.json 读取需求并生成 UI 图片。使用此模块当：需要为 Unity 游戏生成按钮背景/图标/弹窗背景/全屏背景/顶栏等 UI 素材、需要重新生成质量不佳的 UI 图片。即使只是说"帮我生成一个按钮图片""生成游戏背景图""需要UI图标""图片质量不好要重新生成"，也应使用本模块。分成多个 stage，每个 stage 委派 1 个独立 subagent 执行。
---

# Unity UI Image Generator

为游戏 UI 生成图片资产，底层通过 **TJGenerators MCP Server** 的 `generate_image` 工具（`provider: seedream`）生成。本模块从 `image_requirements.json`（由 `ui-doc-builder` Step 3 产出）读取需求并生成指定的 UI 图片。

**核心特性**：尺寸感知（从 image_requirements.json 读取）、异步生成 + 后台 waiter 等待驱动、文字分离（图片不含文字，文字由 UXML/UGUI Text 显示）、缺失像素预防（提示词增强 + 路A确定性像素扫描每图必跑 + 路B风险分层视觉检测 + 确证重试）、透明背景支持（默认 `is_segmentation: true`，仅全屏背景和纯色铺满面板背景为 `false`）、Sprite Sheet 图集生成（Path C）、功能控件子部件独立生成（Path D 回退）。

> ⚠️ **UI 模式无关**：图片生成流程与 ui_mode（ugui / ui_toolkit）无关——两种模式都消费相同的 `image_requirements.json` 并生成相同的 Sprite 图片。下游消费方式（USS `background-image` 或 `Image.sprite`）由 `ui-panel-builder` 处理，不在本模块关注范围。

## When to Use This Skill

- 当前任务要求生成 UI 图片资产时
- 需要重新生成质量不佳的 UI 图片时

**不使用**：图片已由其他途径获得（云端下载、用户导入）；开发者明确要求纯 USS。

## 前置条件

- TJGenerators MCP Server 已配置（见主 SKILL.md 环境自检）
- 用户有足够的 AI 生成积分

## 3 阶段流水线

本模块是一个**3 阶段流水线**。每个阶段由**独立的 subagent** 执行，阶段间通过 state 文件传递状态，不通过对话历史。

> `image_requirements.json` 由 `ui-doc-builder` Step 3 产出（包含功能控件子部件扫描、functional_type 展开、Sprite Sheet 分组和自检）。本模块从 Stage 01 开始消费。

```
Stage 01  Prompt 构建  运行 precompute 工具（路径/预设尺寸/宽高比/透明判定）→ 构建 prompt → 脚本合并写回
  │
Stage 02  生成提交    读 image_requirements.json → 分组 → MCP generate_image 提交 + waiter 等待 + 下载落位 → END TURN
  │
Stage 03  后处理验证  下载完整性核对 → 缺陷检测（路A像素扫描每图必跑 + 路B视觉高风险类别选择性）→ 切片（仅 Path C，检测先行防切片浪费）→ 缩放验证 → 输出结果
```

| 阶段 | SKILL.md | 输入 | 输出 | required_skills |
|------|----------|------|------|-----------------|
| 01-prompt-build | `stages/01-prompt-build/SKILL.md` | `image_requirements.json`（prompt 为 null） | 更新后的 JSON（Precompute 工具填入 generation_path/size/aspect_ratio/is_segmentation，Stage 01 填入 prompt） | 无（纯文档读写 + 运行部署型工具） |
| 02-generate | `stages/02-generate/SKILL.md` | `image_requirements.json`（含 prompt） | 图片下载到 `Assets/Games/{GameName}/UI/Images/` | 无（纯 MCP 提交 + shell 下载） |
| 03-postprocess | `stages/03-postprocess/SKILL.md` | `image_requirements.json` + Images/ 图片 | `image_generation_result.json` + Images/ 最终图片 | unity-operator（切片/后处理/TextureImporter 经 execute_csharp_script 写资产） |

## Stage References 矩阵

> Stage References 矩阵通用规则（必读 references 强制读取、禁止读取非本 stage 文件、subagent 自行读取矩阵）见主 SKILL.md「⛔ 阶段派发守卫」。

每个 stage 执行前**必须读取** `shared/contracts.md`（跨阶段共享契约）+ 对应阶段的 `stages/XX-xxx/SKILL.md`，然后按下表读取该 stage 的必读 references。

### Stage 01 必读 references

| 文件 | 用途（查阅时机） |
|------|------|
| `references/prompt-engineering.md` | §1.1/§2.1 MISSING_PIXEL_PREVENTION 与 text_suffix（构建任一 prompt 时）；§2.1 图生图公式（Path D img2img 条目）；§2.5/§2.6 容器背景与 contaminated 验证（对应条目）；§3 功能控件子部件特殊规则（`functional_type` 条目） |
| `references/text-to-image-prompt-rules.md` | §1.0/§1/§2 功能语义净化与 prompt 公式（构建 Path D 文生图 prompt 前）；§6 art_text 专用公式（`text_type=art_text` 条目） |
| `references/sprite_sheet_prompt_rules.md` | §2/§3 图集 prompt 公式与元素描述拼接（构建 Path C 图集 prompt 前） |

### Stage 01 选读 references（仅 Precompute 工具 FAIL、人工裁决或尺寸抽查核对时读取）

| 文件 | 按需章节 |
|------|---------|
| `shared/adjudication-tables.md` | 权威路径判定表、`is_segmentation` 判定表、API 预设尺寸表（Precompute 工具规格权威）——判定逻辑已由部署型工具 `TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`）内置机械执行并写入 JSON，正常流程无需读取。上游 `ui-doc-builder` 有参考图路径构建时必读 |

### Stage 02 必读 references

| 文件 | 用途（查阅时机） |
|------|------|
| `modules/shared/references/mcp-image-generation.md` | §1 调用参数、§4 提交与并发、§5 等待与收取、§7 错误处理（执行前必读）；§2 图生图上传流程（提交 img2img 任务前查阅） |

### Stage 03 必读 references

| 文件 | 用途 |
|------|------|
| `references/prompt-engineering.md` | §4 像素缺陷检测两路分工（路A确定性扫描/路B风险分层视觉；§2.6 为 contaminated 污染验证 prompt，勿混用） |
| `references/sprite_sheet_prompt_rules.md` | §5/§6 切片规则（按需） |
| `modules/shared/references/mcp-image-generation.md` | §5 等待与收取 / §7 错误处理（恢复 redownload、服务端卡死重提场景按需查阅） |

三个 stage 执行前均**必须读取** `shared/contracts.md`——仅含全阶段通用契约（核心硬约束、字段速查表、state schema）。机械判定表（路径判定/`is_segmentation`/API 预设尺寸）的规格权威在 `shared/adjudication-tables.md`，已由部署型工具 `TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`）机械执行——Stage 01 正常流程无需读取（见上方选读表），仅工具 FAIL、人工裁决或尺寸抽查核对时读取。

## 阶段路由规则

> 阶段派发通用规则见主 SKILL.md「⛔ 阶段派发守卫」。

**执行流程**：
1. 读取 `ui-image-generator-state.json`，确定 `current_stage`
2. 派发 1 个 subagent，传入 `shared/contracts.md`、`stages/{current_stage}/SKILL.md`、该阶段必读 references 路径，并按上表 `required_skills` 列在 task prompt 中写明技能激活要求（「无」也须显式写明；规则见主 SKILL.md「⛔ 技能激活传递」）
3. subagent 完成后更新 `ui-image-generator-state.json`（当前阶段 → `completed`，下一阶段 → `in_progress`）——一律经 `TJStateHelper.SetStage(...)` 一行调用（`next` 参数自动置 `current_stage`），禁止手改 JSON
4. 重复 1-3 直到所有阶段 `completed`

**会话恢复**：读取 `ui-image-generator-state.json`，找到第一个 `in_progress` 或 `pending` 的阶段，从该阶段继续。已完成阶段不重新执行。

## 中间状态文件

| 文件 | 产出阶段 | 消费阶段 | 内容 |
|------|---------|---------|------|
| `ui-image-generator-state.json` | 调用方创建 | 全部 | 阶段流水线状态（current_stage + 各阶段 status），保存在 `context/games/{GameName}/ui-image-generator/` 下 |
| `image_requirements.json` | ui-doc-builder Step 3（prompt 为 null） | Stage 01/02/03 | 图片需求清单（Precompute 工具写入 generation_path/size/aspect_ratio/is_segmentation，Stage 01 写入 prompt） |
| `image_generation_result.json` | Stage 03 | ui-panel-builder | 最终交付结果（generated/failed/uss_substitutes） |

Schema 定义见 `shared/contracts.md`。

## 失败恢复

| 失败场景 | 恢复动作 | 回退前序？ |
|---------|---------|-----------|
| Stage 01 Precompute 工具 FAIL | 按工具日志明细修复 `image_requirements.json` 上游字段后重跑工具（禁止手改计算结果绕过自检） | ❌ |
| Stage 01 失败 | 读已有 `image_requirements.json` 重派 Stage 01（已写入的 precompute 字段与 prompt 均保留，幂等续跑） | ❌ |
| Stage 02 某张图 API 失败 | 标记该条目 failed，继续处理其余 | ❌ 不影响其他图 |
| Stage 02 整体失败 | 读 `image_requirements.json` 重派 Stage 02 | ❌ prompt 已在 JSON 中 |
| Stage 02 中断后恢复（MCP 任务中断/waiter 异常） | 按 state 的 tasks 映射预检：文件存在跳过；缺失但有 `output_url` 重下载；无 task_id/failed 重提交 | ❌ |
| Stage 03 缺陷重试耗尽（仅对两路检测确证的缺陷计数；误报放行不消耗） | 标记 failed_images，继续处理其余 | ❌ |

**会话恢复**：读 `ui-image-generator-state.json` 的 `current_stage` → 从该 stage 继续。已完成阶段不重新执行。

## 与其他技能的协作

**独立运行模式**：本技能作为 UI 图片生成阶段独立运行，产出图片存放到 `Assets/Games/{GameName}/UI/Images/`，供 ui-panel-builder 引用。`ui-panel-builder` Stage 01 会验证本模块产出的图片已存在，缺失图片将导致构建失败。

**图片来源优先级**：用户导入 > AI 生成（有参考图→图生图；无参考图→文生图+style_guidance）> 纯 USS。

**风格指引传递链**：modules/ui-style-reference → `UI参考图分析.md` → ui-doc-builder 吸收到 `UI设计文档.md` §12/§13 → ui-doc-builder Step 3 构建 `image_requirements.json` → 本技能 Stage 01 读取。

`component_type` 与 Brick `assetPropsSchema` 槽位名是不同维度，映射由 `modules/ui-panel-builder` 步骤 1 建立。

## 权威定义源声明

- **路径判定表**：以 `shared/adjudication-tables.md` 为权威源，references 中的路径判定均引用此表
- **`is_segmentation` 判定逻辑**：以 `shared/adjudication-tables.md` 判定表为权威定义
- **功能控件子部件独立生成规则**：以 UI设计文档.md 为权威定义源
- **面板类型风格组格式**：以 UI设计文档.md §13 为权威定义源

## 目录结构

```
ui-image-generator/
├── SKILL.md                              # 本文件（薄 Hub）
├── shared/
│   ├── contracts.md                      # 跨阶段共享契约（核心硬约束 / crop_path 解析 / 字段速查表 / state schema）
│   └── adjudication-tables.md            # 机械判定表（Precompute 工具规格 + FAIL 仲裁；上游 ui-doc-builder 有参考图路径必读）
├── stages/
│   ├── 01-prompt-build/SKILL.md           # 路径判定 + prompt 构建
│   ├── 02-generate/SKILL.md               # 分组 + 反射调用 + 异步等待
│   └── 03-postprocess/SKILL.md            # 后处理 + 验证 + 输出
├── references/                           # 归属见上方「Stage References 矩阵」
│   ├── prompt-engineering.md
│   ├── text-to-image-prompt-rules.md
│   └── sprite_sheet_prompt_rules.md
└── scripts/                            # 图像算法脚本（{PYTHON} 运行）
    ├── segment_control_layers.py          # Stage 02：功能控件透明 PNG 像素级图层切分（cv2/numpy）
    └── tests/test_segment_control_layers.py
```

> **部署型工具索引**（`cn.tuanjie.create.ui` 包 `Editor/UIBuildTools`，均经 `execute_csharp_script` 一行调用，数据从 `image_requirements.json` 自动读取；marker PASS 即成功，FAIL 才按日志排查）：
> - `Create.GameFramework.TJImageGenPipeline.Precompute(gameName)` — Stage 01 路径/预设尺寸/宽高比/is_segmentation 机械判定（`[PRECOMPUTE_CHECK]`）
> - `Create.GameFramework.TJImageGenPipeline.PlanSubmissions(gameName)` — Stage 02 分组/恢复预检/提交计划（`[IMG_RESUME_CHECK]` / `[PLAN_CHECK]`）
> - `Create.GameFramework.TJImageGenPipeline.BuildResult(gameName)` — Stage 03 下载完整性核对 + image_generation_result.json 组装（`[RESULT_CHECK]`）
> - `Create.GameFramework.TJImagePostprocess.DeployDirectUse(gameName)` — Stage 03 前置：Path A direct_use 裁剪图落位
> - `Create.GameFramework.TJImagePostprocess.SliceSpriteSheets(gameName)` / `.ScaleVerify(gameName)` / `.SliceComposites(gameName, groups, orientations)` — Stage 03 切片 / 缩放验证 / 组合切片
> - `Create.GameFramework.TJStateHelper.Show(stateFile)` / `.SetStage(...)` / `.SetTask(...)` — 全阶段 state 文件 typed 一行调用（禁止手改 JSON）
>
> ui-panel-builder 的验证工具（`TJValidateAssets` 等）与 ui-doc-builder 的 `TJBuildImageRequirements`/`TJDocChecks` 见各自模块文档。
