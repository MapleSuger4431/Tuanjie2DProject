# Stage 03: 复合节点预分组与叶子去重

Steps 7-7.1：composite 预分组（`reuse_structure_from`）+ leaf 样式组去重（`style_group`）。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `component-tree.json`（depth=0 根级节点，含 semantic）、`semanticRecords.json` |
| 输出 | 更新后的 `component-tree.json`（含 `reuse_structure_from` / `style_group` / `reuse_from` 标记） |
| 前置门禁 | `pipeline-state.json` 中 `02-root-extraction` = `completed` |
| 完成判据 | 所有 `kind=composite && state=valid` 已评估预分组；所有 `kind=leaf && state=valid && source != uss_only` 已评估去重 |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：目录布局、组件树字段、核心硬约束见 `shared/contracts.md`。

## 7. 复合节点预分组

对 `kind=composite && state=valid` 的根级节点按视觉相似度分组：
1. **分组条件**（全满足）：
   - `component_type` 相同
   - `visual_description` 中的底板/边框/整体形状描述一致（精确匹配底板关键词，如 `"dark semi-transparent horizontal bar"` vs `"white rounded rectangle"` → 不归组）
   - 仅 `display_text` / 数值不同
2. **代表选举**：每组选裁剪面积最大、置信度最高者。
3. **非代表**：标记 `reuse_structure_from:{代表组件名}`，不提交外壳图和子项隔离图。代表完成递归拆分后，非代表复制其子树结构，只替换 `root_box`、裁剪图和 `display_text`。**非代表的 shell 图片也复用代表**——`source` 保持 `ai_generate`，`crop_path` 设为 `reuse_from:{代表组件名}`（指向代表节点的 `final_transparent.png`），Stage 05 装箱时排除这些节点。下游 USS 用 `stretch-to-fill` + 九宫格 `spriteBorder` 适配尺寸差异。
4. **组命名**：`{component_type}_structure_{N}`。

> 仅 `kind=composite` 参与预分组；leaf 的 `style_group` 样式组归并在 Step 7.1 处理。

> ⚠️ **`reuse_structure_from` 与 Step 7.1 的 `crop_path=reuse_from:xxx` 是不同概念，禁止混淆**：
> - `reuse_structure_from`（本步骤）：composite 节点的**递归子树结构 + shell 图片**复用。非代表 composite 不生成独立 shell，直接引用代表的 `final_transparent.png`。此字段写入组件树的 `reuse_structure_from` 属性。非代表的 `source` 保持 `ai_generate`、`crop_path` 设为 `reuse_from:{代表组件名}`。下游 ui-image-generator 走 Path B 不生成。
> - `crop_path=reuse_from:xxx`（Step 7.1）：leaf 节点的**图片文件**复用。非代表 leaf 没有自己的图片，直接引用代表 leaf 的同一个 PNG 文件。此值写入 `crop_path` 字段（`source` 保持 `ai_generate`），输出到 UI参考图分析.md 的 `reuse_from` 列，**改变路径判定**——ui-image-generator 走 Path B 不生成。
> - 两者均通过 `crop_path=reuse_from:{name}` 触发 Path B，但 composite 的 `reuse_structure_from` 还包含子树结构复用（非代表复制代表的子节点层级）。

> ⚠️ **Stage 04 递归循环须排除 `reuse_structure_from:*` 标记的节点**：这些节点不参与图集装箱和图生图，它们的子树结构将在 Step 7.2 从代表节点复制。在递归循环条件"收集所有 `kind=composite && state=valid && 未标记 recursion_done` 的节点"中，须额外排除 `reuse_structure_from:*`。

## 7.1 叶子节点样式组去重

对 `kind=leaf && state=valid && source != uss_only` 的节点按视觉相似度分组，标记 `style_group` 和 `reuse_from`，使下游 ui-image-generator / ui-panel-builder 复用同一张图片而非重复生成。

> ⚠️ **text-label 节点（`source=uss_only`）不参与样式组去重**——text-label 无图片可复用，且各文字内容不同。

**前置校验**：检查 `component-tree.json` 中是否存在非空 `style_group_hint`。若 `semantic_records.json` 中有值但树中为空 → 建树工具（`TJStyleRefTree.BuildTree`）未正确传递 → 停止分组，先修复数据流。若全为空，回退到 `visual_description` 文本比对。

**`style_group` 命名**：`{componentType}_style_{N}`。**非代表**：`source` 保持 `ai_generate`、`crop_path` 设为 `reuse_from:{代表组件名}`，不生成独立图片。art_text 组件不参与去重（像素内容不可复用）。

> 完整规则（识别条件、`partial_override` 判定、旋转复用、`generation_hint` 设置、代表选举等）**读** `references/post-processing.md` §5。**执行 Step 7.1 前必须读该文档**——跳过读取会导致去重判定遗漏边缘情况。

## 完成后

1. 确认所有 composite 已评估 `reuse_structure_from` 标记
2. 确认所有 leaf 已评估 `style_group` / `reuse_from` 标记
3. 更新 `pipeline-state.json`：
   - `03-pre-grouping` → `completed`
   - `04-recursive-extraction` → `in_progress`
4. orchestrator 派发下一阶段 subagent（`stages/04-recursive-extraction/SKILL.md`）
