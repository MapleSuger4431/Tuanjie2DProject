# 机械判定表（Precompute 工具规格与 FAIL 仲裁）

本文件是部署型工具 `Create.GameFramework.TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`，`execute_csharp_script` 一行调用）的**规格权威**，同时是工具 FAIL 时的人工仲裁依据。

- **Stage 01 正常流程无需读取本文件**——三张表已由工具机械执行并写入 `image_requirements.json`（`[PRECOMPUTE_CHECK] PASS` 兜底）。
- 仅以下情况读取：工具 FAIL 按日志排查、对工具输出存疑需人工裁决、Stage 01 完成后尺寸抽查核对。
- **上游 `ui-doc-builder` 有参考图路径（手动执行 §3.2.x）执行前必读本文件**，获取路径判定表与 `is_segmentation` 判定表。

## 权威路径判定表

> 以本表为权威源。`references/prompt-engineering.md` §0 和 `references/sprite_sheet_prompt_rules.md` 中的路径判定均引用此表。
>
> ⛔ **规则同步义务**：本节（前置转换 + 路径判定表）由部署型工具 `Create.GameFramework.TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`）机械执行（写入 `generation_path` 字段）。**修改本节任一规则须同步修改该工具并重跑自检**，反之亦然——两源漂移时 Stage 01 静默产出与文档不符的判定。冲突裁决时以本表为规格权威、工具产物为执行产物，发现不一致回报调用方。

### 前置转换（在路径表之前执行，仅 `kind=leaf`）

1. `functional_type` 非空 → `source` 强制设为 `ai_generate`（优先级最高）
2. `quality=contaminated` → `source` 强制设为 `ai_generate`，`crop_path` 指向 `final_transparent.png`

前置转换优先级：`functional_type > quality > source`。

### 路径判定表

> 前置转换完成后，路径仅由 `source`、`crop_path`、`sprite_sheet_group` 三个字段决定。

| source（转换后） | crop_path | sprite_sheet_group | → 路径 | 说明 |
|---|---|---|---|---|
| `direct_use` | 任意 | — | A | 部署型工具 `TJImagePostprocess.DeployDirectUse`（ui-image-generator Stage 03 前置）执行 File.Copy + ImportAsset（`contaminated` composite 走 `composite_shell` img2img） |
| `ai_generate` | `reuse_from:xxx` | — | B | 引用代表组件图，不生成，不复制——USS 直接引用代表组件图片路径。代表组件图片名应使用 `style_group` 派生的通用名（如 `volume_slider-track.png`），而非绑死到某具体组件名 |
| `ai_generate` | 任意 | 非空 | C | Sprite Sheet（读 `references/sprite_sheet_prompt_rules.md`） |
| `ai_generate` | 任意 | 空 | D | 单张生成（读 `references/prompt-engineering.md`） |
| `uss_only`/`user_import` | — | — | 跳过 | — |

> **img2img vs 文生图**：路径 C/D 根据 `crop_path` 自动判定——`text2img_fallback` → 文生图（不传 `image_urls`）；正常路径 → img2img（传入 `image_urls`，路径 C 传入父级透明 PNG 的 CDN URL）。

## `is_segmentation` 判定表（权威定义）

> 由部署型工具 `Create.GameFramework.TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`）机械执行（写入 `is_segmentation` 字段）——修改本表须同步修改该工具，两源不得漂移。

| 优先级 | 条件 | `is_segmentation` |
|--------|------|-------------------|
| 1 | 上游 `is_segmentation` 字段值为 `true` | `true` |
| 2 | 上游为 `false`，且 `crop_path` 含 `nodes/background/` 或（`crop_path == "text2img_fallback"` 且 `component_type == "background"`） | `false` |
| 2.9 | 上游为 `false`，但不在上述情况中 | `true`（覆盖上游错误值） |
| 3（默认推断） | 上游未指定，且为全屏背景（同优先级 2 条件） | `false` |
| 4（默认推断） | 上游未指定，且不在上述列表中 | `true` |

## API 预设尺寸选择

> 「选择规则（逐项强制计算）」与「极端宽高比回退」由部署型工具 `Create.GameFramework.TJImageGenPipeline.Precompute`（包 `Editor/UIBuildTools`）机械执行（写入 `size` 字段并输出 `[SIZE_CHECK]` 审计行）——修改本节规则须同步修改该工具，两源不得漂移。

MCP `generate_image`（`provider: seedream`）支持以下预设尺寸，提交时以 `image_size: {"width":W,"height":H}` 自定义宽高传入。1K/2K 档已实测可用；4K 被拒绝。被拒时换同宽高比最接近的已验证档位并记录。

### 1K 档

| 预设尺寸 | 宽高比 | 适用场景 |
|---------|--------|---------|
| 1024x1024 | 1:1 | 小型图标、正方形元素 |
| 1152x864 | 4:3 | 小型 4:3 面板/弹窗 |
| 864x1152 | 3:4 | 小型竖版面板 |
| 1312x736 | 16:9 | 小型横幅、HUD 条 |
| 736x1312 | 9:16 | 小型竖幅 |
| 1248x832 | 3:2 | 小型 3:2 元素 |
| 832x1248 | 2:3 | 小型 2:3 元素 |
| 1568x672 | 21:9 | 小型超宽横幅 |

### 2K 档

| 预设尺寸 | 宽高比 | 适用场景 |
|---------|--------|---------|
| 2048x2048 | 1:1 | 正方形背景、Sprite Sheet（Path C）画布 |
| 2304x1728 | 4:3 | 4:3 面板/弹窗背景 |
| 1728x2304 | 3:4 | 竖版面板背景 |
| 2848x1600 | 16:9 | 全屏背景 ← 1920×1080 首选 |
| 1600x2848 | 9:16 | 竖屏全屏背景 |
| 2496x1664 | 3:2 | 3:2 元素 |
| 1664x2496 | 2:3 | 2:3 元素 |
| 3136x1344 | 21:9 | 超宽横幅 |

