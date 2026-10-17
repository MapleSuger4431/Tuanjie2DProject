---
name: ui-style-reference
description: UI 风格参考提取模块。用户上传参考图或 AI 生成参考图（经 ui-mockup-generator 确认后移交）时，先用关键色图生图隔离 UI，再以 OpenCV 像素框作为唯一几何权威，递归拆分复合组件并输出组件树与 UI参考图分析.md。分成多个stage, 每1个 stage 委派1个独立的 subagent 执行。
---

# Unity UI 风格参考提取

> 本模块及其 stages/references 中的 `{PYTHON}` 沿用顶层 `unity-ui-builder/SKILL.md` 指向的 Creator 项目合同；执行阶段前仍须先读 `shared/contracts.md`。

从**参考图**（用户上传或 AI 生成）提取可复用的 UI 组件、层级和布局信息，输出 `context/games/{GameName}/UI参考图分析.md`。布局校正开启时在 Stage 04 之后、Stage 05 之前先用画板校正结构树，再生成图片与分析文档。

本模块包含 **6 个固定参考分析阶段 + 1 个条件式布局校正阶段（Stage 04.5）**。执行任何阶段前，先读 `shared/contracts.md` 获取共享硬约束、目录布局和数据规则，再读对应阶段的 `stages/XX-xxx/SKILL.md` 获取详细步骤。

⚠️ 禁止一次性阅读所有阶段的相关文档。

## 路由与边界

| 来源 | 处理者 | 本模块行为 |
|---|---|---|
| 用户上传参考图 | 本模块 | 调用方 采集参考图到 `UIReferences/` → 派发 Stage 01 |
| AI 效果图（ui-mockup-generator 确认后的参考图） | 本模块 | 调用方 复制参考图到 `UIReferences/` → 派发 Stage 01 |
| 无参考图 | `ui-doc-builder` | 不激活本模块 |

- 本模块不编写游戏 UI、不创建 UXML/USS、不搭建场景。

## 共享契约

> **执行任何阶段前必须读取** `shared/contracts.md`——包含核心硬约束（OpenCV 唯一几何权威、img2img 溯源、prompt 模板规则等）、目录布局、组件树字段合同表、布局字段 vs 生成字段所有权、state 与 source 映射规则、面板级溯源、图生图执行规则。

## 6+1 阶段流水线

```
Stage 01  图生图隔离            Steps 2-4    关键色补边→img2img 隔离(3步)→验证
  │
Stage 02  根级组件提取           Steps 5-6.3  OpenCV→语义分类→quality验证→建树
  │
Stage 03  预分组与去重           Steps 7-7.1  composite预分组 + leaf样式组去重
  │
Stage 04  递归构建组件树         Step 8+7.2   BFS逐层递归拆分composite；完成后检测快照
  │                                    ⚠️ 最耗时阶段；递归未完成禁止进入Stage 04.5/05
  │
Stage 04.5 布局校正解析（条件式） 仅校正开启
  │        ├─ 04.5A 调用 ui-layout-intent-designer → 等待确认 layout_intent.json
  │        └─ 04.5B 确定性 resolver → 最终 component-tree.json + 快照 + 审计
  │
Stage 05  最终图片生成           Steps 8.5-9  背景提取 + 叶子关键色裁剪 + composite shell
  │
Stage 06  输出                   Steps 10-11 preflight门禁→UI参考图分析.md（一次性从最终树生成）
  │
  ├─ 校正关闭 → Stage 04.5 skipped → Stage 05 → Stage 06 → ui-doc-builder
  └─ 校正开启 → Stage 04.5 → Stage 05 → Stage 06 → ui-doc-builder
```

