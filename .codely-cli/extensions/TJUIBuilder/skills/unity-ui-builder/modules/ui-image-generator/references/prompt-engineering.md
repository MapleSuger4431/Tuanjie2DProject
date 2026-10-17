# AI 图片生成提示词工程

> 本文件是 `modules/ui-image-generator` SKILL.md 的配套参考，包含双模式总览、共享规则和图生图提示词构建规则。
> 文生图专属规则（风格注入、增强关键词表、色彩和谐、背景类注意等）见 `references/text-to-image-prompt-rules.md`。

## 0. 双模式提示词体系

本技能支持两种生成模式，提示词构建规则完全不同：

| | 文生图（Text-to-Image） | 图生图（Image-to-Image） |
|---|---|---|
| **触发条件** | 无参考图 / `crop_path == "text2img_fallback"` | 有参考图且有对应透明 PNG（含 `quality == "contaminated"` 的透明 PNG） |
| **prompt 作用** | 完全决定生成内容 | 仅决定"生成什么"，参考图决定"长什么样" |
| **颜色** | ✅ 必须写（如 `teal sea-green`） | ❌ 不写（除非 override），会覆盖参考图颜色 |
| **质感** | ✅ 可以写（如 `glossy, metallic`） | ❌ 不写，参考图已包含 |
| **形状** | ✅ 可以写（如 `pill-shaped`） | ❌ 不写，参考图已包含 |
| **风格** | ✅ 必须写（如 `cartoon style`） | ❌ 不写，参考图已包含 |
| **组件类型** | ✅ 写完整描述 | ✅ 统一写 `"a single ui"`（不再区分组件类型） |
| **isolated 声明** | 不需要 | ✅ 必须（`a single ui, isolated on transparent background`） |

**图生图 prompt 的核心原则**：只写"这是什么"，不写"它长什么样"。

**实测依据**：
- prompt 写 `"green"` → 覆盖参考图的 teal/sea-green，生成纯绿色 ❌
- prompt 不写颜色 → 参考图的蓝绿色调被准确保留 ✅
- prompt 只写 `"game UI button"` → 生成整张界面（组件分离不够） ❌
- prompt 写 `"a single ui, isolated on transparent background"` → 精确生成单个组件 ✅
- 裁剪区域 + 图生图 → 精确生成目标组件（如 Sound Toggle）✅
- 整图 + 图生图 → 只能生成最显眼的组件 ❌

**路径判定**：路径选择（文生图 vs 图生图 vs Sprite Sheet 等）的权威判定表见 `shared/adjudication-tables.md`「权威路径判定表」。本文件仅提供各路径的 prompt 构建规则，不重复路径判定逻辑。

## 1. 共享规则（两种模式均适用）

### 1.1 缺失像素预防关键词

**常量定义**（全屏背景、弹窗背景、面板背景类必须追加，文生图和图生图均适用）：

```
MISSING_PIXEL_PREVENTION = "fully rendered scene from foreground to background, complete composition with no blank or white areas, every pixel filled with detailed content, rich textures throughout, even lighting across entire image, no dramatic spotlight effect"
```

**缺陷重试增强关键词**：

```
DEFECT_RETRY_ENHANCEMENT = "CRITICAL: completely fill every pixel with rich texture and detail, absolutely no blank or solid color areas, every single pixel must have visible texture, grain, or pattern, foreground and background fully rendered"
```

文生图模式中，`MISSING_PIXEL_PREVENTION` 通过提示词增强表注入全屏背景；图生图模式下，对全屏背景也需追加此关键词（图生图也可能产生空白区域）。

### 1.2 圆角与透明背景

- **图片不含圆角**：如果图片仅提供纹理、USS 负责圆角形状，提示词中不要包含 `"rounded corners"` / `"rounded rectangle"` — 图片应为矩形纹理，圆角由 USS `border-radius` 实现
- **透明背景**：绝大多数 UI 图片默认使用 `is_segmentation: true` 参数自动去除背景。提示词中追加 `"on transparent background"` 辅助 AI 理解意图，但**不依赖提示词实现透明** — 实际透明由分割算法保证

### 1.3 按钮图标与底图分离

按钮背景图片的提示词**不得包含**图标的视觉描述（如暂停图标的两条竖线、播放图标的三角形、关闭图标的叉号）。图标应由 UXML 文本元素或独立 VisualElement 显示。提示词仅描述按钮形状和材质（如 "round wooden button with glossy finish"），不含任何图标语义。

## 2. 图生图提示词构建规则

### 2.1 图生图 prompt 公式

```
"{aspect_ratio} a single ui, isolated on transparent background{text_suffix}{exclude_overlays_suffix}{override_suffix}{dynamic_content_suffix}"
```

> **`aspect_ratio`**：从 `image_requirements` 的 `width`/`height` 计算并化简的宽高比字符串（如 `9:16`、`16:9`、`1:1`）。必须前置在 prompt 最前面（组件名称 "a single ui" 之前），禁止放在末尾。图生图模式下虽然参考图决定风格，但宽高比前置仍能帮助 AI 在构图阶段锁定目标比例，避免内容不铺满画布。

其中各 suffix 的构建规则：

