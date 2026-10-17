---
stage: 02-generate
required_skills: []  # 纯 MCP 提交 + shell 下载，无 Editor 写操作（显式声明，防止臆测）
input: image_requirements.json
output: 图片下载到 Assets/Games/{GameName}/UI/Images/ + 更新 ui-image-generator-state.json
reads: shared/contracts.md + 本 SKILL.md + image_requirements.json + modules/shared/references/mcp-image-generation.md（§1 调用参数 / §4 提交与并发 / §5 等待与收取 / §7 错误处理，执行前必读）
runs: Create.GameFramework.TJImageGenPipeline.PlanSubmissions("{GameName}")（marker [IMG_RESUME_CHECK]/[PLAN_CHECK]）+ TJStateHelper.SetTask/SetStage（state 更新）——包 Editor/UIBuildTools，execute_csharp_script 一行调用；只调用不读源码，FAIL 时才按日志排查
---

# Stage 02：提交生成任务（TJGenerators MCP）

读取 `image_requirements.json`，按路径分组预处理，通过 TJGenerators MCP 的 `generate_image` 提交生成任务，后台 waiter 等待完成后将图片下载到最终路径。

> **执行前必读** `modules/shared/references/mcp-image-generation.md`（§1 调用参数、§4 提交与并发、§5 等待与收取、§7 错误处理表）——完整工作流（上传/提交/等待/收取/下载/恢复）以该文件为权威源。本模块硬约束与 state schema 见 `shared/contracts.md`。

> **前置确认**：`image_requirements.json` 自检必须已输出 `[IMAGE_REQ_CHECK] PASS`（由 ui-doc-builder Step 3 执行：无参考图路径由工具内置自检；有参考图路径 §3.2.5 手动自检）。若未执行或未通过，禁止开始生成。

## 2.0 预处理：reuse_from + 功能控件子部件参考图 + sprite sheet 分组

**先运行部署型工具 `Create.GameFramework.TJImageGenPipeline.PlanSubmissions("{GameName}")`**（包 `Editor/UIBuildTools`，`execute_csharp_script` 一行调用）：

```
Create.GameFramework.TJImageGenPipeline.PlanSubmissions("{GameName}");
```

工具读 `image_requirements.json` + `ui-image-generator-state.json`，机械完成本节与 §2.0.1 的全部分组与预检，输出：
- 分组结果：reuse_from 排序（代表组件先行）、sprite sheet 组合并任务、图生图/文生图判定、批次划分（在途 ≤3）
- §2.0.1 恢复预检分类与 `[IMG_RESUME_CHECK] total=X, valid=Y, redownload=Z, resubmit=M` 日志
- 逐任务清单（任务名 → prompt / size / is_segmentation / 是否需上传参考图 / 恢复分类）

AI 仅负责：图生图任务的 `square_input.png` 预处理与上传（按 mcp-image-generation.md §2）、MCP `generate_image` 调用、waiter 派发与收取、下载与 state 更新。**state 更新一律用 `TJStateHelper`**（见 §2.1），禁止手改 JSON。以下规则文本为工具行为的规格说明（工具 FAIL 或结果存疑时的人工裁决依据）：

**reuse_from 排序**：将 `reuse_from` 条目排到代表组件之后（代表组件必须先完成生成）。`reuse_from` 条目不提交生成、不复制文件——Stage 03 跳过它们，USS 直接引用代表组件的图片路径。

**功能控件子部件参考图预处理（`functional_type` 非空、`crop_path` 为正常路径且 `visible_in_reference: true`）**：子部件走 Path C sprite sheet（`sprite_sheet_group` 非空时）或 Path D img2img（回退）。参考图为透明 PNG（`final_transparent.png`），需补为正方形画布：以 `max(w,h)` 为边长，内容居中，周围填透明像素，输出 `square_input.png`，作为图生图的上传输入。

**功能控件子部件 Sprite Sheet（`functional_type` 非空、`sprite_sheet_group` 非空）**：同一父组件的子部件已在上游分入同一 `sprite_sheet_group`，合并为 1 个生成任务，走 Path C。`crop_path` 为正常路径时走 img2img（传入父级透明 PNG），`crop_path` 为 `text2img_fallback` 时走文生图。与普通 sprite sheet 任务同等处理。

**Sprite Sheet 分组（`sprite_sheet_group` 非空）**：同一 `sprite_sheet_group` 的 N 个成员合并为 1 个生成任务（默认路径，img2img 或文生图取决于 `crop_path`）。读取 `sprite_sheet_orientation`（`"row"`/`"column"`），构建图集任务。

