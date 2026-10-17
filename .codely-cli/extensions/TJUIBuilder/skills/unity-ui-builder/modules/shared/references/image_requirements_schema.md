# image_requirements.json Schema

> `image_requirements.json` 是 `ui-image-generator` 和 `ui-panel-builder` 之间的关键契约文件。本文件为两模块共享的权威 schema 定义。
>
> **权威生成者**：`ui-doc-builder` Step 3（`ui-image-generator` Stage 01-03 消费）
> **权威消费者**：`ui-panel-builder` 步骤 1；Job Plan Compiler（只读派生 `ui_image` machine jobs）
> **文件路径**：`context/games/{GameName}/image_requirements.json`
>
> **编译合同**：Planner 不手写 `ui_image` Job，也不把 UI 图片登记到资产规格。Compiler 对每个 `source=ai_generate` 条目按 `name` 确定性派生一个 external Job 与 source receipt；`direct_use` / `uss_only` / `user_import` 不派生 Generator。UI Job 自动等待全部派生 Job；ui_verify 按同一 `name` 复验 receipt semantic ID、actual path、图片真实性与 UXML/USS 引用。UI设计文档声明图片但本文件缺失或 schema 非法时必须 fail closed。不得为调度新增 image_requirements AI 字段。

---

## 字段定义

### 必填字段（所有条目）

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 图片名，用于文件命名。全局唯一 |
| `source` | enum | 生成来源：`ai_generate`（AI 生成）/ `direct_use`（透明 PNG 直接使用，来自 ui-style-reference 的 OpenCV 组件树处理）/ `uss_only`（纯 USS 实现，跳过）/ `user_import`（用户导入，跳过） |
| `component_type` | enum | 组件分类维度：`button` / `icon` / `background` / `panel` / `bar` / `title` / `badge` / `hud_element` / `display` / `toggle` / `container` / `game_content_area` / `custom` / `label`。⚠️ 不是 Brick 的 `assetPropsSchema` 槽位名——两者是不同维度，映射关系由 ui-panel-builder 步骤 1 的 Brick 映射表建立 |
| `panel_name` | string | 所属面板名（如 `MainMenu`、`HUD`） |
| `panel_type` | string | 面板类型（如 `full_screen`、`hud`、`popup`） |
| `uxml_element` | string | 对应 UI 元素的 `name` 属性值（UI Toolkit 模式）或 GameObject name（UGUI 模式），供 ui-panel-builder 通过元素 name 精确匹配图片 |

### 条件必填字段

| 字段 | 条件 | 类型 | 说明 |
|------|------|------|------|
| `crop_path` | `source` 为 `ai_generate` 或 `direct_use` | string | 图生图参考路径。值域：① 文件路径（相对 game context 路径，拼接 `{projectRoot}/context/games/{GameName}/` 前缀解析，如 `UIReferences/Components/btn_final_transparent.png`）——正常裁剪图或透明 PNG，图生图输入；② `skip_generation`——纯文字标签，不生成图片；③ `text2img_fallback`——无可用参考图，回退文生图；④ `reuse_from:{代表组件name}`——复用代表组件图片，不单独生成 |
| `is_segmentation` | `source` 为 `ai_generate` 或 `direct_use` | boolean \| null | 透明背景判定。`true`=透明背景，`false`=不透明，`null`=由 ui-image-generator Stage 01 判定表确定 |
| `width` | `source` 为 `ai_generate` 或 `direct_use` | number | 目标宽度（像素）。从 UI设计文档.md §1 设计分辨率或元素尺寸提取 |
| `height` | `source` 为 `ai_generate` 或 `direct_use` | number | 目标高度（像素）。从 UI设计文档.md §1 设计分辨率或元素尺寸提取 |
| `text_type` | `source` 为 `ai_generate` | enum | 文字类型：`art_text`（艺术字，须文生图）/ `label`（普通文字，走 UXML Label）/ `none`（无文字）。决定文生图 prompt 的 `text_directive`（见 `text-to-image-prompt-rules.md` §1） |
| `display_text` | `text_type` 为 `art_text` 或 `label` | string | 实际文字内容。多行用 `\n` 分隔。`art_text` 组件的文字内容由文生图生成；`label` 组件的文字由 UXML Label 元素渲染 |
| `art_text_style` | `text_type` 为 `art_text` | string | 艺术字视觉风格关键词（如 `metallic engraved text with golden gradient`）。供 ui-image-generator 文生图 prompt 使用（见 `text-to-image-prompt-rules.md` §6） |

