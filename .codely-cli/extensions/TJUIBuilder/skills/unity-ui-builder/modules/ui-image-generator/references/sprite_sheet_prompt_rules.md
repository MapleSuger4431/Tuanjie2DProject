# Sprite Sheet 提示词构建规则

> 本文件是 `modules/ui-image-generator` SKILL.md 的配套参考，包含 Path C（Sprite Sheet 生成）的完整提示词构建规则。
>
> **适用条件**：`sprite_sheet_group` 非空的元素。根据 `crop_path` 自动判定调用模式——`text2img_fallback` → 文生图；正常路径 → img2img（传入父级透明 PNG，上传为 `image_urls`）。

## 1. 设计原理

将多个小型透明 UI 元素合并为一张 sprite sheet 生成，然后通过 alpha 投影切片为独立 PNG。

**双方向堆叠**：根据元素宽高比自动选择堆叠方向，最大化画布空间利用率：

| 方向 | 适用元素 | 排列方式 | 切片轴 | 空间利用 |
|------|---------|---------|--------|---------|
| **行堆叠** (row) | 宽元素 (w ≥ h) | 每个元素占一行，从上到下竖向堆叠 | 沿 X 轴投影 alpha → 找 Y 分段 | 元素横向铺满，行高紧凑 |
| **列堆叠** (column) | 高元素 (h > w) | 每个元素占一列，从左到右横向排列 | 沿 Y 轴投影 alpha → 找 X 分段 | 元素纵向铺满，列宽紧凑 |

**为什么需要双方向**：纯行堆叠时，高元素（如竖条状手柄）会撑高整行，旁边的矮元素旁边大片留白。按宽高比分流后，宽元素走行堆叠、高元素走列堆叠，各自沿短轴紧凑排列，空间浪费最小化。

与功能控件子部件独立生成的区别：

| | 独立生成（Path D） | Path C Sprite Sheet |
|---|---|---|
| 触发条件 | `sprite_sheet_group` 为空 | `sprite_sheet_group` 非空 |
| 生成模式 | 每个元素独立生成（img2img 或文生图） | 多元素合并为一张 sprite sheet 生成（img2img 或文生图） |
| 元素关系 | 同一控件的子部件（track/fill/handle） | 不同元素的并行组合（图标/按钮/徽章） |
| 风格来源 | 参考图（透明 PNG）或风格描述 | `style_baseline` + `panel_type_styles` |
| 切片方法 | 不需要切片（每个子部件独立生成） | Alpha 投影找边界 |
| 堆叠方向 | 不适用 | 行堆叠 + 列堆叠 |

## 2. Prompt 公式

> **宽高比规则豁免说明**：Sprite Sheet 的画布固定为 `2048x2048`（1:1），不适用全局 `{aspect_ratio}` 前置规则。每个元素的宽高比已通过 §3.1 的 `aspect_ratio_description` 逐元素描述（如 `very wide and thin shape, 8:1 width-to-height ratio`），在元素级别满足宽高比引导需求。

### 2.1 行堆叠 Prompt (orientation = "row")

```
"{style_baseline}, game UI sprite sheet kit on transparent background, each element on its own row, stacked vertically: {element_descriptions}. Do not assemble the elements into a complete interface. Each sprite must be on a separate row, fully isolated and non-touching. No text, no other elements."
```

### 2.2 列堆叠 Prompt (orientation = "column")

```
"{style_baseline}, game UI sprite sheet kit on transparent background, each element in its own column, arranged horizontally from left to right: {element_descriptions}. Do not assemble the elements into a complete interface. Each sprite must be in a separate column, fully isolated and non-touching. No text, no other elements."
```

**各字段来源**：

| 字段 | 文生图（`crop_path == "text2img_fallback"`） | img2img（`crop_path` 为正常路径） |
|------|---------------------------------------------|----------------------------------|
| `style_baseline` | 从 `style_guidance.style_baseline` 读取（文字关键词过滤后） | **不注入**（风格由参考图决定） |
| `element_descriptions` | 各成员元素的视觉描述拼接（见 §3） | 同左，但仅描述"这是什么"不写"它长什么样" |
| `image_urls` | 省略 | 父级透明 PNG 的 CDN URL（先上传） |

