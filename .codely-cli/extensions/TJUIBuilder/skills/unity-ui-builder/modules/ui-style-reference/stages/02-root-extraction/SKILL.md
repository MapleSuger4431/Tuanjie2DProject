# Stage 02: 根级组件提取与语义分类

Steps 5-6.3：OpenCV 提取根级组件 → 语义分类 → quality 验证 → 构建初始组件树。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `ui_only_2048.png`（隔离图）、`text_only_2048.png`（纯文字图）、`source_square.png`（原图补边）、`transform.json`、`pipeline-state.json`（含 `panel_provenance.step3_isolation`） |
| 输出 | `component-tree.json`（depth=0 根级节点，含 `panel_provenance`）、`semanticRecords.json`、各节点 `semantic.json` |
| 前置门禁 | `pipeline-state.json` 中 `01-isolation` = `completed` |
| 完成判据 | `preflight_check.py field_completeness` 通过 + `quality_existence` 通过 + `label_geometry` 通过 |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：图生图执行规则、溯源记录、目录布局、核心硬约束见 `shared/contracts.md`。

## 5. OpenCV 提取根级组件

```text
{PYTHON} scripts/extract_reference_components.py \
  {processingDir}/ui_only_2048.png \
  {processingDir}/transform.json \
  {processingDir}/root-extract
```

可选参数：`--min-area 64`、`--min-dimension 32`。脚本输出 `components.json`（含 `original_image_box`）、`component_*.png`（仅语义分类参考）、前景 mask、根框叠加诊断图。裁剪框向外扩展少量像素（默认 4px），与相邻框重叠则不扩展。**OpenCV 框是唯一几何来源**。

脚本自动调用 `filter_contained_bboxes` 丢弃包含小框的大框（边框碎片/背景 blob，全屏轮廓除外）。

<HARD-GATE>执行后验对比前**必须完整读取** `references/opencv-recovery.md`（§1-§4 全文）——该文档定义了后验对比的偏差检查方法、`analyze_multimedia` 验证 prompt 和恢复策略。跳过会导致组件缺失或过度合并非发现在下游。</HARD-GATE>

**后验对比**：执行后检查三种偏差（缺失组件、噪声碎片、过度合并）、相邻组件和自引用组件。相邻组件包含两种处理路径——碎片保留（§3a）和逻辑视觉单元合并（§3b）。恢复策略和验证 prompt 见 `references/opencv-recovery.md`（§1-§4）。

> **自引用组件检查**：执行 §2 `analyze_multimedia` 后验对比之前，先检查 `components.json` 中的 `self_reference_warnings` 字段。若存在自引用组件（`self_reference=true`，面积占比 >=90%），按 `references/opencv-recovery.md` §1.4 处理——有兄弟组件时标记 `state=skip` 并写入 `semanticRecords.json`，不提交 Step 6 语义分类；无兄弟组件时说明 Stage 01 隔离失败，用 `ask_user` 通知 orchestrator 重新派发 Stage 01 Step 3（加强背景移除 prompt 重试）。Stage 01 retry 完成后重新派发 Stage 02。

## 5.5 文字标签检测（OpenCV on text_only_2048.png）

> Stage 01 img2img 执行 "Remove all text, numbers" 后，普通文字在隔离图中消失，OpenCV 无法检测到独立的文字标签区域（如"主音量"、"全屏"、"分辨率"等行级标签）。本步骤从 Stage 01 Step 3c 产出的 `text_only_2048.png`（关键色背景 + 仅普通文字像素）用 OpenCV 精确提取文字标签的 `root_box` 几何坐标。OpenCV 的 `foreground_mask` 检测所有非关键色像素，文字像素无论颜色都能被检测到。

**执行时机**：Step 5 后验对比（§2）通过后、Step 6 语义分类之前。

### 5.5.1 OpenCV 文字提取

> **前置检查**：确认 `text_only_2048.png` 已存在。**只要文件存在，必须使用 `extract_text_labels.py` 做 OpenCV 提取**，禁止以任何理由（包括残留 UI 元素、艺术字未完全移除、3c 验证未通过等）跳过 OpenCV 走 fallback。OpenCV 只检测非关键色像素的 bounding box，不依赖图片纯净度。仅当文件完全不存在（3c 被跳过）时才降级为 §5.5-fallback。

> ⚠️ **必须使用 `extract_text_labels.py`，禁止用 `extract_reference_components.py`**。后者为 UI 控件设计，其 `split_by_projection` 会按投影谷切割文字行、`morphologyEx(MORPH_OPEN)` 会腐蚀文字笔画，导致整行文字被拆成零散单字。

