# 参考图后处理（OpenCV 组件树流程）

> 本文件是 ui-style-reference 各阶段 SKILL.md 步骤 5-9 的配套参考，包含后处理阶段全部子步骤的详细判定规则。
>
> 以 OpenCV 组件树为几何权威。

## 1. 组件树构建

OpenCV 提取产出 `components.json`（几何+裁剪），语义分类产出 `semantic.json`。两者通过部署型工具 `Create.GameFramework.TJStyleRefTree.BuildTree`（包 `Editor/UIBuildTools`，经 `execute_csharp_script` 一行调用）合并为组件树：

```csharp
Create.GameFramework.TJStyleRefTree.BuildTree("{manifest}", "{outputJson}", "{semanticRecords}");
```

合并后的组件树节点包含：`node_id`、`parent_id`、`root_id`、`depth`、`root_box`、`relative_parent_box`、`image_hash`、`semantic`、`provenance`。

后续处理在语义分类和叶子/复合判定完成后，为每个节点补充 `render_mode`、`source`、`shell_path` 等字段，写入最终 `component-tree.json`。

## 2. 语义分类（不做定位）

按 Stage 02 SKILL.md Step 6 的批量标注图方式对 OpenCV 裁剪图进行语义分类：以**原图**为底图（根级用 `source_square.png`；递归层用父组件的原图裁剪），在所有子组件位置画编号框生成标注图，一次 `analyze_multimedia` 调用返回所有组件的语义 JSON。⚠️ **坐标系必须匹配底图**：`source_square.png` 是原始分辨率（如 2560×2560），与隔离图的 2048 空间不同——根级标注必须使用 `components.json` 中的 `original_image_box` 字段（已映射到原图坐标），禁止直接用 `root_box`。⚠️ **禁止使用隔离图做标注底图**——隔离图在 Step 3 已移除所有普通文字，会导致 `display_text` 丢失。AI 仅返回语义 JSON：

- `state`、`name`、`component_type`、`display_text`、`text_type`、`art_text_style`
- `kind: leaf | composite`
- `has_dynamic_content`、`confidence`、`reason`

**严禁返回坐标、位置、尺寸或布局建议。** 低置信度、`state=invalid`、不合法 JSON 或语义与裁剪图冲突时，停止并用 `ask_user` 讨论。

### 2.1 组件类型推断

`component_type` 由 AI 语义分类直接产出，可选值：`button`、`icon`、`background`、`panel`、`bar`、`title`、`badge`、`hud_element`、`display`、`toggle`、`label`、`container`、`game_content_area`、`custom`。

### 2.2 文字类型判定

`text_type` 由 AI 语义分类产出，三种取值（完整枚举，无其他值）：

| text_type | 含义 | 下游行为 |
|-----------|------|---------|
| `art_text` | 文字本身是不可替代的艺术字（金属蚀刻、发光特效、手绘风格等），标准字体无法复现 | 拆分为底板 + 艺术字两层（**读** §3），艺术字层走文生图生成只含文字的透明 PNG |
| `label` | 普通可读文字，标准字体可渲染 | 图生图 prompt 追加 `, no text`；文字由 UXML `Label` 渲染 |
| `none` | 无文字（图标按钮、背景、容器、装饰物） | 图生图 prompt 追加 `, no text` |

### 2.3 艺术字风格提取

仅 `text_type == art_text` 时，AI 同时返回 `art_text_style`（视觉风格关键词，如 `metallic engraved text with golden gradient and outer glow`）。`label` / `none` 组件 `art_text_style` 为空字符串。

### 2.4 动态内容标记

`has_dynamic_content=true` 的节点标记 `dynamic_content` 字段。**text-label 节点**（`source=uss_only`）的 `dynamic_content` 仅记录动态内容信息供下游绑定游戏状态，不含 `generation_hint`（text-label 不生成图片）。**图片生成节点**（非 `uss_only`）的 `dynamic_content` 供下游 `ui-image-generator` 在图生图/文生图 prompt 中排除动态部分，仅生成静态框架。格式：

```yaml
# 图片生成节点的 dynamic_content（含 generation_hint）
dynamic_content:
  type: "number" | "variable_text" | "mixed"
  description: "如：当前分数数值"
  static_parts: "如：'分数：'"
  dynamic_parts: "如：分数数字"
  generation_hint: "空白卡片框架，无动态文字内容，文字区域留空"

# text-label 节点的 dynamic_content（不含 generation_hint）
dynamic_content:
  type: "number" | "variable_text" | "mixed"
  static_parts: "如：'金币:'"
  dynamic_parts: "如：'1000'"
```