**`text_suffix`**（组件文字类型，由上游判定，写入 `component_crops` 的 `text_type` 字段）：

| text_type | text_suffix | 说明 |
|-----------|---------|---------|
| `art_text` | （空） | 艺术字是主视觉内容，必须保留。文生图路径下使用专用公式（见 `text-to-image-prompt-rules.md` §6），包含 `display_text` + `art_text_style` + 多行排版指引 |
| `label` | `, no text` | 普通功能文字，由 UXML Label 渲染，图片不含 |
| `none` | `, no text` | 无文字，图片不含 |

> **数据来源**：`text_type` 由 `modules/ui-style-reference` 的语义分类步骤产出（用户上传参考图与 AI 生成参考图均走同一流程），经 `ui-doc-builder` 吸收到 `UI设计文档.md` 的 `image_requirements`，传递到本模块。值域统一：`art_text` / `label` / `none`。`text_type` 未指定时默认取 `none`（追加 `, no text`）。

**`exclude_overlays_suffix`**（解决问题：背景图包含叠加 UI 元素）：

当组件类型为 `background` 且 `component_crops[component].exclude_overlays` 非空时启用。

构建规则：
1. 从 `exclude_overlays` 列表中提取每个叠加组件的 `componentType`
2. 按类型去重，映射为排除词：

| componentType | 排除词 |
|--------------|--------|
| button | `no buttons` |
| icon | `no icons` |
| title | `no titles, no text` |
| display / hud_element | `no score displays, no numbers` |
| panel | `no panels` |
| badge | `no badges` |
| bar | `no bars` |
| 其他 | `no {componentType}s` |

3. 格式：`, {排除词1}, {排除词2}, ...`
4. 示例：exclude_overlays 包含 3 个 button + 1 个 icon + 1 个 title → `, no buttons, no icons, no titles, no text`

**图生图排除效果的不确定性**：图生图的排除 prompt 可能无法完全去除叠加元素，特别是当叠加元素面积较大时。若生成结果仍残留叠加元素，回退到文生图（从回退风格表提取描述），并在输出中标注 `fallback_reason: "img2img_overlay_remnant"`。

**`dynamic_content_suffix`**（解决问题：艺术字图片含动态数字）：

当 `component_crops[component].dynamic_content` 非空时启用。

构建规则：
1. 读取 `generation_hint` 字段的完整文本
2. 追加到 prompt 末尾：`, {generation_hint}`
3. 额外规则（按 `dynamic_content.type`）：

| type | 额外追加 | 说明 |
|------|---------|------|
| `number` | `, no numbers, no digits, no score value` | 排除所有数字 |
| `variable_text` | `, no variable text content, blank text area` | 排除可变文字 |
| `mixed` | `, no numbers, no digits, no variable text content, blank text area` | 同时排除数字和可变文字 |

4. 示例：分数显示框 → `"{aspect_ratio} a single ui, isolated on transparent background, empty card frame without score numbers, blank area where score would be, no numbers, no digits, no score value"`

**与 `text_suffix` 的交互**：
- `dynamic_content_suffix` 优先级高于 `text_suffix` 默认行为
- 当 `dynamic_content` 存在时，`text_suffix` 仍按上表取值（`art_text` → 空，`none` → `, no text`），`dynamic_content_suffix` 提供更精确的动态内容排除词
- `art_text` 类型仍保留（艺术字是主视觉内容），但艺术字中的动态数字部分仍需通过 `dynamic_content_suffix` 排除

全屏背景例外（不需要 isolated，不需要 text_suffix，但需要 exclude_overlays_suffix 和 dynamic_content_suffix）：
```
"{aspect_ratio} game UI fullscreen background{override_suffix}, {exclude_overlays_suffix}, {scene_description} only, MISSING_PIXEL_PREVENTION{dynamic_content_suffix}"
```

### 2.2 component_type 映射（已废弃）

> 图生图 prompt 已统一使用 `"a single ui"`，component_type 不再写入 prompt。此映射仅保留供 override_suffix 和文生图回退参考，不再以表格形式列出。

### 2.3 override_suffix 构建

仅当当前图片对应组件的保真度为 `partial_override` 且 `override_dimensions` 包含与当前图片相关的维度时使用。

**查找保真度**：直接从 `component_crops[component_name].fidelity` 读取保真度模式。保真度按组件配置（per-component），无需通过参考图编号间接查找。

**解析规则**：
1. 从 `component_crops[component_name].fidelity.override_dimensions` 读取文本（如"按钮颜色改成蓝色，背景换成暗色"）
2. 判断哪些指令与当前图片类型相关（按钮指令→按钮图片，背景指令→背景图片）
3. 提取颜色/质感/形状/风格类关键词（布局/信息架构不在图片层面体现）
4. 以 `, ` 分隔追加

**示例**：

| override_dimensions | 图片类型 | override_suffix |
|---------------------|---------|-----------------|
| "按钮颜色改成蓝色" | 按钮背景 | `, blue color` |
| "按钮颜色改成蓝色" | 图标 | _(无关，不追加)_ |
| "背景换成暗色调" | 全屏背景 | `, dark tone` |
| "所有图标改成扁平风格" | 图标 | `, flat style` |
| "按钮颜色改成蓝色，背景换成暗色" | 按钮背景 | `, blue color` |