```text
python scripts/extract_text_labels.py \
  {processingDir}/text_only_2048.png \
  {processingDir}/transform.json \
  {processingDir}/text-extract \
  --min-area 100 --min-dimension 16 --dilate-width 50
```

脚本使用**水平膨胀**（`--dilate-width`，默认 50px）桥接同一行内的字符间隙，不执行投影分割和形态学开运算，输出完整文字行/词组的 bbox。脚本输出 `text-extract/components.json`，每个检测到的连通区域是一个文字标签，包含精确的 `root_box`（2048 坐标）、`original_image_box`（原图坐标）和 `relative_parent_box`。裁剪图 `text-extract/component_{NNN}.png` 仅用于参考，不用于下游。脚本同时输出 `diagnostic_annotated.png`（带编号框的标注图，仅用于 OpenCV 诊断参考，5.5.2 使用原图重新标注）。

> ⚠️ **膨胀仅用于行内分组，不参与几何**：`root_box` / `original_image_box` / `relative_parent_box` 是该连通域在**原始（未膨胀）mask**上前景像素的紧致包围盒。若误用膨胀后的连通域 stats，每个文字框会整体加宽约 `dilate_width - 1` 像素、x 左移约 `dilate_width / 2` 像素，且该偏差会按百分比忠实传播到最终布局。回归测试：`scripts/tests/test_extract_text_labels.py`。

### 5.5-fallback 文字标签检测（仅当 text_only_2048.png 不存在）

当 Step 3c 被跳过、`text_only_2048.png` 完全不存在时（异常路径——Stage 01 HARD-GATE 禁止跳过 3c，走到本节说明跳过已经过 `ask_user` 确认或流程异常），以本节**替代 §5.5.1 的几何来源**，再按 §5.5.3 创建节点、§5.5.4 挂载。几何与内容两步分离，与递归层 `references/recursive-extraction.md` §8.4.5 同款、共用同一工具 `detect_text_on_original.py`。⛔ **禁止在 fallback 路径用 `analyze_multimedia` 估测百分比几何**——AI 不产几何（系统性漂移实测记录见 §8.4.5）。

**第 1 步 · OpenCV 几何检测**（对原图内容区做色通道文字检测）：

```text
{PYTHON} scripts/detect_text_on_original.py \
  {processingDir}/source_square.png \
  {processingDir}/transform.json \
  {processingDir}/text-extract-fallback \
  --mode root
```

- 可选参数：`--known-rows N`（已知行数先验，如菜单 8 行）、`--channels bright|gold|dark|auto`（文字色通道；`auto` 按行结构评分择优，也可先用已知良好行验证命中后指定）、`--min-area`/`--min-dimension`/`--dilate-width`（默认对齐 §5.5.1）。
- 脚本对 `source_square.png` 的 `original_content_rect` 区域（原始参考图本体）检测：行带锚定 + 等距行模式校验（行中心偏离拟合等距线 > 半行高禁止采信）+ 水平膨胀仅行内桥接（膨胀仅分组，几何取**原始 mask 紧致 bbox**，与 §5.5.1 同一契约）+ 排除关键色像素 + 实心块（图标列）修剪。输出 `text-extract-fallback/components.json`——schema 与 §5.5.1 的 `text-extract/components.json` 一致（`root_box`/`original_image_box`/`relative_parent_box` 已完成坐标换算），并附审计信息（各通道评分、拒收行及原因、修剪记录）。
- **零检出是合法结论**：3c 被跳过通常因为面板无文字——`components` 为空时记录审计后结束本节，不进入第 2 步。
- 确实无法像素检出的文字（极低对比度、强干扰），脚本如实记录「未检出」——**跳过，禁止以 AI 估测填补**，由 04.5A 画板用户补框（画板本就是布局权威，用户拖框优于接受错误几何）。

**第 2 步 · AI 内容读取（只读不测）**：对第 1 步检出框在**原图内容区裁剪**（`source_square.png` 按 `original_content_rect` 裁剪）上画编号标注图（⚠️ 纯红 `#FF0000` 高对比），**复用 §5.5.2 读文 prompt 逐字调用**——一次 `analyze_multimedia` 批量读 `display_text`/`is_dynamic`/`static_part`/`dynamic_part`/字体样式；编号框已画在图上，AI 只读不测。

**空框过滤**：AI 返回 `text` 为空字符串的检出框视为假阳性（图标/装饰被色通道误检），**不创建节点**。