**`style_baseline` 文字关键词过滤**（仅文生图模式）：与独立文生图一致（见 `text-to-image-prompt-rules.md` §2.1）——移除所有文字相关关键词（`text`、`typography`、`font` 等），因为 sprite sheet 中所有元素 `text_type != art_text`，均不含文字。img2img 模式不注入 `style_baseline`，无需过滤。

## 3. 元素描述拼接（element_descriptions）

每个元素的描述格式：

```
{component_type_singular} ({visual_subject}, {color_description}, {aspect_ratio_description}{enhancement_keywords})
```

按 `sprite_sheet_slot` 从小到大（从上到下）拼接，逗号分隔。

### 3.1 各字段构建

**`component_type_singular`**：组件类型单数形式

| `component_type` | `component_type_singular` |
|---|---|
| icon | icon |
| button | button |
| badge | badge |
| title | title decoration |
| hud_element | HUD element |
| bar | `sub_part: track` 或无 `sub_part` → `bar frame`；`sub_part: fill` → `bar`（实心填充条）；`sub_part: handle` → `slider knob`（按实际形状，竖条手柄可写 `slider handle`） |
| toggle | toggle |
| container | container frame |
| display | display frame |

**`visual_subject`**：从 `image_requirements` 该元素的视觉主体描述读取（源自 UI设计文档.md §11/§15）。若无显式描述，从 `component_type` 推导通用描述（如 icon → "a simple geometric shape"）。

**`color_description`**：从 UI设计文档.md §3 配色方案读取该元素的主色调，注入为具体颜色名 + hex 色值。逐元素指定颜色，禁止依赖 `style_baseline` 的泛化颜色描述（如 "vibrant warm colors" 会被 AI 自由解释为粉色+绿色等不正确配色）。格式按 `component_type` 区分：

| component_type | color_description 格式 | 说明 |
|---|---|---|
| bar | `sub_part: track` 或无 `sub_part`：`{border_color_name} ({border_hex}) border only`；`sub_part: fill`：`{color_name} ({hex})` | track 镂空框架只有边框，interior 由 enhancement_keywords 声明为透明，**禁止指定填充色**（否则与 "no solid fill" 矛盾）；fill 是实心填充条，整体着色，**禁止 border only**（fill 就是填充内容本身） |
| 其他 | `{color_name} ({hex})` | 实心元素整体着色 |

若 UI设计文档.md 无逐元素配色，回退到 §3 全局配色方案按 `component_type` 分配：button → 主色，bar → 主色或辅助色作 border，icon → 主色。

**`aspect_ratio_description`**：根据元素的 `width` 和 `height` 计算宽高比，注入为人类可读的形状描述，让 AI 知道每个元素是细长条、正方形还是宽矩形。这是描述性信息（帮助 AI 理解目标形状），不是布局指令——图集中元素自然不铺满整行/列，切片后的透明边距由后处理 trim + 缩放解决。格式规则：

| 宽高比范围 | 描述格式 |
|-----------|---------|
| w/h ≥ 4:1 | `very wide and thin shape, {w}:{h} width-to-height ratio` |
| 2:1 ≤ w/h < 4:1 | `wide rectangular shape, {w}:{h} width-to-height ratio` |
| 0.5:1 < w/h < 2:1 | `roughly square shape, {w}:{h} width-to-height ratio` |
| w/h ≤ 0.5:1 | `tall and narrow shape, {w}:{h} width-to-height ratio` |

**`enhancement_keywords`**：从 `text-to-image-prompt-rules.md` §3 提示词增强表查找该 `component_type` 对应的默认追加关键词。**仅追加与"透明背景"和"孤立形状"相关的关键词**，跳过"on transparent background"（已由 sprite sheet 公式的 `on transparent background` 覆盖）和"no background behind"（已由 `fully isolated` 覆盖）。

**bar 类型的 `sub_part` 路由**：`track`（或无 `sub_part`）→ 进度条框架/滑块轨道行；`fill` → 进度条填充/滑块填充条行；`handle` → 无增强关键词（按实际形状描述即可）。

