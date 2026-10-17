# 最终图片批量生成

树完成后，所有需要图片的节点按**处理配方**分组，每组独立装箱、每页一次图生图。全屏背景保持步骤 8.5 的独立生成流程，不进入图集。

## 前置检查

1. **递归完成**：`component-tree.json` 中所有 `state=valid` 的节点要么是 `kind=leaf`，要么 `kind=composite` 且已标记 `recursion_done`。否则返回 Step 8 继续递归。
2. **叶子完整性**：遍历所有 `kind=leaf` 且 `source != skip_generation` 且 `source != uss_only` 的节点，检查 `nodes/{nodeId}/final_transparent.png` 是否存在且非空。缺失的叶子必须先补齐（从对应的 OpenCV 裁剪图复制，或按 Stage 02 SKILL.md Step 5「后验对比」中的「img2img 完全丢失」恢复策略从原图恢复）。

## 9.1 处理配方

### 关键色裁剪配方（默认路径，无需 AI）

AI 输出已含关键色背景，裁剪后由 `split --clean` 或 `extract_reference_components.py` 自动将关键色转换为透明 alpha 通道，直接使用，不走图集/图生图：

| 配方 | 节点范围 | 处理方式 | render_mode | source |
|---|---|---|---|---|
| `functional_control` | `functional_type` 非空（需拆分多个视觉层）的叶子 | 关键色裁剪→透明 PNG（关键色转换为 alpha 通道），`crop_path` 指向 `final_transparent.png`，下游以透明 PNG 为 img2img 输入 | `direct` | `ai_generate` |
| `leaf_clean` | `text_type=none` 的普通叶子 | 从关键色背景裁剪图裁剪→关键色转换为透明 PNG。保留组件原始像素。 | `direct` | `direct_use` |
| `leaf_plate_no_label` | `text_type=label` 的 UI 组件 | 同上。底板上的文字在关键色背景裁剪图中已被 AI 子项隔离替换为关键色，转换后自然消失。 | `direct` | `direct_use` |
| `art_text` | `text_type=art_text` | 同上。保留艺术字像素。 | `direct` | `direct_use` |

> 为什么默认不用 AI 图生图：裁剪图经关键色→透明转换后分辨率无损、像素保真。
>
> **Fallback 到图生图**：当裁剪后质量不达标（模糊、残缺、染色等）时，fallback 到对应配方的图生图流程（§9.3-9.5），使用 §9.4 的 prompt。
>
> `leaf_plate_no_label` 在 depth=0 时：根级裁剪图中的普通文字尚未被子项隔离移除，关键色转换后文字仍保留。此时需 fallback 到图生图移除文字。depth>=1 的裁剪图文字已被子项隔离替换为关键色，转换后自然消失。

### AI 图生图配方（走图集批处理）

| 配方 | 节点范围 | AI 输出要求 |
|---|---|---|
| `composite_shell` | `kind=composite && state=valid && recursion_done` 且**面积占比 < 90%** 且**无 `reuse_structure_from`** 的节点 | 仅保留底板、边框、阴影和装饰，移除所有内部子 UI、图标、文字和动态内容。 |

> **全屏根容器 / reuse_structure_from composite 不生成 shell**：面积占比 ≥90% 的全屏根容器标记 `source=skip_generation`（背景已由 Step 8.5 覆盖）；`reuse_structure_from` composite 直接复用代表节点的 shell（**读** Stage 05 SKILL.md §9b）。
>
> **其余 composite 禁止跳过 shell**：面积占比 < 90% 的 composite 必须走 `composite_shell` 配方生成外壳图，无例外。

text-label 节点（从 text_only 图像检测）不生成图片，标记 `render_mode=text`、`source=uss_only`；`state=skip` 节点也不进入。

> **禁止跳过装箱**：即使某配方只有 1 个节点，也必须走 §9.3 装箱 -> §9.4 图生图 -> §9.5 拆图清理的完整流程。`split --clean` 内置的关键色→透明转换、alpha 噪声清理、清噪声、缩放到 `export_size` 是产出正确透明 PNG 的必要步骤。

> ✅ **`make_key_color_transparent.py`**：用于独立关键色→透明转换。当需要手动处理单张图片时使用。支持 `--key-color-r/g/b`、`--key-color-hex`、`--transform-json` 等参数指定关键色。

## 9.2 准备输入

对**AI 图生图配方**的节点，从**已有纯色背景的连通域 mask 裁剪图**复制组件区域，保存为 `nodes/{nodeId}/original_crop.png`。来源按节点深度确定：