**第 3 步 · 创建节点与挂载**：按 §5.5.3 创建 `text-label-{NNN}` 节点——几何字段从 `text-extract-fallback/components.json` 复制（非 §5.5.1 的 `text-extract/`），语义字段从第 2 步读文结果填充；§5.5.4 挂载照常执行。

### 5.5.2 读取 display_text、动态判定与字体样式

OpenCV 在 `text_only_2048.png` 上提取的几何坐标通过 `original_image_box` 映射到原图坐标系。用 `original_image_box` 坐标在**原图**（`source_square.png`）上画编号框生成标注图，一次 `analyze_multimedia` 同时读取文字内容、动态判定和字体样式。⚠️ **编号框颜色必须与底图内容高对比**——推荐使用纯红 `#FF0000`。

> **为什么用原图而非 text_only 标注图**：`text_only_2048.png` 是 img2img 产出，文字颜色与原图不一致，无法提取真实字体颜色/大小。原图保留了完整的文字像素和颜色，AI 可以直接从原图视觉读取文字内容和样式，同时在原图语境中更容易判断文字是否为动态值（如"金币: 1000"旁有金币图标）。

prompt（逐字使用，`{scale}` 为原图到设计分辨率的缩放比例，如原图宽1024→设计宽1920则 scale=1.875）：

```
这是原始 UI 参考图，编号框标注了检测到的文字标签。请逐个读出每个编号框内可见的实际文字内容，并判断该文字是固定标签还是运行时会变化的动态值。同时分析每个文字标签的字体样式。

动态值判定：
- 动态值特征：纯数字（如"1000"、"99"）、数字与前缀/后缀组合（如"Lv.5"、"×3"）、可能变化的名称（如玩家名）
- 固定标签特征：功能描述文字（如"主音量"、"全屏"、"分辨率"）

返回JSON数组：[{"index": <编号>, "text": "<完整文字内容>", "is_dynamic": true/false, "static_part": "<固定部分，如'金币:'或'Lv.'，无则空字符串>", "dynamic_part": "<动态部分，如'1000'或'5'，无则空字符串>", "font_color": "<主色调HEX>", "font_size": <按原图像素高度估算后乘以{scale}换算为设计分辨率像素，整数>, "font_weight": "<normal/bold/thin>", "font_style": "<italic/outline/shadow，无则空字符串>"}]。如果框内没有可读文字，text 设为空字符串，is_dynamic 设为 false。
```

> ⚠️ prompt 中不传坐标列表——编号框已画在原图上，AI 直接从图中视觉识别。编号框确保 AI 不会遗漏文字标签。
### 5.5.3 创建 uss_only 节点

对每个检测到的文字标签，在 `semanticRecords.json` 中追加条目：

```json
{
  "node_id": "text-label-{NNN}",
  "component_name": "{从文字内容推导的简洁中文名}",
  "component_type": "label",
  "display_text": "{从 5.5.2 读取的完整文字内容，保留原始文字作为默认值}",
  "text_type": "label",
  "art_text_style": "",
  "font_color": "{从 5.5.2 提取的 HEX 颜色，如 #D4A84B}",
  "font_size": {从 5.5.2 提取的像素大小，整数},
  "font_weight": "{从 5.5.2 提取：normal/bold/thin}",
  "font_style": "{从 5.5.2 提取，如 italic/outline/shadow，无则空字符串}",
  "visual_description": "standalone text label",
  "kind": "leaf",
  "has_dynamic_content": {从 5.5.2 判定：is_dynamic=true 则 true，否则 false},
  "dynamic_content": {
    "type": "{mixed=有 static_part 和 dynamic_part；number=纯动态数字无 static_part；variable_text=非数字型可变文本}",
    "static_parts": "{从 5.5.2 的 static_part 读取，如'金币:'，无则空字符串}",
    "dynamic_parts": "{从 5.5.2 的 dynamic_part 读取，如'1000'，无则空字符串}"
  },
  "functional_type": null,
  "style_group_hint": "",
  "contamination_description": "",
  "confidence": 0.95,
  "state": "valid",
  "quality": null,
  "reasoning": "Standalone text label detected via OpenCV on text_only image. Font style extracted from source_square.png. Dynamic content detection from text analysis. Will be rendered as UXML Label.",
  "root_box": "{从 text-extract/components.json 的 root_box 复制}",
  "original_image_box": "{从 text-extract/components.json 的 original_image_box 复制}",
  "relative_parent_box": "{从 text-extract/components.json 的 relative_parent_box 复制}",
  "render_mode": "text",
  "source": "uss_only"
}
```

