# 文生图提示词构建规则

> 本文件包含文生图（Text-to-Image）模式的完整提示词构建规则。仅在无参考图或裁剪失败回退时使用。图生图模式的规则见 `prompt-engineering.md`。

## 1. 提示词构建公式

> ⚠️ **Path D 是回退路径**：满足 Sprite Sheet 条件（见 `sprite_sheet_prompt_rules.md` §7.1）的元素应走 Path C 图集生成。本节公式仅在文生图元素不满足图集条件、或图集切片失败回退时使用。img2img 模式不使用本公式，见 `prompt-engineering.md`。

```
"{style_baseline}, {panel_type_prefix}, {aspect_ratio} {visual_subject}, {width}x{height} pixels, {style_from_gdd}, {color_from_gdd}, {text_directive}, {negative_ui_suffix}, no watermark, game UI, high quality, detailed texture"
```

### 1.0 功能语义净化（强制，适用于所有组件类型）

图片 prompt 描述的是**视觉外观**，不是**功能用途**。功能语义词会导致 AI 生成不属于当前组件的 UI 元素。

**禁止在 prompt 中出现功能语义词**：

| 禁止词类 | 示例 | 危害 |
|---------|------|------|
| 面板功能名 | settings, menu, game over, pause, pause menu, game over screen, settings panel, main menu | AI 理解为"生成一个设置界面"而非"生成一张背景图"→带出按钮、滑块等 |
| 交互元素词 | button, slider, toggle, dropdown, text field, input, checkbox, radio | 当前组件不是这些元素时，AI 会画出它们 |
| UI 容器词 | panel, dialog, window, form, card, popup（仅当当前组件不是 panel 类型时禁止） | 背景图带出卡片/窗口形状 |

**按 `component_type` 的净化规则和 negative suffix**：

| `component_type` | 净化规则 | 必须追加的 negative suffix |
|------------------|---------|--------------------------|
| `background` | 仅描述视觉场景（环境、配色、氛围、光影）；禁止任何面板功能名、交互元素词、容器词 | `no UI elements, no buttons, no text, no icons, no panels, no frames` |
| `panel` | 可描述面板形状（矩形、圆角、边框）和材质；禁止面板功能名和交互元素词 | `no buttons, no text, no sliders, no interactive elements inside` |
| `button` | 仅描述按钮形状、材质、配色；禁止图标语义和功能用途词（start, settings, exit, retry 等） | `no icons, no text`（已有 §1.3 按钮图标分离规则） |
| `icon` | 描述图标视觉主体（gear, speaker, heart, chevron 等）；这些是视觉语义，不是功能语义 | `no text, no background frame` |
| `hud_element` | 仅描述元素外观（数字框、条形、徽章形状）；禁止 score, health, timer 等功能词 | `no text, no numbers, no background frame` |
| `bar` | 仅描述条形外观（`sub_part: track` / 无 `sub_part` → 镂空框架外观；`sub_part: fill` → 实心填充条外观；`sub_part: handle` → 旋钮/手柄外观）；禁止 score, health, progress 等功能词 | `sub_part: fill` 或 `handle` → `no text`（fill 本身就是填充内容、handle 是实心旋钮，禁用 "no fill content inside"）；`sub_part: track` / 无 `sub_part` → `no text, no fill content inside`（正面增强关键词见 §3，按 `sub_part` 路由：track → 进度条框架条目，fill → 进度条填充条目） |
| `title` | 艺术字走 §6 专用公式；非艺术字标题装饰仅描述装饰外观 | 视情况 |
| `badge` | 仅描述徽章形状和材质 | `no text, no background frame` |
| `container` | 仅描述容器边框和材质；禁止面板功能名和交互元素词 | `no text, no buttons, no interactive elements inside` |
| `display` | 仅描述显示框外观（边框、形状、材质）；禁止 score, health, timer 等功能词 | `no text, no numbers, no background frame` |

**净化执行时机**：在构建 prompt 的每个字段（`visual_subject`、`panel_type_prefix`、`style_from_gdd`、`color_from_gdd`）时，逐字段检查并移除功能语义词。`style_baseline` 也需要净化——见 §2.1 的组件类型特有词过滤规则。

**`negative_ui_suffix`**：从上方表格按 `component_type` 选取。这是独立于 `text_directive`（`no text`）的补充排除词——`text_directive` 只排除文字，`negative_ui_suffix` 排除交互元素和容器。

