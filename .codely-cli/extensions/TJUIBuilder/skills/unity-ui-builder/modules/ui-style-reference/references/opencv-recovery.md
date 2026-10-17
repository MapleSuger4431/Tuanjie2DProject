# OpenCV 后验对比与恢复策略

> 本文件是 Stage 02 SKILL.md Step 5 的配套参考。以下规则在 OpenCV 提取根级组件后立即执行。

## 1. 后验对比（三种偏差）

执行 OpenCV 提取后必须检查三种偏差：

### 1.1 缺失组件

原图可见但未提取的 UI 元素。两种根因：

- **img2img 染色**：元素被染成接近关键色区域（hue 偏移 <=15deg），`foreground_mask` 误排除。自定义脚本收紧 `hue_tolerance` 即可恢复。
- **img2img 完全丢失**：元素被关键色区域整体替换，`ui_only_2048.png` 中该区域已无原始像素。**无法从隔离图恢复**，必须从原始参考图提取：按元素视觉特征（亮/白色元素用灰度亮度阈值、纯色元素用 HSV 色相范围过滤、强边缘元素用 Canny 边缘检测）从原图裁取目标区域生成前景 mask → `morphologyEx(CLOSE->OPEN)` 清理 → mask 作 alpha 通道 → 裁到 tight bounds → 保存为 `nodes/{nodeId}/final_transparent.png`。

### 1.2 噪声碎片

提取出但原图中不存在的微小元素。调高 `--min-dimension` 或 `--min-area` 重新提取。

### 1.3 过度合并

一个组件的面积占内容区 >=40%，或裁剪图中包含多个语义独立的 UI 区域。根因是 UI 元素间的关键色间隙在 img2img 后不够宽或不够纯。处理：缩小 `morph_kernel_size` 使间隙不被弥合，或改用 Canny 边缘检测替代 HSV mask 来分离相邻元素。重新提取后再次执行后验对比。

### 1.4 自引用组件（self_reference）

检查 `components.json` 中的 `self_reference_warnings` 字段。自引用组件（`self_reference=true`，面积占比 >=90% 父级）不是真实 UI 元素，而是 OpenCV 将整个画面识别为单一轮廓——通常因为 Step 3 隔离未完全移除背景，或存在连接所有 UI 元素的全屏容器面板。

**处理方式**（在 §2 `analyze_multimedia` 后验对比之前执行）：

| 情况 | 判据 | 处理 |
|------|------|------|
| **有非自引用兄弟组件** | `self_reference=true` 的组件存在，且同级别有 `self_reference=false` 的组件 | 该组件是无效全屏容器。标记 `state=skip`、`skip_reason="self_reference_full_screen_container"`，直接写入 `semanticRecords.json`（格式见下方），**不提交 Step 6 语义分类**（不画入标注图、不消耗 AI 调用）。其视觉覆盖区域由兄弟组件 + Step 8.5 背景节点共同覆盖 |
| **唯一组件** | `self_reference=true` 的组件是同级唯一组件（无兄弟） | Stage 01 隔离失败（背景未移除导致整个画面被当成一个轮廓）。用 `ask_user` 通知 orchestrator 重新派发 Stage 01 Step 3（加强背景移除 prompt）。retry 仍失败则 `ask_user` 讨论 |

**`semanticRecords.json` 手动写入格式**（"有非自引用兄弟组件"情况）：

对每个自引用组件，在 Step 6 语义分类产出 `semanticRecords.json` 后，追加以下条目（与碎片过滤 §4 的 `fragment_split` 模式完全一致——orchestrator 手动写入 skip 记录）：

```json
{
  "node_id": "component-001",
  "component_name": "full_screen_container",
  "component_type": "container",
  "display_text": "",
  "text_type": "none",
  "art_text_style": "",
  "visual_description": "self-reference component covering >=90% of parent area",
  "kind": "composite",
  "has_dynamic_content": false,
  "functional_type": null,
  "style_group_hint": "",
  "contamination_description": "",
  "confidence": 1.0,
  "state": "skip",
  "quality": null,
  "reasoning": "OpenCV detected a single contour covering >=90% of the content area. This is a full-screen container artifact, not a real UI element. Skipped to avoid processing the entire UI surface as one component.",
  "skip_reason": "self_reference_full_screen_container"
}
```

> ⚠️ 自引用组件 ≠ 过度合并（§1.3）：过度合并是一个组件包含多个语义独立区域，可通过调整 morph_kernel 解决；自引用是整个画面被当成一个轮廓，根因是背景未移除或存在全屏容器面板。

> ⚠️ 此规则仅适用于 **Step 5 根级提取**。递归层级（Step 8）的自引用由 `preflight_check.py validate_recursion` 检测并触发 §8.6 失败补图，不在此处处理。

> ⚠️ 标记 `state=skip` 的自引用组件不产出任何图片，不进入 Step 7/8/9。组件树中保留记录以供追溯。`preflight_check.py` 对 `state != "valid"` 的节点自动跳过所有检查。

