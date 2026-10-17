# MCP AI 图片生成工作流（全模块共享权威源）

> 本文件是 TJUIBuilder 通过 **TJGenerators MCP Server** 进行 AI 图片生成的权威规范，覆盖上传、提交、等待、收取、下载、并发、错误处理与恢复的全流程。所有调用生成的模块（`ui-image-generator`、`ui-mockup-generator`、`ui-style-reference`）均以本文件为权威源。

## 0. 工具链总览

| 工具 | 用途 | 阻塞性 |
|------|------|--------|
| `file_upload` | 本地文件 → 预签名 PUT URL + 永久 CDN `file_url`（图生图输入用） | 同步返回 |
| `generate_image` | 提交生成任务，返回 `task_id` + 现成的 `poll_command_powershell` | 立即返回（异步任务） |
| `check_task` | 查询任务终态 + 输出 CDN URL | 单次调用 |
| `run_shell_command` | 执行 `poll_command_powershell`（阻塞至任务终态）、`curl` 上传/下载 | 阻塞 |

生成阶段不经过 Unity，不要求 Unity Editor 在线。下载目标位于 `Assets/` 下时，**不执行全局 `AssetDatabase.Refresh()`**（unity-operator 横切红线）——资产导入由 ui-image-generator Stage 03 的部署型工具在处理每个资产时逐资产 `ImportAsset`/`SaveAndReimport` 完成；生成阶段无需任何导入操作。

> ⚠️ **PowerShell 宿主环境注记（适用于本文所有 PS 片段）**：Windows PowerShell 5.1 **不支持 `&&` 分隔符**（用 `;`）；宿主 `run_shell_command` 的内联命令中 **`$` 变量会被外层 shell 吞掉**——含 `$` 变量的命令一律写成临时 `.ps1` 文件用 `-File` 执行。

## 1. 调用参数

| 参数 | 说明 |
|------|------|
| `provider` | `ui-style-reference` 用 `"seedream_pro"`；`ui-image-generator` / `ui-mockup-generator` 用 `"seedream"` |
| `prompt` | ⚠️ **≤1024 字符**，超长返回 `INVALID_PARAMS` |
| `image_size` | `{"width":W,"height":H}`，从 `"WxH"` 字符串转换 |
| `image_urls` | 图生图必填：参考图的 CDN `file_url`（先走 §2 上传流程）；文生图省略 |
| `is_segmentation` | true=透明背景 |
| `confirm_cost` | 仅 `provider: "seedream_pro"` 时需要，固定 `true` |

`image_size` 同时接受预设字符串（`square_hd` 等）与自定义宽高 JSON；TJUIBuilder 统一使用自定义宽高 JSON。尺寸白名单已实测（seedream_pro + seedream）：1K/2K 档全部可用，4K 被拒绝；被拒时换宽高比最接近的已验证档位并记录。seedream Lite 对 <2048 的请求会自动上采样到 2048（如请求 1024×1024 实际输出 2048×2048），不影响业务（Stage 03 trim → stretch 会处理到目标尺寸）。

## 2. 图生图输入上传流程（img2img 必经）

本地参考图（`source_square.png`、`text_removed_2048.png`、`input-page-{P}.png`、节点透明 PNG、`square_input.png` 等）必须先上传为 CDN URL 才能作为 `image_urls` 传入。

### 2.1 内容指纹暂存副本（强制）

`file_upload` 按 **file_path** 去重：同一路径再次上传时即使内容已变也会返回 `already_uploaded: true` 和**旧内容**的 URL。因此禁止直接上传会被覆盖重写的业务文件，必须先复制为**内容指纹副本**：

```powershell
$src = "<源文件绝对路径>"
$hash = (Get-FileHash -Algorithm SHA256 $src).Hash.Substring(0, 8)
$dst = "<projectRoot>/context/games/{GameName}/.uploads/<stem>_$hash<ext>"
New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
Copy-Item $src $dst -Force
Write-Output $dst
```