`mixed` 类型中装饰性艺术字保留在生成图，功能性标签排除改用 UXML text。

## 3. 叶子节点处理（图集批处理）

叶子节点的高清透明 PNG 通过**图集批处理**生成（Stage 05 SKILL.md 步骤 9）。同一配方的节点共用图集页，每页只提交一次图生图，由 `generation_atlas.py split --clean` 拆回独立 PNG（关键色→透明转换 + alpha 噪声清理 + 缩放）。

### 3.1 处理配方

节点按以下配方分组，每组独立装箱：

| 配方名 | 节点条件 | 默认处理 | render_mode | source |
|---|---|---|---|---|
| `functional_control` | `functional_type` 非空（需拆分多个视觉层） | 关键色裁剪→透明 PNG（关键色转换为 alpha 通道），`crop_path` 指向 `final_transparent.png`，下游以透明 PNG 为 img2img 输入 | `direct` | `ai_generate` |
| `leaf_clean` | `text_type=none` | 关键色裁剪→透明 PNG（关键色转换为 alpha 通道）；质量差→img2img | `direct` | `direct_use` |
| `leaf_plate_no_label` | `text_type=label` 的 UI 组件 | 关键色裁剪→透明 PNG（关键色转换为 alpha 通道）；质量差→img2img | `direct` | `direct_use` |
| `art_text` | `text_type=art_text` | 关键色裁剪→透明 PNG（关键色转换为 alpha 通道）；质量差→img2img | `direct` | `direct_use` |
| — | text-label 节点（从 text_only 图像检测，独立子节点） | 不生成图片，保留为组件树中的独立子节点 | `text` | `uss_only` |

`state=skip` 节点不进入任何配方。

### 3.2 艺术字拆分规则（art_text，仅当需要独立拉伸底板与文字时执行）

当 `art_text` 节点需要独立拉伸底板与文字时，执行以下三步拆分：

1. **底板图**：通过 `leaf_plate_no_label` 配方图集生成，移除艺术字、保留底板和图形。关键色→透明转换后输出透明 PNG。`render_mode=direct`、`source=direct_use`。
2. **艺术字图**：艺术字部分单独文生图（`text2img_fallback`——art_text 拆分文字层无独立裁剪图，只能文生图）。prompt 须包含 `display_text` 的实际文字内容 + `art_text_style` 的风格描述，生成只含文字的透明 PNG。`render_mode=direct`、`source=ai_generate`、`crop_path=text2img_fallback`。

> ⚠️ `text2img_fallback` 仅用于无可用参考图的场景（裁剪区域过小、art_text 拆分文字层等）。contaminated leaf 走 `quality=contaminated` 信号（`crop_path` 保持为实际裁剪图路径），由 ui-image-generator Path D img2img 移除污染残片。
3. **UXML 组装**：底板作为背景层，艺术字 PNG 作为内容层叠加在底板上方。不使用普通 `Label` 替代艺术字。

底板和艺术字在组件裁剪映射表中拆为两行：`{组件名}_plate` 和 `{组件名}_text`，布局位置/尺寸与原组件相同。

> ⚠️ 不要把底板丢掉：拆分后底板裁剪图必须保留并单独列为一行。

### 3.3 小图标回退

原图裁剪 < 50px 的极小图标，裁剪后分辨率过低时，fallback 到图生图（`leaf_clean` 配方）以提升分辨率。标记 `source=ai_generate`。

## 4. 复合节点处理（图集批处理）

`kind=composite` 的节点处理分两个阶段，**必须严格按顺序执行**：

1. **递归子项隔离**（Stage 04 SKILL.md 步骤 8）：同一 `depth` 的非叶节点共用图集页，每页一次图生图，发现下一层子节点。递归只负责几何和层级发现，不生成最终图片。
2. **最终外壳生成**（Stage 05 SKILL.md 步骤 9，`composite_shell` 配方）：**递归全部完成后**，所有需独立渲染的复合父级按配方装箱，每页一次图生图，仅保留底板/边框/阴影/装饰。

> ⛔ **禁止跳过递归直接生成外壳**：步骤 9 的 `composite_shell` 只处理"已经拆完子项的"复合节点。跳过步骤 8 会导致复合节点内部子组件层级永久丢失，下游 UXML 无法正确嵌套。进入步骤 9 前必须确认所有 `kind=composite` 节点已标记 `recursion_done`。

### 4.1 递归子项隔离