### 2.4 图生图下不使用的规则

以下文生图规则在图生图模式下不适用：`style_baseline` 注入、`panel_type_prefix` 注入、类别增强关键词表、`style_from_gdd`/`color_from_gdd`、`visual_subject` 详细描述。**例外**：缺失像素预防关键词在全屏背景中仍使用（图生图也可能产生空白区域）。

### 2.5 图生图下的容器背景

容器背景（全屏背景、弹窗背景、面板背景）的生成策略取决于参考图类型。图生图模型的本质是"风格还原"而非"内容编辑"——传入包含叠加 UI 元素的图 + `"no UI elements"` prompt，SeeDream 仍会生成包含按钮/面板/文字的完整 UI 截图。

**四种容器背景场景**：

| 场景 | crop_path | exclude_overlays | 策略 |
|------|-----------|-----------------|------|
| 干净容器背景 | 正常文件路径 | 空 | 图生图（下方"干净"行） |
| 被污染的容器背景 | 透明 PNG 路径 | `contaminated` | 图生图（下方"被污染"行）——以透明 PNG 为输入，prompt 基于 `contamination_description` 构建移除指令 |
| 含叠加 UI 的容器背景 | 正常文件路径（整图或裁剪区域） | **非空** | 图生图 + 排除 prompt（下方"含叠加"行） |
| 无参考图的容器背景 | `text2img_fallback` | — | 文生图（下方"无参考图"行）——无可用裁剪图，从风格描述生成 |

**干净容器背景**（`crop_path` 为正常文件路径，`exclude_overlays` 为空）：

使用裁剪区域（或整图）作为 `image_urls`（先按 `mcp-image-generation.md` §2 上传为 CDN URL），走图生图模式。

| 背景类型 | Prompt |
|---------|--------|
| 全屏背景（图生图） | `"{aspect_ratio} game UI fullscreen background{override_suffix}, landscape scene"` |
| 弹窗/面板背景（图生图） | `"{aspect_ratio} a single game UI popup panel card{override_suffix}, isolated on transparent background, no scene, no other UI elements"` |

全屏背景不需要 isolated（整图即内容），弹窗/面板背景需要 isolated（从场景中分离）。图生图模式下风格由参考图决定，prompt 不写风格关键词（如 `vibrant cartoon style`）。

**含叠加 UI 的容器背景**（`crop_path` 为正常文件路径，`exclude_overlays` 非空）：

使用整图（或裁剪区域）作为 `image_urls`（先上传为 CDN URL），走图生图模式，但在 prompt 中追加排除词（§2.1 的 `exclude_overlays_suffix`）。

| 背景类型 | Prompt |
|---------|--------|
| 全屏背景（含叠加 UI） | `"{aspect_ratio} game UI fullscreen background{override_suffix}, {exclude_overlays_suffix}, {scene_description} only, MISSING_PIXEL_PREVENTION"` |
| 弹窗/面板背景（含叠加 UI） | `"{aspect_ratio} a single game UI popup panel card{override_suffix}, isolated on transparent background, no scene, no other UI elements, {exclude_overlays_suffix}"` |

**⚠️ 已知限制**：图生图模型的"风格还原"本质意味着 prompt 中的排除词（如 "no buttons"）可能无法完全去除叠加元素。若生成结果仍残留叠加元素，回退到文生图路径。
**被污染的容器背景**（`quality == "contaminated"`，`crop_path` 指向 `final_transparent.png`——含组件像素 + 邻居残片的透明 PNG）：

走图生图模式，以透明 PNG 为 `image_urls`（先上传为 CDN URL）。透明 PNG 主体内容正确、有邻居残片，图生图比文生图保真度更高。prompt 基于 `contamination_description`（描述污染内容的位置和视觉特征）构建定制化移除指令，公式为 `{base_prompt}, Remove the {contamination_description} that does not belong to this component.`。

> **`contamination_description` 为空时的兜底**：当 `quality=contaminated` 但 `contamination_description` 字段缺失或为空时（上游数据链传递断裂），使用通用移除指令替代：`{base_prompt}, Remove any elements at the edges and corners that do not belong to the main component.`。此兜底确保 prompt 不会出现 `Remove the undefined...` 这类无效文本。

| 背景类型 | 图生图 Prompt |
|---------|-------------|
| 全屏背景（contaminated 图生图） | `"{aspect_ratio} game UI fullscreen background{override_suffix}, landscape scene, Remove the {contamination_description} that does not belong to this component."` |
| 弹窗/面板背景（contaminated 图生图） | `"{aspect_ratio} a single game UI popup panel card{override_suffix}, isolated on transparent background, no scene, no other UI elements, Remove the {contamination_description} that does not belong to this component."` |

生成后必须用 `analyze_multimedia` 验证产出图（验证规则见 §2.6），检测两种失败：残片残留 或 残片被"补全"为完整元素。验证失败按 §2.6 重试策略处理（最多 3 次），3 次仍失败才回退到文生图 `text2img_fallback`。

