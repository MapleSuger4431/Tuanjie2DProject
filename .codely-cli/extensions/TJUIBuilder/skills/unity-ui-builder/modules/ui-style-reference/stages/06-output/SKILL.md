# Stage 06: 输出 UI参考图分析.md

Steps 10-11：preflight 门禁 → 输出 → 验证。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `component-tree.json`（完整树 + 所有图片产出） |
| 输出 | `UI参考图分析.md` |
| 前置门禁 | `pipeline-state.json` 中 `05-final-generation` = `completed` |
| 完成判据 | `preflight_check.py preflight` 退出码 0 + `UI参考图分析.md` 已写入 |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：目录布局、组件树字段、核心硬约束见 `shared/contracts.md`。

## 10. 输出 UI参考图分析.md

<HARD-GATE>输出前执行 `{PYTHON} scripts/preflight_check.py preflight {processingDir}/component-tree.json {processingDir}`，退出码非 0 则**禁止输出**。脚本检查：① quality 字段存在且非 MISSING；② composite 节点已标记 recursion_done（递归完成）；③ `direct_use`/`ai_generate` 节点的 `final_transparent.png` 存在且有 alpha 通道且透明像素占比 > 0；④ background 节点存在；⑤ img2img 溯源：background 节点和 `render_mode=shell_background` 的 composite shell 节点必须有非空 `img2img_provenance.task_id`；⑥ 面板级隔离溯源 `panel_provenance.step3_isolation.task_id` 非空。任一失败 → 脚本输出 `PRE-FLIGHT FAIL` 并返回非零退出码，回退到对应阶段。

preflight 通过后，额外计算 `content_coverage = sum(所有 valid 节点的 root_box 面积) / content_rect 面积`。若 `< 0.05` 则禁止输出——大概率是 OpenCV 漏检或 AI 错误标 skip。报警告并列出所有 `state=skip` 的节点，人工审查是否有误判。</HARD-GATE>

从 `component-tree.json` 写入 `UI参考图分析.md`。输出格式（组件裁剪映射表、面板布局表、文生图回退风格参数）**读** `references/output-format.md`。

## 11. 验证

- 确认 `UI参考图分析.md` 文件存在且可被下游 `ui-doc-builder` 读取
- 确认多面板场景下所有面板的组件树已合并到同一份 `UI参考图分析.md`

## 完成后

1. 更新 `pipeline-state.json`：
   - `06-output` → `completed`
   - `current_stage` → `completed`
2. 产出 `UI参考图分析.md`，从 `component-tree.json` 一次性生成；校正开启时附带 `layout_resolution` 审计元数据
3. 回流 `ui-doc-builder` Step 1/2