- **必须逐层执行**：depth-1 发现的子组件如果是 composite，必须继续做 depth-2 的图集子项隔离，不能直接标记 `recursion_done` 跳过。只有满足 `recursion_done` 标记条件（**读** `references/recursive-extraction.md` §8.7）才可跳过。
- 同一 `depth` 的非叶节点的 `isolation_input.png` 从上一层 `extract_reference_components.py` 的连通域 mask 裁剪图复制（depth=0 从 `root-extract/`，depth≥1 从 `nodes/{parentNodeId}/child-extract/`），由 `generation_atlas.py pack` 装入 2048×2048 图集页。节点保持原宽高比的矩形槽位，不旋转、不补正方形。
- 每页提交一次图生图（`is_segmentation: false`），提示词要求分别处理每个槽位：仅保留直接子 UI、移除父级底板和所有普通文字、其余填充关键色。
- 拆图后逐槽执行 OpenCV：`extract_reference_components.py` 传入 `--parent-content-rect 0 0 {slotImageWidth} {slotImageHeight}`（slot image 已从页面 content_rect 裁出，坐标系从 (0,0) 开始），子框相对该 slot image 换算为 `relative_parent_box`。当 `split-results.json` 标记 `aspect_warning: true` 时，slot 图已正常输出，直接在此 slot 上跑 OpenCV——AI 轻微调整内容布局是正常行为，不需要重试。
- 子级专用 transform 的 `scaled_content_rect` 按裁剪图宽高比计算，`source_size` 设为裁剪图原始尺寸。

### 4.2 最终外壳生成

- 树完成后，复合父级从**已有纯色背景的图**裁剪组件区域（裁剪来源按深度确定，**读** `references/final-generation.md` §9.2），进入 `composite_shell` 配方图集。
- 每页提交一次图生图（`is_segmentation: false`），提示词要求仅保留底板、边框、阴影和装饰，移除所有内部子 UI、图标、文字。`is_segmentation: false` 输出含关键色背景的 RGB 图，`split --clean` 将关键色转换为透明 alpha 通道（含噪声清理和缩放）。
- 拆图（`--clean`）后输出 `shell_transparent.png`，`render_mode=shell_background`、`source=direct_use`。

> ⚠️ **禁止从原图裁剪**：原图无纯色背景，AI 无法区分场景背景和 UI 元素，会原样返回。必须从已有纯色背景的图裁剪。

**九宫格设置**：外壳图有边框/圆角时须设置 Sprite `spriteBorder`（左右覆盖圆角区域），USS 用 `-unity-background-scale-mode: stretch-to-fill`。border 值从图片边缘到圆角结束处测量，通常占宽度 10%-15%。图集缩放比例不得直接作为 Sprite border。

### 4.3 图集失败补图

- 首轮按槽位验证；只把失败节点重新装入补充图集，最多 2 轮。
- `retry-1`：提高 `min_content_px`（更大槽位），每页最多 4 项。
- `retry-2`：每页最多 2 项。
- 第二轮仍失败则停止讨论，不自动逐节点生成回退。

### 4.4 停止与异常条件

以下任一情况必须写诊断并停止，与用户讨论，禁止自动扁平化或猜测：

- 子项不在父框内、面积未严格小于父级，或轮廓覆盖父级自身；
- 节点 ID、祖先链或图像 hash 循环/重复；
- 直接子项归属不唯一、严重重叠或无新候选；
- alpha mask、隔离图验证、图生图或语义分类失败；
- 低置信度、无效语义或父/子关系与视觉不一致。

正常流程没有固定递归层数上限。每一层都重复"装箱 → 图生图 → 拆图 → OpenCV → 语义分类 → 合并组件树"，直到全部保留节点为叶子。

## 5. 样式组识别与去重

识别参考图中视觉风格相同的组件，归入同一 `style_group`，避免 AI 为相同风格组件分别生成不一致的图片。

**识别方式**：以语义分类返回的 `style_group_hint` 字段为主要依据。AI 在标注图中同时看到所有组件，已返回 `style_group_hint` 值——值相同的组件即归同一组。`style_group_hint` 为空的组件不参与去重。

**识别条件**（`style_group_hint` 相同的组件仍需满足以下全部条件才最终归组）：

1. `component_type` 相同（同为 button / 同为 icon 等）
2. 无 `partial_override` 导致视觉差异
3. **图标内容一致**：从 `visual_description` 中提取图标/装饰内容关键词，**精确一致**才能归组。`style_group_hint` 仅描述底板/容器风格（如"绿色方形小按钮"），不编码图标差异。同一底板上不同图标的按钮（如好友双人物 vs 邮件信封 vs 活动清单）即使 `style_group_hint` 相同、`visual_description` 文本高度相似，图标关键词不同则不归组。

