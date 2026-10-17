# 共享契约（所有阶段共用）

本文件定义跨阶段共享的硬约束、数据规则和执行规则——仅含**全阶段通用**内容；Stage 专属内容分置于：机械判定表（`shared/adjudication-tables.md`，Precompute 工具规格与 FAIL 仲裁）、MCP 生成工作流（`modules/shared/references/mcp-image-generation.md`，全模块共享权威源）、`02-generate.tasks` 恢复规则（`stages/02-generate/SKILL.md` §2.0.1）。

每个阶段的 `stages/XX-xxx/SKILL.md` 均依赖本文件——执行任何阶段前应先读取本文件。

## 核心硬约束

1. **所有参数从 image_requirements 读取**（由 `ui-doc-builder` Step 3 从 `UI设计文档.md` 构建），禁止写死颜色、尺寸、风格。图片需求优先来自 Brick 的 `assetPropsSchema`（如 `backgroundImage`、`iconAsset`）
2. **图片不含文字**：图片仅作背景/图标，文字由 UXML 显示。图生图 prompt 也须根据 `text_type` 追加 `text_suffix`（读 `references/prompt-engineering.md` §2.1），只要不是 `art_text` 就追加 `, no text`。`art_text` 拆分文字层走文生图，prompt 须包含 `display_text` 实际文字 + `art_text_style` 风格 + 多行排版指引（读 `references/text-to-image-prompt-rules.md` §6）
3. **所有图片保存到 `Assets/Games/{GameName}/UI/Images/`**
4. **双模式生成**：有参考图时用图生图（`image_urls`，先上传为 CDN URL），无参考图时用文生图。图生图 prompt 只写"这是什么"不写"它长什么样"，风格由参考图决定。路径判定见 `shared/adjudication-tables.md`「权威路径判定表」（由 Precompute 工具机械执行）
5. **`is_segmentation`**：透明背景判定，以 `shared/adjudication-tables.md` 判定表为权威定义（上游 `false` 非全屏背景时会被覆盖为 `true`）
6. **后处理不可跳过**：API 预设尺寸与 UI 目标尺寸不匹配，跳过会导致 GPU 显存浪费和视觉伪影
7. **生成是异步的、MCP 提交 + waiter 等待驱动的**：`generate_image`（TJGenerators MCP）提交后立即返回 `task_id` 和现成的 `poll_command_powershell` 轮询命令。每个任务派 1 个后台 waiter task 执行 poll 命令（阻塞至终态）→ `check_task(task_id)` 一次收取 CDN `output_url` → `curl.exe` 下载到最终路径。生成阶段不经过 Unity。完整规则见 `modules/shared/references/mcp-image-generation.md`
8. **通过 MCP `generate_image` 工具调用**（`provider: seedream`，非高成本模型，无需 `confirm_cost`）：prompt ≤1024 字符。调用参数见 `modules/shared/references/mcp-image-generation.md` §1
9. **`analyze_multimedia` 失败处理**：API 超时/错误时最多重试 3 次。3 次仍失败则跳过当前验证步骤，标注 `verification_skipped: {reason}`，不阻塞后续流程
10. **不要假设「输出像素精确」**：seedream 实际返回**可能上采样**（如请求 `1568x672` 返回 `3136x1344`，约为请求的 2 倍），且部分返回为 **JPEG 编码字节但按 `.png` 扩展名落盘**（Unity `ImageConversion.LoadImage` 可正常解码）。最终尺寸/格式以 Stage 03 的 `ScaleVerify`（trim 透明边框 → 拉伸到目标尺寸）为准，JPEG 落盘图由 Stage 03 统一转码为真 PNG。API 预设尺寸白名单见 `shared/adjudication-tables.md`「API 预设尺寸选择」

### crop_path 路径解析

`crop_path` 字段使用**相对 game context 路径**（如 `UIReferences/Processing/MyMenu/nodes/component-001/final_transparent.png`）。解析为绝对路径时：

1. 前缀拼接：`{projectRoot}/context/games/{GameName}/{crop_path}`（`projectRoot` = `Application.dataPath` 的父目录）
2. `crop_path` 为 `skip_generation` / `text2img_fallback` / `reuse_from:xxx` 时不解析为文件路径（是语义标记）

## image_requirements.json 字段速查表