| 节点深度 | 来源 | 说明 |
|---------|------|------|
| depth=0 | `root-extract/component_{NNN}.png`（Stage 02 产出） | 已用连通域 mask 裁剪，排除兄弟组件像素——直接复制，禁止从 `ui_only_2048.png` 做 bbox 裁剪 |
| depth>=1 | `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（上一层产出） | 已用连通域 mask 裁剪——直接复制，禁止从 slot 图做 bbox 裁剪 |

若裁剪图任一边 < 128px，用关键色 pad 到 128x128。

> **禁止从原图或隔离图做 bbox 裁剪**：bbox 裁剪会把兄弟组件像素混入。必须使用上一层的连通域 mask 裁剪图。

**关键色裁剪配方**使用 `extract_reference_components.py` 的裁剪输出（`component_{NNN}.png`，含关键色背景）作为输入，经关键色→透明转换生成最终透明 PNG：

| 节点深度 | 裁剪来源 | 生成方式 |
|---------|---------|---------|
| depth=0 | `root-extract/component_{NNN}.png`（Step 5 输出） | 关键色→透明转换（裁剪图已含关键色背景） |
| depth>=1 | `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（Step 8.4 输出） | 同上 |

标记 `render_mode=direct`、`source=direct_use`。默认不进入 9.3-9.5；仅当裁剪后质量不达标时 fallback 到图生图。

## 9.3 装箱

每个配方独立调用 `generation_atlas.py pack`；同一配方可跨 `depth` 合并。生成 `atlas-items.json` 时 `atlas_kind` 设为 `final_generation`，`recipe` 设为对应配方名。

```text
{PYTHON} scripts/generation_atlas.py pack \
  --items {recipeDir}/atlas-items.json \
  --output-dir {processingDir}/Atlases/final-generation/{recipe}/
```

## 9.4 图生图（每页一次）

> 所有配方使用 `is_segmentation: false` + 关键色。AI 输出含关键色背景的 RGB 图，拆图时 `--clean` 统一将关键色转换为透明 alpha 通道。无需区分 per-item 处理路径。

每页只使用一个共享配方提示词，附加"各槽位相互独立、保持槽位和内容位置、禁止跨槽串图"约束，按 `shared/contracts.md`「图生图执行规则」由单个 subagent 逐个提交（在途数 < 3）：

```text
generate_image（TJGenerators MCP）:
provider: seedream_pro
image_urls: <input-page-{P}.png 按 mcp-image-generation.md §2 上传的 CDN URL>
image_size: {"width":2048,"height":2048}
is_segmentation: {is_segmentation}
confirm_cost: true
prompt: "{recipe_prompt}。每个槽位相互独立。保持每个槽位内的位置和大小。不要跨槽位移动内容。"
```

### 配方参数

> **`{key_color}` 从 `transform.json` 的 `key_color.name` 和 `key_color.rgb` 字段读取**，替换为颜色名称+十六进制（如 `magenta (#FF00FF)`，hex 从 rgb 的 r/g/b 转大写拼接），禁止硬编码颜色。

| 配方 | is_segmentation | prompt | 备注 |
|---|---|---|---|
| `leaf_clean` | `false` | `{key_color}背景上的独立游戏图标。无文字、无场景、无其他UI元素。` | fallback only |
| `leaf_plate_no_label` | `false` | `保留按钮/面板底板和图形。移除所有文字、数字和标签。` | fallback only |
| `art_text` | `false` | `保留艺术字及其风格。移除背景。将背景设为{key_color}。`（不追加 `no text`） | fallback only；如需拆分，plate 用 `leaf_plate_no_label` prompt，text 用文生图（`text2img_fallback`——art_text 拆分文字层无独立裁剪图，只能从 `display_text` + `art_text_style` 文生图） |
| `composite_shell` | `false` | `对每个UI组件，仅提取背景。移除所有按钮、文字、图标、角色插画和前景对象。只保留背景图案、颜色和环境。{key_color}背景。` | 使用 `is_segmentation=false` 输出含关键色背景的 RGB 图，`split --clean` 转换为透明 PNG |

### prompt 调整方向（按配方类型）

- `leaf_clean`：图标不完整 -> 放宽隔离约束（如 `纯色背景上的游戏图标`）；**contaminated 叶子**（`quality=contaminated`）基于 `contamination_description` 构建移除指令（如 `移除不属于本组件的{contamination_description}。`）
- `leaf_plate_no_label`：底板文字残留 -> 加强移除（如 `完全移除所有文字`）；contaminated 时同上基于 `contamination_description` 构建移除指令
- `art_text`：艺术字风格改变 -> 简化 prompt 只保留 `保持文字风格不变`；如需拆分，plate 部分参考 `leaf_plate_no_label`
- `composite_shell`：子项残留 -> 加强移除（如 `完全移除所有内部内容和前景对象`）