**⚠️ img2img 模式端形约束例外**：bar 的**端形约束关键词**（strictly rectangular / flat squared-off ends / no tapered / no rounded capsule 等）仅**文生图模式**（`crop_path == "text2img_fallback"`）注入；**img2img 模式形状由参考图决定**，只注入 hollow frame 透明中心类关键词（服务于 track/fill 分离），不注入端形约束（否则与参考图形状冲突）。

**⚠️ Prompt 长度预算（API 硬限制 1024 字符）**：MCP `generate_image`（`provider: seedream`）的 prompt 上限为 **1024 字符**（超限返回 `INVALID_PARAMS: Prompt length exceeds the 1024 character limit`）。Path C 图集把 N 个元素的描述拼进同一个 prompt，极易超限，**目标总长 ≤ 1000 字符**：

- **bar 类增强关键词必须使用 Path C 精简变体**（见下表），不使用 `text-to-image-prompt-rules.md` §3 的完整长关键词（长关键词是 Path D 单元素 prompt 的预算专用）
- 仍超限时按优先级裁剪：① `style_baseline` 压缩为风格名 + 媒介/质感核心词（全局颜色枚举词、氛围词可删——元素颜色已逐个注入；**与条目级约束冲突的词如 rounded organic shapes 对 bar 元素必须删**）② 元素定性形状短语（如 `very wide and thin shape`——保留数字宽高比即可）③ 材质氛围修饰词（如 handle 的 `soft watercolor shading`）
- **禁止裁剪**：颜色名+hex、数字宽高比、端形约束、镂空/实心约束
- 构建完成后必须字符计数验证 ≤ 1024，超限回退继续按优先级裁剪

**bar 的 Path C 精简增强关键词**（文生图模式；img2img 模式仍按上方例外只保留 hollow 类）：

| sub_part | Path C 精简关键词 |
|---|---|
| `track`（或无 `sub_part`） | `flat squared-off rectangular ends, uniform height, hollow frame, completely empty see-through center, no solid fill inside, no tapered or pointed tips, no rounded capsule ends` |
| `fill` | `flat squared-off rectangular ends, uniform height, fully solid color fill, no hollow center, no tapered or pointed tips, no rounded capsule ends` |
| `handle` | 无（按实际形状描述即可） |

### 3.2 `panel_type_prefix` 处理

Sprite sheet 中的元素可能来自不同面板。`panel_type_prefix` 抑制规则与文生图路径 D 完全一致：

| 图片类型 | 是否注入 `panel_type_prefix` |
|---------|----------------------------|
| 按钮背景 | ❌ 抑制 |
| 图标 | ❌ 抑制 |
| 徽章 | ❌ 抑制 |
| HUD 元素 | ❌ 抑制 |
| 进度条框架 | ❌ 抑制 |
| 标题装饰 | ✅ 注入 |
| 容器 | ❌ 抑制 |
| 显示框 | ❌ 抑制 |

被抑制的元素不注入 `panel_type_prefix`，仅使用 `style_baseline`。

**混合面板处理**：同一 sprite sheet 中来自不同面板类型的元素，各元素独立判定是否注入 `panel_type_prefix`。对需要注入的元素，其 `panel_type_prefix` 追加在该元素的 `enhancement_keywords` 中（如 `, {panel_type_prefix}`）。

### 3.3 完整示例

**行堆叠**（4 个宽元素，w ≥ h：暂停图标、爱心图标、金币图标、设置齿轮图标，同一 style_baseline，配色方案主色暖橙 #FF8C42）：

```
"cartoon style, vibrant colors, game UI sprite sheet kit on transparent background, each element on its own row, stacked vertically: icon (a pause symbol with two vertical rounded bars, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline), icon (a heart shape with glossy finish, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline), icon (a golden coin with star emblem, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline), icon (a gear cog with 8 teeth, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline). Do not assemble the elements into a complete interface. Each sprite must be on a separate row, fully isolated and non-touching. No text, no other elements."
```

**列堆叠**（3 个高元素，h > w：竖条手柄、开关旋钮、下拉箭头，同一 style_baseline，配色方案主色暖橙 #FF8C42）：