## 2. 后验对比执行方式（三态门禁）

先跑"降档重跑发现"半场（把 §2.5 恢复策略第 1 步提前为发现工具；门禁契约见 `../shared/contracts.md`「三态门禁」节）：

```text
{PYTHON} scripts/extract_reference_components.py \
  {processingDir}/ui_only_2048.png \
  {processingDir}/transform.json \
  {processingDir}/root-extract-downgraded \
  --min-area 32 --min-dimension 16

{PYTHON} scripts/find_missing_components.py \
  {processingDir}/root-extract/components.json \
  {processingDir}/root-extract-downgraded/components.json \
  --json-output {processingDir}/root-extract/missing-components-check.json
```

- 退出码 0（pass：零候选框、无"框太小"、无超大方框）→ ✅ 免视觉，后验对比结束，进入 Step 6 语义分类
- 退出码 2（review）→ **仅对被标记对象**处理：
  - **候选框**（降档新增、与原框 IoU<0.1 且不被 80% 包含且最小边 ≥16px）→ 把候选框画在 `diagnostic_root_boxes.png` 对应位置（纯红 `#FF0000`），一次 `analyze_multimedia` 只确认候选区域：确认为真实元素 → 按 §1.1 恢复；确认为噪声碎片 → 忽略
  - **框太小**（面积 < 256px²，机械判定）→ 按 §1.2 处理（调高阈值重提）
  - **超大方框**（面积 ≥ 内容区 40% 且非全幅，机械判定 = §1.3 过度合并触发条件）→ 按 §1.3 处理；"框内含哪几个语义区域"仍需视觉（下方 prompt 只回答该框）
- 审计：`missing-components-check.json` 必须落盘，免视觉判定留痕

**候选区域仲裁 prompt**（仅 review 时；逐字使用）：

```
这张图显示了OpenCV提取的边界框叠加在UI隔离图上。对每个框回答：1）它是单个UI元素还是包含多个独立的UI区域？2）所有框之外是否有可见但未被提取的UI元素？3）是否有框太小而无意义？逐框回答。
```

## 2.5 递归层级后验对比（Stage 04 适用）

Stage 04 的 `validate_recursion` 仅检测零子组件和自引用，**不检测缺失组件**——即"隔离图中可见但 OpenCV 未提取到的 UI 元素"。递归层级必须在 `validate_recursion` 通过后额外执行后验对比。

**执行方式（三态门禁）**：先跑"降档重跑发现"半场（恢复策略第 1 步提前为发现工具；门禁契约见 `../shared/contracts.md`「三态门禁」节）：

```text
{PYTHON} scripts/extract_reference_components.py \
  {slotImage} {slotsDir}/{nodeId}.transform.json {childOutputDir}-downgraded \
  --min-area 32 --min-dimension 16 \
  --parent-content-rect 0 0 {slotImageWidth} {slotImageHeight}

{PYTHON} scripts/find_missing_components.py \
  {childOutputDir}/components.json \
  {childOutputDir}-downgraded/components.json \
  --json-output {childOutputDir}/missing-components-check.json
```

- 退出码 0（pass：零候选框、无"框太小"、无超大方框）→ ✅ 免视觉，进入 §8.5 语义分类
- 退出码 2（review）→ **仅对被标记对象**处理（同 §2 根级规则）：
  - **候选框** → 把候选框画在 `nodes/{parentNodeId}/child-extract/diagnostic_root_boxes.png` 对应位置（纯红 `#FF0000`），一次 `analyze_multimedia` 只确认候选区域：确认为真实元素（如细边框下拉框、小箭头、浅色开关轨道）→ 按下方恢复策略处理；确认为噪声碎片 → 忽略
  - **框太小**（机械判定）→ 按 §1.2 处理
  - **超大方框**（≥40% 内容区，机械判定 = §1.3 触发条件）→ 按 §1.3 处理；"框内多元素"的语义拆分仍需视觉（下方 prompt 只回答该框）
- 审计：`missing-components-check.json` 必须落盘，免视觉判定留痕

**候选区域仲裁 prompt**（仅 review 时；逐字使用）：

```
这张图显示了OpenCV提取的边界框叠加在复合组件子项的UI隔离图上。对每个框回答：1）它是单个UI元素还是包含多个独立的UI区域？2）所有框之外是否有可见但未被提取的UI元素（如细边框下拉框、小箭头、浅色开关轨道）？3）是否有框太小而无意义？逐框回答。如果有遗漏的元素，描述它们的大致位置。
```

**仲裁判定与处理**（沿用原判定表）：

| 结果 | 处理 |
|------|------|
| 无缺失 | ✅ 进入 §8.5 语义分类 |
| 缺失组件（AI 确认候选） | 按下方恢复策略处理 |
| 过度合并 | 按 §1.3 处理（缩小 `morph_kernel_size` 或改用 Canny） |
| 噪声碎片 | 按 §1.2 处理（调高 `--min-dimension` 或 `--min-area`） |