**图标关键词提取与比对规则**：
- 从 `visual_description` 中剥离底板描述部分（形状、主色调、边框风格），提取剩余的图标/装饰内容关键词
- 底板描述示例：`"Small green rounded square button with a white..."` → 底板 = `Small green rounded square button with white border`
- 图标关键词 = 底板描述之后的内容：`icon of two figures`、`envelope icon`、`list/clipboard icon`
- 比对方式：**关键词集合完全相同**才归组（`two figures` ≠ `envelope` ≠ `list/clipboard`）
- 特殊值：`visual_description` 中不含图标关键词（纯背景/纯文字组件）时，跳过此条件

> `style_group_hint` 是 AI 在全局视角下的初步建议，比逐个比较 `visual_description` 自由文本更可靠。当 `style_group_hint` 全为空时（AI 未返回有效建议），回退到 `visual_description` 文本比对（形状/主色调/图标内容描述一致时归组）。

**`style_group` 命名格式**：`{componentType}_style_{N}`（如 `button_style_1`）。对于功能控件子部件，`style_group` 应使用描述控件类型的通用名（如 `volume_slider`、`toggle_switch`），而非绑死到某个具体组件（如 `slider_music`）。

**代表组件选举**：每个 `style_group` 中选举一个代表组件：
1. 裁剪面积最大的（裁剪图最清晰）
2. 来自更早参考图的
3. 无 `partial_override` 的

**代表组件图片命名**：代表组件在 `image_requirements.json` 中的 `name` 使用 `style_group` 派生的通用名，而非具体组件名。例如 `style_group: "volume_slider"` 的代表组件 track 子部件 `name` 为 `volume_slider-track`，而非 `music-volume-slider-track`。这样复用方（如 sfx slider）引用时 USS 写 `url("Images/volume_slider-track.png")`，语义清晰，不会误解为某专属组件的图片。

**非代表组件处理**：
- `source` 保持 `ai_generate`
- `crop_path` 设为 `reuse_from:{代表组件名}`
- **组件名唯一化（确定性兜底，强制）**：归组并选举代表后，凡组内非代表与代表 `component_name` 相同（Stage 02 分类未按位置序号区分并列同类组件的漏网情况），必须按 node_id 升序为每个同名非代表改名为 `{基名}{序号}`（序号从 1 起），代表保留原名不变；改名同步写入 `component-tree.json` 的 `semantic.component_name` 与 `semanticRecords.json` 对应记录，且先于 `exclude_overlays` 列表生成（列表使用改名后的最终名）。`reuse_from:{代表组件名}` 一律引用代表的最终名。此规则保证下游按名解析器（画板预览 nameToId、resolver 悬空判定、preflight 3c）无歧义——各解析器均跳过 `crop_path` 带 `reuse_from:` 前缀的节点作为映射目标，名字唯一化后代表是唯一目标
- 不生成独立裁剪图和生成图片，共享引用代表组件的裁剪图和生成图片
- 旋转复用时追加 `,rotation:{角度}`（如 `reuse_from:上方向按钮,rotation:90`）

**代表组件文字排除**：当归组原因是"仅文字不同"时，代表组件的 `generation_hint` 必须设置为 `"仅保留背景，无文字、无标签、无字母、无单词"`。

## 6. 背景组件标记 exclude_overlays

当背景组件覆盖全屏或大面积区域时，自动扫描同一参考图中的所有内容层组件，记录为该背景的 `exclude_overlays`。

**识别条件**：
1. `component_type` 为 `background`
2. 覆盖面积满足 `w_pct >= 90 && h_pct >= 90`
3. 同一参考图中存在非背景组件

**`exclude_overlays` 格式**：`[{ "componentName": "...", "componentType": "..." }, ...]`

## 7. 保真度判定

解析组件的 `annotation` 字段（若用户提供），确定保真度模式：

| Annotation 内容 | 保真度模式 | override_dimensions |
|----------------|-----------|-------------------|
| 空字符串或纯描述性文字 | `exact` | 无 |
| 包含具体调整指令 | `partial_override` | 提取调整内容 |
| 包含"不参考"/"忽略"/"skip" | `skip_component` | 无 |

## 8. 兼容性

- 缺少树字段的旧文档可继续按原有根百分比消费。
- text-label 节点是组件树中的独立子节点（`source=uss_only`、`render_mode=text`），不生成图片。文字内容、字体样式和动态判定信息全部在 text-label 节点上。下游 ui-panel-builder 通过组件树层级关系（`parent_id` → 子 text-label）读取文字信息，在 UXML 中创建 `<ui:Label>` 元素并按 `relative_parent_box` 精确定位。`display_text` 保留原始识别文字作为默认值，`dynamic_content.static_parts`/`dynamic_parts` 标识需要绑定游戏状态的动态部分。
