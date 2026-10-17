# Resolved UI Tree v1（审计中间产物）

`LayoutIntent/resolved-ui-tree.json` 是部署型工具 `Create.GameFramework.TJResolveUiTree`（包 `Editor/UIBuildTools`，Stage 04.5B）从 `component-tree.original.json` 与已确认 `layout_intent.json` 生成的审计中间产物。禁止手工编辑。下游的权威布局数据是各面板 `Processing/{Panel}/component-tree.json`（最终树），不再读取本文件。

## 权威与优先级

| 数据 | 权威范围 |
|---|---|
| `component-tree.original.json` | Stage 04 检测快照，不可变；画板与 resolver 的唯一几何/裁剪/语义来源 |
| `layout_intent.json` | 最终节点集合、像素几何、父子关系、顺序、名称、描述、组件类型 |
| `component-tree.json`（最终树） | resolver 输出 + Stage 05 追加生成字段；Stage 05/06 与下游 builder 的唯一消费对象 |
| `component-tree.resolved.json` | resolver 写入的快照，字节与活树一致；仅供 `--check` 重建校验 |
| `resolved-ui-tree.json`（本文件） | 审计中间产物，记录输入 hash、变更统计、删除节点与 reuse 改写，不参与下游流程 |

校正关闭时 Stage 04.5 被跳过，不生成本文件与 `component-tree.resolved.json`，最终树直接是 Stage 04 的检测输出。

## 顶层字段

| 字段 | 含义 |
|---|---|
| `schema` | 固定 `resolved-ui-tree/v1` |
| `gameId` | 游戏 ID |
| `authority` | 各类文件的权威范围声明 |
| `inputs.layoutIntent` | layout intent 相对路径与 SHA-256 |
| `inputs.detectedTrees` | 每个参考面板 original tree 的相对路径与 SHA-256 |
| `canvases[]` | 各画布的审计摘要 |
| `resolvedTreeHash` | 对不含此字段的规范化 JSON 计算 SHA-256 |

## 画布审计

每个 canvas 保留 `id`、`uiViewId`、`uiViewType`、`width`、`height`、`referencePanel`、`referenceAnalysisHash`，并提供：

- `nodes[]`：每个最终节点的审计视图（`id`、`finalNodeId`、`sourceReferenceNodeId`、`changeKind`、`name`、`description`、`componentType`、`parentId`、`depth`、`order`、`rect`、`relativeToParent`），数组顺序即全局渲染顺序。
- `deletedSourceNodeIds[]`：原参考树可进入画板、但被用户从最终布局删除的节点 ID。
- `reuseIndependenceRewrites[]`：因代表节点被删除而改写为独立生成的 reuse 节点记录（`node_id`、`previous_crop_path`、`representative_node_id`）。
- `changeSummary`：`unchanged` / `modified` / `added` / `deleted` 计数。

## 消费门禁

Stage 04.5B 完成前必须运行 resolver 的 `--check`，它从 original tree + layout intent 重建最终树和审计，并要求磁盘上的 `component-tree.resolved.json`、活 `component-tree.json` 的布局投影、以及 `resolved-ui-tree.json` 全部与重算结果一致。校验失败即停止，不得交付旧文件或回退到检测布局。

本文件不再包含任何 Markdown 重写门禁——`UI参考图分析.md` 由 Stage 06 从最终 `component-tree.json` 一次性生成，不读取本文件，也不再含"Stage 07 校正区块"。