**无参考图的容器背景**（`crop_path` 为 `"text2img_fallback"`）：

**禁止走图生图**（无可用参考裁剪图）。必须走文生图，使用从参考图提取的描述作为 prompt。

| 背景类型 | 文生图 Prompt |
|---------|-------------|
| 全屏背景（文生图回退） | `"{style_baseline}, {scene_description}, {aspect_ratio} game UI fullscreen background, no UI elements, no buttons, no text, no panels, landscape scene only, high quality"` |
| 弹窗/面板背景（文生图回退，带装饰/纹理） | `"{style_baseline}, {panel_description}, {aspect_ratio} game UI popup panel card, panel filling entire canvas from top edge to bottom edge and left edge to right edge, no transparent margins, no empty space around panel, no buttons, no text, no icons, no UI elements inside, high quality"` |
| 弹窗/面板背景（文生图回退，纯色/近纯色） | `"{filtered_style_baseline}, {aspect_ratio} {sanitized_color_description} game UI popup panel card, solid colored rectangle filling entire canvas from top edge to bottom edge and left edge to right edge, no landscape, no scenery, no nature, no scene, no objects, no patterns, flat colored surface only, no transparent margins, no empty space around panel, no buttons, no text, no icons, no UI elements inside, high quality"` |

**纯色面板与带装饰面板的区分**：当 UI设计文档.md/回退风格表中面板描述为纯色（无渐变/纹理/装饰/圆角，`background-size: 100% 100%`）时，使用纯色面板行。纯色面板需要：
1. `filtered_style_baseline`：从 `style_baseline` 中移除场景渲染关键词（cartoon style, bold outlines, atmosphere 等），仅保留 UI 质感词（详见 `text-to-image-prompt-rules.md` §4.2）
2. `sanitized_color_description`：将场景歧义颜色名（如"深森林绿"）替换为不含场景联想的表达（如"dark green (#1a3a2a)"）（详见 `text-to-image-prompt-rules.md` §4.1）
3. 场景排除约束：`no landscape, no scenery, no nature, no scene, no objects, no patterns, flat colored surface only`（详见 `text-to-image-prompt-rules.md` §4.3）

描述从 UI设计文档.md §13 的「文生图回退风格参数」→「回退组件风格表」中查找对应背景条目的提取值。

如果全屏背景是复合类型（包含边框/暗角/装饰），prompt 为：
```
"{aspect_ratio} game UI fullscreen background with decorative elements, no UI elements, no buttons, no text"
```

此时 `is_segmentation: false`（全屏背景不需要透明）。
弹窗/面板背景的 `is_segmentation` 判定仍按 `shared/adjudication-tables.md` 的 `is_segmentation` 判定表执行（弹窗背景默认 `true`）。

### 2.6 污染组件验证与重试策略

对 `quality=contaminated` 的组件，img2img 生成后**必须**用 `analyze_multimedia` 验证。验证检测两种失败模式：

| 失败模式 | 含义 | 检测方法 |
|---------|------|---------|
| `still_contaminated` | 残片仍存在于边缘/角落 | AI 确认看到不属于主组件的碎片 |
| `completed` | 残片被 AI "补全"为完整元素（而非移除） | AI 确认边缘处出现了原 contamination_description 中描述的元素，且看起来完整而非碎片 |

**验证 prompt**（逐字使用，`{contamination_description}` 来自 `semantic.json`）：

```
Identify the main visual element in this image. Then describe ALL visible elements, paying special attention to the edges and corners. The original image had contamination: {contamination_description}. Check two failure modes: (1) Does any part of the contamination still remain? (2) Was the contamination 'completed' or 'filled in' into a full UI element instead of being removed? Answer 'clean' (contamination removed), 'still_contaminated' (fragment still present), or 'completed' (fragment was completed into a full element) with a brief reason.
```

**重试策略**（最多 3 次）：

| 重试轮次 | 策略 | prompt 调整 |
|---------|------|------------|
| retry-1 | 加强移除指令 | 追加 `Completely remove all traces of {contamination_description}. Do not complete or fill in any partial elements. Leave that area empty.` |
| retry-2 | 换移除策略 | 改为 `Erase and make transparent the area where {contamination_description} appears. Keep only the main {component_type} element.` |
| retry-3 | 最后重试 | 改为 `Remove everything except the main central element. The {contamination_description} must be completely gone, not completed, not filled in.` |

3 次仍失败 → 回退到文生图 `text2img_fallback`，在输出中标注 `fallback_reason: "contaminated_img2img_failed_after_3_retries"`。

## 3. 功能控件子部件独立生成

当组件的 `functional_type` 非 `null` 时，透明 PNG（ui-style-reference 产出的 `final_transparent.png`，已去关键色）将多个视觉层（如 track + handle）烘焙在同一张图中。ui-image-generator 先**脚本分割产几何、AI 只命名语义**（§3.1 两步法）确定拆分方案，然后每个子部件独立生成或合并为 sprite sheet 生成（可见子部件走 Path C sprite sheet，回退时走 Path D 独立 img2img；不可见子部件走 Path D 文生图），各自按目标尺寸选择 API 预设，后处理统一 trim → stretch。