**`text_directive` 取值**（由组件的 `text_type` 字段决定）：

| text_type | text_directive | 说明 |
|-----------|---------------|------|
| `art_text` | 见 §6 艺术字文生图 prompt 构建 | 不追加 "no text"，改为注入艺术字专用 prompt（含 `display_text` 文字内容 + `art_text_style` 风格描述 + 多行排版指引） |
| `label` / `none` / 未指定 | `no text` | 图片不含文字，文字由 UXML 显示 |

**各字段来源**：
- `style_baseline`：从 `style_guidance.style_baseline` 读取。仅包含所有面板类型共享的视觉关键词
- `aspect_ratio`：从 `image_requirements` 的 `width`/`height` 计算并化简的宽高比字符串（如 `9:16`、`16:9`、`4:3`、`1:1`）。**必须直接前置于 `{visual_subject}`（组件名称）之前**，禁止放在 prompt 末尾或使用"宽高比为 X:Y"后缀式表达。AI 模型对 prompt 开头部分权重更高，前置式（如 `9:16 popup panel`）能让模型在构图阶段就锁定目标比例，后缀式（如 `popup panel, aspect ratio 9:16`）容易被模型忽略导致内容不铺满画布
- `panel_type_prefix`：从 `style_guidance.panel_type_styles` 中查找（见第3节抑制规则）
- `visual_subject`：从 image_requirements 读取该元素的具体视觉主体描述（源自 UI设计文档.md）
- `style_from_gdd`：UI设计文档.md 中的风格描述
- `color_from_gdd`：UI设计文档.md 中的颜色方案

## 2. 风格注入流程

### 2.1 `style_baseline` 注入

从 `style_guidance.style_baseline` 读取通用视觉基线，注入所有图片的提示词。

**`style_baseline` 组件类型特有词过滤规则（强制）**：

`style_baseline` 可能包含组件类型特有词（如 `"flat icons with bold silhouettes"`、`"pill-shaped buttons"`），这些词描述的是特定组件类型的视觉样式，注入到其他类型组件的 prompt 会误导 AI 生成错误内容（如在 slider 部件的 sprite sheet prompt 中看到 "icons" 就生成一堆图标，而非滑块轨道/填充条/旋钮）。

- 在注入 `style_baseline` 前，**移除**与当前条目 `component_type` 不匹配的组件类型特有词
- 需要移除的组件类型特有词模式：`icons`、`buttons`、`badges`、`pills`、`bars`、`labels`、`toggles`、`sliders`、`dropdowns` 等组件类型名词
- 保留当前条目 `component_type` 对应的词（如 `component_type: icon` 时保留 "icons"）
- 移除后将残留的逗号间距清理干净（避免 `", ,"`）

**`style_baseline` 文字关键词过滤规则**：

`style_baseline` 可能包含文字相关的关键词（如 `"bold text with black stroke"`、`"vibrant typography"`），这些对艺术字图片有意义，但对"不含文字"的图片类型会导致 AI 生成文字内容。

- 对于 `text_type != art_text` 的组件（即 `label` / `none` / 未指定），在注入 `style_baseline` 前，**移除**所有文字相关的关键词
- 需要移除的关键词模式：包含 `text`、`typography`、`lettering`、`font`、`word`、`caption`、`title`、`stroke text`、`text with` 等文字语义的短语
- `text_type == art_text` 的组件**保留** `style_baseline` 中的文字关键词（艺术字需要文字风格描述辅助生成）

**过滤执行顺序**：先执行组件类型特有词过滤，再执行文字关键词过滤。

### 2.2 `panel_type_prefix` 查找与抑制

**查找流程**：
1. 获取当前图片的 `panel_name`
2. 遍历 `style_guidance.panel_type_styles` 的每个面板类型
3. 检查该面板类型的 `panels` 列表是否包含 `panel_name`
4. 找到匹配 → 使用该面板类型的 `style_prefix` 作为 `panel_type_prefix`
5. 未找到匹配 → 省略 `panel_type_prefix`，仅使用 `style_baseline`

**`panel_type_prefix` 抑制规则**（关键决策）：

`style_prefix` 描述的是面板级别的装饰和氛围（如木纹底板、撕纸边框、水果装饰），这些关键词对背景类图片有意义，但对独立元素会引入无关视觉内容——导致 Logo 背景出现木纹、按钮上长出水果。