**缺失组件恢复策略**（递归层级）：

1. **调整 OpenCV 参数重试**：降档产物（`{childOutputDir}-downgraded/`）即本步的执行结果——候选确认后直接沿用其 `components.json` 中的框（禁止再降参重跑）；确认的缺失元素按 §1.1 处理。
2. **参数调整仍缺失**：按 §1.1 的 "img2img 完全丢失" 策略——从原图裁剪区域按元素视觉特征（亮/白色元素用灰度亮度阈值、纯色元素用 HSV 色相范围过滤、强边缘元素用 Canny 边缘检测）生成前景 mask → `morphologyEx(CLOSE->OPEN)` 清理 → mask 作 alpha 通道 → 保存为 `nodes/{nodeId}/final_transparent.png`。同时手动在 `components.json` 中补录该组件的 `root_box`（从仲裁确认的位置坐标转换）。
3. **恢复后重新执行后验对比**（重跑 `find_missing_components.py` 确认零候选），才进入语义分类。

## 3. 相邻组件检查

检查是否存在两个框相邻（间距 < 10px）且在垂直方向高度重叠 > 50%——这通常表示 OpenCV 将一个视觉元素拆成了两个框。

**两类处理**：

### 3a. 同一逻辑元素的碎片（如长标题拆成两半）

两个框属于同一 UI 控件的不同部分（如长标题被空格拆开）。此类相邻框应：
- 在 Step 6 语义分类中**都标为 valid**（各自有独立像素）
- 在 Step 7.1 中归入同一 `style_group`（如果视觉风格一致）
- 下游各自生成透明 PNG（关键色→透明转换后），ui-panel-builder 可将它们放在相邻位置还原原始布局
- ⚠️ **禁止将其中一个标 `state=skip`**——每个框都有独立像素内容，skip 会导致像素永久丢失

### 3b. 属于同一视觉单元的相邻组件（如角色立绘+宠物跟随）

两个框都是独立视觉元素，但属于同一逻辑视觉单元（如角色立绘和旁边跟随的宠物）。在语义分类完成后（Step 6 产出 `component_type` 后），对 `adjacent_pairs` 中的每对用 `analyze_multimedia` 做"逻辑元素归属"判定：

> ⚠️ **执行时机**：在 Step 6 语义分类之后、Step 6.3 建树之前。此时已知道每个组件的 `component_type`，可以据此过滤。

prompt（逐字使用）：
```
这张图显示了OpenCV提取的边界框叠加在UI参考图上。某些相邻的框可能属于同一逻辑视觉单元（如角色和宠物、角色和武器）。对每对相邻的框回答：（1）这两个框是否属于同一逻辑视觉单元、应作为整体显示？是/否（2）如果是，简述原因（如"角色和宠物"、"角色手持武器"）。返回JSON数组：[{"a": 框编号, "b": 框编号, "same_unit": true/false, "reason": "..."}]。
```

**合并条件**（全部满足才合并）：

> ⚠️ **前置：skip 组件恢复评估**。在检查合并条件前，先扫描 `adjacent_pairs` 中是否有被 `state=skip` 的组件。如果 skip 的 reasoning 含"残缺"、"重复"、"重叠"等与相邻相关的关键词，将该组件**临时恢复为 `valid`** 参与合并评估。合并评估结束后，未被合并的恢复为 `skip`。

1. AI 判定 `same_unit=true`
2. 两个框在 `adjacent_pairs` 中已配对（gap < 10px 且重叠 > 50%）
3. 两个框都不是 `state=skip`（经前置恢复评估后）
4. **两个框的 `component_type` 都属于视觉内容类型**：`game_content_area`、`display`、`icon`、`custom`（仅当 AI 在 reasoning 或 visual_description 中标注为"角色立绘"、"宠物"、"武器"、"装饰物"等非 UI 控件内容时）。**禁止合并 `button`、`panel`、`bar`、`badge`、`hud_element` 等可交互 UI 控件**——即使 AI 判定 `same_unit=true`，这些是独立交互控件不应合并

**合并操作**：
- 将两个框的 `root_box` 合并为一个外接矩形（union bbox），更新到 `components.json` 中保留节点的 `root_box`
- **删除被合并的组件**：从 `components.json` 的 `components` 数组中删除、从 `adjacent_pairs` 中删除引用该组件的配对、从 `semanticRecords.json` 中删除该记录、删除 `root-extract/component_{NNN}.png` 裁剪图、删除 `nodes/{nodeId}/` 目录
- 用合并后的 bbox 重新裁剪保留节点的 `component_{NNN}.png`（从当前层的 OpenCV 输入图按新 bbox 裁剪；depth=0 时输入图为 `ui_only_2048.png`，depth≥1 时为父节点的 slot 图）
- 合并后的组件 `kind` 判定为 `leaf`（视觉单元不是可交互容器）

> ⚠️ 合并后的组件不应判为 `composite`——即使包含多个视觉元素（角色+宠物），只要不是可独立交互的 UI 控件，就是 `leaf`。