### 3.1 视觉层分析拆分（脚本产几何 + AI 产语义）

**不再使用固定拆分模板**（如 slider 固定拆 track+handle）。不同 UI 设计的 slider 可能由 2 张（track+handle）或 3 张（track+fill+handle）组成。

> ⚠️ **slider/progress_bar 的 fill 层独立提取**：若参考图中 fill 与 track 视觉上可区分（不同颜色/纹理），**fill 必须作为独立层提取**，不能与 track 合并——fill 在 USS 中赋给 `.unity-base-slider__tracker` 元素，其宽度由 C# 脚本随 value 动态调整；若 fill 烘焙在 track 图中，fill 永远是静态的、不随 value 变化。若 fill 与 track 视觉上完全相同（同色同材质，无法区分），则不提取 fill 层——脚本对单色组件输出的 `indistinguishable` 标记就是这个判定的确定性信号。**fill 指引已内置在 §3.1 两步法中**，无需二次追问。

> ⚠️ **dropdown/toggle 的展开态部件推算**：下拉框等 toggle 控件在参考图中通常只展示收起态（track + icon）。AI 语义命名时须推算展开态所需的部件（如 `expanded_panel_bg`），即使参考图中不可见。展开态部件的 `visual_description` 从收起态 track 的视觉特征推导（同材质/同色调的弹出面板背景）。推算指引已内置在 §3.1 第 2 步 prompt 中。

对每个 `functional_type != null` 的组件，按两步执行——**几何与语义分离**。历史流程由 AI 估测 `size_ratio`/`fills_parent`，与 ui-style-reference §8.4.5 已确证的「AI 产几何系统性漂移」同源，禁止。

**第 1 步 · 脚本几何分割（几何唯一来源）**：

```text
{PYTHON} scripts/segment_control_layers.py {final_transparent.png 路径} \
  --json-output {layers JSON 输出路径}
```

脚本输出（确定性，逐层）：`bbox`（图像像素）、`size_ratio`（bbox ÷ **alpha 可见 bbox** 的两轴比率——分析对象即组件自身裁剪图，alpha 可见区域 = 父组件区域，语义等价于"相对父组件"，padded canvas 不会歪曲比率）、`fills_parent`（两轴比率均 ≥0.9；true 层 `size_ratio` 固定 `{1.0, 1.0}`）、色彩统计、`indistinguishable`（单色组件标记）、纯红编号框诊断标注图。

- **fallback**：脚本输出 < 2 层（`fallback: true`）→ 回退旧 AI-only 分析，结果必须标注**未经脚本几何**。fallback prompt（逐字使用）：

```
This is a {functional_type} UI control crop from a game settings panel, with a transparent background. Analyze this control and list every distinct visual layer that could be separated into independent image assets. For each layer, provide: (1) layer_name (e.g., "track", "fill", "handle", "knob", "bg", "icon"), (2) visual_description (brief English description of what it looks like), (3) fills_parent (true if this layer covers the entire component area, false if it's a small movable part like a knob/handle), (4) size_ratio (JSON object with "width" and "height" as floats 0.0-1.0, representing this layer's pixel size as a fraction of the parent component's pixel size; for fills_parent:true layers use {"width":1.0,"height":1.0}; for fills_parent:false layers estimate the actual visible size ratio, e.g. a circular handle on a wide slider might be {"width":0.15,"height":0.8}), (5) visible_in_reference (true if this layer is visible in the reference image, false if it must be inferred from other visible parts). If this is a slider or progress_bar, check whether the colored portion that changes with the value (fill) is visually distinguishable from the track (different color or texture). If yes, identify it as a separate 'fill' layer. If the fill and track are visually identical (same color and texture), do not create a separate fill layer. If this is a dropdown or toggle control, also identify parts that would appear in the expanded state (e.g., expanded_panel_bg) even if they are not visible in the reference image — infer their visual style from the collapsed track and set visible_in_reference to false. Return a JSON array. Do not return absolute coordinates or pixel sizes.
```

**第 2 步 · AI 语义命名（只读不测）**：对脚本诊断标注图（纯红编号框）一次 `analyze_multimedia`，prompt（逐字使用）：

```
This is a {functional_type} UI control crop from a game settings panel, with numbered red boxes marking detected visual layers. For each numbered layer, provide: (1) layer_name (e.g., "track", "fill", "handle", "knob", "bg", "icon"), (2) visual_description (brief English description of what it looks like), (3) visible_in_reference (always true for numbered layers — they come from pixel detection). If this is a slider or progress_bar, decide whether the value-changing portion (fill) appears as its own numbered layer or is baked into the track; single-colour layers marked indistinguishable must NOT be split into track+fill. If this is a dropdown or toggle control, ALSO append inferred parts that would appear in the expanded state (e.g., expanded_panel_bg) even though they are not visible in the reference image: give each inferred part layer_name, visual_description inferred from the collapsed track's style, visible_in_reference: false, plus estimated fills_parent and size_ratio — the ONLY geometry values you may produce, and only for inferred invisible parts. Return a JSON array with one object per numbered layer plus any inferred invisible parts. Do not return any coordinates or pixel sizes.
```