- 上传暂存目录：`context/games/{GameName}/.uploads/`（不存在则创建；整条流水线完成后清空）
- 内容相同的重复上传自然去重（同 hash → 同路径 → `already_uploaded: true` → 跳过 PUT，直接用 `file_url`）
- 内容变化后 hash 变化 → 新路径 → 全新上传，杜绝旧 URL 命中

### 2.2 上传三步

1. `file_upload(file_path = <指纹副本路径>)` → 返回 `{ upload_url, file_url, already_uploaded }`
2. `already_uploaded == false` 时：`curl.exe -s -o NUL -w "%{http_code}" -X PUT -H "Content-Type: image/jpeg" -T "<指纹副本路径>" "<upload_url>"`，确认输出 `200`（⚠️ Windows PowerShell 下必须写 `curl.exe`——裸 `curl` 是 Invoke-WebRequest 的别名，参数不兼容；`upload_url` 1 小时过期，须立即上传；`already_uploaded == true` 时跳过本步）
3. 用 `file_url`（**不是** upload_url）作为 `generate_image` 的 `image_urls` 参数

## 3. provider 分配与 confirm_cost

| 模块 | provider | confirm_cost | 说明 |
|------|----------|-------------|------|
| ui-style-reference | `"seedream_pro"` | `true`（固定） | 复杂 img2img 隔离需高质量 |
| ui-image-generator | `"seedream"` | 不需要 | UI 资产生成 |
| ui-mockup-generator | `"seedream"` | 不需要 | 参考图生成 |

`seedream_pro` 为高成本模型，未携带 `confirm_cost: true` 时工具不创建任务。所有 `seedream_pro` 调用固定携带 `confirm_cost: true`，不需要 `ask_user` 预先确认。

## 4. 提交模式与并发守卫

服务端对生成任务的并发上限是**账户级**的（同账户其他会话/残留任务共享配额）。MCP 没有在途任务全量查询接口（无 `GetAllTasks` 等价物），因此：

| 项 | 值 | 说明 |
|----|-----|------|
| 服务端账户级上限 | 5 | 超限返回 `TaskLimitExceeded`（HTTP 500） |
| 安全并发数 | **3** | 留 2 个余量给服务端残留任务 / 其他会话 |
| 在途统计口径 | **state 文件** | 本流水线 `status ∈ {submitted, generating}` 的任务数（会话视角） |
| 提交模式 | **批次 ≤3 逐个提交** | 每次 `generate_image` 调用提交 1 个任务，批次内逐个调用 |

### 提交流程

1. **统计总任务数**（分组后的任务数：sprite sheet 组合并为 1 个，`reuse_from` 不提交）
2. **在途检查**：读 state 文件统计本流水线在途数；在途 ≥ 3 → 等待已有 waiter 返回终态后再提交
3. **在途 < 3** → 逐个调用 `generate_image`（每次调用即 1 次提交；返回 `task_id` + `poll_command_powershell`）
4. **提交后立即写 state**：`task_id` + `status: "submitted"`（禁止依赖内存或对话历史）
5. 每个已提交任务按 §5 派发等待；waiter 返回终态并完成下载后，在途数下降，继续提交下一批
6. 全部任务提交并收取完成 → 进入模块自身的后处理阶段

### TaskLimitExceeded 处理

提交失败且错误消息含 `TaskLimitExceeded` / 并发上限时：

1. 标记该任务 `retry_pending`（写入 state）
2. `run_shell_command "Start-Sleep -Seconds 60"`
3. 回到在途检查，在途 < 3 后重新提交（最多重试 3 次）

### ⛔ 禁止行为

- ⛔ **禁止跳过在途检查一次性提交全部任务**——服务端并发上限是账户级的
- ⛔ **禁止任务已提交后再次调用 `generate_image` 提交相同需求**——重复提交会生成多份相同图片，浪费积分；waiter 超时/异常 ≠ 任务失败，必须先 `check_task` 确认终态
- ⛔ **禁止用 prompt 文本对比去猜任务与图片的对应关系**——多张图可能共享相同 prompt；以 state 文件的 tasks 映射为准
- ⛔ **禁止伪造 task_id**——溯源字段必须来自 `generate_image` 的实际返回值

