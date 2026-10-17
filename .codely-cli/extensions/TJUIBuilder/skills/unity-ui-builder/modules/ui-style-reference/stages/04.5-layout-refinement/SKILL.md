# Stage 04.5: 参考图布局校正解析（条件式）

仅当 `UI需求.md` 的「参考图布局校正」为 `开启` 时执行。位于 Stage 04 之后、Stage 05 之前。先调用独立的 `ui-layout-intent-designer` 采集并确认布局，再用确定性 resolver 把检测树与返回的正式布局解析为**最终布局树**，供 Stage 05/06 直接消费。

## 步骤契约

| 项 | 内容 |
|----|------|
| 初始输入 | 各面板 `component-tree.original.json`（Stage 04 检测快照，不可变）、参考图路径 |
| 被调 skill 输出 | 已确认的 `LayoutIntent/layout_intent.json` |
| 输出 | 各面板最终 `component-tree.json`（resolver 输出）+ `component-tree.resolved.json`（快照）+ `LayoutIntent/resolved-ui-tree.json`（审计） |
| 阶段前置门禁 | 所有面板 `04-recursive-extraction=completed`；`参考图布局校正=开启` |
| resolver 前置门禁 | 被调 skill 已返回，且 `layout_intent.json.isConfirmed=true`、目标画布完整、`referenceAnalysisHash` 匹配 `component-tree.original.json` 字节 |
| 完成判据 | resolver 退出码 0；最终树与快照存在；`--check` 全部通过 |
| 执行性质 | 纯确定性程序；禁止 AI 补全、解释冲突或修改输出 |
| 必读 reference | `references/resolved-ui-tree-schema.md` |

> **权威关系**：`component-tree.original.json` 是检测快照（不可变，画板/resolver 只读）；`layout_intent.json` 是用户布局权威；`component-tree.json`（最终树）由 resolver 写入布局字段、Stage 05 之后追加生成字段；`resolved-ui-tree.json` 是审计。resolver 绝不读取活 `component-tree.json`，因此重复执行不会叠加校正。详见 `shared/contracts.md`。

## 04.5A：调用布局 skill

Stage 04 完成、校正开启时，本阶段调用 `ui-layout-intent-designer`（路线 B），明确传入目标游戏、目标面板、每个面板的 `component-tree.original.json` 与原始参考图路径，并请求"外部结构化初稿"模式。原图在画板中只作为低透明度、不可交互的底层参考层，不生成模拟背景节点，也不进入 resolver 或 Stage 05；正式背景仍由 Stage 05 Step 8.5 处理。两个 skill 的边界只有调用参数与正式输出文件：布局 skill 不读取或修改本模块的 `pipeline-state.json`、最终树或 Stage 04.5 状态，也不负责回调本阶段。

布局 skill 打开画板并等待用户确认时，由本阶段把 `04.5-layout-refinement` 和 `current_stage` 保持为 `waiting_for_layout`。布局 skill 返回后，本阶段重新读取并校验 `LayoutIntent/layout_intent.json`；不得依赖布局 skill 修改状态来判断完成。

未被任何画布覆盖的面板保留检测树（`component-tree.json` 即 Stage 04 的检测输出），直接进入 Stage 05。

## 等待与恢复

会话恢复时：

1. `layout_intent.json` 缺失或 `isConfirmed!=true`：由 Stage 04.5 再次调用/恢复布局 skill，Stage 04.5 保持 `waiting_for_layout`。
2. 存在比正式文件更新的未确认 `layout_intent.draft.json`：视为用户仍在编辑，恢复画板；不得自动用较旧的已确认正式文件生成最终树。
3. 已确认但任一 `referenceAnalysisHash` 与当前 `component-tree.original.json` 不一致：停止并报告检测树已变化；禁止用旧布局生成最终树。
4. 已确认、没有更新的未确认草稿且 hash 匹配：将所有目标面板的 Stage 04.5 改为 `in_progress`，执行 resolver。

## 04.5B：执行确定性 resolver