⚠️ **AI 对可见层返回的任何几何字段（size_ratio / fills_parent / 坐标 / 百分比）一律作废丢弃**，仅取 `layer_name` / `visual_description` / `visible_in_reference`。`fills_parent` / `size_ratio` 一律取脚本值。不可见推算部件（如 `expanded_panel_bg`）无像素可测，其 `fills_parent` / `size_ratio` 取 AI 推算值并标记 `visible_in_reference: false`——这是唯一允许的 AI 几何。

**合并后条目示例**（脚本几何 + AI 语义；2 层 slider，track 与 fill 视觉相同——脚本标记 indistinguishable，不拆 fill）：
```json
[
  {"layer_name": "track", "visual_description": "Horizontal wooden bar with rounded ends, uniform wood texture", "fills_parent": true, "size_ratio": {"width": 1.0, "height": 1.0}, "visible_in_reference": true},
  {"layer_name": "handle", "visual_description": "Gold circular knob", "fills_parent": false, "size_ratio": {"width": 0.15, "height": 0.8}, "visible_in_reference": true}
]
```

**3 层 slider**（fill 可分色——脚本色彩聚类已将其拆为独立编号层）与 **dropdown**（展开态不可见，AI 推算）同理：可见层 `size_ratio`/`fills_parent` 取脚本值；不可见层取 AI 推算值，如 `expanded_panel_bg` 的 `size_ratio: {"width": 1.0, "height": 2.0}`（推算展开面板约与收起态同宽、2 倍高——推算值不受 0.0-1.0 约束，该约束仅适用于脚本可见层）。

### 3.2 构建子部件需求（独立生成或图集生成）

根据 AI 分析结果，每个子部件作为**独立条目**生成。`image_requirements.json` 中每个子部件保留独立条目，各自走 Path D 或 Path C。

> **路径选择**：`sprite_sheet_group` 非空时走 Path C sprite sheet（默认路径——同一图集中生成能保持各子部件的相对尺寸关系，如 track 和 fill 同宽）。`sprite_sheet_group` 为空时回退到 Path D（可见子部件独立 img2img，不可见子部件独立文生图）。分组规则见 `sprite_sheet_prompt_rules.md` §7.2。

**构建规则**：

1. 每个 layer 创建一个 `ai_generate` 条目，`sub_part` = `layer_name`（供 ui-panel-builder 消费）
2. `visible_in_reference: true` 的子部件：`crop_path` 指向透明 PNG（`final_transparent.png`），走 **Path C sprite sheet**（`sprite_sheet_group` 非空时）或 **Path D img2img**（回退）——以透明 PNG 为参考，prompt 使用标准图生图公式（§2.1 `"{aspect_ratio} a single ui, isolated on transparent background{text_suffix}..."`），API 预设尺寸按该子部件的目标 width × height 从 `shared/adjudication-tables.md` 尺寸表选择
3. `visible_in_reference: false` 的子部件（如 dropdown 展开态面板）：`crop_path` 设为 `text2img_fallback`，走 **Path D 文生图**（或 Path C Sprite Sheet 如已分入图集组），prompt 追加同组件可见部件的 `visual_description` 作为风格基线。⚠️ **可拉伸性约束**：dropdown 的 `expanded_panel_bg` 高度不固定（取决于选项数量），必须通过 USS `scale-to-fit` 或 9-slice 拉伸。其文生图 prompt 必须描述为**简洁可拉伸矩形**——`simple flat rectangular panel, uniform border, no decorative patterns, stretchable`，禁止复杂花纹或装饰性边框（拉伸时会变形）。仅保留与收起态相同的配色和材质质感
4. `is_segmentation: true`（所有子部件透明背景）
5. **各子部件目标尺寸**（写入 `image_requirements.json` 供 API 预设选择和后处理缩放使用）：

| `fills_parent` | 宽 | 高 |
|---|---|---|
| `true` | 原始 width | 原始 height |
| `false` | `round(原始width × size_ratio.width)` | `round(原始height × size_ratio.height)` |

> `size_ratio` 可见层一律取 §3.1 脚本输出；不可见推算部件取 AI 推算值。脚本 fallback（`fallback: true`，未经脚本几何）时取 AI 返回值，AI 也未返回则回退到 `min(原始width, 原始height)` 正方形。

6. `fills_parent: false` 的子部件须在 `image_requirements.json` 中写入 `size_ratio` 字段（供 ui-panel-builder 使用）
7. `text_field` 类型：AI 分析通常只返回 1 个 `bg` 层，走 Path D 单张生成
8. `progress_bar` 类型：AI 分析通常返回 2 个层（track + fill），两者都 `fills_parent: true`。fill 层在 USS 中通过 `width` 百分比表示进度值，初始设为 `100%`
9. `other` 类型：无预定义层结构，完全依赖 AI 分析返回的 `layer_name` 和 `fills_parent`

### 3.3 参考图预处理