## 9.5 拆图与清理

```text
{PYTHON} scripts/generation_atlas.py split \
  --page {outputPage} \
  --manifest {manifest} \
  --output-dir {processingDir}/Atlases/final-generation/{recipe}/nodes/ \
  --clean
```

`--clean` 模式：裁掉 `slot_rect` 外围安全区，保留 `content_rect` 内部透明边距和原始锚点；关键色→透明转换（关键色像素 alpha=0）、alpha 噪声清理（alpha < 10 -> 0）、保留抗锯齿（不做硬二值化）；按 manifest 中 `export_size` 缩放（缩小时 LANCZOS，放大时双三次）。

> `composite_shell` 配方使用 `is_segmentation=false`，AI 输出含关键色背景的 RGB 图。`split --clean` 将关键色转换为透明 alpha 通道，执行噪声清理和缩放。

拆图验证：透明度、非空内容、越界、原图宽高比（误差 <=0.5%）和串图检查。失败项记录原因。

**拆图失败的特例处理**：**读** Stage 04 SKILL.md「拆图失败的特例处理」段。

**关键色残留检测（强制）**：`split --clean` 或 `make_key_color_transparent.py` 输出每张透明 PNG 后，必须检测是否有关键色残留。关键色残留表现为图片边缘的 magenta/紫色边框或光晕，是 key_color→透明转换失败的标志。

检测分两步——先用 Python 脚本做确定性快筛，再对检出问题的图用 `analyze_multimedia` 做视觉确认：

**第 1 步：Python 脚本快筛（全部图片，确定性，零 token）**

```text
python scripts/check_key_color_residue.py {transparent_png} {transform_json}
```

脚本检查图片边缘 5px 范围内是否有非透明像素的 RGB 值接近关键色（tolerance=30）。退出码 0=clean，1=residue detected，2=error。批量执行时逐张检查，收集退出码 1 的图片。

**第 2 步：analyze_multimedia 确认（仅对脚本检出的图片）**

仅对第 1 步退出码 1 的图片调 `analyze_multimedia`：
```text
检查这张图片的边缘或边框是否有任何洋红色或亮紫色/粉色像素。
洋红色残留表示关键色透明转换失败。
回答：{"has_key_color_residue": true/false, "residue_location": "edges/borders/none"}
```

判定：
- 脚本退出码 0 → 合格，继续
- 脚本退出码 1 + `analyze_multimedia` 确认 `has_key_color_residue=true` → **不合格**，禁止作为 `final_transparent.png` 使用，必须重新走该配方的 img2img fallback
- 脚本退出码 1 + `analyze_multimedia` 确认 `has_key_color_residue=false` → 假阳性，合格，继续

> 此检测在 §9.7 写回组件树前执行。不合格图片不写回，标记该节点为 `generation_failed`，触发 §9.6 失败补图。

**输出验证**：**读** Stage 04 SKILL.md「最终图片输出验证」段。

## 9.6 失败补图

**读** Stage 04 SKILL.md「失败补图」段。

## 9.7 写回组件树

通过节点写回独立文件：
- 叶子：`nodes/{nodeId}/final_transparent.png`，`render_mode=direct`、`source=direct_use`
- 外壳：`nodes/{nodeId}/shell_transparent.png`，`render_mode=shell_background`、`source=direct_use`
- 艺术字（`art_text`）：默认生成 `nodes/{nodeId}/final_transparent.png`；如需独立拉伸，拆分为底板 `nodes/{nodeId}/plate.png` + 艺术字 `nodes/{nodeId}/art_text.png`，在裁剪映射表中拆为两行

节点附加 `generation_atlas` 诊断引用（`page_index`、`slot_rect`、`content_rect`、`recipe`、`retry_round`）。

### 外壳九宫格

外壳图有边框/圆角时须设置 Sprite `spriteBorder`（左右覆盖圆角区域），USS 用 `-unity-background-scale-mode: stretch-to-fill`。border 值从图片边缘到圆角结束处测量，通常占宽度 10%-15%。图集缩放比例不得直接作为 Sprite border。

### 艺术字拆分（`art_text`，仅当需要独立拉伸底板与文字时执行）

1. 底板：关键色裁剪得到 plate PNG（或 `leaf_plate_no_label` 配方图生图 fallback）
2. 艺术字：文生图（`text2img_fallback`——art_text 拆分文字层无独立裁剪图，只能从 `display_text` + `art_text_style` 文生图。prompt 含 `display_text` + `art_text_style`）
3. UXML 中底板为背景层，艺术字 PNG 为内容层叠加。不可用普通 `Label` 替代艺术字。

不需要独立拉伸时直接使用单张图片即可。
