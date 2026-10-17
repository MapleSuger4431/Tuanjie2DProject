# Stage 04: 递归构建组件树

Step 8 + Step 7.2：BFS 逐层递归拆分 composite 节点 + 结构验证。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `component-tree.json`（含预分组标记，depth=0 根级节点）、`root-extract/component_{NNN}.png`（depth=0 composite 裁剪来源，Stage 02 连通域 mask 产出） |
| 输出 | 完全展开的 `component-tree.json`（所有 `kind=composite` 标记 `recursion_done` 或全部为 `kind=leaf`） |
| 前置门禁 | `pipeline-state.json` 中 `03-pre-grouping` = `completed` |
| 完成判据 | 所有 `state=valid` 的 composite 已标记 `recursion_done` 或全部为 leaf；`preflight_check.py label_geometry` 通过 |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：图生图执行规则、溯源记录、目录布局、核心硬约束见 `shared/contracts.md`。

> ⚠️ **这是整个流程中耗时最长、步骤最多的阶段。** 递归只负责发现几何和层级，不生成最终图片。BFS 逐层批处理。

> 📋 **步骤契约**
>
> | 项 | 内容 |
> |----|------|
> | 输入（depth=0）| `root-extract/component_{NNN}.png`（Stage 02 产出，已用连通域 mask 裁剪）→ 复制为 `nodes/{nodeId}/isolation_input.png`。禁止从 `ui_only_2048.png` 重新做 bbox 裁剪 |
> | 输入（depth≥1）| `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（上一层产出，已用连通域 mask 裁剪）→ 复制为 `nodes/{nodeId}/isolation_input.png`。禁止从 slot 图重新做 bbox 裁剪 |
> | 装箱工具 | `generation_atlas.py pack`（**禁止手写裁剪+粘贴替代**） |
> | 图生图 | 每页一次 img2img，prompt **读**下方模板 |
> | 拆图工具 | `generation_atlas.py split` |
> | OpenCV | `extract_reference_components.py`（逐槽） |
> | 合并树 | `TJStyleRefTree.MergeChildTree`（原 `component_tree.py --merge-tree`） |
> | 输出 | `slots/{nodeId}.png` + `.transform.json`、`nodes/{childNodeId}/original_crop.png`、`nodes/{childNodeId}/semantic.json` |
> | 硬规则 | ① 每一层的 `isolation_input.png` 都从**上一层 `extract_reference_components.py` 的连通域 mask 裁剪图**复制（depth=0 从 `root-extract/`，depth≥1 从 `nodes/{parentNodeId}/child-extract/`），禁止从 `ui_only_2048.png` 或 slot 图做 bbox 裁剪 ② 0子/自引用必须走失败补图，不可直接标 `recursion_done` ③ 语义分类标注底图用**原图裁剪**（非隔离图），否则 `display_text` 丢失 |

<HARD-GATE>执行 Stage 04 前**必须完整读取** `references/recursive-extraction.md`（§1-§8.7 全文）——该文档定义了裁剪来源坐标系转换、retry 规则等**流程必读信息**，跳过会导致图生图输入错误、子组件层级永久丢失。</HARD-GATE>

> **进入 Stage 05 的前置条件**：`component-tree.json` 中所有 `state=valid` 的节点要么是 `kind=leaf`，要么 `kind=composite` 且已标记 `recursion_done`。否则**禁止进入 Stage 05**，继续递归拆分。校正开启时下一步是 Stage 04.5（先校正结构树再生成图片）；校正关闭时直接进 Stage 05。

## 递归循环伪代码（必须逐层执行，禁止跳层）

```
depth = 1
while True:
    # 收集待拆节点：composite + valid + 未标 recursion_done + 非 reuse_structure_from
    # ⚠️ game_content_area 强制跳过递归——它是 3D 场景渲染区域，不含可交互子 UI
    待拆 = 组件树中所有 kind=composite && state=valid
           && 未标记 recursion_done
           && 未标记 reuse_structure_from:*
           && component_type != 'game_content_area' 的节点
    if 待拆为空:
        break  # 递归结束

    # 每层完整执行：
    # 1. 装箱（generation_atlas.py pack）
    # 2. 图生图子项隔离（每页一次 img2img）
    # 3. 验证输出页（validate_output_page.py 三态门禁：pass 免视觉 / fail slot 直 §8.6 / review slot 视觉仲裁）
    # 4. 拆图（generation_atlas.py split）
    # 5. 逐槽 OpenCV（extract_reference_components.py）
    # 6. 验证 OpenCV 结果（检查 0 子/自引用/子项过少）
    # 7. 语义分类（analyze_multimedia 批量标注）
    # 8. 合并到组件树（TJStyleRefTree.MergeChildTree --parent-id）
    # 8.4.5 文字标签补充检测（detect_text_on_original.py 从原图检测独立文字标签，创建 uss_only 节点）
    # 9. 对失败节点执行失败补图（retry-1 → retry-2）
    #     retry-2 仍失败且 AI 误判 composite → 降级为 leaf（改 kind、裁剪使用、设 source）
    #     retry-2 仍失败且非误判 → 标 recursion_done（不降级）

    # 检查新加入的子节点
    新composite = 新子节点中 kind=composite 的
    if 新composite 为空:
        break  # 全部是叶子，递归结束
    depth += 1