| 阶段 | SKILL.md | 输入 | 输出 |
|------|----------|------|------|
| 01-isolation | `stages/01-isolation/SKILL.md` | `UIReferences/{Panel}.png` | `source_square.png`、`text_removed_2048.png`、`ui_only_2048.png`、`text_only_2048.png`、`transform.json`、`pipeline-state.json`（含 `panel_provenance`） |
| 02-root-extraction | `stages/02-root-extraction/SKILL.md` | 隔离图 + 纯文字图 + 原图补边 + `pipeline-state.json` | `component-tree.json`(depth=0, 含 `panel_provenance`)、`semanticRecords.json` |
| 03-pre-grouping | `stages/03-pre-grouping/SKILL.md` | `component-tree.json` + `semanticRecords.json` | 更新后的树（含 reuse 标记） |
| 04-recursive-extraction | `stages/04-recursive-extraction/SKILL.md` | 预分组后的树 + `root-extract/component_{NNN}.png`（depth=0 输入） | 完全展开的树 + `component-tree.original.json`（检测快照）+ `nodes/*/original_crop.png` + `slots/` 产物 |
| 04.5-layout-refinement | `stages/04.5-layout-refinement/SKILL.md` | 各面板 `component-tree.original.json`；本阶段调用布局 skill 取得已确认 layout intent | 最终 `component-tree.json`（resolver 输出）+ `component-tree.resolved.json`（快照）+ `resolved-ui-tree.json`（审计） |
| 05-final-generation | `stages/05-final-generation/SKILL.md` | 最终树（校正开启时含 `layout_resolution`）+ 隔离图 + 原图 + 裁剪图 | 所有节点产物：shell 节点为 `shell_transparent.png`，其余为 `final_transparent.png`；另含 `nodes/background/` |
| 06-output | `stages/06-output/SKILL.md` | 最终树 + 所有图片 | `UI参考图分析.md` |

## Stage References 矩阵

> Stage References 矩阵通用规则（必读 references 强制读取、禁止读取非本 stage 文件、subagent 自行读取矩阵）见主 SKILL.md「⛔ 阶段派发守卫」。

每个 stage 执行前**必须读取** `shared/contracts.md`（跨阶段共享契约）+ 对应阶段的 `stages/XX-xxx/SKILL.md`，然后按下表读取该 stage 的必读 references。

### Stage 01 必读 references

无。

### Stage 02 必读 references

| 文件 | 用途 |
|------|------|
| `references/opencv-recovery.md` | §1-§4 后验对比偏差检查方法、analyze_multimedia 验证 prompt、恢复策略 |
| `references/quality-and-semantics.md` | §1-§4 Quality 两步验证 prompt、kind 判定规则、碎片过滤 |

### Stage 03 必读 references

| 文件 | 用途 |
|------|------|
| `references/post-processing.md` | §5 样式组识别与去重规则（执行 Step 7.1 前必读） |

### Stage 04 必读 references

| 文件 | 用途 |
|------|------|
| `references/recursive-extraction.md` | §1-§8.7 全文。裁剪来源坐标系转换、图集装箱/拆图、OpenCV 验证、语义分类、失败补图 retry 规则 |

### Stage 04.5 必读 references

| 文件 | 用途 |
|------|------|
| `references/resolved-ui-tree-schema.md` | resolved tree 审计范围、最终树权威关系与 `--check` 门禁 |

### Stage 05 必读 references

| 文件 | 用途 |
|------|------|
| `references/final-generation.md` | §9.1-9.7 全文。处理配方路由、图集输入准备、拆图清理参数、配方 prompt 表 |
| `references/post-processing.md` | §3-§4 叶子节点处理配方、复合节点 shell 生成 |

### Stage 06 必读 references

| 文件 | 用途 |
|------|------|
| `references/output-format.md` | UI参考图分析.md 输出格式（组件裁剪映射表、面板布局表、文生图回退风格参数） |

## 派发前：参考图就位（调用方职责）

派发 Stage 01 前由调用方确保参考图已就位：