```
"cartoon style, vibrant colors, game UI sprite sheet kit on transparent background, each element in its own column, arranged horizontally from left to right: bar (a vertical slider handle, warm orange (#FF8C42) colored, tall and narrow shape, 1:4 width-to-height ratio, rounded capsule shape with glossy orange finish, bold outline), toggle (a toggle knob, warm orange (#FF8C42) colored, tall and narrow shape, 1:2 width-to-height ratio, round ball shape with glossy finish, bold outline), icon (a downward arrow for dropdown, warm orange (#FF8C42) colored, tall and narrow shape, 1:2 width-to-height ratio, bold silhouette, high contrast outline). Do not assemble the elements into a complete interface. Each sprite must be in a separate column, fully isolated and non-touching. No text, no other elements."
```

**行堆叠，混合类型含 bar 框架**（2 个图标 + 1 个按钮背景 + 1 个滑动条轨道 + 1 个开关底框，配色方案主色暖橙 #FF8C42 辅助色亮黄 #FFD23F 背景色浅奶油 #FFF8E7）：

```
"cartoon style, vibrant colors, game UI sprite sheet kit on transparent background, each element on its own row, stacked vertically: icon (a speaker with sound waves, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline), icon (a musical note, warm orange (#FF8C42) colored, roughly square shape, 1:1 width-to-height ratio, bold silhouette, high contrast outline), button (a rounded rectangle button frame, warm orange (#FF8C42) colored, wide rectangular shape, 4:1 width-to-height ratio, gradient and subtle shadow effect embedded, glossy finish, only the button shape visible), bar frame (a horizontal slider track bar, warm orange (#FF8C42) border only, very wide and thin shape, 8:1 width-to-height ratio, flat squared-off rectangular ends, uniform height, hollow frame, completely empty see-through center, no solid fill inside, no tapered or pointed tips, no rounded capsule ends). Do not assemble the elements into a complete interface. Each sprite must be on a separate row, fully isolated and non-touching. No text, no other elements."
```

**行堆叠，slider 子部件组**（同一 slider 的 track + fill + handle，文生图模式，配色方案主色暖橙 #FF8C42 辅助色亮黄 #FFD23F。⚠️ track 与 fill 的端形约束必须完全一致——两者都是矩形平端，fill 伸缩时才不与 track 错位。本示例已按 1024 预算使用 Path C 精简关键词 + 裁剪后的 style_baseline）：

```
"cartoon style, vibrant colors, game UI sprite sheet kit on transparent background, each element on its own row, stacked vertically: bar frame (a horizontal slider track bar, warm orange (#FF8C42) border only, 16:1 width-to-height ratio, flat squared-off rectangular ends, uniform height, hollow frame, completely empty see-through center, no solid fill inside, no tapered or pointed tips, no rounded capsule ends), bar (a horizontal slider fill bar, bright yellow (#FFD23F) colored, 16:1 width-to-height ratio, flat squared-off rectangular ends, uniform height, fully solid color fill, no hollow center, no tapered or pointed tips, no rounded capsule ends), slider knob (a circular slider handle knob, warm orange (#FF8C42), 1:1 width-to-height ratio, round ball shape with glossy finish, bold outline). Do not assemble the elements into a complete interface. Each sprite must be on a separate row, fully isolated and non-touching. No text, no other elements."
```

## 4. 生成参数

| 参数 | 值 | 说明 |
|------|---|------|
| `provider` | `"seedream"` | 与所有生成路径一致（`modules/shared/references/mcp-image-generation.md` §3 权威定义） |
| `image_size` | `{"width":2048,"height":2048}` | 正方形预设，与垂直堆叠布局一致 |
| `is_segmentation` | `true` | 透明背景，所有元素均需透明 |
| `image_urls` | 文生图：省略；img2img：父级透明 PNG 的 CDN URL | 根据 `crop_path` 自动判定——`text2img_fallback` → 文生图；正常路径 → img2img（父级透明 PNG 先按 mcp-image-generation.md §2 上传） |

## 5. 切片后处理

