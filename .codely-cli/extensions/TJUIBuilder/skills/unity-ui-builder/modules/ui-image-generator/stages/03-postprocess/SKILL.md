---
stage: 03-postprocess
required_skills: [unity-operator]  # 切片/后处理/TextureImporter 经 execute_csharp_script 写资产
input: image_requirements.json + Images/ 中的生成图片
output: image_generation_result.json + Images/ 中的最终图片
reads: shared/contracts.md + 本 SKILL.md + image_requirements.json + references/prompt-engineering.md（§4 像素缺陷检测两路分工）+ references/sprite_sheet_prompt_rules.md（§5/§6 切片规则，仅 Path C 时查阅）+ modules/shared/references/mcp-image-generation.md（§5 等待与收取 / §7 错误处理，恢复与服务端卡死场景按需查阅）
runs: Create.GameFramework.TJImageGenPipeline.BuildResult("{GameName}")（marker [RESULT_CHECK]）+ TJImagePostprocess.DeployDirectUse/SliceSpriteSheets/ScaleVerify（marker [Verify]）+ TJStateHelper.SetStage（完成后 state 更新）——包 Editor/UIBuildTools，execute_csharp_script 一行调用；只调用不读源码，FAIL 时才按日志排查
---

# Stage 03：后处理、验证与输出

接收所有生成结果，执行下载完整性核对、切片、缺陷检测、缩放验证，输出最终交付报告。

> 等待与收取规则、错误处理表见 `modules/shared/references/mcp-image-generation.md` §5/§7（服务端卡死重提、redownload 场景按需查阅）；本模块硬约束与 state schema 见 `shared/contracts.md`。

> 当前 UI 图片任务只有在本阶段核对/缩放/Importer 完成、验证与清单回写完成后才算完成。CDN URL、`output_url` 或 `task_id` 都不是交付物。

## 3.1 下载完整性核对

**Path A direct_use 落位（前置）**：先运行部署型工具一行调用 `Create.GameFramework.TJImagePostprocess.DeployDirectUse("{GameName}");`——从各 `source == "direct_use"` 条目的 `crop_path`（game-context 相对路径，指向 `context/games/{GameName}/UIReferences/` 下裁剪图）复制到 `Assets/Games/{GameName}/UI/Images/{name}.png` 并设置 TextureImporter（幂等，覆盖重跑；functional_type/contaminated 的 direct_use 行已由 precompute 重路由到 C/D，工具自动跳过）。无 direct_use 条目时输出 no-op 日志，直接继续。FAIL（crop 源缺失/语义 crop_path）→ 修复上游 `crop_path` 后重跑。direct_use 条目的 trim/stretch 由 3.4 `ScaleVerify` 与其他条目统一处理。

图片已由 Stage 02 直接下载到 `Assets/Games/{GameName}/UI/Images/`（sprite sheet 文件名为 `sprite_sheet_group` 值）。**用部署型工具 `Create.GameFramework.TJImageGenPipeline.BuildResult("{GameName}");`（包 `Editor/UIBuildTools`，`execute_csharp_script` 一行调用）一次完成核对与结果组装**：

1. 通过 `execute_csharp_script` 一行调用：`Create.GameFramework.TJImageGenPipeline.BuildResult("{GameName}");`
2. 工具逐条核对每个生成条目的目标文件 `Assets/Games/{GameName}/UI/Images/{name}.png` 存在且非零字节；文件缺失但 state 的 `02-generate.tasks` 中有对应 `output_url` → 输出 `[DOWNLOAD_CHECK] redownload` 行（CDN URL 永久有效，直接重新下载，不重新生成）；无法恢复 → 计入 `failed_images`（`failure_reason: "download_missing"`）。**Path C 成员例外**：成员文件由本阶段切片产出，切片前不存在属正常——工具对「成员缺失但所在组原始 sheet 文件在场」的条目记 `pending_slice`（不算 failed、无红字），因此 3.1 首跑预期 `[RESULT_CHECK] PASS`（日志带 pending_slice 提示）；切片 + 缩放完成后**重跑 BuildResult 终检**，此时 `pending_slice` 应清零、全部成员进入 `generated_images`
3. 工具同时组装 §3.7 的 `image_generation_result.json`；确认日志末尾 `[RESULT_CHECK] PASS`

**`reuse_from` 条目跳过**：`crop_path` 为 `reuse_from:xxx` 的条目不核对、不复制——工具在 `image_generation_result.json` 中直接映射到代表组件的图片路径，USS 引用代表组件的 PNG 文件。

## 3.2 Sprite Sheet 切片（仅 Path C）

> 编号说明：本节编号（3.2 切片 / 3.3 检测）是文档结构序，不是执行序。Sprite sheet 的执行序 = 3.1 → 3.3（整图两路检测）→ 3.2（切片）→ 3.4；非图集图片跳过本节。

若存在 `sprite_sheet_group` 非空的条目：sprite sheet 先做整图缺陷检测（3.3），再用**部署型工具**切片（alpha 投影，非连通区域检测），一行调用：`Create.GameFramework.TJImagePostprocess.SliceSpriteSheets("{GameName}");`（分组/方向/元素顺序自动从 `image_requirements.json` 的 `sprite_sheet_group`/`sprite_sheet_slot`/`sprite_sheet_orientation` 读取，无需手填字典）。若分段数 < `sprite_sheet_total`（元素粘连），回退到逐张文生图（Path D）。回退时为每个元素按 `text-to-image-prompt-rules.md` §1 公式构建独立 prompt，使用按目标尺寸选择的 API 预设。