```

## recursion_done 标记条件（满足任一即可标记）

1. 子项隔离图生图经 retry-2 重试后仍返回 0 个子组件
2. 子项隔离图生图连续 2 轮补图后仍失败（retry-2 仍失败）
3. 复合节点的原图裁剪面积 < 64x64px（尺寸过小）
4. 子项隔离后 OpenCV 找到的子组件面积未严格小于父级（自引用轮廓），经 retry-2 重试仍如此
5. retry-2 仍 0 子项时：仅当原图裁剪有实际视觉内容（非关键色像素面积 > 5%）且确认为 AI 误判 composite（实际是单一视觉单元）时，将该 composite 节点**降级为 leaf** 处理——用原图裁剪直接使用透明 PNG，标记 `kind=leaf`、`source=direct_use`（或 `ai_generate` 如果 `quality=contaminated` 或 `functional_type` 非空——二者与 `direct_use` 互斥）。`recursion_reason` 注明 `"degraded_to_leaf_after_retry"`。composite 一般不能直接使用（含多个子 UI），仅在 AI 误判时降级；否则按现有条件标记 `recursion_done`。

> 条件 1、4、5 的前置要求：首次出现 0 子或自引用时**不可直接标 recursion_done**，必须先走失败补图重试。只有 retry-2 耗尽后才可用这些条件标记。

> "子组件中有 composite"不是 `recursion_done` 的标记条件——必须继续递归到下一层拆分。

## 每层核心操作

每层执行：`generation_atlas.py pack` 装箱 → img2img 子项隔离 → `generation_atlas.py split` 拆图 → `extract_reference_components.py` 逐槽 OpenCV → 语义分类 → `TJStyleRefTree.MergeChildTree` 合并树。详细操作参数和校验规则**读** `references/recursive-extraction.md`（§8.1-§8.5）。

**图生图子项隔离**（每页一次 img2img）：

```text
generate_image（TJGenerators MCP）:
provider: seedream_pro
image_urls: <input-page-{P}.png 按 mcp-image-generation.md §2 上传的 CDN URL>
image_size: {"width":2048,"height":2048}
is_segmentation: false
confirm_cost: true
prompt: "对每个独立的UI复合组件，移除其最底层背景。用{key_color}填充整个背景。保留所有按钮和图标。每个槽位相互独立。移除所有底板和面板。保持每个槽位内的位置和大小。不要跨槽位移动内容。移除所有文字、数字和装饰。保留艺术字。"
```

> **`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 字段读取**，替换为颜色名称+十六进制（如 `magenta (#FF00FF)`，hex 从 rgb 的 r/g/b 转大写拼接），禁止硬编码颜色。

> **`functional_type` 非 null 的 composite 的 prompt 增强**：当待拆 composite 的 `functional_type` 为 `slider`/`toggle` 时，在标准 prompt 末尾追加：`保留滑块轨道、手柄和旋钮可见。保留开关和勾选标记可见。`

**验证输出页（三态门禁）**：收到 AI 输出后立即执行 `validate_output_page.py`（命令与三分支见 `references/recursive-extraction.md` §8.3）——pass 免视觉直接进入拆图；fail slot（大块均匀非关键色——底板未移除，机械确定）标记 `img2img_quality_failed` 进入 §8.6 失败补图；review slot 仅对被标记 slot 视觉仲裁（prompt **读** §8.3 逐字使用，脚本 `reasons` 作补充证据），确认未移除的 slot 给出背景底板描述用于重试追加 prompt。未验证直接拆图会导致 OpenCV 找不到子组件（0 子或自引用），被误标为 `recursion_done` 永久丢失子组件层级。审计 `output-page-validation-{P}.json` 必须落盘。

**OpenCV 结果验证**：每个 slot 的 OpenCV 提取完成后执行两步验证：

**第 1 步 · `validate_recursion` 门禁**（检测零子和自引用）：

```text
{PYTHON} scripts/preflight_check.py validate_recursion {childOutputDir}/components.json
```

脚本自动检测三种故障：
- **零子组件**：AI 未移除背景底板 → 进入失败补图
- **自引用**（子组件面积 ≥90% 父级）：AI 未移除背景底板 → 进入失败补图
- **`self_reference_warnings`**：`extract_reference_components.py` 已在 components.json 中标记的自引用组件

**退出码 1 = 禁止标 `recursion_done`，必须进入 §8.6 失败补图**。只有 `validate_recursion` 通过（退出码 0）后才可进入第 2 步。

**第 2 步 · 后验对比**（检测缺失组件，`validate_recursion` 通过后执行；三态门禁）：

`validate_recursion` 仅检测零子和自引用，**不检测缺失组件**——即"隔离图中可见但 OpenCV 未提取到的 UI 元素"（如细边框下拉框、小箭头、浅色轨道）。按 `references/opencv-recovery.md` §2.5 执行降档重跑 + `find_missing_components.py` 门禁：退出码 0（零候选/无框太小/无超大方框）→ ✅ 免视觉；退出码 2 → 仅对被标记对象处理（候选框仅对候选区域视觉确认，确认为真实元素按 §1.1 恢复；框太小 → §1.2；超大方框 → §1.3）。prompt 和恢复策略见 `references/opencv-recovery.md` §2.5。