### 可选字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `quality` | enum | 裁剪质量（来自 ui-style-reference Stage 02 两步验证）：`clean`（裁剪图确认干净）/ `contaminated`（裁剪图确认混入相邻元素像素）。`direct_use` + `contaminated`（leaf）时 ui-image-generator Stage 01 前置转换将 `source` 改为 `ai_generate`（`crop_path` 指向 `final_transparent.png`——含组件像素 + 邻居残片的透明 PNG），由 Path D img2img 以透明 PNG 为输入移除污染内容；`contaminated`（composite）仅作诊断标记，不触发 fallback。缺省视为 `clean` |
| `contamination_description` | string | 仅 `quality=contaminated` 时存在。AI 在 ui-style-reference Stage 02 确认污染时产出的描述文本，包含污染内容的位置和视觉特征（如 "a blue panel fragment in the bottom-right corner"）。ui-image-generator 据此构建定制化移除 prompt（如 `Remove the {contamination_description} that does not belong to this component.`） |
| `style_group` | string | 样式组标识。同类型+同外观的组件归为同组，仅代表组件生成图片，其他组件引用代表组件。**代表组件的图片文件名使用 `style_group` 值**（如 `style_group: "volume_slider"` + `sub_part: "track"` → 图片名 `volume_slider-track`），而非绑死到某个具体组件名（如 `music-volume-slider-track`），避免复用方引用时产生语义歧义 |
| `reuse_from` | string | 复用声明，格式 `reuse_from:{代表组件name}`。标记此条目复用代表组件的同一张图片文件，不单独生成。代表组件的 `name` 应使用 `style_group` 派生的通用名（如 `volume_slider-track`），而非具体组件名（如 `music-volume-slider-track`），使复用方引用时语义清晰 |
| `exclude_overlays` | string[] | 需要从图生图中排除的叠加 UI 元素名称列表。用于全屏背景含叠加 UI 时，在 prompt 中追加排除词 |
| `dynamic_content` | string | 动态内容描述（如 `mixed:分数数字`）。标记该组件含运行时动态内容，生成图时排除动态部分，仅生成静态框架 |
| `generation_hint` | string | 动态内容的生成提示词，配合 `dynamic_content` 使用 |
| `scene_description` | string | 全屏背景的场景内容描述（如 `"landscape with mountains and sunset"`）。仅在图生图/文生图全屏背景 prompt 中使用（`prompt-engineering.md` §2.5 的 `{scene_description}` 占位符）。来源：UI设计文档.md §12.2 或从参考图分析推导。非全屏背景组件为空 |
| `functional_type` | enum | 功能性控件类型（来自 ui-style-reference 语义分类）：`slider`（滑动条）/ `toggle`（开关）/ `dropdown`（下拉选择框）/ `text_field`（输入框）/ `progress_bar`（进度条）/ `other`（未列举但需要拆分多个视觉层的可交互控件）/ `null`（非功能性控件——普通按钮、图标、背景等整图可直接使用）。仅标记需要拆分为 2 个以上视觉层的控件。设置后 ui-image-generator 将通过 AI 分析透明 PNG 确定子部件，同一父组件的子部件走 Path C sprite sheet（默认）或 Path D 独立生成（回退），ui-panel-builder 映射到对应 Brick 控件 |
| `sub_part` | enum | 功能控件的子部件标识（仅 `functional_type` 非空时存在）：`track`（轨道/底板）/ `handle`（拖拽手柄）/ `knob`（开关旋钮）/ `fill`（进度条填充）/ `bg`（输入框背景）/ `icon`（下拉箭头等图标）/ `expanded_panel_bg`（展开态面板背景）。同 `functional_type` 的可见子部件共享透明 PNG（`final_transparent.png`）作为 `crop_path`；不可见子部件（`visible_in_reference: false`）的 `crop_path` 为 `text2img_fallback` |
| `node_id` | string | OpenCV 组件树节点 ID；缺省时兼容旧消费者 |
| `parent_component` | string | 父组件语义名；根节点为空 |
| `parent_uxml_element` | string | 父 UXML 元素名；供 builder 建立父子结构 |
| `depth` | number | 组件树深度；根为 0 |
| `render_mode` | string | `direct` / `shell_background` / `text`，描述布局角色 |
| `shell_path` | string | 从面板根到节点的容器路径 |
| `relative_parent_x_pct` / `relative_parent_y_pct` / `relative_parent_w_pct` / `relative_parent_h_pct` | number | 相对直接父级百分比；子元素布局使用这些值 |
| `root_x_pct` / `root_y_pct` / `root_w_pct` / `root_h_pct` | number | 相对参考图根的诊断坐标，仅 overlay/debug 使用 |
| `expanded_from` | string | 功能控件展开溯源。原始 `functional_type` 非空的组件条目标记自身的 `name`；展开后的子部件条目标记原始组件的 `name`。供下游追溯 1→N 的展开关系 |
| `sub_parts_summary` | string | 展开结果摘要，格式 `"{sub_part1},{sub_part2},..."`（如 `"track,fill,handle"`）。仅原始 `functional_type` 非空的组件条目存在此字段 |
| `visible_in_reference` | boolean | 子部件在参考图中是否可见（仅 `functional_type` 非空的子部件存在）。`true`：参考图可见，`crop_path` 指向透明 PNG，走 Path C sprite sheet（默认）或 Path D img2img（回退）；`false`：参考图不可见（如 dropdown 展开态面板），`crop_path` 为 `text2img_fallback`，走 Path D 文生图（**回退路径**）或 Path C Sprite Sheet（若被分入 `sprite_sheet_group`）。缺省视为 `true` |
| `sprite_sheet_group` | string | Sprite sheet 图集分组标识（Path C 使用）。同组的小型透明元素合并为一张 sprite sheet 生成，生成后通过连通区域切片为独立 PNG。值为图集任务名（如 `"sprite_sheet_0"`），用于生成阶段的文件命名。虚拟图集任务本身不写入 JSON，仅成员条目在 JSON 中。仅符合条件的元素（`source=ai_generate` + `is_segmentation=true` + `text_type!=art_text` + `max(width,height)≤768`，且 `crop_path` 为 `text2img_fallback` 或正常文件路径或 `functional_type` 非空）且被分入图集时存在此字段。Path C 根据 `crop_path` 自动判定 img2img 或文生图。图集是满足条件元素的**默认路径**，满足条件的元素必须分入图集 |
| `sprite_sheet_slot` | number | 元素在 sprite sheet 中的序号（0-based，从上到下）。供切片时按垂直位置匹配元素名 |
| `sprite_sheet_total` | number | 该 sprite sheet 中的元素总数。同一 `sprite_sheet_group` 的所有条目共享此值。每张图集最多 8 个元素（当元素 `max(w,h) > 512` 时最多 4 个） |
| `sprite_sheet_preset` | string | Sprite sheet 生成的 API 预设尺寸，固定为 `"2048x2048"`（正方形） |
| `sprite_sheet_orientation` | string | Sprite sheet 堆叠方向：`"row"`（行堆叠，宽元素 w ≥ h 竖向排列，沿 X 轴投影切片）或 `"column"`（列堆叠，高元素 h > w 横向排列，沿 Y 轴投影切片）。仅 `sprite_sheet_group` 非空时存在。决定 prompt 公式选择（§2.1 行堆叠 / §2.2 列堆叠）和切片脚本参数 |