> ⚠️ **切片前 alpha 验证（强制）**：`SliceSpriteSheets` 工具在 `ImageConversion.LoadImage` 后会检查源纹理的最大 alpha 值。若 alpha 全为 0（文件不可读或损坏），工具会输出 `FAIL` 并跳过该 sheet，而非静默产出垃圾切片。切片后每个输出图片也会验证非透明像素占比，低于 1% 时标记 `FAIL`。发现 `FAIL` 条目时必须回退到 Path D 逐张文生图。

## 3.3 像素缺陷检测（两路分工）

按 `references/prompt-engineering.md` §4 执行两路检测——**两路各抓各的缺陷类、互不依赖**，路 B 的触发条件是"这张图属于什么类别"，不是"路 A 检出了什么"：

- **路 A：确定性像素扫描（每图必跑，先于路 B）**——对每张落位图（独立图 + sprite sheet 整图）按 §4.1 做像素统计，零视觉调用。确证（transparent_hole / white_base_showing 超阈值）直接进入下方重试阶梯；阈值边缘/疑似设计性镂空按 §4.1 仲裁句升级路 B 仲裁；未检出即该两类 pass。扫描数据记入执行报告的"误报核验记录"。
- **路 B：视觉检测（高风险类别选择性执行）**——按 §4.2 触发条件表决定是否对整图跑 1 次 `analyze_multimedia`（prompt 逐字使用 §4.3 模板，禁止自拟或借用旧图集版口径）；sprite sheet 对整图跑（属触发类别），切片后不再单独检测；同 `style_group` 第 2+ 张跳过并记入报告。路 B 初判的像素类缺陷（transparent_hole / white_base_showing）仍须路 A 扫描数据确证——不支持则按误报放行；`truncated_element` / `other` 允许凭初判进重试但须注明"未经像素确证"。

**缺陷处理（递进式重试，仅对两路确证的缺陷生效）**：第 1 次追加 `MISSING_PIXEL_PREVENTION` 关键词重试；第 2 次换 API 预设尺寸或调整 `is_segmentation` 重试。最多重试 2 次（不含原始生成）。**服务端卡死不计数**（waiter 多轮 poll 后仍在途属于服务端问题，经 `check_task` 确认终态后重新提交，最多额外 2 次）。2 次缺陷重试后仍确证缺陷 → 标记为 `failed_images`，`failure_reason: "pixel_defect_retry_exhausted"`。

与 contaminated 验证（`references/prompt-engineering.md` §2.6）的优先级：像素缺陷检测优先执行，两套重试机制互不计数。

## 3.4 图片尺寸校验与缩放

用**部署型工具**执行，一行调用：`Create.GameFramework.TJImagePostprocess.ScaleVerify("{GameName}");`（目标尺寸/`is_segmentation` 自动从 `image_requirements.json` 的 `width`/`height`/`is_segmentation` 读取，无需手填字典）。

统一缩放策略——所有图片走同一条路径：

1. **裁剪透明边框**：`TrimUtility.TrimTransparentBorders` 遍历 alpha 通道，`alpha > 0.01` 判定为内容，裁剪到内容 bounding box。对不透明图片（`is_segmentation: false`）此步是 no-op（所有像素 alpha = 1.0，bounding box = 整张图）
2. **拉伸到目标尺寸**：`TrimUtility.ScaleTexture` 将裁剪后图片直接缩放到 `width × height` 目标尺寸

`is_segmentation` 字段不再影响缩放逻辑，仅用于 TextureImporter 的 `alphaIsTransparency` 设置。

## 3.5 尺寸验证

缩放完成后必须验证所有图片的实际尺寸是否匹配目标。验证已合并入 `ScaleVerify` 工具（自动输出 `[Verify]` 行）。若存在 ❌ 条目，必须返回 3.4 重新执行缩放。

## 3.6 资产导入（逐资产，禁止全局 Refresh）

工具在处理每个资产时已逐资产 `ImportAsset`/`SaveAndReimport`（TextureImporter 设置随处理完成）——**不再执行全局 `AssetDatabase.Refresh()`**（unity-operator 横切红线；Stage 02 下载的原始 PNG 无需提前导入，本阶段工具按文件系统读取并逐资产导入）。本节无额外操作。

## 3.7 返回结果

`image_generation_result.json` 已由 3.1 的部署型工具 `TJImageGenPipeline.BuildResult` 产出（比对 `image_requirements` 与实际产出），写入 `context/games/{GameName}/image_generation_result.json`，供 `modules/ui-panel-builder` 步骤 1 消费：

```json
{
  "generated_images": [{"name": "btn_play", "path": "Assets/Games/{GameName}/UI/Images/btn_play.png", "panel_name": "MainMenu"}],
  "reused_images": [{"name": "btn_back", "reuse_from": "btn_primary", "path": "Assets/Games/{GameName}/UI/Images/btn-primary.png", "panel_name": "Settings"}],
  "uss_substitutes": [{"name": "panel_bg", "skip_reason": "developer_request"}],
  "failed_images": [{"name": "icon_coin", "type": "icon", "panel_name": "HUD", "failure_reason": "pixel_defect_retry_exhausted"}],
  "total_count": 15, "expected_count": 18, "failed_count": 3
}
```

`failed_count > 0` 时**必须用文字明确列出缺失图片名**。`failed_images` 包含所有未能成功生成的图片（超时、缺陷检测失败、API 错误、未被尝试等），§3.3 缺陷重试耗尽的图片必须列入。`reused_images` 列出所有 `reuse_from` 条目及其代表组件的图片路径，供 ui-panel-builder 直接在 USS 中引用代表组件图片。

## 完成后

1. 确认 `image_generation_result.json` 已写入
2. 用部署型工具更新 `ui-image-generator-state.json`（禁止手改 JSON）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "03-postprocess", "completed", current: "completed");`
3. 向调用方报告结果