| 图片类型 | 是否注入 `panel_type_prefix` | 原因 |
|---------|----------------------------|------|
| 全屏背景、全屏背景（复合）、弹窗背景 | ✅ 注入 | 面板级装饰就是背景本身的一部分 |
| 顶部栏/底部栏 | ✅ 注入 | 栏是面板级布局元素，装饰与面板风格一致 |
| 标题装饰 | ✅ 注入 | 标题装饰与面板整体风格相关 |
| **Logo** | ❌ 抑制 | Logo 是独立品牌标识，不应混入面板级装饰 |
| **按钮背景** | ❌ 抑制 | 按钮是独立可交互元素，面板级装饰会使其出现不该有的内容 |
| **图标** | ❌ 抑制 | 图标是独立语义符号，面板级装饰会干扰图标本身的表达 |
| **徽章** | ❌ 抑制 | 徽章是独立形状元素，面板级装饰会使其变形或引入无关内容 |
| **HUD 元素** | ❌ 抑制 | HUD 元素是功能性浮空元素，面板级装饰会干扰其可读性 |
| **进度条框架/滑块轨道** | ❌ 抑制 | 框架是功能组件，面板级装饰会干扰框架的清晰度和透明中心区域 |
| **容器** | ❌ 抑制 | 容器是布局元素，面板级装饰会干扰其结构清晰度 |
| **显示框** | ❌ 抑制 | 显示框是功能性 HUD 元素，面板级装饰会干扰其可读性 |

抑制时直接省略 `panel_type_prefix`，仅使用 `style_baseline`。但 `category_overrides` 仍然生效。

### 2.3 类别增强关键词查找

查找 `style_guidance.panel_type_styles[匹配的面板类型].category_overrides[图片类别]`：
- 存在 → 使用面板类型特定的类别关键词
- 不存在 → 回退到默认增强关键词（见第4节提示词增强表）

## 3. 提示词增强表

按图片类型追加的默认关键词（当无面板类型类别覆盖时使用）：

| 图片类型 | 默认追加关键词 | UI 美学原理 |
|---------|---------------|-----------|
| 按钮背景 | `"gradient, subtle shadow effect embedded, glossy finish, on transparent background, only the button shape visible, no background behind"` | 动态感+透明背景 |
| 图标 | `"simple geometric shape on transparent background, bold silhouette, high contrast outline"` | 简洁性+对比度 |
| 弹窗背景 | `"semi-transparent frosted glass effect, subtle radial gradient, partially see-through with blurred game scene behind"` | 视觉层次+透明叠加 |
| 全屏背景 | `MISSING_PIXEL_PREVENTION`（见 `prompt-engineering.md` §缺失像素预防关键词） | 缺失像素预防 |
| 全屏背景（复合） | `MISSING_PIXEL_PREVENTION` + `"all decorative elements (borders, corner decorations, slash effects, vignette) rendered as part of the unified composition, elements interact with each other (occlusion, juice splashes on surface, consistent lighting)"` | 复合背景一体化 |
| 弹窗背景 / 面板背景 | `MISSING_PIXEL_PREVENTION` | 缺失像素预防 |
| 标题装饰 | `"elegant ornamental detail, metallic or glowing accent"` | — |
| 徽章 | `"transparent background, bold distinct shape, clean edges"` | — |
| 顶部栏 | `"gradient, semi-transparent, horizontal composition"` | 视觉层次 |
| HUD 元素 | `"floating element, no background frame, bold stroke for readability"` | 浮空元素可读性 |
| Logo | `"bold colorful text, thick outline sticker effect"` | — |
| 容器 | `"transparent background, clean edges, subtle border"` | 布局容器结构清晰 |
| 显示框 | `"transparent background, clean rectangular frame, bold border for readability"` | HUD 显示框可读性 |
| 进度条框架/滑块轨道 | `"strictly rectangular bar with flat squared-off vertical ends, straight parallel top and bottom edges, uniform height across the entire length, hollow frame with completely empty see-through center, only outer border and shadow visible, absolutely no solid fill or colored zones inside the frame, the interior must be fully transparent, do not render any fill content inside the frame, no tapered or pointed tips, no rounded capsule ends, no lens-shaped or leaf-shaped silhouette"` | 框架与动态内容分离。用 "completely empty see-through center" + "absolutely no solid fill" 替代容易歧义的 "transparent center"（AI 常忽略后者生成实心填充）。**矩形平端默认**：AI 对 8:1 以上扁长条有柳叶形/锥形收尾先验，默认锁定矩形直边平端（见 §3 末尾矩形平端默认说明） |
| 进度条填充/滑块填充条（`sub_part: fill`） | `"strictly rectangular solid bar with flat squared-off vertical ends, straight parallel top and bottom edges, uniform height across the entire length, fully solid color fill throughout the entire bar, no hollow center, no tapered or pointed tips, no rounded capsule ends, no lens-shaped or leaf-shaped silhouette"` | fill 是随 value 按宽度百分比伸缩的实心条，端形必须与 track 一致（track/fill 端形不一致时伸缩错位）。"no hollow center" 防止 AI 把 fill 也画成镂空框（bar 类型增强关键词常含 hollow frame 表述，fill 必须反向排除） |