## 5. 等待与收取（waiter 模式）

### 5.1 后台 waiter task

stage subagent 提交任务后，每提交 1 个任务立即派发 **1 个后台 waiter task** 并行等待：

```
循环（在途 <3 时逐个提交）:
  1. 调用 generate_image → 获得 task_id + poll_command_powershell
  2. 立即写 state: tasks.{name} = { task_id, status: "submitted" }
  3. 派后台 waiter: task(run_in_background=true, subagent_name="general-purpose")
     waiter 内部: run_shell_command(poll_command_powershell) → check_task(task_id) → 返回 {status, output_url}

所有任务已提交后:
  4. 逐个收取 waiter 通知（<bg_task_done> 自动到达）
  5. 对每个 waiter 结果:
     - completed → curl.exe 下载到最终路径 → 校验非零字节 → 更新 state: status="completed", output_url, result_path
     - failed → 更新 state: status="failed" → 按错误表处理
  6. 在途数下降后继续提交下一批
  7. 全部收取完成 → 完成 stage（下载目标在 Assets/ 下时不做全局导入——由 Stage 03 部署型工具逐资产 ImportAsset）
```

### 5.2 waiter task 模板

```
task(
  subagent_name: "general-purpose",
  description: "等待图片生成任务完成 {task_id}",
  run_in_background: true,
  prompt: """
  你是 AI 生成任务等待器，只执行以下三步，禁止任何其他操作：

  1. 用 run_shell_command 执行以下 PowerShell 轮询命令（该命令会阻塞至任务到达终态后自行退出，只需执行一次，禁止中途打断）：
     <工具返回的 poll_command_powershell 全文>

  2. 轮询命令退出后，调用 check_task(task_id = "{task_id}") 恰好一次，读取任务状态与图片输出 URL。

  3. 按以下 JSON 返回结果（简洁，不要多余文字）：
     { "task_id": "{task_id}", "status": "<completed/failed>", "output_url": "<图片 CDN URL，失败则空串>", "error": "<失败原因，无则空串>" }

  ⛔ 禁止循环调用 check_task；禁止重新提交任何生成任务；禁止下载、移动或删除任何文件；禁止分析图片内容。
  """
)
```

### 5.3 收取与下载

对每个 waiter 返回的结果：

1. `status == "completed"` → 取 `output_url`，执行下载：
   ```powershell
   New-Item -ItemType Directory -Force (Split-Path "<目标路径>") | Out-Null
   curl.exe -sS -L -o "<目标本地路径>" "<output_url>"
   ```
2. **下载校验**：`File.Exists(<目标路径>)` 且 `(Get-Item <目标路径>).Length -gt 0`；不满足 → 重试下载（≤2 次），仍失败按错误表处理
3. **立即更新 state**：`status: "completed"`、`output_url`、`result_path = <目标本地路径>`
4. `status == "failed"` → 记录 `error`，`status: "failed"`，按 §7 错误表处理
5. **下载目标位于 `Assets/` 下时**：不执行全局 `AssetDatabase.Refresh()`（unity-operator 横切红线）——由 ui-image-generator Stage 03 部署型工具逐资产 `ImportAsset`/`SaveAndReimport` 导入；下载到 `context/` 下的无需 Unity 参与
6. **上传暂存目录清理**：模块流水线全部完成后清空 `context/games/{GameName}/.uploads/`

### 5.4 降级模式（前台串行）

若执行环境不支持后台 task 完成通知（waiter 结果无法回注，subagent 语境下常见），stage subagent 退化为逐个前台执行：`run_shell_command "<poll_command_powershell>"`（阻塞至该任务终态）→ `check_task` 一次 → 下载 → 更新 state，然后处理下一个任务。提交侧的批次 ≤3 与在途检查规则不变。

