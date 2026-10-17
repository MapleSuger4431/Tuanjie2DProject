# 共享契约（所有阶段共用）

本文件定义跨阶段共享的硬约束、目录布局、数据规则和执行规则。每个阶段的 `stages/XX-xxx/SKILL.md` 均依赖本文件——执行任何阶段前应先读取本文件。

本文及各 stage/reference 中的 `{PYTHON}` 沿用顶层 `unity-ui-builder/SKILL.md` 指向的 `references/python-contract.md` 跨平台解析合同。

## 术语约定

"图集"指 `generation_atlas.py` 的装箱/拆图流程（atlas）；ui-image-generator 中"sprite sheet"指 Path C 小型元素图集。两者含义不同。

## 核心硬约束

- **OpenCV 像素框是位置、尺寸和裁剪范围的唯一真相**。AI 只能判定语义，绝不能输出或修正坐标。
- 所有用户确认、异常讨论和停止点必须用 `ask_user`。**图片上传是唯一例外**——禁止用 `ask_user` 收图，必须在聊天中直接输出文字提示用户上传图片，然后**立即停止回合**（不发任何工具调用），等待用户在下一轮消息中提供图片文件。
- **prompt 模板与重试机制**：所有 \`\`\` 代码块内的 prompt 均为**默认模板**，首次必须逐字使用。生成后必须验证：
  - **验证方式**：该环节有三态门禁脚本时**先跑脚本**（见下方「三态门禁」节）——`pass` 免视觉直接过，`review`/`fail` 按门禁块动作执行；无门禁脚本的环节用 `analyze_multimedia` 分析生成图是否符合预期，或通过下游步骤的后验对比间接判定。
  - **重试触发**：验证发现 prompt 导致的问题（元素丢失、残留、跨槽串图等）时，调整 prompt 重新提交。每轮保留前一次结果为 `*_r{N}.png`，最多 3 轮重试。
  - **最优结果选取**：取验证得分最高的结果。
  - **无法通过 prompt 解决的问题**（如 img2img 染色、完全丢失）按对应阶段的后验对比中的错误恢复策略处理，不计入 prompt 重试次数。
  - 通过 subagent 委派执行图生图时，**必须将 prompt 写入 `prompt-{step}.txt` 文件后传文件路径**，subagent `read_file` 读取后原样作为 `generate_image` 的 `prompt` 参数，禁止让 subagent 自行转述 prompt。
- **`analyze_multimedia` 失败处理**：API 超时/错误时最多重试 3 次。3 次仍失败则：语义分类失败 → `ask_user` 讨论（后续步骤依赖语义结果）；验证类步骤失败 → 跳过当前验证，标注 `verification_skipped: {reason}`，不阻塞后续流程。
- **禁止自定义脚本替代流程步骤**：禁止在 `Processing/` 或任何目录下创建自定义 Python 脚本来替代、加速或跳过流程中的任何步骤。唯一允许的自定义脚本是：将 `scripts/` 下的只读模板复制到 `Processing/{PanelName}/` 下修改命令行参数后执行——"修改参数"指调整 `--min-area` 等已知参数，不包括编写新的流程逻辑。核心脚本**禁止复制后修改逻辑**。
- **禁止跳过任何步骤或子步骤**：SKILL.md 中描述的每个步骤（包括 3a、3b 等子步骤）都是必须执行的。不存在任何隐含的"task constraint"允许跳过步骤。若认为某步骤不必要，必须用 `ask_user` 向用户确认后才能调整，禁止 subagent 自行决定跳过。跳过步骤会导致下游全部基于错误的中间产物构建。
- **img2img 溯源记录**：每次 img2img 调用完成后，必须将返回的 `task_id`、`generator_id` 和步骤编号写入产出节点的 `img2img_provenance` 字段（节点级）或 `pipeline-state.json` 的 `panel_provenance` 字段（面板级 Step 3 隔离）。缺失 `task_id` 的节点在 preflight 中会被判定为"未经过 img2img"，阻止输出。`task_id` 必须来自 AI 生成 API 的实际返回值，禁止伪造。
- 生成图片必须委派subagent。

## 三态门禁（validator 统一规范）

所有"先脚本、视觉兜底"的验证脚本（`validate_shell` / `validate_background` / `validate_text_only` / `validate_ui_only_reference` / `validate_output_page` / `find_missing_components` / `segment_control_layers` 等）遵循同一契约。SKILL.md 中的门禁块按本节模板书写，执行者（LLM agent）逐字照办，禁止自行发挥分支动作。

**输出（verdict JSON）**：stdout 打印一份 JSON，审计副本落盘 processingDir 对应文件（`--json-output`/`--audit` 参数）。必含字段：

- `verdict`：`pass` | `review` | `fail`
- `metrics`：本次计算的全部指标数值
- `thresholds`：本次实际使用的阈值（含参数名与值——阈值一律经 CLI/JSON 参数传入，标定是数据不是代码）
- `reasons[]`：非 pass 时的具体原因（供视觉仲裁 prompt 引用 / 执行报告留痕）
- `evidence_files[]`：标注图/掩码诊断图路径
- `audit_file`：审计 JSON 落盘路径

**退出码**：`0` = pass；`2` = review；`1` = fail 或脚本错误（`reasons` 注明 `error`）。禁止其他退出码语义。

**三分支动作**（由各环节 SKILL.md 门禁块写明，此处为缺省语义）：

- **pass**：免视觉直接过。**初始阈值保守**——只有证据压倒性清晰才 pass（宁可降级 review 多花一次视觉，也不放走缺陷）。所有 pass 判定必须落盘审计，供事后回放与阈值校准（各 panel 的审计 JSON 即影子日志）。
- **review**：视觉仲裁——该环节现有判定表 / prompt **原样保留**为仲裁依据，禁止改写语义；脚本 `reasons` 作为补充证据一并提供。
- **fail**：按该环节的 FAIL 分级——仅"机械确定"失败（证据确凿、无误伤面）免视觉直接 retry；其余 fail 也走视觉仲裁。哪个失败类别属于"机械确定"由各门禁块明示。

**SKILL.md 门禁块统一模板**（逐字套用）：

```text
{PYTHON} scripts/<validator>.py <args...>
```
- 退出码 0（pass）→ <该环节通过动作；免视觉>
- 退出码 2（review）→ 对 <哪些对象> 执行 <现有视觉 prompt 引用>（脚本 reasons 作为补充证据）
- 退出码 1（fail）→ <分级动作：机械确定类别 → 直接 retry；其余 → 视觉仲裁>
- 审计：<audit 文件路径>——免视觉判定必须留痕，禁止只看 stdout 不落盘

**阈值校准**：默认值为保守初值。用各 panel processingDir 累积的审计 JSON 回放对比（脚本判定 vs 实际视觉/人工判定），零分歧后可放宽 PASS 带；有分歧先解释、再调阈、再复跑。禁止拍脑袋改阈值。

## 环境与目录

首次使用时执行 `{PYTHON} .codely-cli/extensions/TJUIBuilder/skills/unity-ui-builder/scripts/setup_env.py`（Pillow/OpenCV/numpy），输出 `ENV_READY` 后才能继续。

每张原图的约定目录：

```text
context/games/{GameName}/UIReferences/
├── {ImageName}.{ext}                         # 用户原图，只读保留
└── Processing/{ImageName}/
    ├── pipeline-state.json                   # 阶段流水线状态（见路由 SKILL.md）
    ├── transform.json
    ├── source_square.png                      # 原图补边到正方形（原始分辨率，不缩放）
    ├── ui_only_2048.png                       # 图生图输出 2048x2048（仅用于 OpenCV 几何检测）
    ├── text_only_2048.png                     # 纯文字图 2048x2048（关键色背景 + 仅普通文字像素，供 Step 5.5 OpenCV 提取文字标签几何）
    ├── validation.json
    ├── root-extract/
    │   ├── components.json
    │   └── component_{NNN}.png               # 根级组件裁剪图（仅用于语义分类参考）
    ├── text-extract/
    │   ├── components.json                    # 文字标签 OpenCV 提取结果（含 root_box 几何）
    │   └── component_{NNN}.png               # 文字标签裁剪图（仅用于参考）
    ├── Atlases/
    │   ├── child-isolation/depth-{N}/
    │   │   ├── input-page-{P}.png             # 装箱输入页（关键色背景+节点槽位）
    │   │   ├── atlas-manifest-{P}.json        # 槽位坐标/缩放/manifest
    │   │   ├── output-page-{P}.png             # AI 图生图输出
    │   │   ├── slots/{nodeId}.png             # 拆图后逐槽裁剪图
    │   │   ├── slots/{nodeId}.transform.json  # 逐槽 transform（下游 OpenCV 用）
    │   │   └── retry-{R}/                      # 补图轮次
    │   └── final-generation/{recipe}/
    │       ├── input-page-{P}.png
    │       ├── atlas-manifest-{P}.json
    │       ├── output-page-{P}.png
    │       └── retry-{R}/
    ├── nodes/{nodeId}/
    │   ├── semantic.json                      # 语义分类结果（每个节点一个，包括根级和递归子节点）
    │   ├── isolation_input.png                # 父级区域图（depth=0 复制自 root-extract 连通域 mask 裁剪图；depth≥1 复制自父节点 child-extract 连通域 mask 裁剪图；仅 composite 节点）
    │   ├── child-extract/                     # OpenCV 提取中间产物（仅 composite 节点，不含 nodes/ 子目录）
    │   │   ├── transform.json                 # 子级专用 transform（source_size 为裁剪图尺寸）
    │   │   ├── components.json                # OpenCV 子组件 manifest
    │   │   └── component_{NNN}.png           # 子组件裁剪图（仅用于语义分类参考）
    │   ├── original_crop.png                 # 从关键色隔离图裁剪的组件裁剪图（Step 9 用）
    │   ├── final_transparent.png             # 图集拆出的透明 PNG（Step 9 产出）
    │   └── gen_params.json
    ├── diagnostics/
    └── component-tree.json                   # 校正关闭时=Stage 04 检测输出；校正开启时=Stage 04.5B resolver 输出（最终树）
    └── component-tree.original.json          # Stage 04 检测快照（不可变；校正开启时 Stage 04.5 与 resolver 的唯一几何输入）
    └── component-tree.resolved.json          # Stage 04.5B resolver 快照（与活树字节一致，供 --check 重建校验；仅校正开启时存在）