> 上述 OpenCV 布局字段均为可选字段；缺失时保留旧文档消费者行为。

---

## source 字段与生成路径映射

| `source` | `crop_path` | `sprite_sheet_group` | 生成路径 | 说明 |
|-----------|-------------|---------|---------|------|
| `direct_use` | 透明 PNG 路径 | — | A：File.Copy + ImportAsset | ui-style-reference 的 OpenCV 组件树产出的透明 PNG 直接使用（`contaminated` composite 走 `composite_shell` img2img） |
| `ai_generate` | `reuse_from:{name}` | — | B：引用代表组件图 | 不生成，共享代表组件的图片文件。代表组件图片名为 `style_group` 派生通用名（如 `volume_slider-track.png`），非具体组件名 |
| `ai_generate` | 任意 | 非空 | C：Sprite Sheet（**默认路径**） | 多元素合并为一张 sprite sheet 生成后切片，见 `sprite_sheet_prompt_rules.md`。`crop_path` 为正常路径时走 img2img（传入父级透明 PNG），`text2img_fallback` 时走文生图 |
| `ai_generate` | 任意 | 空 | D：单张生成（**回退路径**） | `crop_path` 为正常路径时走 img2img，`text2img_fallback` 时走文生图，见 `prompt-engineering.md` |
| `uss_only` / `user_import` | — | — | 跳过 | 不生成图片 |