生成后图片由 Stage 02 直接下载落位 `Assets/Games/{GameName}/UI/Images/{sprite_sheet_group}.png`，然后切片：

1. **切片**：使用部署型工具 `Create.GameFramework.TJImagePostprocess.SliceSpriteSheets("{GameName}")`（alpha 投影，非连通区域检测；分组/方向/元素顺序自动从 `image_requirements.json` 读取——`sprite_sheet_group`/`sprite_sheet_slot`/`sprite_sheet_orientation`，无需手填字典）：
   - orientation = `"row"`：沿 X 轴投影 alpha → 找 Y 分段 → 按从上到下匹配元素名
   - orientation = `"column"`：沿 Y 轴投影 alpha → 找 X 分段 → 按从左到右匹配元素名
2. **缩放**：复用部署型工具 `Create.GameFramework.TJImagePostprocess.ScaleVerify("{GameName}")`，目标尺寸/`is_segmentation` 自动从 `image_requirements.json` 读取（切片元素按条目 width/height 缩放）。所有元素统一走 trim → stretch 路径：先裁剪透明边框，再拉伸到目标尺寸
3. **验证**：已合并入 `ScaleVerify` 工具（自动输出 `[Verify]` 行）

**回退**：若投影分段数 < `sprite_sheet_total`（元素粘连，投影无零间隙），该 sprite sheet 回退为逐张独立生成（Path D），对每个元素使用标准 prompt 公式（img2img 或文生图取决于该元素的 `crop_path`）。

## 6. 缺陷检测（两路分工，sprite sheet 属路 B 触发类别）

Sprite sheet 是路 B 视觉检测的触发类别之一（多元素同画布，元素内部缺失风险高；`SliceSpriteSheets` 的分段数检查只抓粘连，不抓元素内部缺陷）。按 `prompt-engineering.md` §4 两路执行：

- **路 A 确定性像素扫描**：每张 sprite sheet 整图先按 §4.1 做像素统计（零视觉调用），确证 transparent_hole / white_base_showing 直接进重试；切片不重复扫（派生自已扫描整图）。
- **路 B 视觉检测**：对每张 sprite sheet（而非每个切片）跑 1 次 `analyze_multimedia`。**prompt = 下述图集形态说明句 + `prompt-engineering.md` §4.3 模板全文**（含 style_baseline 注入与设计特征白名单，禁止沿用旧的"pure white / single color"口径——浅色粉彩/水彩元素会被误判）：

```
This image is a sprite sheet containing multiple UI elements stacked in a single column or row on a transparent background.
```

（透明背景本身与元素之间的空白间隔不是缺陷。）

- 路 B 初判的 `transparent_hole` / `white_base_showing` 仍须路 A 扫描数据确证——不支持则按误报放行且不消耗重试次数（核验数据记入执行报告）；`truncated_element` / `other` 允许凭初判进重试但注明"未经像素确证"。

缺陷重试与 Stage 03 §3.3 一致（最多重试 2 次，重试时重新生成整张 sprite sheet；仅对两路确证的缺陷计数）。

## 7. 分组规则（ui-doc-builder Step 3：无参考图路径由工具内置；有参考图路径 §3.2.4 手动执行）

### 7.1 筛选条件

元素必须**同时**满足以下条件才可入图集：

| 条件 | 说明 |
|------|------|
| `source == "ai_generate"` | AI 生成 |
| `crop_path` 为 `text2img_fallback` 或正常文件路径或 `functional_type` 非空 | 有参考图走 img2img，无参考图走文生图（Path C 自动判定） |
| `is_segmentation == true`（或由判定表推断为 `true`） | 透明背景 |
| `text_type != "art_text"` | 艺术字有特殊 prompt 需求 |
| `sprite_sheet_group` 为空 | 尚未被分入图集（防重复分组） |
| `component_type` ∈ {`icon`, `button`, `badge`, `title`, `hud_element`, `bar`, `toggle`, `container`, `display`} 或 `functional_type` 非空 | 透明 UI 元素或功能控件子部件 |
| `max(width, height) ≤ 768` | 元素最长边不超过 768px，确保 2048 画布每行/列至少容纳 2 个元素 |

