# 递归构建组件树（子项隔离阶段）

递归阶段**只负责发现几何和层级**，不生成最终图片。以 BFS 逐层批处理：同一 `depth` 的非叶节点共用图集页，每页只提交一次图生图，减少 AI 调用次数。

## 8.1 准备图集输入

对当前层所有 `kind=composite` 且不是 `reuse_structure_from:*` 的节点：

1. 获取父级区域图，保存为 `nodes/{nodeId}/isolation_input.png`。来源按节点深度确定：

   | 节点深度 | 来源 | 说明 |
   |---------|------|------|
   | depth=0 | `root-extract/component_{NNN}.png`（Stage 02 产出） | Stage 02 的 `extract_reference_components.py` 用连通域 mask 裁剪，已排除同级兄弟组件像素——直接复制使用，禁止从 `ui_only_2048.png` 重新做 bbox 裁剪 |
   | depth>=1 | `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（上一层产出） | 上一层的 `extract_reference_components.py` 在该 slot 上提取子组件时用连通域 mask 裁剪，已排除同级兄弟组件像素——直接复制使用，禁止从 slot 图重新做 bbox 裁剪 |

   ⚠️ **核心原则**：每一层的 `isolation_input.png` 都从**上一层 `extract_reference_components.py` 的连通域 mask 裁剪图**复制，不从 `ui_only_2048.png`、slot 图或任何其他来源做 bbox 矩形裁剪。bbox 裁剪会把兄弟组件像素混入，导致递归时重复发现已提取的组件。
2. 若裁剪图任一边 < 128px，用关键色 pad 到 128x128（API 最小输入要求）。
3. 收集所有裁剪图，生成 `atlas-items.json`：

```json
{
  "atlas_kind": "child_isolation",
  "recipe": "direct_children_without_text",
  "depth": 1,
  "background_mode": "key_color",
  "page_size": 2048,
  "gutter_px": 32,
  "items": [
    {"node_id": "popup_01", "source_path": ".../isolation_input.png",
     "source_size": {"width": 900, "height": 540},
     "logical_size": {"width": 450, "height": 270}}
  ]
}
```

## 8.2 装箱

```text
{PYTHON} scripts/generation_atlas.py pack \
  --items {processingDir}/Atlases/child-isolation/depth-{N}/atlas-items.json \
  --output-dir {processingDir}/Atlases/child-isolation/depth-{N}/
```

脚本执行确定性 shelf 装箱：节点保持原宽高比、不旋转、不拆分；槽位间关键色安全间距；空间不足自动分页。每页输出 `input-page-{P}.png`（2048x2048 关键色画布）和 `atlas-manifest-{P}.json`（记录 `slot_rect`、`content_rect`、缩放比例）。

## 8.3 图生图子项隔离（每页一次）

对每张 `input-page-{P}.png` 提交图生图，按 `shared/contracts.md`「图生图执行规则」由单个 subagent 逐个提交（在途数 < 3）：

```text
generate_image（TJGenerators MCP）:
provider: seedream_pro
image_urls: <input-page-{P}.png 按 mcp-image-generation.md §2 上传的 CDN URL>
image_size: {"width":2048,"height":2048}
is_segmentation: false
confirm_cost: true
```

> **`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 字段读取**，替换为颜色名称+十六进制（如 `magenta (#FF00FF)`，hex 从 rgb 的 r/g/b 转大写拼接），禁止硬编码颜色。

prompt（模板，首次逐字使用）：

```text
对每个独立的UI复合组件，移除其最底层背景。用{key_color}填充整个背景。保留所有按钮和图标。每个槽位相互独立。移除所有底板和面板。保持每个槽位内的位置和大小。不要跨槽位移动内容。移除所有文字、数字和装饰。保留艺术字。
```

**验证（强制，不可跳过；三态门禁）**：收到 AI 输出后立即执行脚本门禁——不验证直接拆图会导致 OpenCV 在未移除背景的 slot 上找不到子组件（0 子或自引用），进而被误标为 `recursion_done`，永久丢失内部子组件层级。