- **用户上传**：在聊天中直接输出文字提示（文字提示必须是"您可以拖拽图片到聊天框"）用户上传参考图（**禁止用 `ask_user`**），输出后**立即停止回合**。用户提供图片后保存到 `UIReferences/`。若已有 `UI参考图分析.md`，用 `ask_user` 询问"重新处理"或"停止并讨论"。
- **AI 生成参考图**：`ui-mockup-generator` 确认后由调用方复制到 `UIReferences/{PanelName}.png`，文件名 = 面板名。

⛔ **前置门禁**：派发 Stage 01 前必须确认 `UIReferences/` 下存在至少一张 `{PanelName}.{ext}` 文件。若为空则禁止派发。

`UIReferences/{PanelName}.{ext}` 就位后创建 `pipeline-state.json`，派发 Stage 01 subagent。

## 阶段路由规则

每个面板在 `Processing/{PanelName}/` 下维护 `pipeline-state.json`：

```json
{
  "panel": "{PanelName}",
  "current_stage": "01-isolation",
  "stages": {
    "01-isolation":            { "status": "in_progress" },
    "02-root-extraction":      { "status": "pending" },
    "03-pre-grouping":         { "status": "pending" },
    "04-recursive-extraction": { "status": "pending" },
    "04.5-layout-refinement":  { "status": "pending" },
    "05-final-generation":      { "status": "pending" },
    "06-output":               { "status": "pending" }
  },
  "panel_provenance": {
    "step3_isolation": null
  }
}
```

Stage 01 完成 img2img 隔离后将三个子步骤（3a `text_removal`、3b `background_removal`、3c `text_extraction`）各自的 `task_id` 和 `generator_id` 写入 `panel_provenance.step3_isolation`。`step3_isolation` 的字段为 `text_removal`、`background_removal`、`text_extraction`（权威全文见 `shared/contracts.md`）。

**每个阶段由 1 个 subagent 执行，该 subagent 并行处理所有面板**。调用方为每个阶段派发一个 subagent，传入 `shared/contracts.md` + `stages/{current_stage}/SKILL.md` + 该阶段的必读 references 路径及所有面板名。subagent 在同一阶段内并行处理所有面板，完成后逐面板更新 `pipeline-state.json`，调用方派发下一阶段。

> 阶段派发通用规则（禁止合并阶段、subagent 自行读取矩阵）见主 SKILL.md「⛔ 阶段派发守卫」。

**执行流程**：
1. 读取所有面板的 `pipeline-state.json`，确定 `current_stage`（多面板应同步推进）
2. 派发 1 个 subagent，传入 `shared/contracts.md`、`stages/{current_stage}/SKILL.md`、该阶段必读 references 路径及所有面板名
3. subagent 在该阶段内并行处理所有面板，完成后逐面板更新 `pipeline-state.json`（当前阶段 → `completed`，下一阶段 → `in_progress`）
4. 重复 1-3 直到所有面板的所有阶段进入终态：`completed` 或 `skipped`

**Stage 04 完成路由**（校正开关来自 `UI需求.md` 的「参考图布局校正」，由 `ui-doc-builder` Step 0.4 已落盘）：

- 校正关闭/字段缺失：`04.5-layout-refinement=skipped`，直接 `05-final-generation=in_progress`
- 校正开启：`04.5-layout-refinement=in_progress`（画板打开后进入 `waiting_for_layout`）
- **只有 `04.5-layout-refinement` 为 `completed` 或 `skipped` 后才能设置 `05-final-generation=in_progress`**

Stage 04.5 是条件式阶段：

- 校正关闭：`04.5-layout-refinement=skipped`，Stage 04 完成后直接进入 Stage 05
- 校正开启：Stage 04 完成后进入 Stage 04.5；由 Stage 04.5A 调用 `ui-layout-intent-designer` 获取已确认的 `layout_intent.json`，再由 Stage 04.5B 执行确定性 resolver 生成最终树
- Stage 04.5 的调用边界、等待、恢复、hash 门禁和状态转换只按 [`stages/04.5-layout-refinement/SKILL.md`](stages/04.5-layout-refinement/SKILL.md) 执行