`visible_in_reference: true` 的子部件走 Path D img2img（回退时）或 Path C sprite sheet（默认），参考图为透明 PNG（`final_transparent.png`，ui-style-reference 产出，已去关键色）。原始透明 PNG 的宽高比可能极端（slider 约 12:1），需补为正方形画布：以 `max(w, h)` 为边长，原始内容居中，周围填透明像素。补图脚本在 Stage 02 中执行，输出到 `crop_path` 同目录下 `square_input.png`，作为图生图的 `image_urls`（先上传为 CDN URL）。

### 3.4 生成与后处理

每个子部件的生成路径取决于是否有参考图：
- 可见子部件（有参考图）：Path D img2img，`image_urls` 为 `square_input.png` 上传后的 CDN URL，`image_size` 为按目标尺寸选择的 API 预设
- 不可见子部件（无参考图，未分入图集）：Path D 文生图，`image_size` 为按目标尺寸选择的 API 预设
- 不可见子部件（无参考图，已分入图集）：Path C Sprite Sheet 文生图，同一父组件的子部件合并为 1 次生成调用

后处理与所有其他路径一致：Stage 03 部署型工具 `Create.GameFramework.TJImagePostprocess.ScaleVerify("{GameName}")` 对每个子部件执行 trim 透明边框 → stretch 到目标尺寸 → 设置 TextureImporter → 验证。Path C 子部件的切片由部署型工具 `Create.GameFramework.TJImagePostprocess.SliceSpriteSheets("{GameName}")` 执行。

## 4. 像素缺陷检测：两路分工（Stage 03 §3.3 / sprite_sheet_prompt_rules.md §6 配套）

> **为什么两路分工**：原设计是"每张图先烧一次视觉调用（analyze_multimedia）初判，缺陷再核验"——真缺陷是低频事件，为小概率缺陷付每图一次的高频成本不划算；且实测视觉对浅色粉彩/水彩风格存在系统性误报（btn_watercolor 案例：浅薄荷绿 RGB(200,242,234) 被判"白底露出"，像素核验透明 0.00%、纯白 0.00%、彩色不透明 98%+——纯误报，差点烧掉 2 次重生成积分）。
>
> 新架构按"代码能不能判"分工：**路 A（确定性像素扫描）每图必跑**，零视觉调用、零误报，抓 transparent_hole / white_base_showing 两类；**路 B（视觉检测）只在代码判不了的缺陷类和高风险类别上跑**，抓 truncated_element、艺术字字形错误、构图破碎。两路各抓各的、互不依赖——路 B 的触发条件是"这张图属于什么类别"，不是"路 A 检出了什么"。

### 4.1 路 A：确定性像素扫描（每图必跑，主门）

**执行对象**：每张落位图——独立图 + sprite sheet 整图（切片派生自已扫描的整图，不重复扫）。在 Stage 03 §3.3 先于路 B 执行。

**指标与阈值**：

**统计区域**：三项指标统一在**内缩区域**上统计——对内容 bounding box（alpha > 0.01 的最外包围盒）四边各内缩 `R = max(8, round(min(w, h) × 0.05))` 像素。内缩即机械排除圆角透明区，无需识别具体圆角；设计性中部镂空/蕾丝纹理不在内缩保护范围，落入下方"阈值边缘"升级仲裁。

| 指标 | 判定 |
|------|------|
| 透明占比（alpha < 0.01） | 内缩区域透明像素 > 2% → **确证 transparent_hole** |
| 纯白占比（RGB 全通道 > 250 且 alpha > 0.99） | 内缩区域纯白像素 > 5% **且**存在面积 ≥ 全图 0.5% 的纯白连通域（4-连通，纯 Python 洪泛实现，无需 OpenCV）→ **确证 white_base_showing** |
| 彩色不透明占比 | 内缩区域透明 ≈ 0% 且纯白 ≈ 0% 且彩色不透明 ≥ 95% → 误报特征（供路 B 仲裁比对） |

**处理**：

- **确证** → 直接进入 Stage 03 §3.3 重试阶梯（确定性结论无需视觉背书，扫描数据记入执行报告的"误报核验记录"）
- **阈值边缘**（透明占比略超但可能是设计性镂空/蕾丝纹理/极端 legal 用例）→ 升级路 B 视觉仲裁：用 §4.3 模板 + 末尾追加扫描数据仲裁句（见下）
- **未检出** → 该两类缺陷 pass，该图不再为此消耗任何视觉调用

**仲裁句格式**（路 A 边缘地带升级路 B 时，追加在 §4.3 模板末尾）：

```
Note: a deterministic pixel scan measured {transparent_pct}% transparency in region {region}. Decide whether this is an intentional design feature of the style (e.g. hollow pattern, lace texture) or a real defect.
```

### 4.2 路 B：视觉检测（高风险类别选择性执行）

**检测范围**：代码判不了的缺陷类——truncated_element（形状截断）、艺术字字形错误、构图破碎、风格异常。

**触发条件**（满足其一即对该图跑 1 次 analyze_multimedia；prompt 逐字使用 §4.3 模板）：