```text
{PYTHON} scripts/validate_output_page.py \
  {processingDir}/Atlases/child-isolation/depth-{N}/output-page-{P}.png \
  {processingDir}/Atlases/child-isolation/depth-{N}/atlas-manifest-{P}.json \
  --json-output {processingDir}/Atlases/child-isolation/depth-{N}/output-page-validation-{P}.json
```

- 退出码 0（pass：全部 slot 的关键色覆盖率/内容岛/均匀块/保留率全绿）→ 免视觉，进入 §8.4 拆图
- 退出码 1（fail：存在"大块均匀非关键色"slot——底板未移除，机械确定）→ 将这些 slot 对应的 nodeId 标记为 `img2img_quality_failed`，直接进入 §8.6 失败补图（无需视觉确认机械结论）；其余 slot 继续正常流程。若**全部** slot 均 fail → 调整 prompt 后整页重试（计入 prompt 重试次数）
- 退出码 2（review：个别 slot 关键色覆盖率偏低/疑似内容丢失/slot 空——密集内容等合法情况可能触发）→ 仅对 review 的 slot 视觉仲裁：把脚本 `reasons` 作补充证据，按下方页面验证 prompt 分析整页但**只回答被标记的 slot**；确认背景未移除的 slot 标记 `img2img_quality_failed` 进入 §8.6
- 审计：`output-page-validation-{P}.json` 必须落盘，免视觉判定留痕