**必填字段（所有条目）**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 图片名，用于文件命名。全局唯一 |
| `source` | enum | `ai_generate` / `direct_use` / `uss_only` / `user_import` |
| `component_type` | enum | `button`/`icon`/`background`/`panel`/`bar`/`title`/`badge`/`hud_element`/`display`/`toggle`/`container`/`game_content_area`/`custom`/`label` |
| `panel_name` | string | 所属面板名（如 `StartMenu`、`HUD`） |
| `panel_type` | string | 面板类型（如 `full_screen`、`hud`、`popup`） |
| `uxml_element` | string | 对应 UXML 元素的 `name` 属性值 |

**条件必填字段**：

| 字段 | 条件 | 类型 | 说明 |
|------|------|------|------|
| `crop_path` | `source` 为 `ai_generate` 或 `direct_use` | string | 图生图参考路径 / `skip_generation` / `text2img_fallback` / `reuse_from:xxx` |
| `is_segmentation` | `source` 为 `ai_generate` 或 `direct_use` | bool\|null | `true`=透明背景，`false`=不透明 |
| `width` | 生成类 | int | 目标宽度（px），从 UI设计文档.md 提取 |
| `height` | 生成类 | int | 目标高度（px），从 UI设计文档.md 提取 |
| `text_type` | 生成类 | enum | `art_text`/`label`/`none`。默认 `none` |
| `display_text` | `text_type=art_text` | string | 艺术字文字内容 |
| `art_text_style` | `text_type=art_text` | string | 艺术字风格描述 |
| `prompt` | 生成类 | string | AI 生成提示词（Stage 01 构建） |
| `functional_type` | 有 §12 或无参考图扫描时 | enum\|null | `slider`/`toggle`/`dropdown`/`text_field`/`progress_bar`/`other`/`null`。无 §12 时由 ui-doc-builder §3.2 扫描 UXML 元素类型产出 |
| `sprite_sheet_group` | `functional_type` 非空或 `crop_path` 为 `text2img_fallback` 或正常文件路径 | string\|null | Sprite Sheet 分组名。同一父组件的子部件由 ui-doc-builder Step 3（无参考图路径由工具内置；有参考图路径 §3.2.4 手动执行）分入同一组。为空时走 Path D 独立生成（回退）而非默认的 Path C |

## 机械判定表（指针）

路径判定表、`is_segmentation` 判定表、API 预设尺寸选择（含选择规则与极端宽高比回退）的规格权威在 `shared/adjudication-tables.md`——已由部署型工具 `TJImageGenPipeline.Precompute` 机械执行写入 JSON（`[PRECOMPUTE_CHECK]` 兜底）。Stage 01 正常流程无需读取；工具 FAIL、人工裁决或尺寸抽查核对时读取（见 Stage 01 SKILL.md `optional_reads`）。

## 生成调用与错误处理（指针）

MCP `generate_image` 的调用参数、提交与并发、等待与收取、错误处理统一见 `modules/shared/references/mcp-image-generation.md`（§1 调用参数、§4 提交与并发、§5 等待与收取、§7 错误处理）——该文件为全模块共享权威源。本模块特有的 `02-generate.tasks` 恢复规则见 `stages/02-generate/SKILL.md` §2.0.1。

## ui-image-generator-state.json Schema

> 保存在 `context/games/{GameName}/ui-image-generator/` 下。

```json
{
  "game_name": "{GameName}",
  "current_stage": "01-prompt-build",
  "stages": {
    "01-prompt-build": { "status": "pending" },
    "02-generate":    { "status": "pending", "tasks": {} },
    "03-postprocess":  { "status": "pending" }
  }
}
```

- `current_stage`：当前执行阶段。调用方恢复时读此字段确定从哪个 stage 继续
- `status`：`pending` → `in_progress` → `completed`（或 `failed`）
- 调用方在派发每个 stage 前将对应 stage 设为 `in_progress`，subagent 完成后设为 `completed` 并将下一 stage 设为 `in_progress`。**state 变更一律经 `TJStateHelper`（`Show`/`SetStage`/`SetTask`，包 `Editor/UIBuildTools`）typed 一行调用，禁止手改 JSON**；`SetStage` 的 `next` 参数在置下一阶段 `in_progress` 的同时自动更新 `current_stage`
- `image_requirements.json` 由 `ui-doc-builder` Step 3 产出，本模块从 Stage 01 开始消费

`02-generate.tasks` 字段（`task_id`/`status`/`output_url`/`result_path` 及恢复规则）的完整 schema 见 `stages/02-generate/SKILL.md` §2.0.1。