执行部署型工具一行调用（包 `Editor/UIBuildTools`，经 `execute_csharp_script`）：

```csharp
Create.GameFramework.TJResolveUiTree.Run("context/games/{GameName}");
```

resolver 的确定性规则：

- **输入**：`component-tree.original.json`（每面板）+ `layout_intent.json`。绝不读取活 `component-tree.json`。
- **输出**：每面板 `component-tree.json`（活树，布局字段）+ `component-tree.resolved.json`（快照，字节与活树一致）+ `LayoutIntent/resolved-ui-tree.json`（审计）。
- `layout_intent.json` 决定最终画布/节点集合、像素几何、父子关系、同级顺序、名称、描述和组件类型——这些写入最终树的布局字段。
- 通过 `sourceReferenceNodeId + sourceReferencePanel` 从 `component-tree.original.json` 复制生成字段（`semantic`、`quality`、`functional_type`、`crop_path`、`source`、`render_mode`、`img2img_provenance`、`generation_atlas`、`panel_provenance` 等）与裁剪几何（`original_image_box` → `source_crop_box`）。原始检测几何不进入布局字段。
- 来源为空（用户新增）的区域标记 `change_kind=added`、`origin=layout_added`，按确定性规则补默认生成字段（Label/container→`uss_only`；其余→`ai_generate`+`text2img_fallback`）；不伪造 `source_crop_box`。
- 与同一检测树的初始投影不同的来源区域标记 `change_kind=modified`。
- 检测树中可进入画板、但未被最终布局引用的节点写入 `deleted_source_node_ids`，Stage 05 不为它们生成图片。
- **reuse_from 悬空处理**：保留节点中 `crop_path=reuse_from:X` 的，若代表 X ∈ 删除集，则该节点改写为独立生成（`crop_path=""`、`semantic.source="ai_generate"`），并在 `reuse_independence_rewrites` 中记录。resolver 之后不存在指向已删除节点的悬空引用。
- 保留完整像素和浮点百分比，不经过 Markdown 转换。
- 多面板**全成全不写**：所有面板的最终树与审计全部校验通过后才一次性提交，禁止只更新一半面板。
- 若活 `component-tree.json` 已含 Stage 05 产出（background 节点或 `img2img_provenance`），resolver 默认拒绝覆盖（避免丢弃已生成图片）；需要强制重解析时传 `force: true`。
- resolver 不读取、不写入 `UI参考图分析.md`——该文件由 Stage 06 从最终树一次性生成。

resolver 退出码非 0（`Run` 返回 false）时保持 `in_progress`，报告 `RESOLVE FAIL` 原因并停止。禁止回退为直接读取活树或 original tree 自行处理。

## 输出验证

生成后再次执行只读校验：

```csharp
Create.GameFramework.TJResolveUiTree.Run("context/games/{GameName}", check: true);
```

必须确认：

- 重建校验：`resolve(original tree + layout intent)` ≡ 各面板 `component-tree.resolved.json` 快照；
- 活文件校验：活 `component-tree.json` 的布局投影（布局字段 + `semantic.component_name/component_type`，排除 Stage 05 生成字段与 background 节点）≡ 快照；
- 审计 `resolved-ui-tree.json` 与重算结果一致、`resolvedTreeHash` 可重算一致；
- 节点 ID 唯一、父级存在且无循环、父节点先于子节点、子节点位于父节点内（画板校验已保证，resolver 再次验证）；
- `source_reference_node_id` 有效且不重复、`source_crop_box` 未被布局几何覆盖；
- 连续两次执行得到相同的 `resolvedTreeHash`。

## 完成后

1. 所有目标面板的 `04.5-layout-refinement` → `completed`、`05-final-generation` → `in_progress`。
2. 记录 `LayoutIntent/resolved-ui-tree.json`、`resolvedTreeHash` 和各面板 `component-tree.resolved.json`。
3. orchestrator 派发 Stage 05 subagent，最终 `component-tree.json` 是其唯一输入。