```

**节点 ID 命名规则**：
- **根级节点**（depth=0）：`component-{NNN}`（如 `component-001`），由 `extract_reference_components.py` 自动编号。
- **递归子节点**（depth>=1）：`{父nodeId}-{NNN}`（如 `component-003-007`），合并到组件树时按父 ID + 子序号生成。
- 节点 ID 全局唯一。所有 `nodes/{nodeId}/` 路径、组件树 `node_id`、图集 `atlas-items.json` 的 `node_id` 均使用此命名。

`Processing/` 是可追溯中间产物，不能覆盖原图。每个节点记录输入/输出图、OpenCV manifest、语义 JSON、图生图 task ID、hash、置信度、失败原因和父子关系。

## 核心数据规则

### 布局校正后的权威关系

参考图布局校正开启时，三份 JSON 的职责固定如下：

- `component-tree.original.json`：Stage 04 检测快照，**不可变**。画板（Stage 04.5A）和 resolver（Stage 04.5B）只读取它，绝不写入。`referenceAnalysisHash` 始终对该文件的字节计算。
- `LayoutIntent/layout_intent.json`：用户布局权威，决定最终节点集合、几何、层级、顺序、名称与组件类型。
- `component-tree.json`（校正开启）：Stage 04.5B resolver 输出的最终布局树。布局字段（节点集合、`parent_id`、`root_box`、`relative_parent_box`、`source_crop_box`、`change_kind`、`layout_region_id`、`source_reference_node_id`、`deleted_source_node_ids` 等）由 resolver 一次写入；Stage 05 之后追加**生成字段**（`source`、`crop_path`、`render_mode`、`quality`、`img2img_provenance`、`generation_atlas` 以及注入的 `background` 节点）。`component-tree.resolved.json` 是 resolver 写入的**快照**，字节与活树在写入时刻完全一致，供 `--check` 重建校验。
- `LayoutIntent/resolved-ui-tree.json`：Stage 04.5B 的审计中间产物，禁止手工编辑，下游不直接消费。

> 旧规则"禁止回写 component tree"已废止。新规则：**original tree 不可修改；最终 `component-tree.json` 可由 resolver 生成、由 Stage 05 追加生成字段**。`--check` 通过"布局投影"比对保证 Stage 05 只改生成字段、不改布局字段——一旦布局字段被手工修改即可发现。

下游不得同时读取 original tree 与 layout intent 后自行判断冲突。校正开启时必须先运行 Stage 04.5B resolver 写出最终 `component-tree.json`，Stage 05/06 直接消费该最终树；解析失败时停止，不得回退到 original tree，也不得把 resolved-ui-tree.json 作为额外 AI 输入。校正关闭时 `component-tree.json` 仍是 Stage 04 的检测输出，Stage 04.5 被跳过。

### 关键色补边和方形画布

1. 补边画布使用关键色像素（由 `prepare_reference_square.py` 自动分析原图选择反差最大的颜色，优先洋红色），填充区域为关键色 RGB 值（alpha=255）。
2. 原图始终放方形画布左上角：横图仅向下补关键色、竖图仅向右补关键色、方图不补边。禁止向左或向上补边。
3. 补边画布不缩放，保持原始分辨率。`transform.json` 记录原图尺寸、方形画布尺寸、补边、内容矩形（2048 映射）、`background_mode: "key_color"`、`key_color`（含 `name`、`rgb`、`hsv`、`overlap_ratio`）。

对原图 `W0 x H0`，令 `S=max(W0,H0)`：`contentWidth2048 = round(2048 * W0 / S)`，`contentHeight2048 = round(2048 * H0 / S)`。根级百分比相对非填充区域计算。

### 组件树

以 OpenCV 组件树为原始检测几何权威。根百分比仅用于诊断；**子节点 UXML/USS 只能使用相对直接父级百分比**。布局校正开启时，最终 `component-tree.json` 中 `root_box` / `relative_parent_box` 由用户确认布局决定，是最终布局几何；`source_crop_box` / `source_relative_parent_box` 来自 original tree 的 `original_image_box` / `relative_parent_box`，是原参考图裁剪几何。Stage 05 裁剪时只能用 `source_crop_box` 和节点目录中的连通域裁剪图，**不得用校正后的 `root_box` 回参考图重新裁剪**。

> **布局字段 vs 生成字段所有权**（校正开启时）：`node_id`、`layout_region_id`、`source_reference_node_id`、`change_kind`、`origin`、`parent_id`、`root_id`、`depth`、`root_box`、`relative_parent_box`、`source_crop_box`、`source_relative_parent_box`、`layout_description` 以及 `semantic.component_name` / `semantic.component_type` 归 resolver（布局权威）；`source`、`crop_path`、`render_mode`、`semantic.state`、`semantic.kind`、`quality`、`img2img_provenance`、`generation_atlas` 以及注入的 `background` 节点归 Stage 05（生成期）。`--check` 只比对前者，preflight 只校验后者。

每个节点应提供下列合同字段，供 `UI参考图分析.md`、`UI设计文档.md`、`image_requirements.json` 与 builder 下游传递：

| 字段 | 含义 |
|------|------|
| `node_id` | 稳定节点标识 |
| `parent_component` / `parent_uxml_element` | 父组件语义名 / 对应父 UXML 元素；根节点为空 |
| `depth` | 根为 0 的树深度 |
| `render_mode` | `direct` / `shell_background` / `text`；仅描述布局角色，不新增运行时 API |
| `shell_path` | 从面板根到节点的容器路径 |
| `root_box` | OpenCV 提取的根级像素框；含 `percent`（0..100，权威）和 `percentage`（0..1，legacy） |
| `relative_parent_box` | OpenCV 提取的相对直接父级像素框；含 `percent`（0..100，权威）和 `percentage`（0..1，legacy） |
| `relative_parent_x_pct` / `relative_parent_y_pct` / `relative_parent_w_pct` / `relative_parent_h_pct` | 相对直接父级的百分比（0..100），是子布局唯一坐标来源 |
| `root_x_pct` / `root_y_pct` / `root_w_pct` / `root_h_pct` | 相对参考图根的诊断坐标（0..100），仅供 overlay/debug，不得用于子布局 |
| `generation_atlas` | 图集诊断引用：`{page_index, slot_rect, content_rect, recipe, retry_round}`。仅用于追溯，不参与运行时布局 |
| `semantic.quality` | 裁剪质量（来自 Stage 02 两步验证）：`clean` / `contaminated`。`direct_use` + `contaminated`（leaf）时 ui-image-generator 将 `source` 改为 `ai_generate`，由 Path D img2img 移除污染内容；`contaminated`（composite）仅作诊断标记，不触发 fallback。缺省视为 `clean` |
| `semantic.contamination_description` | 仅 `quality=contaminated` 时存在。AI 在 Stage 02 确认污染时产出的描述文本（位置 + 视觉特征），供下游 ui-image-generator 构建定制化移除 prompt |
| `semantic.functional_type` | 功能性控件类型标记。值域：`slider`/`toggle`/`dropdown`/`text_field`/`progress_bar`/`other`/`null`。仅标记需要拆分多个视觉层的控件。下游 ui-image-generator 据此将透明 PNG 独立生成各子部件；ui-panel-builder 据此映射到 Brick。仅标记，不改变图片处理流程 |
| `semantic.style_group_hint` | AI 在语义分类中基于全局标注图视角返回的样式分组建议。值相同的组件视觉外观一致，供样式组去重使用。为空字符串表示无同组伙伴 |
| `semantic.font_color` | 仅 text-label 节点（`source=uss_only`）。文字主色调 HEX（如 `#D4A84B`），从原图 `source_square.png` 提取（非 `text_only_2048.png`）。下游 USS `color` 属性的直接来源。文字标签是独立节点，组件节点不携带此字段——下游 builder 通过组件树层级关系读取子 text-label 节点的字体信息 |
| `semantic.font_size` | 仅 text-label 节点（`source=uss_only`）。基于设计分辨率估算的像素字号（整数）。下游 USS `font-size` 属性的直接来源 |
| `semantic.font_weight` | 仅 text-label 节点（`source=uss_only`）。`normal`/`bold`/`thin`。下游 USS `-unity-font-style` 属性的直接来源 |
| `semantic.font_style` | 仅 text-label 节点（`source=uss_only`）。特殊样式如 `italic`/`outline`/`shadow`，无则空字符串 |
| `img2img_provenance` | img2img 产出溯源：`{task_id, generator_id, step}`。`task_id` 为 AI 生成 API 返回的非空字符串。仅经过 img2img 的节点需要此字段：background 节点、composite shell 节点。`source=direct_use` 的 leaf 节点从隔离图裁剪（非独立 img2img），不需要此字段。缺失 `task_id` 的节点在 preflight 中会被判定为"未经过 img2img"，阻止输出 |