> ⚠️ **`dynamic_content` 字段仅 `has_dynamic_content=true` 时存在**，`has_dynamic_content=false` 的节点不包含此字段。`display_text` 始终保留原始识别到的完整文字作为默认值（如"金币: 1000"），下游 ui-panel-builder 根据 `dynamic_content.static_parts`/`dynamic_parts` 知道哪部分需要绑定游戏状态。

同时为每个文字标签创建 `nodes/text-label-{NNN}/` 目录和 `semantic.json`（内容同上，不含几何字段）。

> **几何数据**：文字标签节点**现在具有 OpenCV 检测的 `root_box`**（从 `text_only_2048.png` 提取），在建树时（Step 6.3）不再跳过几何合并。`render_mode` 设为 `text`，`source` 设为 `uss_only`。下游 `ui-panel-builder` 根据 `component_type=label`、`display_text` 和 `root_box`/`relative_parent_box` 在 UXML 中创建 Label 元素并精确定位。

### 5.5.4 父子关系挂载（不做去重、不删除节点）

文字标签**保留为独立节点，不回填、不删除**。文字的位置、内容、字体样式、动态判定等全部信息始终在 text-label 节点上，不复制到组件节点。

**在 Step 6 语义分类完成后执行父子挂载**：计算文字标签 `root_box` 被某个 OpenCV 组件 `root_box` **包含的比例**（文字框与组件框交集面积 / 文字框面积）。若 > 80%，将文字标签挂为该组件的子节点：

1. 设置 `parent_id` 为该组件的 `node_id`
2. 计算 `relative_parent_box`（文字框相对父组件框的百分比）——这是下游 UXML/USS 布局唯一坐标来源：
   - `x_pct = (text_box.x - parent_box.x) / parent_box.width * 100`
   - `y_pct = (text_box.y - parent_box.y) / parent_box.height * 100`
   - `w_pct = text_box.width / parent_box.width * 100`
   - `h_pct = text_box.height / parent_box.height * 100`
   - 并将同值写入顶层四标量 `relative_parent_x_pct` / `relative_parent_y_pct` / `relative_parent_w_pct` / `relative_parent_h_pct`（04.5A 画板导入优先读取四标量，缺失或陈旧会静默回退旧几何）
3. 文字标签的 `root_box` 保留不变（全局坐标，用于递归层级重新评估归属）
4. `depth` 设为父组件 `depth + 1`

若文字标签不被任何组件包含 > 80%，保持 `parent_id=null`、`depth=0`，作为面板级独立标签。面板级标签的 `relative_parent_box` 恒与 `root_box` 同值——percent/percentage 的 base 是 **content_rect**（非方形画布尺寸），顶层四标量同步写入该值；后续任何阶段（Stage 04 改挂/重算）都不得为无父标签改用其他 base 或「顺带批量重算」。

> ⚠️ **不回填**：组件节点的 `display_text`/`font_color`/`font_size` 等字段不从文字标签复制。组件的 `display_text` 由 Step 6 语义分类产出，文字标签的字体信息由 5.5.2 产出，各管各的。下游 builder 通过组件树层级关系读取子 text-label 节点的字体信息。
>
> ⚠️ **递归层级（Stage 04）的重新挂载——必须先投影回同一坐标空间再比较**：Stage 04 拆分出的子组件 `root_box` 位于**子项隔离图**坐标系，与 text-label 的 `root_box`（位于其当前父组件 `root_box` 所在坐标系）不同空间，禁止直接比较。重新评估步骤：
> 1. **投影**：对当前父组件 P 的每个子组件 C，计算其在 P.root_box 空间的投影框 `C_box = P.root_box ⊕ C.relative_parent_box.percent`（`x = P.root_box.x + P.root_box.width × pct.x / 100`，y/width/height 同理）。
> 2. **比较**：计算 text-label `root_box` 被 `C_box` 包含的比例（交集面积 / 文字框面积），多个 > 80% 时取包含比例最高者。
> 3. **改挂**：`parent_id` 更新为该子组件，`depth` 更新为子组件 `depth + 1`；`relative_parent_box` 按 text-label `root_box` 相对**该子组件投影框**的百分比重新计算（上方公式），并**同步覆盖顶层四标量 `relative_parent_x/y/w/h_pct`**。
> 4. 无子组件达标时保持挂载不变。
> text-label 的 `root_box` 永远保留在其检测时所在坐标系，不随改挂变换。这样文字始终跟随最精确的父级。

