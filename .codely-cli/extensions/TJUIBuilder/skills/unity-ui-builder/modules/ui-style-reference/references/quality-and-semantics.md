# 语义分类与 Quality 验证详细规则

> 本文件是 Stage 02 SKILL.md Step 6/6.1/6.2 的配套参考。以下规则在语义分类和 quality 验证时逐字执行。

## 0. 组件命名唯一性规则

- `component_name` 在同一参考图内必须可区分：**不同组件禁止同名**
- 同一图中**视觉外观相同、功能相同的并列同类组件**（如 8 个相同的服务器条目按钮），`name` 必须按阅读位置（从左到右、从上到下）追加序号：`服务器条目按钮1` … `服务器条目按钮8`
- 原因：Stage 03 归组去重后，非代表 `crop_path` 记 `reuse_from:{代表组件名}`，下游按名解析（画板预览 nameToId、resolver 悬空判定、preflight 3c）依赖组件名唯一指向代表；全同名会让按名解析命中任意一个非代表并自环
- Stage 03（`references/post-processing.md` §5「组件名唯一化」）对漏网同名情况做确定性兜底改名

## 1. Quality 两步验证 prompt（逐字使用）

对 `suspect` 组件，用 `analyze_multimedia` 分析其裁剪图。裁剪图背景为关键色（已由 `extract_reference_components.py` 转换为透明 alpha=0），非硬编码：

```
首先，识别这张图中的主要视觉元素（主导组件）。然后，描述所有可见元素，特别注意边缘和角落。对每个元素，记录其艺术风格（如有机/手绘风格 vs 几何/精确风格）以及它是装饰性还是功能性。透明背景（alpha=0，由关键色转换而来）不是污染。重要：颜色相似的元素可能仍属于不同的UI组件——优先以形状和边缘特征而非颜色相似性判断。检查裁剪边界处是否有元素看起来是更大相邻UI元素的横截面（如只看到面板或按钮的顶部或侧边边缘，该元素延伸到本裁剪区域之外）。描述完后回答：这张图的边缘或角落是否包含不属于主组件的其他UI元素像素（如部分按钮、图标、文字、面板或装饰）？回答'clean'或'contaminated'并简述理由。
```

- `analyze_multimedia` 确认有污染 → 最终 `quality = contaminated`，同时将 AI 描述的污染内容（位置 + 视觉特征，如 "a blue panel fragment in the bottom-right corner"）写入 `semantic.json` 的 `contamination_description` 字段，供下游 ui-image-generator 构建图生图清理 prompt
- `analyze_multimedia` 确认无污染 → 最终 `quality = clean`（降回）

最终 `quality` 字段写入 `semantic.json`，下游 ui-image-generator 读取。

## 2. `quality=contaminated` 处理规则（按节点类型区分）

| 节点类型 | `quality=contaminated` 的行为 | 原因 |
|----------|------------------------------|------|
| **leaf** | 标记 `source=ai_generate`、`crop_path` 指向 `final_transparent.png`（关键色裁剪产出的透明 PNG，含组件像素 + 邻居残片）、`quality=contaminated`、`contamination_description` 记录污染内容描述。由 Step 9a 关键色裁剪产出透明 PNG，下游 ui-image-generator 以透明 PNG 为图生图输入，prompt 基于 `contamination_description` 构建定制化移除指令重新生成 | 透明 PNG 主体内容正确、有邻居残片，图生图比文生图保真度更高 |
| **composite** | 仅记录 `quality=contaminated` 作为诊断标记，**不触发 fallback**、不改变 `source`/`render_mode` | composite 本就走 `composite_shell`（img2img），Step 8 子项隔离会移除残片，quality 字段不改变其处理路径 |

## 3. `kind` 判定规则

- `composite`：组件内部包含**可交互的子 UI 元素**（按钮、图标、进度条、独立文字行等）。⚠️ **标题装饰图案、按钮装饰元素不是"可交互子UI"**——art_text 标题（含装饰图案如花朵、云朵底板）和带装饰的按钮（含叶子、图标等装饰）整体判为 `leaf`，装饰是视觉内容的一部分。
- `leaf`：组件是**单一视觉单元**（角色立绘、场景插画、单个图标、单行文字、单个按钮等）。视觉内容丰富但无可交互子 UI 的判为 `leaf`，标记 `state=valid`、`source=direct_use`（从裁剪图关键色→透明转换直接使用）。仅当组件无实际视觉内容（纯文字标签将由 UXML Label 渲染）时才标 `state=skip`、`source=skip_generation`。⚠️ **角色立绘中的宠物、武器、装饰物不是"可交互子UI"**——它们是视觉内容的一部分，不独立交互，因此角色展示区整体判为 `leaf`。即使 `component_type=game_content_area` 且包含多个视觉元素，只要这些元素不独立交互就判为 `leaf`，不递归拆分、不生成 shell。⚠️ **art_text 标题和带装饰的按钮也判为 `leaf`**——文字和装饰已烘焙在图片中，整图可直接使用，不需要 composite shell 生成。

> ⚠️ `state=skip` 标记的节点**不产出任何图片**，下游 ui-image-generator 不为其生成内容，最终 UI 会完全缺失该元素。`skip_generation` 仅适用于确实不需要图片的元素（如纯文字标签将由 UXML Label 渲染）。**有实际视觉内容的装饰元素**（角色插画、场景图标、装饰性图形等）应标 `state=valid`、`source=direct_use`（从裁剪图关键色→透明转换直接使用），而非 `skip_generation`。

## 4. 碎片识别与过滤

语义分类后、建树前执行。基于 Step 6 语义结果 + Step 5 `adjacent_pairs` 数据做静态判断，识别 OpenCV 将一个视觉元素拆成的碎片并将其过滤。

**过滤条件**（满足全部才标 `state=skip`，`skip_reason="fragment_split"`）：
1. `confidence` 为 `medium` 或 `low`
2. 存在另一个组件与之在 `adjacent_pairs` 中配对（gap < 10px 且垂直高度重叠 > 50%）
3. 本组件或配对组件的 `reasoning` 中包含以下特征词之一（不区分大小写）：`split`、`fragment`、`segment`、`partial`、`残片`、`拆分`、`被检测拆开`

三条同时满足 → 标 `state=skip`、`skip_reason="fragment_split"`。配对组件（置信度高或面积大的那个）保留为 `state=valid`。碎片组件不进入下游、不产出图片，组件树中保留记录。

> 仅过滤 `kind=leaf` 的碎片；`kind=composite` 不适用此规则。
>
> 若组件有实际视觉内容（即使它"属于"另一个更大的元素），**禁止标 `state=skip`**。此时应标 `state=valid`、`source=direct_use`，并在 `reasoning` 中注明"与 {相邻组件名} 属于同一逻辑元素但各自有独立像素内容"。
>
> **常见误判场景**：OpenCV 将一个长标题拆为两个相邻框，AI 将其中一个标为"另一个的一部分"并 skip。这会导致标题的一半像素永久丢失。正确做法是两个框都保留为 valid，下游各自裁剪后直接使用透明 PNG。