## 2.0.1 恢复预检（由 TJImageGenPipeline.PlanSubmissions 自动完成）：验证已完成任务的实际文件存在性

> 当 Stage 02 状态为 `in_progress`（恢复场景）时，state 文件中的 `02-generate.tasks` 映射是恢复依据。

**`02-generate.tasks` 字段 schema**（key 为 `image_requirements.json` 的 `name`；sprite sheet 任务用 `sprite_sheet_group` 名）：

```json
{
  "bg-mainmenu": {
    "task_id": "img_1_abc123",
    "status": "completed",
    "output_url": "https://cdn.tuanjie.cn/gen/img_1_abc123.png",
    "result_path": "Assets/Games/{GameName}/UI/Images/bg-mainmenu.png"
  },
  "art-title": {
    "task_id": "img_2_def456",
    "status": "submitted",
    "output_url": null,
    "result_path": null
  }
}
```

- `task_id`：MCP `generate_image` 返回的任务 ID（溯源与 `check_task` 查询依据）
- `status`：`submitted` → `completed` / `failed`
- `output_url`：`check_task` 返回的图片 CDN URL（**永久有效**，供恢复时重新下载）
- `result_path`：**下载后**的最终本地路径（`Assets/Games/{GameName}/UI/Images/{name}.png`；sprite sheet 用 `sprite_sheet_group` 名）

在分组完成后、提交任何新任务前，执行以下预检：

1. 读取 `ui-image-generator-state.json` 的 `02-generate.tasks` 映射
2. 对每个 `status == "completed"` 的条目，检查 `result_path` 指向的文件是否存在（`result_path` 是项目内相对路径，拼接项目根目录后做存在性检查）
3. 文件存在 → 保留该条目，视为有效成果，跳过提交
4. **文件不存在但 `output_url` 非空 → 标记为 `redownload`**，直接重新下载（CDN URL 永久有效），不重新生成
5. 无 `task_id` 或任务 `failed` 或文件缺失且无 `output_url` → 标记为 `resubmit`，与未提交的新任务同等处理
6. `tasks` 中不存在的 `name`（新图片或首次提交）→ 正常提交
7. 输出预检日志：`[IMG_RESUME_CHECK] total=X, valid=Y, redownload=Z, resubmit=M, redownload_list=[...], resubmit_list=[...]`

> ⚠️ **禁止通过对比 prompt 文本猜测任务与图片的对应关系**。多张图片可能共享相同 prompt（如不同面板的全屏背景），prompt 匹配不可靠。必须以 state 文件的 `tasks` 映射为准。
>
> ⚠️ **禁止信任任务状态而不验证文件**。用户可能手动删除 `Assets/Games/{GameName}/UI/Images/` 中的图片文件，导致 state 记录与磁盘实际状态不一致。

<HARD-GATE>
## 2.0.2 提交模式：批次 ≤3 + state 视角在途检查

> ⚠️ 完整并发规则、上传/提交/等待/下载模板以 `modules/shared/references/mcp-image-generation.md` 为权威源。

分组完成后，按批次提交任务，每批次在途 ≤3：

1. **统计总任务数**：Sprite Sheet 分组后的任务总数（同 `sprite_sheet_group` 合并为 1 个，`reuse_from` 不提交）
2. **逐个提交循环**：state 在途数 <3 时逐个调用 `generate_image`（`provider: seedream`；每次调用即 1 次提交；图生图任务提交前先按 mcp-image-generation.md §2 上传参考图拿 `file_url`）→ 每次提交后**立即**将 `task_id` + `status: "submitted"` 写入 state → 在途 ≥3 时等待已有 waiter 返回终态再继续提交
3. **所有任务已提交并收取完成** → 进入「完成后」收尾

⛔ 提交遇 `TaskLimitExceeded` 时禁止立即重试——`Start-Sleep 60` 后回到在途检查再重提（最多 3 次）。

> 恢复场景下，§2.0.1 预检标记为 `resubmit` 的任务需要重新提交，与未提交的新任务同等处理；`redownload` 条目只重新下载，不重新生成。
</HARD-GATE>

## 2.1 调用 generate_image（MCP）

通过 TJGenerators MCP 的 `generate_image` 工具提交。参数速查见 `modules/shared/references/mcp-image-generation.md` §1，完整工作流见该文件。