## 6. 语义分类，不做定位

对 Step 6 的语义分类，禁止逐个调用 `analyze_multimedia`，改为批量标注图方式（Step 6.2 的验证不受此限制）：
1. 以**原图**为底图（根级用 `source_square.png`；递归层用父组件的原图裁剪），在所有子组件位置画编号框。⚠️ **编号框颜色必须与底图内容高对比**——不要使用绿色或原图中已有的主色调，推荐使用纯红 `#FF0000`。⚠️ **坐标系必须匹配底图**：`source_square.png` 是原始分辨率（如 2560×2560），而 `root_box` 是 2048 空间坐标——根级标注**必须使用 `components.json` 中的 `original_image_box` 字段**（已映射到原图坐标），禁止直接用 `root_box`（会因 scale=0.8 导致框偏左上、标错内容）。递归层标注底图是父组件的原图裁剪，子组件 `root_box` 已在该裁剪的坐标系内，可直接使用。⚠️ **禁止使用隔离图（`ui_only_2048.png` / `isolation_input.png`）做标注底图**——隔离图在 Step 3 已移除所有普通文字（"Remove all text, numbers"），在其上做语义分类会导致 `display_text` 丢失（按钮标签、设置项名称等全部变成空字符串）。原图保留了完整的文字内容，AI 可以正确读取每个组件的 `display_text`。
2. 对标注图调用一次 `analyze_multimedia`，prompt 要求 AI 对每个编号返回语义 JSON。prompt 模板如下（逐字使用）：

<details>
<summary>classify_extracted_component prompt（逐字使用，禁止修改）</summary>

分析一张已标注编号框的UI组件概览图。编号框标注了每个已提取的UI组件。你的职责仅是补全语义，绝不检测、推断、修正或返回位置与尺寸。

⚠️ **全局原则——忽略普通文本**：在评估组件的所有语义字段（component_type、has_dynamic_content、functional_type、kind、visual_description、style_group_hint 等）时，必须将普通文本（text_type=label）视为不存在。普通文字在最终 UI 中由 UXML Label 渲染，不生成独立图片、不构成视觉内容、不构成交互功能。唯一例外：`display_text` 字段需如实读取文字内容。

OpenCV几何是唯一权威；不得新增、删除、合并、拆分或重排组件。框内关键色背景不是组件的一部分。

对每个编号框输出一个JSON对象，组成数组。不要Markdown、解释或额外字段。注意JSON类型：空文字为`""`，无值用JSON `null`（非字符串`"null"`），布尔用`true`/`false`。字段如下：

- `index`: 编号（从1开始）
- `state`: `valid`|`invalid`|`skip`。无法可靠分类时用`invalid`。
- `name`: 简洁中文语义名。同一图中视觉外观相同、功能相同的并列同类组件（如多个相同的服务器条目按钮）必须按阅读位置（从左到右、从上到下）追加序号：`服务器条目按钮1`…`服务器条目按钮8`；不同组件禁止同名
- `component_type`: `button`|`icon`|`background`|`panel`|`bar`|`title`|`badge`|`hud_element`|`display`|`toggle`|`label`|`container`|`game_content_area`|`custom`
- `display_text`: 图上实际可见文字；无文字为空字符串；多行用`\n`
- `text_type`: `none`(无文字)|`label`(普通可本地化文字，由UXML Label渲染)|`art_text`(不可替代的艺术字)
- `art_text_style`: 仅`art_text`填简短视觉关键词，其他为空字符串
- `visual_description`: 简短英文描述形状、主色调、图标内容
- `style_group_hint`: 为**视觉外观相同的同类组件**返回相同标识值（如`btn_blue_pill`）。利用全局视角判断哪些组件形状、配色、装饰风格完全一致（仅文字不同也可归同组）。无同组伙伴为空字符串。命名建议：`{type简写}_{主色}_{形状特征}`
- `kind`: `leaf`|`composite`
  - `leaf`: 按钮、图标、标签、进度条等原子交互单元，即使内部有图标+文字组合也不拆分。**角色立绘、场景插画、宠物头像、游戏内容展示区等视觉内容也判为 `leaf`**——即使其中包含多个视觉元素（角色+宠物+武器），只要这些元素不是可独立交互的 UI 控件，整体就是单一视觉单元。⚠️ **art_text 标题（标题文字烘焙在图片中，含装饰图案如花朵、云朵底板）整体判为 `leaf`**——装饰图案是视觉内容的一部分，不独立交互。⚠️ **带装饰的按钮（按钮底图含叶子、图标等装饰元素，文字可能烘焙在图片中）整体判为 `leaf`**——装饰元素是按钮视觉的一部分，不是独立可交互子 UI
  - `composite`: 含多个**非文字的独立可交互**子UI的容器（弹窗、Tab栏、多按钮面板等）。判断标准：子项是否可以独立于父组件存在/交互。⚠️ 判断前先忽略所有 text_type=label 的元素（视为不存在），若剩余非文字元素仅一个则 kind=leaf。⚠️ **角色立绘中的宠物、武器、装饰物不是"可交互子UI"**——它们是视觉内容的一部分，不独立交互，因此角色展示区整体判为 `leaf`。⚠️ **标题装饰图案、按钮装饰元素不是"可交互子UI"**——它们是视觉内容的一部分，标题和按钮整体判为 `leaf`