**bar 类型的 `sub_part` 路由**：`component_type: bar` 的条目按 `sub_part` 路由到上表行——`track`（或无 `sub_part`）→ 进度条框架/滑块轨道；`fill` → 进度条填充/滑块填充条；`handle` → 不套用上表（handle 是独立旋钮/手柄小件，按其实际形状描述，如圆形旋钮）。

**⚠️ Path C 图集必须用精简变体**：上表 bar 长关键词仅适用于 Path D 单元素 prompt（单元素预算充足）。Path C sprite sheet 把 N 个元素拼进同一 prompt，受 1024 字符 API 硬限制约束（超限返回 `INVALID_PARAMS`），bar 条目在图集 prompt 中必须改用 `sprite_sheet_prompt_rules.md` §3.1「Prompt 长度预算」的 Path C 精简关键词，并按该节裁剪 `style_baseline`。

**矩形平端默认（可覆盖）**：slider / progress_bar 的 track 与 fill 默认为**矩形、左右两端平直**（flat squared-off vertical ends）。原因：(1) 文生图模型对 8:1 以上扁长条有强烈的柳叶形/锥形收尾先验，不约束端形时几乎必然生成尖头或胶囊头；(2) fill 在 USS 中随 value 按宽度百分比伸缩、track 可能 9-slice 拉伸，矩形平端在两种伸缩下都不变形，异形端头必然错位；(3) track 与 fill 端形必须一致。仅当用户风格需求**明确要求**其他端形（如"两端尖的装饰进度条"、"胶囊形滑条"）时才覆盖此默认，且必须同步应用于 track 与 fill 两者。

## 4. 纯色面板背景的提示词规则

当面板背景为**纯色/近纯色**（无渐变/纹理/装饰/圆角，`background-size: 100% 100%`）时，文生图模型极易将颜色名误读为场景（如"深森林绿"→生成森林）。需执行以下 3 步：

### 4.1 场景歧义颜色名消歧

将颜色描述改写为通用颜色名 + hex 色值（如 `dark green (#1a3a2a)`），避免自然界/场景词汇（forest, sky, ocean, sunset 等）。

### 4.2 抑制 `style_baseline` 场景关键词

从 `style_baseline` 中移除风格/画风词（cartoon style, realistic 等）、场景氛围词、描边词、场景构成词。仅保留 UI 质感词（matte finish, clean edges 等）。

### 4.3 追加场景排除约束

prompt 必须追加：`"solid colored rectangle filling entire canvas, no landscape, no scenery, no nature, no scene, no objects, no patterns, flat colored surface only"`

### 4.4 完整 prompt 示例

```
"{filtered_style_baseline}, 4:3 dark green (#1a3a2a) vertical rectangle panel, matte finish, game UI popup panel card, solid colored rectangle filling entire canvas from top edge to bottom edge and left edge to right edge, no landscape, no scenery, no nature, no scene, no objects, no patterns, flat colored surface only, no transparent margins, no empty space around panel, no buttons, no text, no icons, no UI elements inside, high quality"
```

### 4.5 适用范围

仅适用于纯色/近纯色面板背景。不适用于带装饰/纹理的面板、弹窗卡片（有独立形状/阴影/圆角）、全屏背景（本身就是场景）。

## 5. 色彩和谐与背景类注意

- 渐变从主色到稍深同色系。提示词中**绝不能包含** `"solid color"` / `"flat color"` / `"plain"` — 会导致空白图片
- **禁止** `"dark vignette corners"` / `"strong vignette"` / `"dramatic spotlight"` → 极端聚光灯效果
- **推荐** `"subtle gentle corner shading"` / `"slight darkening at edges"` / `"soft corner shadow"`
- **必须追加** `"even lighting across entire image, no dramatic spotlight effect"`