**state 与 source 映射规则**：

| state | 附加条件 | source | 说明 |
|-------|---------|--------|------|
| `skip` | — | `skip_generation` | 噪声/非 UI 元素，不生成图片 |
| `valid` | 全屏根容器（面积占比 >=90% 的 composite） | `skip_generation` | 有效但无需生成，背景已由 background 节点覆盖 |
| `valid` | text-label 节点（从 text_only 图像检测） | `uss_only` | 纯文字，UXML Label 渲染 |
| `valid` | `functional_type` 非空（slider/toggle/dropdown/text_field/progress_bar/other） | `ai_generate` | 需拆分多个视觉层的功能控件；从关键色隔离图裁剪产出透明 PNG，`crop_path` 指向该透明 PNG，下游以透明 PNG 为 img2img 输入独立生成各子部件 |
| `valid` | `quality=contaminated`（leaf） | `ai_generate` | 从关键色隔离图裁剪产出透明 PNG（含组件像素 + 邻居残片），`crop_path` 指向 `final_transparent.png`，下游以透明 PNG 为 img2img 移除残片 |
| `valid` | 其他 | `direct_use` 或 `ai_generate` | 按处理配方决定（关键色裁剪可直接使用 → `direct_use`；fallback 图生图 → `ai_generate`） |