> 4K 档不支持。2K 档无法满足目标像素需求时，取该宽高比最大的 2K 预设，由 Stage 03 缩放到目标尺寸补偿。

### 选择规则（逐项强制计算）

对 `image_requirements.json` 中每个 `source == "ai_generate"` 的条目，**必须逐项执行以下计算并记录推理过程**，禁止笼统判断后批量套用同一预设。

#### 术语

- **目标宽高比** = `width : height`（从 image_requirements.json 的 `width`/`height` 字段读取）
- **预设宽高比** = 上表各预设的比值（如 1024x1024 = 1:1, 1312x736 = 16:9 ≈ 1.78:1, 1568x672 = 21:9 ≈ 2.33:1）
- **偏离度** = `max(目标比值, 预设比值) / min(目标比值, 预设比值)`（始终 ≥ 1.0；值越小越接近）

#### 步骤

1. **计算目标宽高比**：`target_ratio = width / height`
2. **逐个预设计算偏离度**：`deviation = max(target_ratio, preset_ratio) / min(target_ratio, preset_ratio)`
3. **选择偏离度最小的预设宽高比**（即 `deviation` 最小的那个）
4. **在同宽高比的预设中，选择实际像素 ≥ 目标像素的最小档位**（1K → 2K 逐级升级，确保下采样，避免浪费画布；2K 最大档仍不满足时取该宽高比最大 2K 预设，Stage 03 缩放补偿）
5. Sprite Sheet（Path C）固定使用 `2048x2048`（2K 1:1），不按宽高比选择。图集是满足条件元素的**默认路径**，Path D 仅用于不满足图集条件的回退

#### 极端宽高比回退（deviation > 2.0 时）

当目标宽高比极端（如 slider track 600×40 = 15:1、button 400×80 = 5:1），所有预设的 deviation 均 > 2.0 时，按以下优先级回退：

| 优先级 | 策略 | 适用场景 | 生成尺寸 | 后处理 |
|--------|------|---------|---------|--------|
| 1（首选） | **正方形画布策略** | 目标宽高比 > 4:1 或 < 1:4 的细长元素（slider track/fill、button、dropdown collapsed 等） | 选 `1024x1024`（1K 1:1）或 `2048x2048`（2K 1:1，当目标像素 > 1K 档容量时） | Stage 03 裁剪透明边框后缩放到目标 width×height |
| 2（次选） | **最近宽高比凑合** | 目标宽高比在 2:1 ~ 4:1 之间，偏离度 1.5~2.0 | 选 deviation 最小的预设 | Stage 03 裁剪+缩放修正 |

> ⚠️ **正方形画布策略的 prompt 调整**：使用此策略时，prompt 中的 `{width}x{height} pixels` 保持写目标尺寸（如 `600x40 pixels`），但实际生成画布是正方形。AI 会在正方形画布中生成目标元素，周围留白/透明。Stage 03 的 `TrimTransparentBorders` 会自动裁剪到实际内容边界，再缩放到目标尺寸。
>
> **为什么不用最接近的宽预设（如 21:9）凑合**：15:1 的 slider track 用 21:9（2.33:1）画布生成，AI 会被迫拉伸或居中放置，生成质量下降且后处理裁剪损失大。正方形画布让 AI 自由布局，后处理精确裁剪到目标比例，质量更优。

#### 计算示例

| 图片 | 目标尺寸 | target_ratio | 最接近预设 | deviation | 选择结果 | 理由 |
|------|---------|-------------|-----------|-----------|---------|------|
| bg-mainmenu | 1080×1920 | 0.5625 (9:16) | 1600×2848 (9:16) | 1.0 | `1600x2848` | 完全匹配 |
| panel-gameover | 800×600 | 1.333 (4:3) | 1152×864 (4:3) | 1.0 | `1152x864` | 完全匹配 |
| btn-primary | 400×80 | 5.0 (5:1) | 1568×672 (21:9≈2.33) | 2.15 | `1024x1024` | deviation > 2.0，回退到正方形画布 |
| volume-slider-track | 600×40 | 15.0 (15:1) | 1568×672 (21:9≈2.33) | 6.44 | `1024x1024` | deviation > 2.0，回退到正方形画布 |
| volume-slider-handle | 48×48 | 1.0 (1:1) | 1024×1024 (1:1) | 1.0 | `1024x1024` | 完全匹配 |

#### `{aspect_ratio}` 计算字段

> **不写入 `image_requirements.json`**，由 Stage 01 Step 1 在确定 API 预设后计算，直接注入 prompt 公式。

从 `image_requirements` 条目的 `width`/`height` 计算宽高比并化简为最简整数比：

1. `raw_ratio = width / height`（浮点数）
2. 化简为最简整数比：`gcd = gcd(width, height)`，`aspect_ratio = "{width/gcd}:{height/gcd}"`
3. 示例：`1080×1920` → `gcd=120` → `9:16`；`800×600` → `gcd=200` → `4:3`；`400×80` → `gcd=80` → `5:1`

**prompt 注入规则**：`{aspect_ratio}` 必须前置于组件名称之前（如 `9:16 popup panel`），禁止放在 prompt 末尾或使用后缀式表达（如 `aspect ratio 9:16`）。详见 `text-to-image-prompt-rules.md` §1 和 `prompt-engineering.md` §2.1。