构建参数时：
- `provider` 固定 `"seedream"`
- `prompt` 从 `image_requirements.json` 读取（**Stage 01 已按公式构建并覆盖写入**，禁止使用 null 或上游预写值；≤1024 字符）
- `image_size` 从 `image_requirements.json` 的 `size` 读取并转换为 `{"width":W,"height":H}`
- `is_segmentation` 从 `image_requirements.json` 读取（Stage 01 已按判定表确定）
- 图生图时传 `image_urls`（参考图/裁剪区域先按 mcp-image-generation.md §2 内容指纹上传为 CDN URL）

**提交后必须立即更新 state 文件**：将 `task_id` 和 `status: "submitted"` 写入 `ui-image-generator-state.json` 的 `02-generate.tasks.{name}` 中（`name` 为 `image_requirements.json` 的图片名；sprite sheet 任务用 `sprite_sheet_group` 名）。每次收取后也更新 `status`、`output_url` 和 `result_path`。字段说明见本文件 §2.0.1。

> **state 更新一律用 `TJStateHelper`（禁止手改 JSON）**：
> - 提交后：`Create.GameFramework.TJStateHelper.SetTask("<state文件>", "02-generate", "<任务名>", taskId: "<id>", status: "submitted");`
> - 收取后：`... SetTask("<state文件>", "02-generate", "<任务名>", status: "completed", outputUrl: "<url>", resultPath: "<路径>");`（失败任务用 `status: "failed", error: "<原因>"`）
> - 阶段状态：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "<阶段名>", "<status>"[, next: new[]{"<阶段名>","<status>"}]);`

提交后按 §2.2 派发等待。

## 2.2 等待、收取与下载（waiter 模式）

`generate_image` 是异步 API（30-90 秒），提交后为每个任务派 1 个后台 waiter task 并行等待：

1. 每提交 1 个任务，立即按 `modules/shared/references/mcp-image-generation.md` §5.1 的 waiter task 模板派发 1 个后台 task（传入 `task_id` + 工具返回的 `poll_command_powershell` 全文）
2. waiter 完成通知到达后读取结果：`status == "completed"` → 用 `output_url` 下载到 `Assets/Games/{GameName}/UI/Images/{name}.png`（sprite sheet 用 `sprite_sheet_group` 名），校验文件存在且非零字节，**更新 state**：`status = "completed"` + `output_url` + `result_path = 下载后路径`
3. `status == "failed"` → 记录 `error`，**更新 state** `status = "failed"`，按错误处理表处理
4. waiter 异常/超时 → 直接 `check_task(task_id)` 一次取当前终态；仍在途则再执行一次 poll 命令（累计 ≤3 轮）；**禁止在确认 `failed` 前重新提交**（重复生成浪费积分）
5. 全部任务收取完成 → 进入「完成后」收尾。**不执行全局 `AssetDatabase.Refresh()`**（unity-operator 横切红线）——原始 PNG 无需提前导入：Stage 03 工具按文件系统读取切片/缩放，并在处理每个资产时逐资产 `ImportAsset`/`SaveAndReimport`

**禁止**：在 waiter 未返回前循环调用 `check_task`；对已提交任务再次调用 `generate_image`。

若执行环境不支持后台 task 完成通知，按 `mcp-image-generation.md` §5.3 降级为前台串行模式：逐个 `run_shell_command "<poll_command_powershell>"`（阻塞至终态）→ `check_task` 一次 → 下载 → 更新 state。

## 2.3 失败处理

- **提交失败**（TaskLimitExceeded / RATE_LIMITED / NETWORK_ERROR / SERVER_ERROR）：按 `modules/shared/references/mcp-image-generation.md` §7 错误处理表重试；`TaskLimitExceeded` 必须 `Start-Sleep 60` 后在途 <3 再重试（≤3 次）
- **生成失败**（check_task 返回 failed）：记录 error，标记该条目 failed，继续处理其余；1 个失败不影响其他
- **下载失败/空文件**：重试下载 ≤2 次；仍失败标记 failed，不阻塞其他图片
- Sprite Sheet 成员不单独提交（已合并为虚拟图集任务）

## 完成后

1. 所有生成任务已收取完成（completed 图片已下载到 `Assets/Games/{GameName}/UI/Images/` 且 state 已更新；失败任务已标记 failed）
2. 无需全局导入——资产导入由 Stage 03 工具逐资产完成（见 §2.2 第 5 条）
3. 更新 `ui-image-generator-state.json`（用 `Create.GameFramework.TJStateHelper.SetStage("<state文件>", ...)`）：
   - `02-generate` → `completed`
   - `03-postprocess` → `in_progress`
4. 调用方派发 Stage 03 subagent