**会话恢复**：读取所有面板的 `pipeline-state.json`，找到第一个 `in_progress` 或 `pending` 的阶段，从该阶段继续；`waiting_for_layout` 按 Stage 04.5 文档的「等待与恢复」处理，`skipped` 不执行。已完成阶段不重新执行——**但必须先校验该阶段的关键产出文件实际存在**（见下方「恢复文件校验」）。

**恢复文件校验**：对每个 `status == "completed"` 的阶段，检查该阶段的关键产出文件是否实际存在于磁盘。文件不存在说明该阶段被中断或产出被删除，须将该阶段回退为 `in_progress` 重新执行。

| 阶段 | 关键产出文件（缺失则回退） |
|------|--------------------------|
| `01-isolation` | `source_square.png`、`ui_only_2048.png`、`transform.json` |
| `02-root-extraction` | `component-tree.json` |
| `03-pre-grouping` | `component-tree.json`（含 `reuse_structure_from` / `style_group` 标记） |
| `04-recursive-extraction` | `component-tree.json` + `component-tree.original.json`（所有 composite 标记 `recursion_done` 或全部为 leaf） |
| `04.5-layout-refinement` | 各面板 `component-tree.resolved.json` + `component-tree.json`（含 `layout_resolution`）；仅校正开启，关闭时为 `skipped` |
| `05-final-generation` | 所有 `direct_use`/`ai_generate` 节点产物（shell 节点为 `shell_transparent.png`，其余为 `final_transparent.png`）、background 节点的 `final_transparent.png` |
| `06-output` | `UI参考图分析.md` |

> ⚠️ **禁止信任 `completed` 状态而不验证文件**。用户可能手动删除中间产物，导致下游阶段输入缺失。回退后该阶段重新执行，已有产出文件会被覆盖。

**`pipeline-state.json` 创建**：调用方在采集参考图后、派发 Stage 01 前创建，所有阶段初始化为 `pending`，`01-isolation` 设为 `in_progress`。

## 多面板并行处理规则

当 `UIReferences/` 下存在多张参考图时，各面板的流水线**完全独立**——各有独立的 `Processing/{PanelName}/` 目录、`pipeline-state.json`、`transform.json`、组件树，互不依赖。

每个阶段由 1 个 subagent 执行，该 subagent 在阶段内并行处理所有面板：

| 阶段 | 单 subagent 内并行方式 |
|------|---------|
| Stage 01 | Python 脚本和 `analyze_multimedia` 各面板并行；3a 与 3c 并行提交，3b 等 3a 完成后提交；跨面板的 img2img 逐个提交（在途数 < 3） |
| Stage 02 | OpenCV 提取和 `analyze_multimedia` 语义分类各面板并行 |
| Stage 03 | 预分组与去重各面板并行 |
| Stage 04 | 递归拆分各面板并行；图生图任务逐个提交（在途数 < 3）；完成后各面板复制检测快照 |
| Stage 04.5 | 等待一次多画布布局确认后，用确定性 resolver 一次处理所有面板（全成全不写）；不得调用 AI 合并数据 |
| Stage 05 | 背景提取 img2img 逐个提交所有面板；最终图片生成逐个提交（在途数 < 3） |
| Stage 06 | 合并所有面板的组件树到同一份 `UI参考图分析.md` |

**失败恢复**：subagent 逐面板更新 `pipeline-state.json`，若某面板失败则其 state 保持 `in_progress`，调用方仅对该面板重派一个小 subagent 补完该阶段。

## 交付

> 参考图（用户上传或 AI 生成）已按关键色图生图、OpenCV 组件树和递归层级处理完成。校正关闭时 Stage 04.5 跳过，直接由 Stage 05/06 产出 `UI参考图分析.md`；校正开启时先在 Stage 04.5 校正结构树，再由 Stage 05 生成图片、Stage 06 一次性从最终树生成 `UI参考图分析.md`，随后回流 `ui-doc-builder`。