> 🔴 **0 子和自引用不是 recursion_done**：必须先走失败补图重试。只有 retry-2 耗尽后才可标 `recursion_done`。`validate_recursion` 脚本是自动执行此检查的硬门禁——跳过它直接标 `recursion_done` 会导致子组件层级永久丢失。

**合并到组件树**：每层 OpenCV 完成后，必须调用 `TJStyleRefTree.MergeChildTree` 将子节点合并进树（不调用则子节点不会进入树）。语义分类标注底图用**原图裁剪**（非隔离图），否则 `display_text` 丢失。

**失败补图**：失败节点单独重新装箱、重新提交图生图。retry-1 调整 prompt 加强背景移除，retry-2 换策略（alpha 清理 / 裁剪聚焦）。详细 retry prompt 和策略**读** `references/recursive-extraction.md` §8.6。retry-2 仍失败则 `ask_user` 讨论，此时才可标 `recursion_done`（含降级为 leaf 的条件 5）。

> ⛔ **composite_shell 配方禁止 fallback 到去关键色路径**：retry 全部失败后，仅允许 `ask_user` 或降级为 leaf。**严禁**自动回退到从 `isolation_input.png` 生成 shell——直接裁剪保留全部内容像素，不会移除内部子 UI。

**拆图失败的特例处理**：`split --clean` 的 gutter contamination 检测偏保守——shell 边缘画到 slot 边界附近就会触发，但槽位内部可能完全正确。手动裁剪仅允许在以下条件：失败原因**仅为** `gutter_contamination`（槽位外围安全区有非关键色像素）且 `analyze_multimedia` 确认槽位 `content_rect` 内部内容正确。手动裁剪时从输出页裁剪该 slot 的 `content_rect` 区域，保存后执行关键色→透明转换和 alpha 噪声清理。以下失败原因禁止手动裁剪，必须重试：前景元素残留、内容缺失、跨槽串图、宽高比严重失真。

**最终图片输出验证**（Stage 04 和 Stage 05 共用）：拆图清理后、写回组件树前，对每张产出文件执行验证：
1. **透明度验证**：加载 PNG，检查 alpha 通道存在且透明像素占比 > 0（关键色已在 `split --clean` 中转换为 alpha=0）。Stage 05 背景提取的产出（`is_segmentation=false` 的全屏背景图）跳过。
2. **尺寸验证**：图片尺寸是否匹配 manifest 中的 `export_size`。`--clean` 模式会 trim 透明边框并缩放到 `export_size`，这是预期行为。若图片仍为 AI 输出的 2048x2048 而 `export_size` 远小于此 → 判定"缩放被跳过"。
3. 任一验证失败 → **禁止进入写回**，回退到失败补图。

> 裁剪来源坐标系、拆图校验规则等关键细节**读** `references/recursive-extraction.md`（§1-§8.7 全文）。

## 7.2 结构验证（按组执行，代表节点递归完成后触发）

> ⚠️ **执行时机**：本步骤在递归过程中按组触发（每组代表节点 `recursion_done` 后立即执行），与递归交织执行。

代表节点完成递归拆分（Step 8）后，对同组的非代表执行结构验证：
1. **对比子项**：比较非代表与代表的子项数量和布局。如果吻合 → 复制代表子树结构（`root_box`、裁剪图、`display_text` 替换为非代表自身的值）。
2. **不吻合处理**：子项数量/位置差异大时 → 取消 `reuse_structure_from` 标记，对该非代表独立执行递归拆分。

> 预分组仅按外部视觉特征分组，内部布局在递归前未知。结构验证确保假阳性（外观看似相同但内部不同）在递归后被捕获并修正。

> ⚠️ **时机**：结构验证**按组独立触发**——当一个组的代表节点完成递归（`recursion_done` 已标记）后，立即对该组的所有非代表执行结构验证，不需要等待其他组的代表节点完成。不同组之间互不阻塞。验证通过的非代表直接标记 `recursion_done`（复制子树）；不吻合的非代表需回到递归独立拆分。

## 完成后

1. 确认所有 `state=valid` 的 composite 已标记 `recursion_done` 或全部为 leaf
2. 执行 `{PYTHON} scripts/preflight_check.py label_geometry {processingDir}/component-tree.json` 通过（text-label 挂载门禁，规则见 `references/recursive-extraction.md` §8.4.5）
3. 将 `component-tree.json` 复制为 `component-tree.original.json`
4. 更新 `pipeline-state.json`：
   - `04-recursive-extraction` → `completed`
   - 校正开启：`04.5-layout-refinement` → `in_progress`
   - 校正关闭/字段缺失：`04.5-layout-refinement` → `skipped`、`05-final-generation` → `in_progress`
5. orchestrator 派发 Stage 04.5（校正开启）或 Stage 05（校正关闭）subagent