| 触发类别 | 判据 | 理由 |
|---------|------|------|
| sprite sheet 整图 | `sprite_sheet_group` 非空（切片前对整图跑） | 多元素同画布，元素内部缺失风险高；`SliceSpriteSheets` 的分段数检查只抓粘连，不抓元素内部缺陷 |
| 艺术字图片 | `text_type == "art_text"` | 字形完整性/错字只有视觉能判 |
| 极端长宽比 | 目标 `width/height > 4` 或 `< 1/4` | 文生图对扁长元素有形变先验 |
| style_group 首张图 | `style_group` 非空且为该组第一张 | 首张验证过关后，组内同风格图不再重复跑 |
| 无 style_group 的独立图 | `style_group` 为空 | 无同组比对基准，视作自身组的首张（保守跑） |
| 路 A 边缘地带仲裁 | 路 A 升级请求 | 脚本向视觉求助（§4.1 仲裁句） |

**跳过条件**：同 `style_group` 的第 2+ 张（首张已过）。该跳过**仅取消"style_group 首张图"这一触发来源**——若该图同时命中其他触发类别（sprite sheet 整图 / 艺术字 / 极端长宽比 / 路 A 升级仲裁），路 B 照常执行。跳过决定记入执行报告。sprite sheet 切片后不再单独检测（整图已覆盖）。

**路 B 初判的处理**：

- 初判 `VERDICT: DEFECT` 且 `defect_class ∈ {transparent_hole, white_base_showing}`：此类像素可判缺陷**仍须路 A 扫描数据确证**（视觉是概率性的）。**确证基准**：路 B 返回的 `region` 是自由文本，脚本不解析、不映射像素框——一律以该图路 A 的内缩区域三占比为基准：达标 → 确证进重试（属路 A 本可判缺陷，路 B 只是先行报出）；未达标 → 可选做一次**局部重扫**（按 region 描述近似圈定像素框重算三占比），局部达标同样确证，局部未达标或无法近似圈框 → 按误报放行，不消耗重试次数
- 初判 `truncated_element` / `other`：无法像素确证，允许凭初判进入重试阶梯，但须在报告中注明"未经像素确证"

### 4.3 缺陷检测 prompt 模板（路 B 逐字使用，禁止自拟）

对触发路 B 的图执行以下 prompt。`{style_baseline}` 从 `image_requirements.json` 的 `style_guidance.style_baseline` 读取；为空时删除该句，保留通用非缺陷清单。

```
Analyze this game UI image for AI generation defects only. The intended art style is: {style_baseline}. First, treat the following as INTENTIONAL design features of this style, NOT defects: light pastel or near-white colored fills, watercolor washes with uneven coverage and soft blooming edges, paper or canvas texture, transparency at the rounded corners of rounded rectangles, hollow or outline interiors used as design, highlight spots and soft glow, and any uneven color transitions implied by the style description above. Then check ONLY for real generation defects:
1) transparent holes appearing INSIDE areas that should be filled (not at rounded corners and not in intended hollow parts),
2) unpainted regions where the raw white canvas shows through because pixels were never rendered,
3) elements broken or truncated mid-shape,
4) large runs of pure white or near-white pixels (ALL channels RGB > 250) that break the visual continuity of a colored surface.
Answer exactly in this format:
VERDICT: CLEAN
or
VERDICT: DEFECT | defect_class: <transparent_hole|white_base_showing|truncated_element|other> | region: <where, e.g. "bottom-center third of the button fill"> | evidence: <one sentence>
```

> **旧口径废弃说明**：旧 sprite sheet 检测 prompt 的"元素几乎单色无可见细节"（almost entirely a single color with no visible detail）检查项**有意移除，禁止加回**——极简/扁平/水彩风格的浅色低细节元素是合法风格特征（btn_watercolor 误报源之一）；"元素未被渲染"的像素表现已被路 A 的 white_base_showing / transparent_hole 确定性覆盖，视觉无法可靠区分"风格性低细节"与"生成性空白"。

### 4.4 核验工具与证据记录

**扫描/核验工具**：Python + Pillow（`python -c "import PIL"` 可用时优先）或 PowerShell `System.Drawing`（Windows 自带），纯像素统计 + 连通域洪泛（纯 Python 可实现，供 §4.1 连续区块判定）即可，不依赖 OpenCV。

**证据记录**：所有确证/误报判定均须把三组占比 + 平均色记入 Stage 03 执行报告的"误报核验记录"（name / 缺陷类别来源（路A 或 路B 初判）/ 三占比 / 平均色 / 判定），供人工复核。中间地带（路 A 阈值边缘且路 B 仲裁仍不确证）→ 按误报放行。理由：重生成有确定的积分成本，而漏检一张可用图可被后续人工验收兜底，两者成本不对称。

**实测误报案例（why 佐证）**：btn_watercolor，卡通水彩按钮底图。路 B 式初判"底中部蓝绿水彩未衔接、白底露出"；像素核验该区域**透明占比 0.00%、纯白占比 0.00%（RGB 全通道 >250）、彩色不透明占比 98%+、平均色 RGB(200,242,234)（浅薄荷绿——正是生成 prompt 要求的 light green 过渡）**，仅有透明像素位于圆角矩形四角——典型误报，未消耗任何重试即放行。此案例即两路分工的直接动因：浅色粉彩填充对视觉检测是系统性误报源，而对确定性像素扫描是零误报的通过项。