> **功能控件子部件入图集**：`functional_type` 非空的子部件不再被排除。无论 `crop_path` 是 `text2img_fallback`（走文生图）还是正常文件路径（走 img2img，传入父级透明 PNG，上传为 `image_urls`），入图集都比独立生成更优——同一父组件的子部件（如 track + fill + handle）放在同一张图集中生成，AI 能看到各部件的相对尺寸关系（track 和 fill 同宽、handle 更小），生成的宽高比比独立生成时各自在正方形画布上"盲猜"更准确。条件：`functional_type` 非空 + `expanded_from` 非空（同一父组件的子部件分入同一图集组）。

### 7.2 分组策略

> ⚠️ **强制性**：所有满足 §7.1 条件的元素**必须**分入 sprite sheet，不得单独走 Path D。仅当某元素不满足 §7.1 任一条件时，才允许走 Path D 单张文生图。当 eligible 元素仅 1 个时，仍走 Path D（单元素图集无意义）。

1. **功能控件子部件优先分组**：`functional_type` 非空且 `expanded_from` 相同的子部件必须分入同一图集组。同一父组件的子部件（如 track + fill + handle）在同一图集中生成，AI 能理解它们的相对尺寸关系（track/fill 同宽、handle 较小），避免独立生成时宽高比偏差过大。子部件单独成组，不与非功能控件元素混组。`crop_path` 为正常文件路径时走 img2img（传入父级透明 PNG），`crop_path == "text2img_fallback"` 时走文生图
2. 按 `style_baseline` 值分组（同风格 → 同图集池）
3. 同池内按 `panel_type_prefix` 进一步分组（同面板装饰 → 同图集）
4. **方向分流**：在每个面板组内，按宽高比将元素分为两组：
   - **行堆叠组** (w ≥ h)：宽元素，将走行堆叠（竖向排列）
   - **列堆叠组** (h > w)：高元素，将走列堆叠（横向排列）
   - 正方形元素 (w = h) 归入行堆叠组（默认方向）
5. **Bin-packing 计算张数**（每组内执行）：
   - **行堆叠组**：画布最大高度 = 2048px。累加各元素高度（含 16px 间距），超过 2048 则开新张。即 `张数 = ceil(Σ(height_i + 16) / 2048)`
   - **列堆叠组**：画布最大宽度 = 2048px。累加各元素宽度（含 16px 间距），超过 2048 则开新张。即 `张数 = ceil(Σ(width_i + 16) / 2048)`
   - 每张最多 **8** 个元素（保守上限，保证切片可靠性）
   - 当组内元素 `max(w,h) > 512` 时，每张最多 **4** 个元素（大元素需更多画布空间）
   - 若单元素尺寸 > 2048px（任一维度），不参与 sprite sheet，单独生成
6. 子组内按目标宽高比排序（相似宽高比放一起，减少空间浪费）
7. 为每个子组创建虚拟图集任务并标记成员（虚拟任务**不写入** `image_requirements.json`，仅成员条目在 JSON 中，虚拟任务在 Stage 02 §2.0 运行时按 `sprite_sheet_group` 分组构建）：
   - 虚拟图集任务名 = `sprite_sheet_{序号}`（用于生成阶段的文件命名）
   - 成员元素的 `sprite_sheet_group` = 虚拟图集任务名
   - 成员元素的 `sprite_sheet_slot` / `sprite_sheet_total` / `sprite_sheet_preset` / `sprite_sheet_orientation` 写入各自条目

### 7.3 不入图集的元素

不满足条件的元素继续走 Path D 独立生成（img2img 或文生图取决于 `crop_path`）。包括：
- 全屏背景（`is_segmentation: false`，尺寸大）
- 面板/弹窗背景（尺寸大，可能不同 `is_segmentation`）
- 艺术字（`text_type: art_text`）
- 功能控件子部件中 `crop_path == "text2img_fallback"` 但 `max(width, height) > 768` 的（尺寸过大）
- 功能控件子部件中 `crop_path` 为正常文件路径但 `max(width, height) > 768` 的（尺寸过大，走 Path D 独立 img2img）
- 尺寸 `max(width, height) > 768` 的非功能控件元素