> ⚠️ **poll_command_powershell 已知问题（2026-09 实测）**：MCP 返回的 poll 脚本使用函数名 `Try`，与 PowerShell 保留字 `try` 冲突导致语法错误。执行前把函数名（定义与调用处）替换为非保留字（如 `Invoke-PollTask`），或改用语义等价的轮询循环：立即查询一次 → 每 10 秒查询一次 → 到达终态（completed/failed）即退出（上限 180 次）。

### waiter 超时/异常

waiter 未返回、超时或返回异常时：直接 `check_task(task_id)` 一次取当前终态；仍在途 → 重新执行一次 poll 命令（累计 ≤3 轮）；确认 `failed` 才按错误表处理。**waiter 异常不构成重新提交的依据**。

## 6. 恢复规则（state 文件为唯一恢复依据）

恢复场景（state 中阶段状态为 `in_progress`）下，提交任何新任务前必须执行预检：

1. 读取 state 文件的 tasks 映射（禁止信任任何任务追踪器的 completed 状态而不验证文件）
2. `result_path` 文件存在 → 有效成果，跳过
3. **文件缺失但 `output_url` 存在 → 直接重新下载**（CDN URL 永久有效），不重新生成
4. 无 `task_id`、或任务 `failed` → 重新提交（与未提交的新任务同等处理）
5. state tasks 映射中不存在的 `name` → 正常提交
6. 输出预检日志：`[IMG_RESUME_CHECK] total=X, valid=Y, redownload=Z, resubmit=M`

## 7. 错误处理表

| 错误 | 含义 | 处理 |
|------|------|------|
| `TaskLimitExceeded`（HTTP 500 / 并发上限） | 账户级在途超限 | Start-Sleep 60 → 在途 <3 后重试，最多 3 次 |
| `RATE_LIMITED` | 请求频率过高 | 等待 30 秒后重试，最多 3 次 |
| `NETWORK_ERROR` / `SERVER_ERROR` | 网络/服务器错误 | 重试，最多 3 次 |
| `INVALID_PARAMS` | 参数错误 | 检查 prompt（≤1024 字符，超长需压缩去重保语义）与 image_size JSON 格式 |
| 积分/配额不足 | 账户积分耗尽 | `ask_user` 告知并停止生成阶段 |
| MCP 工具不可用 | TJGenerators MCP 未配置/不可达 | 提示在 settings.json 配置 `mcpServers.TJGenerators`（`httpUrl: https://ai-generator.tuanjie.cn/mcp`）并重启会话 |
| 下载失败/文件为空 | CDN 下载异常 | 重试下载 ≤2 次；仍失败标记 failed，不阻塞其他图片 |

| 场景 | 最大重试次数 |
|------|-------------|
| RATE_LIMITED / NETWORK_ERROR / SERVER_ERROR | 3 次 |
| TaskLimitExceeded | 3 次 |
| 生成超时（waiter 3 轮 poll 后仍在途） | 1 次（check_task 确认终态后决定，禁止盲目重提） |
| 缺陷检测失败（ui-image-generator Stage 03 §3.3，仅对两路检测确证的缺陷计数重试） | 2 次（含原始生成共 3 次；误报经像素扫描核验后放行，不消耗次数） |

## 8. 溯源记录（img2img / panel_provenance）

- 每次生成调用完成后，将返回的 `task_id` 与 `"seedream_pro"` 写入节点级 `img2img_provenance: {task_id, generator_id, step}` 或面板级 `pipeline-state.json` / `component-tree.json` 的 `panel_provenance` 字段
- `generator_id` 字段名保留（schema 兼容），值统一为 `"seedream_pro"`（MCP provider 标识）
- `task_id` 必须来自 `generate_image` 的实际返回值；缺失 `task_id` 的节点在 preflight 中会被判定为「未经过生成」，阻止输出
- 已记录 `task_id` 的任务禁止再次提交（防重复生成，浪费积分）