- `has_dynamic_content`: 是否含运行时会变化的数值或文本
- `functional_type`: `slider`|`toggle`|`text_field`|`progress_bar`|`dropdown`|`other`|`null`
  - 仅当组件是**需要拆分为多个视觉层的可交互功能控件**时填写。判定依据：组件是否包含**可移动的交互部件**（slider的handle、toggle的knob）或**可变化的填充层**（progress_bar的fill）或**展开态部件**（dropdown的expanded_panel_bg）。只有需要拆分为2个以上视觉层时才标记。text_field虽无移动部件但有明确输入功能且需独立bg层。普通按钮（即使有交互功能）整图可直接使用，标`null`
  - `slider`: 含track+handle的拖拽控件
  - `toggle`: 含track+knob的拨动开关
  - `text_field`: 输入框，需独立bg层
  - `progress_bar`: 含track+fill，无拖拽部件
  - `dropdown`: 下拉选择框，含收起态bg+arrow+展开态expanded_panel_bg。判定信号：按钮带下拉箭头（▼/v）、display_text 含选项值、"Dropdown button" 等
  - `other`: 其他需拆分多视觉层的控件（非dropdown）
  - `null`: 普通按钮、图标、背景、装饰等，整图可直接使用。⚠️ 带下拉箭头（▼/v）的按钮**不是普通按钮**，必须标 `dropdown`
- `confidence`: 0.0-1.0。低于流程阈值时必须停止并讨论
- `reason`: 简短分类理由

skip判定——满足任一条件返回`state=skip`，`confidence=0.99`：
- 截图水印、UID编号、平台标识等非游戏UI功能性元素
- 极小碎片（单个文字字符、微小装饰像素块），不构成独立UI组件
- 裁剪噪声产生的伪元素（边缘残留、抗锯齿碎片）
- **残缺的UI部件、不完整的边框**。⚠️ 如果组件与标注图中另一个编号框重叠或相邻，不要因此判定为残缺——后续 §3b 会评估相邻组件是否属于同一逻辑视觉单元并合并。将此类组件标为 `valid`，让 §3b 决定是否合并
- **纯文字组件**：组件仅含普通文本（text_type=label）、无功能控件（functional_type=null）。文字将由 UXML Label 渲染，无需生成图片

禁止输出任何几何信息（rect/bbox/x/y/width/height/坐标/位置/布局/边界框）。禁止修正OpenCV裁剪范围、父子关系或组件数量。无法可靠判断语义时返回`state=invalid`、降低`confidence`并说明原因。

</details>

AI 仅返回：组件名（视觉相同的并列同类组件须按位置编号唯一命名，如`服务器条目按钮1`）、类型、显示文字、文字类型、艺术字风格、视觉描述（`visual_description`，简短英文描述形状/主色调/图标内容）、叶子/复合判断、底板/动态内容、**功能性控件类型**（`functional_type`：`slider`/`toggle`/`text_field`/`progress_bar`/`dropdown`/`other`/`null`，标记组件是否为需要拆分多个视觉层的可交互功能控件。普通按钮、图标等整图可直接使用的标 `null`；带下拉箭头的按钮标 `dropdown`）、**样式组提示**（`style_group_hint`：对同一张标注图中视觉风格相同的组件返回相同的组名，如 `button_style_1`；风格不同的留空）、置信度和理由。**严禁返回坐标/位置/尺寸/百分比/布局建议**。

> `quality` 字段不由本次语义分类返回，由下方的两步验证独立确定后写入 `semantic.json`。