> ⚠️ **`source=direct_use` 与 `functional_type` 非空互斥**：`direct_use` 表示"整图可直接部署"，`functional_type` 非空表示"需独立生成子部件后分别部署"。

缺少树字段的旧 `UI参考图分析.md` 可继续按原有根百分比消费。

### 面板级溯源（panel_provenance）

组件树 JSON 顶层增加 `panel_provenance` 字段，记录面板级 img2img 调用的溯源信息：

```json
{
  "nodes": [...],
  "panel_provenance": {
    "step3_isolation": {
      "text_removal": {
        "task_id": "<非空字符串，MCP generate_image 返回>",
        "generator_id": "seedream_pro"
      },
      "background_removal": {
        "task_id": "<非空字符串，MCP generate_image 返回>",
        "generator_id": "seedream_pro"
      },
      "text_extraction": {
        "task_id": "<非空字符串，MCP generate_image 返回>",
        "generator_id": "seedream_pro"
      }
    }
  }
}
```

Stage 01 隔离 img2img 完成后将三个子步骤（3a 文字移除、3b 背景移除、3c 纯文字提取）各自的 `task_id` 和 `generator_id` 写入 `pipeline-state.json` 的 `panel_provenance.step3_isolation` 对应子字段。Stage 02 构建组件树时从此字段读取并写入 `component-tree.json` 顶层的 `panel_provenance.step3_isolation`。preflight 检查 `text_removal` 和 `background_removal` 子字段非空——缺失意味着隔离被跳过，整个组件树的几何数据来源不可信。`text_extraction` 子字段缺失时，Stage 02 Step 5.5 仍优先使用 OpenCV 对 `text_only_2048.png` 提取文字标签（只要文件存在）；仅当文件本身不存在时才降级为 Stage 02 §5.5-fallback 的原图 OpenCV 两步检测（`detect_text_on_original.py`，几何仍来自像素检测）。