## 6. 艺术字文生图 prompt 构建（text_type == art_text）

当组件 `text_type == art_text` 且走文生图路径（`crop_path == "text2img_fallback"`——art_text 拆分文字层无独立裁剪图，只能文生图）时，使用专用 prompt 公式替代标准文生图公式。

**核心差异**：艺术字 prompt 必须包含实际文字内容、视觉风格描述和行数排版指引，**不追加** "no text"。

### 6.1 prompt 公式

```
"{art_text_style}, {aspect_ratio} artistic game text '{display_text_flat}', {line_directive}, on transparent background, {width}x{height} pixels, high quality, detailed texture"
```

**各字段来源**：

| 字段 | 来源 | 说明 |
|------|------|------|
| `art_text_style` | `image_requirements` 的 `art_text_style` 字段 | 艺术字视觉风格关键词（如 `metallic engraved text with golden gradient and outer glow`）。若 `art_text_style` 为空，回退到 `style_baseline` 中的文字关键词（§2.1 保留策略） |
| `aspect_ratio` | 从 `image_requirements` 的 `width`/`height` 计算并化简 | 宽高比字符串（如 `4:1`、`8:3`）。必须前置在 "artistic game text" 之前，禁止放在末尾 |
| `display_text_flat` | `display_text` 中的 `\n` 替换为空格后的纯文本 | 实际文字内容（如 `STEAM BARRAGE`），用于让 AI 识别文字内容 |
| `line_directive` | 从 `display_text` 中的 `\n` 数量推导 | 行数排版指引（见 §6.2） |
| `width`x`height` | `image_requirements` 的目标尺寸 | 与底板尺寸相同（拆分行共享布局尺寸） |

### 6.2 行数排版指引（line_directive）

从 `display_text` 中的 `\n` 分隔符推导行数和每行内容：

| `display_text` 中的 `\n` | 行数 | `line_directive` |
|--------------------------|------|-----------------|
| 无 `\n` | 1 行 | `single line` |
| 1 个 `\n`（2 行） | 2 行 | `arranged in two lines, first line '{line1}', second line '{line2}'` |
| N 个 `\n`（N+1 行） | N+1 行 | `arranged in {N+1} lines: {line1}, {line2}, ... {lineN+1}` |

**示例**：
- `display_text: "STEAMBARRAGE"` → `line_directive: "single line"`
- `display_text: "STEAM\nBARRAGE"` → `line_directive: "arranged in two lines, first line 'STEAM', second line 'BARRAGE'"`

### 6.3 完整 prompt 示例

**单行艺术字**（`display_text: "BATTLE"`，`art_text_style: "metallic engraved text with golden gradient and outer glow"`）：
```
"metallic engraved text with golden gradient and outer glow, 4:1 artistic game text 'BATTLE', single line, on transparent background, 800x200 pixels, high quality, detailed texture"
```

**多行艺术字**（`display_text: "STEAM\nBARRAGE"`，`art_text_style: "neon glowing text with electric blue outline"`）：
```
"neon glowing text with electric blue outline, 8:3 artistic game text 'STEAM BARRAGE', arranged in two lines, first line 'STEAM', second line 'BARRAGE', on transparent background, 800x300 pixels, high quality, detailed texture"
```

### 6.4 `panel_type_prefix` 和 `style_baseline` 处理

艺术字 prompt **不注入** `panel_type_prefix`（面板级装饰会干扰文字本身的表达）。`style_baseline` 中的文字关键词保留（§2.1 过滤规则改为按 `text_type` 判定），非文字关键词可选注入作为辅助风格。

### 6.5 `is_segmentation`

艺术字图片 `is_segmentation` 默认为 `true`（透明背景），与 `shared/adjudication-tables.md` 的 `is_segmentation` 判定表一致。

### 6.6 与图生图路径的关系

`text_type == art_text` 的组件在图生图路径下（`_plate` 行），`text_suffix` 机制（`prompt-engineering.md` §2.1）仍适用——图生图生成底板时 `art_text` → 空 suffix（不追加 "no text"），因为底板裁剪图本身不含文字。本节仅适用于文生图路径（`_text` 行，`crop_path == "text2img_fallback"`——art_text 拆分文字层无独立裁剪图，只能文生图）。