<HARD-GATE>**`quality` 字段必须由两步验证流程产出，禁止手动赋值。** orchestrator 不得在 `semantic_records.json` 或 `semantic.json` 中直接硬编码 `quality="clean"`。语义分类阶段产出的 `semantic_records.json` **不得包含 `quality` 字段**——该字段在下方的两步验证完成后才写入 `semantic.json`。若 `semantic_records.json` 中已存在 `quality` 字段，两步验证流程会覆盖它。</HARD-GATE>

**`quality` 裁剪质量判定规则**（两步验证）：

**第1步：IoU 脚本初筛**（基于 `components.json` 的 bbox 几何计算，高召回、确定性）：
- 执行部署型工具一行调用 `Create.GameFramework.TJStyleRefTree.Triage("{components.json}");`（包 `Editor/UIBuildTools`，经 `execute_csharp_script`），工具对同一父级下所有组件两两计算 bounding box 的 IoU，阈值 >5% 标记为 `suspect`。
- 工具输出 `suspect` 组件列表和重叠 pair 详情。
- `clean` 的组件跳过第2步，直接用 `clean`。

> ⚠️ bbox 几何重叠 ≠ 裁剪图实际污染——重叠区域可能全是关键色背景（会被忽略）。`suspect` 仅表示"需要进一步确认"，不是最终判定。

**第2步：裁剪图确认**（对 `suspect` 组件，逐个用 `analyze_multimedia` 分析实际裁剪图，高精度）：
- 对每个 `suspect` 组件，用 `analyze_multimedia` 分析其 `root-extract/component_XXX.png`（或递归层的对应裁剪图）。
- 验证 prompt 和 `quality=contaminated` 处理规则（leaf vs composite）**读** `references/quality-and-semantics.md` §1-§2（prompt 逐字使用）。

最终 `quality` 字段写入 `semantic.json`，下游 ui-image-generator 读取。

**`kind` 判定规则**：`composite`（含可交互子 UI）vs `leaf`（单一视觉单元）。`state=skip` 安全守卫：
> 1. 组件裁剪图（`root-extract/component_{NNN}.png`）的非关键色像素面积 < 总面积的 5%
> 2. 组件在原图标注中不包含任何可识别的视觉内容（文字、图标、插图、装饰图案）

**碎片识别与过滤**：语义分类后、建树前执行，基于 `adjacent_pairs` + 语义结果识别 OpenCV 拆碎的元素。完整过滤条件、误判场景和 `kind` 详细判定**读** `references/quality-and-semantics.md` §3-§4。

将语义 JSON 保存到 `nodes/{nodeId}/semantic.json`。`state=skip` 不进入下游流程。低置信度/`state=invalid`/语义冲突时 `ask_user` 讨论。

### 6.2 自动化 quality 验证

> 本步骤是 Step 6 两步验证的自动化执行入口。`quality` 字段缺失是下游全部数据被污染的根因——Stage 03 预分组、Stage 05 图片生成、Stage 06 输出、ui-image-generator 路径判定都依赖 `quality` 值。跳过此步骤等同于让所有下游决策基于假数据。

执行部署型工具一行调用 `Create.GameFramework.TJStyleRefTree.RunQualityVerification("{processingDir}/root-extract/components.json", "{processingDir}/semanticRecords.json", "{processingDir}");`（包 `Editor/UIBuildTools`，经 `execute_csharp_script`；返回 false 等价其退出码 1）

工具自动完成：
1. 程序集内直调 `Triage` 计算 IoU，标记 suspect（第 1 步）
2. 输出 `quality_verification.json`，列出需要 `analyze_multimedia` 确认的 suspect 组件及其裁剪图路径
3. 非 suspect 节点自动标记 `quality=clean`（IoU 初筛通过）

脚本输出后，orchestrator 必须对 `quality_verification.json` 中每个 `suspects_pending_verification` 条目：
1. 读取 `crop_image_path` 指向的裁剪图
2. 调用 `analyze_multimedia` 分析该裁剪图，使用 `references/quality-and-semantics.md` §1 的两步式 prompt
3. 将 AI 返回的 `clean`/`contaminated` 写入 `nodes/{nodeId}/semantic.json` 的 `quality` 字段
4. 若 `contaminated`，同时写入 `contamination_description`（AI 描述的污染内容）
5. 重新执行 `TJStyleRefTree.RunQualityVerification` 确认 `pending_verification` 为 0

**完成判据**：`quality_verification.json` 的 `summary.pending_verification` 为 0。非 0 时禁止进入 Step 6.3。