### 图生图执行规则

图生图步骤在 subagent 内执行（适用于所有阶段的图生图步骤）。生成通过 TJGenerators MCP 的 `generate_image` 工具（`provider: "seedream_pro"`，携带 `confirm_cost: true`）完成；完整工作流（上传/提交/等待/收取/下载/错误/恢复）以 `modules/shared/references/mcp-image-generation.md` 为权威源：

1. **prompt 写入文件传路径**：orchestrator 将完整 prompt 写入 `prompt-{step}.txt` 文件，subagent `read_file` 读取后**原样**作为 `generate_image` 的 `prompt` 参数（≤1024 字符）。禁止 subagent 自行转述或改写 prompt。

2. **图生图输入上传**：img2img 的输入图（`source_square.png`、`text_removed_2048.png`、`input-page-{P}.png`、节点透明 PNG 等）先按 `mcp-image-generation.md` §2 做**内容指纹副本**并 `file_upload`，取得 CDN `file_url` 后作为 `image_urls` 传入。⚠️ 同名文件被重试覆盖后内容已变，必须按新 hash 重新上传，禁止复用旧 `file_url`。

3. **批次提交任务**：逐个提交（在途 < 3）；提交流程/等待/重试细节以 `modules/shared/references/mcp-image-generation.md` §4 为准。