> **img2img vs 文生图**：路径 C/D 根据 `crop_path` 自动判定——`text2img_fallback` → 文生图（不传 `image_urls`）；正常路径 → img2img（传入 `image_urls`，路径 C 传入父级透明 PNG 的 CDN URL）。

> ⚠️ `direct_use` + `contaminated`（leaf）在 ui-image-generator Stage 01 前置转换中已将 `source` 改为 `ai_generate`（`crop_path` 指向 `final_transparent.png`），因此表中无 `direct_use + contaminated`（leaf）行——该场景由 `ai_generate + 透明 PNG 路径 + contaminated` 行覆盖。

---

## 与 UI设计文档.md §12.2 的字段对应

部分字段从 `UI设计文档.md` §12.2 组件裁剪映射表继承，由 ui-doc-builder Step 3 生成；ui-image-generator Stage 01 读取：

| UI设计文档.md §12.2 列名 | image_requirements.json 字段 |
|--------------------------|-------------------------------|
| 组件名 | `name` |
| 组件类型 | `component_type` |
| text_type | `text_type` |
| 裁剪图路径 | `crop_path` |
| source | `source` |
| quality | `quality` |
| contamination_description | `contamination_description` |
| functional_type | `functional_type` |
| 保真度 | （用于 ui-image-generator 判定生成路径，不直接写入 JSON） |
| display_text | `display_text` |
| art_text_style | `art_text_style` |
| X%/Y%/宽%/高% | （供 ui-panel-builder 布局定位，不写入 JSON） |
| style_group | `style_group` |
| reuse_from | `reuse_from` |
| exclude_overlays | `exclude_overlays` |
| dynamic_content | `dynamic_content` |
| generation_hint | `generation_hint` |

---

## 命名一致性说明

`component_type` 是本 schema 的标准字段名。ui-image-generator 早期文档中曾使用 `type` 作为字段名，两者指同一字段，**以 `component_type` 为准**。

---

## 示例