**review 仲裁 prompt**（逐字使用，`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 读取，替换为颜色名称+十六进制，如 `magenta (#FF00FF)`）：
```text
这是一张UI复合组件子项隔离的AI图生图输出。AI应移除最底层背景和底板（移除区域填充为{key_color}），移除所有文字和数字，保留所有按钮和图标。

对这页中每个可见的槽位回答：
1. AI是否实际移除了背景底板？是/否/部分
2. 背景应为{key_color}的地方是否正确？是/否
3. 文字和数字是否已移除？是/否
4. 按钮和图标是否保留？是/否

逐个列出每个槽位的结果。如果任何槽位的背景未被移除，该槽位需要单独重试。
```

**仲裁判定规则**（沿用原判定表）：
- **被仲裁 slot 全部通过** → 进入 §8.4 拆图
- **部分 slot 失败**：某 slot 背景未移除 → 将该 slot 对应的 nodeId 标记为 `img2img_quality_failed`，进入 §8.6 失败补图（逐个单独重试）
- **全页失败**：所有 slot 都未移除背景 → 调整 prompt 后整页重试（计入 prompt 重试次数）

详细规则**读** Stage 04 SKILL.md Step 8「图集输出验证规则」的「子项隔离输出页验证」段。

## 8.4 拆图与逐槽 OpenCV

收到 AI 输出 `output-page-{P}.png` 后：

```text
{PYTHON} scripts/generation_atlas.py split \
  --page {outputPage} \
  --manifest {manifest} \
  --output-dir {processingDir}/Atlases/child-isolation/depth-{N}/slots/
```

拆图脚本按 manifest 逐槽裁剪，检查：关键色安全间距有无前景污染（跨槽串图则该节点失败）、有效前景、宽高比和分辨率。通过项输出 `{nodeId}.png`；失败项记录原因。同时自动在每个 slot 旁生成 `{nodeId}.transform.json`，直接用作下游 OpenCV 的 `{childTransformJson}`。

> **`aspect_warning`（软警告，非失败）**：当 `split-results.json` 中某 slot 的 `aspect_warning: true` 时，表示 AI 改变了内容宽高比（如内容收缩或位移），但 gutter 干净、内容存在。slot 图已正常输出，**直接进入 OpenCV 提取**——OpenCV 会在实际内容上提取子组件，不受预期 aspect ratio 影响。**禁止**因此重试 img2img 或修改 key_color。内容布局轻微变化是 AI 正常行为。

> §8.3 验证已标记 `img2img_quality_failed` 的 slot 不执行 OpenCV——背景未移除的 slot 上跑 OpenCV 必然得到 0 子或自引用。这些 slot 直接进入失败补图。

对通过项逐槽执行 OpenCV。slot image 是已从页面 `content_rect` 裁出的独立图，坐标系从 (0,0) 开始：

```text
{PYTHON} scripts/extract_reference_components.py \
  {slotImage} {slotsDir}/{nodeId}.transform.json {childOutputDir} \
  --parent-content-rect 0 0 {slotImageWidth} {slotImageHeight}
```

子节点的 `relative_parent_box` 相对该 slot image（即父节点 `content_rect`）换算：

```text
xPct = (childX - contentX) / contentW * 100
yPct = (childY - contentY) / contentH * 100
```

子级专用 transform 的 `scaled_content_rect` 按裁剪图宽高比计算，`source_size` 设为裁剪图原始尺寸。

**验证（强制，不可跳过）**：每个 slot 的 OpenCV 提取完成后**必须**执行两步验证：

**第 1 步 · `validate_recursion` 门禁**：

```text
{PYTHON} scripts/preflight_check.py validate_recursion {childOutputDir}/components.json
```

脚本自动检测三种故障并输出判定：

| 结果 | 判定 | 处理 |
|------|------|------|
| `validate_recursion` 退出码 0（通过） | ≥1 个子组件，无自引用 | 进入第 2 步后验对比 |
| `validate_recursion` 退出码 1 + `ZERO_CHILDREN` | img2img 失败 | 标记 `extraction_failed: zero_children`，进入 §8.6 失败补图 |
| `validate_recursion` 退出码 1 + `SELF_REFERENCE` | img2img 失败 | 标记 `extraction_failed: self_reference`，进入 §8.6 失败补图 |

**第 2 步 · 后验对比**（`validate_recursion` 通过后执行；三态门禁）：

`validate_recursion` 仅检测零子和自引用，**不检测缺失组件**。按 `references/opencv-recovery.md` §2.5 执行降档重跑 + `find_missing_components.py` 门禁：退出码 0（零候选/无框太小/无超大方框）→ 免视觉，后验对比通过；退出码 2 → 仅对被标记对象处理（候选框视觉确认或 §1.1 恢复、框太小 §1.2、超大方框 §1.3），prompt 和恢复策略见 `references/opencv-recovery.md` §2.5。后验对比通过后才可进入 §8.5 语义分类。后验对比通过后还需执行 §8.4.5 文字标签补充检测。

## 8.4.5 文字标签补充检测（递归层级）

与 Stage 02 Step 5.5 同理：img2img 子项隔离也执行了 "Remove all text, numbers"，父组件内部的独立文字标签（如设置面板中每行控件左侧的"主音量"、"全屏"等标签）在隔离图中消失，OpenCV 无法检测到。

> **根级 vs 递归层的差异与共同底线**：Stage 02 Step 5.5（根级）使用 Stage 01 Step 3c 产出的 `text_only_2048.png` 通过 OpenCV 精确提取文字标签几何坐标。递归层级没有对应的 per-slot `text_only` 图，但**几何同样必须来自 OpenCV 像素检测**——在父组件的原图裁剪上按文字色通道检测。⛔ **禁止用 AI 视觉估测（analyze_multimedia 返回百分比）作为 `root_box`/`original_image_box` 几何来源**：AI 对小文字的百分比估测已实测产生系统性漂移（每行位置偏 20-80px、尺寸小 20-40%、脱离等距行模式），且与其自洽的 `relative_parent_box` 一起构成「内部自洽但整体错误」的几何，挂载校验无法拦截（2026-09-04 ChongZhi 实测：侧栏 8 行菜单文字全部错位，仅靠用户在画板发现）。AI 的职责边界：**只读内容**（`display_text`/`is_dynamic`/字体样式），**不产几何**。

**执行方式（两步，几何与内容分离）**：

1. **OpenCV 几何检测**：对父组件的**原图裁剪**（`source_square.png` 按父组件 `original_image_box` 裁剪的区域——`original_image_box` 是原图空间坐标，而源图以原始分辨率置于 `source_square.png` 左上角（仅右/下补关键色边），因此该坐标可直接用于 `source_square.png` 裁剪，**禁止再乘 scale**）执行 `detect_text_on_original.py`（下述行带锚定 / 色通道 / 固定算法 / 等距行校验规则均由脚本实现）：

```text
{PYTHON} scripts/detect_text_on_original.py \
  {processingDir}/source_square.png \
  {processingDir}/transform.json \
  {processingDir}/nodes/{parentNodeId}/text-extract \
  --mode parent \
  --crop-rect {P.original_image_box.x} {P.original_image_box.y} {P.original_image_box.width} {P.original_image_box.height} \
  --parent-root-box {P.root_box.x} {P.root_box.y} {P.root_box.width} {P.root_box.height} \
  --parent-original-box {P.original_image_box.x} {P.original_image_box.y} {P.original_image_box.width} {P.original_image_box.height}
```

可选：`--known-rows N`（已知行数先验）、`--channels bright|gold|dark|auto`（色通道择优）。输出 `text-extract/components.json` 的 `root_box`/`original_image_box`/`relative_parent_box` 已按下方坐标换算公式折算完毕，创建条目时直接复制。

逐行带检测文字像素的规则（脚本行为规范）：
   - **行带锚定**：已知该父组件内应检测 N 行文字时（如菜单 8 行、列表行 6），按行高与行距模式划分行带（等距行模式本身是强校验信号——检测出的行中心若偏离等距模式 > 半行高，说明检测有误，禁止采信）；无先验行数时按水平投影谷切分。
   - **色通道检测**：按参考图实际配色选择文字通道——亮字（灰度 > 135）、金/米字（R>150 且 G>110）、深色字（灰度 < 110-120，金底/米底上的暗字）。通道阈值须先用已知良好行（可用上轮产物或相邻清晰行）验证命中，再全量应用。
   - **固定算法**：水平膨胀分组（`extract_text_labels.py` 同款：dilate 仅做行内字符桥接）+ **原始 mask 紧致 bbox**；排除图标列/徽标（文字左缘通常整齐对齐，图标区单独排除）。
   - 确实无法像素检测的文字（极低对比度、全遮挡行），如实记录「未检出」并跳过——由 04.5A 画板阶段用户手工补框（画板本就是布局权威，用户拖框优于接受错误几何）。禁止以 AI 估测填补。
2. **AI 内容读取**：对全部检出框画编号标注图（红框高对比，坐标系匹配底图），**一次** `analyze_multimedia` 批量读 `display_text`/`is_dynamic`/`static_part`/`dynamic_part`/字体样式，prompt 逐字使用 Stage 02 Step 5.5.2 的读文 prompt（编号框已画在图上，AI 只读不测）。

**创建 `uss_only` 节点**：对每个检测到的文字标签，在 `semanticRecords.json` 中追加 `text-label-{NNN}` 条目（格式同 Stage 02 Step 5.5，含 `has_dynamic_content` 和 `dynamic_content` 字段——从 Step 5.5.2 读文结果返回的 `is_dynamic`/`static_part`/`dynamic_part` 填充），`node_id` 命名为 `{parentNodeId}-text-{NNN}` 以保持父子关系。创建 `nodes/{parentNodeId}-text-{NNN}/` 目录和 `semantic.json`。

**坐标换算（递归层，OpenCV 像素框 → 各坐标空间）**：OpenCV 在父组件原图裁剪（尺寸 = `P.original_image_box.width × height`）上检出的紧致像素框 `(cx, cy, cw, ch)`，写入条目时：

- `original_image_box`（原图空间）= `{x: P.original_image_box.x + cx, y: P.original_image_box.y + cy, width: cw, height: ch}`
- `root_box`（与 `P.root_box` 同空间）= `P.root_box ⊕ (cx/crop_w × 100, cy/crop_h × 100, cw/crop_w × 100, ch/crop_h × 100)`
- `relative_parent_box` 初始与 `root_box` 同值；父子挂载与后续重新挂载统一按下方投影算法计算。

**挂载规则**：同 Stage 02 Step 5.5.4——文字标签保留为独立节点，不回填不删除。⚠️ **同级 OpenCV 子组件的 `root_box` 位于子项隔离图坐标系，与 text-label 的 `root_box` 不同空间，禁止直接比较**。在 §8.5 语义分类完成后执行父子挂载，必须先投影：

1. 对每个同级子组件 C，计算投影框 `C_box = 父组件.root_box ⊕ C.relative_parent_box.percent`（把 C 投回 text-label `root_box` 所在坐标系；`x = 父.root_box.x + 父.root_box.width × pct.x / 100`，y/width/height 同理）。
2. 计算文字标签 `root_box` 被 `C_box` 包含的比例（交集面积 / 文字框面积），取 > 80% 中包含比例最高者。
3. 达标：`parent_id` 设为该子组件，`depth` = 子组件 `depth + 1`，`relative_parent_box` = 文字 `root_box` 相对 `C_box` 的百分比，并**同步覆盖顶层四标量 `relative_parent_x/y/w/h_pct`**（04.5A 画板导入优先读取四标量，不同步会静默回退旧挂载值）；不达标：保持挂在父组件，`relative_parent_box` = 文字 `root_box` 相对父组件 `root_box` 的百分比（四标量同步）。
4. Stage 04 后续层拆出新的子组件时，已挂载的 text-label 按同一投影算法重新评估——如果某个新子组件的投影框包含该 text-label > 80%，更新 `parent_id`、`relative_parent_box` 与四标量指向新子组件。text-label 的 `root_box` 永远保留在其检测时所在坐标系，不随改挂变换。
5. **重算范围纪律**：四标量/`relative_parent_box` 重算**只允许作用于实际改挂或新增的标签**，禁止对未受影响的存量标签做「顺带批量重算」——任何重算都可能引入新的 base 错误（2026-09-04 实测：批量重算把面板级标签的 percent 误用了方形画布 base）。**无父标签（`parent_id=null` 面板级）的 `relative_parent_box` 恒等于 `root_box` 的百分比形式（base 是 content_rect），任何阶段不得改用其他 base**。

此外，`generation_atlas.py split` 会在拆图阶段自动检测 **AI 未修改输出**（输入页与输出页像素完全一致），在 `split-results.json` 中标记 `ai_unchanged_output: true` 并将所有项设为 `failed`，reason=`ai_unchanged_output`。这可以在拆图阶段就拦截 AI 原样返回的问题，无需等到 OpenCV。

**裁剪图归档**：OpenCV 完成后，将每个子组件裁剪图从 `child-extract/component_{NNN}.png` 复制到 `nodes/{childNodeId}/original_crop.png`（`shared/contracts.md`「环境与目录」段定义的标准位置）。同时更新组件树该节点的 `file` 字段指向 `nodes/{childNodeId}/original_crop.png`。此文件是 Step 9 最终图片生成的输入——若缺失，Step 9 无法准备图集输入，ui-image-generator 会回退文生图（`text2img_fallback`）导致风格不一致。

## 8.5 语义分类与合并

按 Step 6 的标注图方式对子组件批量语义分类。子节点的 `semantic.json` 直接保存到 `nodes/{childNodeId}/semantic.json`（不是 `child-extract/nodes/` 下）。`child-extract/` 目录只存放 OpenCV 中间产物（transform.json、components.json、component_*.png），不创建 `nodes/` 子目录。

将语义结果与几何合并到组件树：

```csharp
// 注：mergeTree 与 output 为同一路径是故意的——工具先读入内存再覆盖写回，in-place 安全
Create.GameFramework.TJStyleRefTree.MergeChildTree("{childManifest}",
    "{processingDir}/component-tree.json", "{processingDir}/component-tree.json",
    "{parentNodeId}", "{childSemanticRecords}");
```

## 8.6 失败补图

失败节点单独重试。首轮图集中多个节点共用一页，某个节点失败不影响其他节点。将失败的 nodeId 单独重新装箱、重新提交图生图。

### retry-1：调整 prompt

将失败节点单独装箱，用 `analyze_multimedia` 分析，判断应该移除的背景的具体描述，可追加在prompt后面。
然后重新提交图生图，prompt调整可参考：
- 加强背景移除指令：`完全移除最底层背景。所有移除区域填充为{key_color}。`
- 追加具体移除项：`移除所有底板、面板和容器。移除所有文字、数字和标签。`
- 保留隔离约束：`保留所有按钮、图标和交互元素的位置。`

输出保存到 `retry-1/output-page-0.png`，然后执行 §8.3 验证 → 拆图 → §8.4 OpenCV 验证。

### retry-2：换策略

retry-1 仍失败的节点。以下策略都是对 **source 图的预处理**，不是跳过装箱的借口。

> ⛔ **retry-2+ 与首轮共用同一条链**：pack → img2img(size=2048) → split → OpenCV。
> 无论 retry 几轮、对 source 做了什么预处理，这条链都不能断。
> `size` 参数始终等于 `page_size`（2048），禁止改。
> 
> 图生图执行规则见 `shared/contracts.md`。

策略（任选其一，预处理后重新 pack）：

1. **关键色裁剪**：`isolation_input.png` 不再需要去色（AI 输出已含关键色背景），直接使用作为新 source → 重新 `pack` → img2img（size=2048）→ split
2. **裁剪聚焦**：从 `isolation_input.png` 中裁出失败区域（如中心区域或 OpenCV 检测到子组件的区域），作为新 source → 重新 `pack` → img2img（size=2048）→ split

### 仍失败

retry-2 仍失败则停止并用 `ask_user` 讨论，不自动逐节点生成回退。此时才可标 `recursion_done`（`recursion_reason` 注明 `"zero_children_after_retry"` 或 `"self_reference_after_retry"`）。

### ⛔ 禁止的 workaround（FOREVER）

当 split 失败或 OpenCV 返回 0 子时，以下操作**绝对禁止**——它们不在流程中，会产出不合格图片或永久丢失层级：

| 禁止操作 | 后果 |
|---------|------|
| 设置 `key_color=None` | 禁用关键色→透明转换，magenta 残留在图片边缘 |
| 用 alpha 通道检测替代 key_color 清洗 | 源图是 RGB 无 alpha，检测无效 |
| 手动裁剪 content_rect 跳过 pack→img2img→split 链 | 绕过流程，坐标不保证 |
| 接受带 magenta 边框/光晕的图片作为 `final_transparent.png` | 下游 USS 引用会显示紫色边框 |
| 不走 retry-1/retry-2 就标 `recursion_done` | 子组件永久丢失 |

**唯一正确的失败路径**：retry-1（调 prompt）→ retry-2（换策略）→ 仍失败 → `ask_user`

详细规则**读** Stage 04 SKILL.md Step 8「失败补图」段。

## 8.7 递归控制

每层必须完成"装箱 -> 图生图 -> 拆图 -> OpenCV -> 语义分类 -> 合并组件树"后才能进入下一层。

**递归循环**（必须逐层执行，禁止跳层）：

```
depth = 1
while True:
    待拆 = 收集组件树中所有 kind=composite && state=valid && 未标记 recursion_done 的节点
    # ⚠️ game_content_area 强制跳过递归——它是 3D 场景渲染区域，不含可交互子 UI
    待拆 = [n for n in 待拆 if n.semantic.component_type != 'game_content_area']
    if 待拆为空:
        break  # 递归结束

    # 1. 当前层所有待拆 composite 装箱成图集页
    # 2. 每页提交一次图生图做子项隔离
    # 2.5 ★ 必须验证输出页（§8.3 验证）：analyze_multimedia 检查背景是否移除
    #        - 背景未移除的 slot → 标记 img2img_quality_failed → 进入 §8.6 失败补图
    # 3. 拆图 -> 逐槽 OpenCV
    # 3.5 ★ 必须验证 OpenCV 结果（§8.4 验证）：检查 0 子/自引用/子项过少
    #        - 0 子或自引用 → 进入 §8.6 失败补图重试（retry-1 → retry-2）
    #        - retry-2 耗尽后才可标 recursion_done
    # 4. 语义分类 -> 合并到组件树
    # 5. 对失败节点执行 §8.6 失败补图（retry-1 → retry-2 → 仍失败才标 recursion_done）

    # 检查新加入的子节点
    新composite = 新子节点中 kind=composite 的
    if 新composite 为空:
        break  # 全部是叶子，递归结束
    depth += 1
    # 继续循环：对新的 composite 再次装箱图集做子项隔离
```

> **禁止跳层**：如果 depth-1 发现的子组件中有 `kind=composite`，**必须对它们做 depth-2 的图集子项隔离**，不能直接标记 `recursion_done` 跳过。只有满足下方"标记条件"的 composite 才可跳过。

### 递归完成判定（进入步骤 9 的前置条件）

检查 `component-tree.json` 中所有 `state=valid` 的节点：
- 若存在 `kind=composite` 且未标记 `recursion_done` 的节点 -> **禁止进入步骤 9**，继续步骤 8 拆分
- 若全部 `kind=composite` 节点已标记 `recursion_done` 或全部为 `kind=leaf` -> 递归结束，进入步骤 9

### `recursion_done` 标记条件（满足任一即可标记）

1. 子项隔离图生图经 retry-2 重试后仍返回 0 个子组件
2. 子项隔离图生图连续 2 轮补图后仍失败（retry-2 仍失败）
3. 复合节点的原图裁剪面积 < 64x64px（尺寸过小）
4. 子项隔离后 OpenCV 找到的子组件面积未严格小于父级（自引用轮廓），经 retry-2 重试仍如此

> 条件 1 和 4 的前置要求：首次出现 0 子或自引用时**不可直接标 recursion_done**，必须先走失败补图重试。只有 retry-2 耗尽后才可用这两条标记。标记时 `recursion_reason` 必须注明 `"zero_children_after_retry"` 或 `"self_reference_after_retry"`。

> "子组件中有 composite"不是 `recursion_done` 的标记条件——必须继续递归到下一层拆分。

标记 `recursion_done` 时必须记录原因。标记后的节点在步骤 9 中作为 `composite_shell` 配方生成外壳图。其挂载的 text-label 子节点作为独立 UXML Label 渲染，不随外壳图生成。

> **禁止跳过递归直接生成外壳**：步骤 9 的 `composite_shell` 配方只处理"已经拆完子项的"复合节点。跳过步骤 8 直接生成外壳会导致复合节点内部的子组件层级永久丢失，下游 UXML 无法正确嵌套。

### 停止条件

以下任一情况必须写诊断并停止，禁止自动扁平化或猜测：
- 子项不在父框内、面积未严格小于父级，或轮廓覆盖父级自身；
- 节点 ID、祖先链或图像 hash 循环/重复；
- 直接子项归属不唯一、严重重叠或无新候选；
- alpha mask、隔离图验证、图生图或语义分类失败；
- 低置信度、无效语义或父/子关系与视觉不一致。

> **组件树最终装配**：`extract_reference_components.py` 产出几何骨架，`TJStyleRefTree.BuildTree/MergeChildTree`（原 `component_tree.py`）合并几何与语义。`render_mode`、`source`、`shell_path` 等字段在步骤 9 最终生成完成后写入 `component-tree.json`。