4. **等待与收取**：每提交 1 个任务，按 `mcp-image-generation.md` §5.1 派发 1 个后台 waiter task（执行该任务的 `poll_command_powershell` → `check_task` 恰好一次）；若执行环境不支持后台等待，按 §5.3 前台串行执行 poll 命令。

   | 事件 | 处理 |
   |------|------|
   | `status=completed` | 用 `output_url` 下载到目标文件名（`curl.exe -sS -L -o <目标路径> <output_url>`），校验文件存在且非零字节 |
   | `status=failed` | 检查 error，按重试规则处理（重新提交计入 prompt 重试次数） |
   | waiter 异常/超时 | `check_task` 一次确认终态；确认 `failed` 前禁止重新提交（任务可能仍在执行中，重提导致重复生成） |

5. **溯源记录**：将返回的 `task_id`、`generator_id`（值固定 `"seedream_pro"`）和步骤编号写入产出节点的 `img2img_provenance` 字段（节点级）或 `pipeline-state.json` 的 `panel_provenance` 字段（面板级 Step 3 隔离）。详见核心硬约束·img2img 溯源记录。

6. **禁止直接搬运 img2img RGB 输出**：`is_segmentation: false` 的 img2img 输出是 RGB 图（无 alpha 通道），关键色区域是实色像素而非透明。禁止把下载结果直接重命名/搬运为 `final_transparent.png`。必须经 `generation_atlas.py split --clean` 或 `make_key_color_transparent.py` 做关键色→透明转换。preflight 会检查 `final_transparent.png` 的 alpha 通道和透明像素占比。