> **quality 回写**：quality 验证完成后，将所有 quality 结果回写到 `semanticRecords.json` 对应记录的 `quality` 字段——包括 `auto_clean_nodes` 的 `clean`、analyze_multimedia 确认的 `contaminated`。`TJStyleRefTree.BuildTree` 从 `semanticRecords.json` 读取 quality，不从个别 `semantic.json` 读取。

### 6.3 构建初始组件树

将 Step 5 的 OpenCV 几何数据与 Step 6 的语义分类结果合并为初始组件树。**这是 Stage 03/04 的前置依赖——没有组件树，预分组和递归都无法操作。**

```csharp
Create.GameFramework.TJStyleRefTree.BuildTree("{processingDir}/root-extract/components.json",
    "{processingDir}/component-tree.json", "{semanticRecords.json}");
```

`semanticRecords.json` 是一个 JSON 数组，每个元素含 `node_id` 和 Step 6 产出的语义字段。合并后每个节点包含：`node_id`、`parent_id`（根级为 null）、`root_id`、`depth`（根级为 0）、`root_box`、`relative_parent_box`、`image_hash`、`semantic`（含 `component_name`、`component_type`、`display_text`、`text_type`、`art_text_style`、`visual_description`、`kind`、`has_dynamic_content`、`functional_type`、`style_group_hint`、`contamination_description`、`confidence`、`state`、`quality`、`reasoning`）、`provenance`。

> 此初始树只有 depth=0 的根级节点。Stage 04 递归过程中通过 `TJStyleRefTree.MergeChildTree`（原 `component_tree.py --merge-tree`）逐层追加子节点。

> **panel_provenance 写入**：构建组件树时，从 `pipeline-state.json` 的 `panel_provenance.step3_isolation` 字段读取 Stage 01 产出的 `task_id` 和 `generator_id`，写入 `component-tree.json` 顶层的 `panel_provenance.step3_isolation` 字段。

> **文字标签节点合并**：Step 5.5 创建的 `text-label-{NNN}` 节点不在 `root-extract/components.json` 中（它们来自 `text-extract/components.json`），`TJStyleRefTree.BuildTree` 不会自动包含它们。建树工具执行完成后，orchestrator 需手动将文字标签节点追加到 `component-tree.json` 的 `nodes` 数组中。每个文字标签节点包含：`node_id`、`parent_id`（初始为 null，5.5.4 挂载后更新）、`root_id`（自身）、`depth`（初始为 0，挂载后更新）、`root_box`（从 Step 5.5.1 OpenCV 提取，**始终保留**）、`original_image_box`、`relative_parent_box`（初始与 root_box 相同，5.5.4 挂载后更新为相对父组件的百分比）、`image_hash`（空字符串，`uss_only` 节点无图片）、`semantic`（含 `component_type=label`、`display_text`、`text_type=label`、`render_mode=text`、`source=uss_only` 等）。`TJStyleRefTree.ValidateTree` 已对 `uss_only` 节点跳过 `image_hash` 和 `root_box` 必填检查。

> **字段完整性检查**：建树后，检查 `semantic` 子对象中是否存在 Step 6 产出的全部字段（特别是 `style_group_hint`、`functional_type`、`contamination_description`）。若 `semantic_records.json` 中有但树中缺失，报错列出缺失的字段名和节点 ID。**执行** `{PYTHON} scripts/preflight_check.py field_completeness {processingDir}/component-tree.json --records {semanticRecords.json}`。

> ⚠️ **quality 字段存在性检查**：建树后，执行 `{PYTHON} scripts/preflight_check.py quality_existence {processingDir}/component-tree.json`，遍历所有 `state=valid` 节点检查 `semantic.quality` 非空（`clean`/`contaminated`）。缺失时回退到 Step 6.2 执行 `TJStyleRefTree.RunQualityVerification`。禁止手动硬编码 `quality` 值。

> ⚠️ **文字标签挂载自检**：文字标签节点挂载（5.5.4 / 8.4.5）后，执行 `{PYTHON} scripts/preflight_check.py label_geometry {processingDir}/component-tree.json`，校验文字标签节点 `root_box`/`relative_parent_box` 的挂载自洽性；无可挂载标签的图标型面板通过为合法结果。

## 完成后

1. 确认 `preflight_check.py field_completeness` 通过
2. 确认 `preflight_check.py quality_existence` 通过
3. 确认 `preflight_check.py label_geometry` 通过
4. 更新 `pipeline-state.json`：
   - `02-root-extraction` → `completed`
   - `03-pre-grouping` → `in_progress`
5. orchestrator 派发下一阶段 subagent（`stages/03-pre-grouping/SKILL.md`）