```json
[
  {
    "name": "main_menu_bg",
    "source": "ai_generate",
    "component_type": "background",
    "panel_name": "MainMenu",
    "panel_type": "full_screen",
    "uxml_element": "background",
    "crop_path": "UIReferences/Components/main_menu_bg.png",
    "is_segmentation": false,
    "width": 1920,
    "height": 1080,
    "text_type": "none"
  },
  {
    "name": "play_button",
    "source": "direct_use",
    "component_type": "button",
    "panel_name": "MainMenu",
    "panel_type": "full_screen",
    "uxml_element": "play_button",
    "crop_path": "UIReferences/Components/component_0_transparent.png",
    "is_segmentation": true,
    "width": 280,
    "height": 80,
    "text_type": "label",
    "display_text": "开始游戏"
  },
  {
    "name": "title_text",
    "source": "ai_generate",
    "component_type": "title",
    "panel_name": "MainMenu",
    "panel_type": "full_screen",
    "uxml_element": "title_label",
    "crop_path": "text2img_fallback",
    "is_segmentation": true,
    "width": 600,
    "height": 200,
    "text_type": "art_text",
    "display_text": "SNAKE\nBITES",
    "art_text_style": "Bubbly cartoonish orange font with white and green outline"
  },
  {
    "name": "pause_button_icon",
    "source": "ai_generate",
    "component_type": "icon",
    "panel_name": "HUD",
    "panel_type": "hud",
    "uxml_element": "pause-btn",
    "crop_path": "reuse_from:settings_button_icon",
    "is_segmentation": true,
    "width": 64,
    "height": 64,
    "text_type": "none"
  },
  {
    "name": "settings_slider_track",
    "source": "ai_generate",
    "component_type": "bar",
    "panel_name": "Settings",
    "panel_type": "popup",
    "uxml_element": "volume-slider",
    "crop_path": "UIReferences/Components/settings_slider_final_transparent.png",
    "is_segmentation": true,
    "width": 400,
    "height": 80,
    "text_type": "none",
    "functional_type": "slider",
    "sub_part": "track",
    "expanded_from": "settings_slider"
  },
  {
    "name": "settings_slider_fill",
    "source": "ai_generate",
    "component_type": "bar",
    "panel_name": "Settings",
    "panel_type": "popup",
    "uxml_element": "volume-slider",
    "crop_path": "UIReferences/Components/settings_slider_final_transparent.png",
    "is_segmentation": true,
    "width": 400,
    "height": 80,
    "text_type": "none",
    "functional_type": "slider",
    "sub_part": "fill",
    "expanded_from": "settings_slider"
  },
  {
    "name": "settings_slider_handle",
    "source": "ai_generate",
    "component_type": "bar",
    "panel_name": "Settings",
    "panel_type": "popup",
    "uxml_element": "volume-slider",
    "crop_path": "UIReferences/Components/settings_slider_final_transparent.png",
    "is_segmentation": true,
    "width": 64,
    "height": 64,
    "text_type": "none",
    "functional_type": "slider",
    "sub_part": "handle",
    "expanded_from": "settings_slider"
  },
  {
    "name": "pause_icon",
    "source": "ai_generate",
    "component_type": "icon",
    "panel_name": "HUD",
    "panel_type": "hud",
    "uxml_element": "pause-btn",
    "crop_path": "text2img_fallback",
    "is_segmentation": true,
    "width": 64,
    "height": 64,
    "text_type": "none",
    "sprite_sheet_group": "sprite_sheet_0",
    "sprite_sheet_slot": 0,
    "sprite_sheet_total": 4,
    "sprite_sheet_preset": "2048x2048"
  },
  {
    "name": "heart_icon",
    "source": "ai_generate",
    "component_type": "icon",
    "panel_name": "HUD",
    "panel_type": "hud",
    "uxml_element": "lives-display",
    "crop_path": "text2img_fallback",
    "is_segmentation": true,
    "width": 48,
    "height": 48,
    "text_type": "none",
    "sprite_sheet_group": "sprite_sheet_0",
    "sprite_sheet_slot": 1,
    "sprite_sheet_total": 4,
    "sprite_sheet_preset": "2048x2048"
  }
]
```
