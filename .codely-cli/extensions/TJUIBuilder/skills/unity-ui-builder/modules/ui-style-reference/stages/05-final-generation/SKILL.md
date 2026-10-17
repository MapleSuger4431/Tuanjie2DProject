# Stage 05: 最终图片批量生成

Steps 8.5-9：背景提取 → 叶子关键色裁剪 + 复合 shell 图生图。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `component-tree.json`（完全展开，所有 composite 标记 `recursion_done`）、`source_square.png`（原图补边，语义分类标注底图）、`UIReferences/{PanelName}.{ext}`（原图，Step 8.5 背景提取用）、`root-extract/component_{NNN}.png`（depth=0 连通域 mask 裁剪图）、`nodes/{parentNodeId}/child-extract/component_{NNN}.png`（depth≥1 连通域 mask 裁剪图）、`nodes/{nodeId}/isolation_input.png`（Stage 04 复制的连通域 mask 裁剪图，composite shell 输入） |
| 输出 | 所有 `nodes/{nodeId}/final_transparent.png`（shell 节点为 `shell_transparent.png`）、`nodes/background/final_transparent.png` |
| 前置门禁 | `pipeline-state.json` 中 `04-recursive-extraction` = `completed`；`04.5-layout-refinement` = `completed` 或 `skipped`；所有 composite 已标记 `recursion_done` 或全部为 `kind=leaf` |
| 完成判据 | 所有 `direct_use`/`ai_generate` 节点产物存在（shell 节点为 `shell_transparent.png`，其余为 `final_transparent.png`）且有 alpha 通道；§9a 路由一致性校验通过 |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」 |

> **共享契约**：图生图执行规则、溯源记录、目录布局、核心硬约束见 `shared/contracts.md`。

> **前置检查**：进入 Stage 05 前确认 `component-tree.json` 中所有 `kind=composite` 节点已标记 `recursion_done` 或全部为 `kind=leaf`。否则返回 Stage 04 继续递归。

> **校正树读取规则**（`component-tree.json` 含顶层 `layout_resolution` 块时，即校正开启）：
> - `root_box` / `relative_parent_box` 是**最终布局几何**（画布空间）；`source_crop_box` / `source_relative_parent_box` 是**原参考图裁剪几何**（来自 detected tree）。
> - 裁剪时只能用 `source_reference_node_id`、原节点目录（`nodes/{nodeId}/`、`root-extract/`、`nodes/{parentNodeId}/child-extract/`）和 `source_crop_box`。**严禁用校正后的 `root_box` 回参考图重新裁剪**——校正后的几何空间与参考图不同，回裁会得到错位裁剪。
> - `deleted_source_node_ids` 中的节点不生成最终图片（其节点目录可能仍在磁盘，但 Stage 05 不读取、不产出）。
> - `origin=layout_added` 节点：`uss_only`（Label/纯布局容器）跳过图片生成；`ai_generate`+`text2img_fallback` 走文生图回退（由下游 ui-image-generator 处理，Stage 05 不产出 `final_transparent.png`）。
> - `reuse_from:X` 节点：resolver 已把代表被删的悬空引用改写为独立生成（`crop_path=""`、`source=ai_generate`），Stage 05 按正常独立生成节点处理；仍指向存活代表的 `reuse_from` 节点继续复用代表图片、不独立生成。
> - 面积占比判定（≥90% 全屏根容器不生成 shell）用**最终 `root_box` 相对画布**。
> - Stage 05 在最终树中注入 `background` 节点（`node_id=background`）时，`--check` 已豁免 background 节点；background 不要求 `source_crop_box`、`source_reference_node_id` 或 `recursion_done`。

## 8.5 背景提取

> 在 Stage 04 完成后执行。背景图不参与 OpenCV 轮廓检测——它从原图直接生成，不是从 `ui_only_2048` 提取。图生图步骤在 subagent 内执行（**读** `shared/contracts.md` 图生图执行规则）。

从原图提取全屏背景。使用**原图**（非 `source_square.png` 补边图）作为图生图输入，按设计分辨率宽高比选 API 预设尺寸（16:9 -> `2848x1600`、9:16 -> `1600x2848`），`is_segmentation: false`（背景图为全屏不透明图，非透明隔离）。

> ⚠️ **必须使用原图而非 `source_square.png`**：`source_square.png` 含关键色补边，以它为输入会导致背景图中残留关键色区域。原图的宽高比已与 API 预设尺寸一致，无需补边。

prompt（模板，首次逐字使用）：

```text
移除所有UI元素、文字、图标和按钮。保留背景图案、环境和角色插画。
```

生成后用 `inspect_image_alpha.py` 检查尺寸；随后执行**背景纯度三态门禁**（门禁契约见 `shared/contracts.md`「三态门禁」节）：

```text
{PYTHON} scripts/preflight_check.py validate_background {背景图路径} \
  --audit {processingDir}/background-validation.json
```
- 退出码 0（pass：无分散小元素嫌疑——脚本已排除大块连通域）→ ✅ 免视觉，直接使用，标记 `source=direct_use`
- 退出码 2（review：分散小连通域数量/面积超标，或边缘密度过高——花纹/散点装饰背景会假阳性，非机械确定，**禁止直接 retry**）→ 视觉仲裁：用 `analyze_multimedia` 分析是否纯背景（脚本 `reasons` 作补充证据）。确认有残留 → 以原图为图生图输入重试（加强移除 prompt，如追加 `完全移除所有UI元素、按钮、文字、图标`），最多重试 2 轮；仍失败才退化为文生图（标记 `source=ai_generate`、`crop_path=text2img_fallback`）
- 退出码 1（fail：图片不可读等错误）→ 修复后重跑
- ⚠️ 背景**合法包含角色立绘**（见下方说明）——禁止以"有大块高对比内容"为由仲裁为残留
- 审计：`background-validation.json` 必须落盘，免视觉判定留痕

缩放到精确设计分辨率（禁止裁剪）。保存为 `nodes/background/final_transparent.png`，标记 `render_mode=shell_background`、`component_type=background`。在组件树中新增根级节点 `node_id=background`，`root_box` 覆盖整个内容区，`parent_id=null`。

> img2img 会自动去除叠加在背景上的 UI 元素，只保留环境背景和角色立绘。角色立绘在 Stage 01 隔离时被移除（不参与 OpenCV 组件提取），在背景提取时从原图保留——最终背景图包含角色立绘，作为 UI 底层视觉。

> **溯源记录**：在 background 节点的 `img2img_provenance` 字段中记录 `{task_id, generator_id: "seedream_pro", step: "8.5"}`（见 `shared/contracts.md` 核心硬约束·img2img 溯源记录）。

## 9. 最终图片批量生成

> 📋 **步骤契约**
>
> | 项 | 内容 |
> |----|------|
> | leaf 输入（depth=0）| `root-extract/component_{NNN}.png`（Stage 02 连通域 mask 裁剪产出，直接复制，禁止从 `ui_only_2048.png` 做 bbox 裁剪） |
> | leaf 输入（depth≥1）| `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（上一层连通域 mask 裁剪产出，直接复制）或 `nodes/{nodeId}/original_crop.png` |
> | leaf 默认路径 | 连通域 mask 裁剪图 → 关键色→透明转换 → `final_transparent.png`（**不需要 AI**，裁剪图已含关键色背景） |
> | composite shell 路径 | `nodes/{nodeId}/isolation_input.png`（Stage 04 复制的连通域 mask 裁剪图）→ `generation_atlas.py pack` → img2img（`is_segmentation=false` + 关键色） → `generation_atlas.py split` |
> | 输出 | `nodes/{nodeId}/final_transparent.png`（透明 PNG，供下游 ui-image-generator 使用） |
> | 硬规则 | ① leaf 和 composite shell 的输入都从**上一层 `extract_reference_components.py` 的连通域 mask 裁剪图**复制（depth=0 从 `root-extract/`，depth≥1 从 `nodes/{parentNodeId}/child-extract/`），禁止从 `ui_only_2048.png` 或 slot 图做 bbox 裁剪 ② composite shell 使用 `is_segmentation=false` + 关键色 ③ 装箱用 `generation_atlas.py pack`，禁止手写替代 ④ 产出后必须跑 §9.5 透明度验证 |

<HARD-GATE>执行 Step 9 前**必须完整读取** `references/final-generation.md`（9.1-9.7）——该文档定义了处理配方路由、图集输入准备、拆图清理参数等**流程必读信息**，跳过会导致图片路径错误、配方选错。</HARD-GATE>

两条产出路径依赖关系不同，须分开处理：

### 9a. 叶节点处理

- **depth=0 叶子**：可立即执行（不依赖 Stage 04），裁剪来源是 `root-extract/component_{NNN}.png`（Step 5 输出）。
- **depth≥1 叶子**：须等 Stage 04 发现该节点后执行，裁剪来源是 `nodes/{parentNodeId}/child-extract/component_{NNN}.png`（Step 8.4 输出）。

默认路径（无需 AI）：AI 隔离图（`is_segmentation=false`）输出含关键色背景，直接从关键色隔离图裁剪组件 → 关键色→透明转换 → 透明 PNG。质量不达标时 fallback 到 AI 图生图。

> **`quality=contaminated` 的叶子节点**：从关键色隔离图裁剪产出透明 PNG（`final_transparent.png`）。标记 `source=ai_generate`（仍需下游移除残片）、`crop_path` 指向 `final_transparent.png`（透明 PNG，含组件像素 + 邻居残片）、`quality=contaminated`。裁剪后邻居残片保留在透明 PNG 中，由下游 ui-image-generator 以透明 PNG 为图生图输入（Path D），prompt 基于 `semantic.json` 中的 `contamination_description` 字段构建定制化移除指令（如 `移除不属于本组件的{contamination_description}。`）。生成后由 ui-image-generator 验证并按其重试流程处理。

> **`functional_type` 非空的叶子节点**：从关键色隔离图裁剪产出透明 PNG（`final_transparent.png`）。标记 `source=ai_generate`（仍需下游生成子部件）、`crop_path` 指向 `final_transparent.png`（透明 PNG）。裁剪后得到一张可用透明 PNG，但下游 ui-panel-builder 需要将 track/handle 等子部件分别赋给 Unity 控件的内部子元素（如 `.unity-slider__tracker`、`.unity-slider__dragger`），一张图片无法拆开赋值。由下游 ui-image-generator 以透明 PNG 为参考（Path C sprite sheet 为默认，Path D 独立生成为回退），先 `analyze_multimedia` 分析视觉层组成，再将子部件合并为 sprite sheet 生成或独立生成。不可见子部件（如 dropdown 展开态面板）走文生图单独生成。**`source=direct_use` 与 `functional_type` 非空是互斥的**——`direct_use` 表示"整图可直接部署"，而 functional_type 非空表示"需生成子部件后分别部署"，两者不可能同时成立。Step 6 语义分类产出 `functional_type` 后，Step 9a 据此将 source 设为 `ai_generate` 而非 `direct_use`。

> ⚠️ **`functional_type` 在 composite 节点上的行为（同样触发 functional_control）**：
> `functional_control` 配方适用于**所有 `functional_type` 非空的节点**，不论 `kind=leaf` 还是 `kind=composite`。当 `kind=composite` 节点有 `functional_type` 非空时（如 Settings 面板的 slider 行、toggle 行）：
> - `functional_type` 字段同时作为**下游元数据**（ui-panel-builder 据此在 UXML 中创建 Unity 原生 Slider/Toggle 控件）和**路径判定信号**（`functional_type` 非 `—` → Path C/D 生成子部件）
> - **触发** `functional_control` 配方，`source` 设为 `ai_generate`（不保留 `direct_use`）
> - composite 的 shell 背景仍走 `composite_shell`（Step 9b）路径移除内部子 UI，但 `source` 标记为 `ai_generate` 而非 `direct_use`——因为整图含 track+knob 合在一起，需要下游 ui-image-generator 走 Path C/D 生成子部件后分别部署
> - 若 Stage 04 递归分解发现了 leaf 子节点（如独立的 track 子部件），子节点按各自配方处理；composite 自身的 `final_transparent.png` 仍走 Path C/D 供下游生成

配方路由（`functional_control`/`leaf_clean`/`leaf_plate_no_label`/`art_text`/uss_only）**读** `references/final-generation.md` §9.1。

> ⚠️ `art_text`（艺术字）节点**不得标记 `uss_only`**——艺术字无法用 UXML Label 渲染，必须保留像素。

**Step 9a 路由一致性校验**：生成所有透明 PNG 后，检查组件树中每个节点的 `source` 与产出物是否一致：
1. `source=ai_generate` 且 `functional_type` 非空的节点（不论 leaf/composite）：确认 `final_transparent.png` **已存在**（从关键色隔离图裁剪产出的透明 PNG，供下游 img2img 使用）。⚠️ `source=direct_use` 与 `functional_type` 非空互斥——若发现 composite 节点有 `functional_type` 非空但 `source=direct_use`，必须修正为 `ai_generate`。
2. `source=ai_generate` 且 `quality=contaminated` 的 leaf 节点：确认 `final_transparent.png` **已存在**（从关键色隔离图裁剪产出的透明 PNG，含组件像素 + 邻居残片，供下游 img2img 移除残片使用）。
3. `source=direct_use` 的节点：确认 `final_transparent.png` 已存在。
4. `crop_path=reuse_from:*` 的节点：确认**没有**独立生成 `final_transparent.png`（应引用代表组件的图片）。

### 9b. 复合节点 Shell 生成（严禁跳过，且必须按步骤执行）

> 须等 Stage 04 全部完成（所有 composite 标记 `recursion_done`）。

面积占比 < 90% 的 composite 走 `composite_shell` 配方：AI 图生图移除内部子 UI（按钮、图标、文字），仅保留底板/边框/阴影/装饰。

面积占比 ≥ 90% 的全屏根容器不生成 shell（`source=skip_generation`），背景已由 Step 8.5 的 `background` 节点覆盖。

> ⚠️ **Stage 03 的 `reuse_structure_from` composite 不生成独立 shell**：它们的 `crop_path` 已设为 `reuse_from:{代表组件名}`（`source` 保持 `ai_generate`），指向代表节点的 `final_transparent.png`。装箱时排除这些节点，代表节点的 shell 产出后直接复用。下游 USS 用 `stretch-to-fill` + 九宫格 `spriteBorder` 适配尺寸差异。

**Shell 输出验证**：拆图完成后、写回组件树前，对每张 composite shell 产出执行验证。

**第 1 步 · 脚本门禁（三态，自动，禁止跳过）**：

```text
{PYTHON} scripts/preflight_check.py validate_shell {shell_image_path} \
  --transform {processingDir}/transform.json \
  --audit {processingDir}/shell-validation-{nodeId}.json
```

脚本对每张 shell 图计算两个方向的机械证据（契约见 `shared/contracts.md`「三态门禁」节）：
- **清理不足方向**：边框内部颜色方差——纯色底板方差极低（~2-3），有结构性装饰（行分隔线、单元格轮廓、纹理图案）的底板方差天然偏高（数百到数千），真正的残留交互元素方差通常 > 1000（pass 档初始保守值 50，用审计回放标定后放宽）；
- **过度清理方向**：**包围孔洞比**——底板被关键色吞掉后经 `--clean` 变透明，会在 shell 内部留下被边框包围的透明孔洞。此方向此前是脚本盲区（"均匀=PASS"），现已机械可测。

- 退出码 0（pass：方差低于 pass 档且包围孔洞比达标）→ ✅ **免视觉**：合格的 shell，直接裁剪写回组件树
- 退出码 2（review：方差超 pass 档——结构性装饰或残留都可能；或孔洞比超标——过度清理嫌疑）→ 执行第 2 步 AI 双向确认
- 退出码 1（fail：图片不可读等错误）→ 修复后重跑
- 审计：`shell-validation-{nodeId}.json` 必须落盘（每张 shell 一个文件，禁止互相覆盖），免视觉判定留痕

**第 2 步 · AI 双向确认（仅 review 时执行，脚本通过则跳过）**：

对 review 的 shell 图用 `analyze_multimedia` 分析，检查两个方向（脚本 `reasons` 作补充证据）：

1. **底板完整性**：面板内部的背景填充（白底/渐变底/纯色底板）是否保留。底板被移除（内部变为关键色/透明）= **过度清理**。
2. **内容清除度**：按钮/滑块/开关/文本框/图标等交互子 UI 是否已移除。子组件残留 = **清理不足**。

prompt（逐字使用）：
```text
这张图应该是一个 UI 面板外壳——只应保留边框、框架、阴影、装饰元素和底板（最底层的填充层）。所有交互子元素（按钮、滑块、开关、文本框、图标、文字）应该已被移除。

⚠️ 重要区分：属于面板底板本身的结构性装饰元素（行分隔线、单元格轮廓、网格线、装饰性边框、纹理图案、装饰花纹）不是交互元素，应当保留。只将真正的交互控件（可点击的按钮、可拖拽的滑块、拨动开关、输入框、图标图片、可读文字标签）标记为残留。

分析这张图并回答：（1）边框内部的底板/填充是否保留？是/否（2）内部是否还有残留的交互 UI 元素（按钮、滑块、开关、文字、图标）？不要将结构性分隔线、单元格轮廓或装饰图案计为残留。是/否，列出它们。返回JSON：{"plate_preserved": true/false, "content_residual": true/false, "residual_description": "..."}。
```

**验证结果判定**（三态门禁；原判定表保留为 review 仲裁依据）：
- 脚本 **pass**（免视觉）→ ✅ **通过**：合格的 shell，裁剪后写回组件树
- 脚本 **review** + AI `plate_preserved=true && content_residual=false` → ✅ **误报通过**：脚本因结构性装饰（行分隔线、单元格轮廓、纹理图案等）导致的高方差误判，AI 确认无残留交互元素，裁剪后写回组件树
- 脚本 **review** + AI `content_residual=true` → ❌ **清理不足**：进入 retry
- AI `plate_preserved=false` → ❌ **过度清理**：底板被移除，shell 不可用，进入 retry（脚本的包围孔洞指标通常同向标记）

**成功的面板立即裁剪并写回**：通过验证的 shell 从拆图输出裁剪到 `nodes/{nodeId}/shell_transparent.png`，标记 `render_mode=shell_background`、`source=direct_use`，不再参与后续 retry 轮次。同时在节点 `img2img_provenance` 字段中记录 `{task_id, generator_id: "seedream_pro", step: "9b"}`——`task_id` 必须来自 MCP `generate_image` 的实际返回值，禁止伪造。

**失败处理（仅对验证失败的面板，最多 2 轮 retry）**：

**retry-1：调整 prompt 精确指定保留/移除**
- 过度清理（底板丢失）→ 追加：`保留背景底板并填充边框内部。只移除交互元素。`
- 清理不足（内容残留）→ 追加：`完全移除所有按钮、滑块、开关、文本框、下拉框、图标和文字标签。`
- 仅将**验证失败的面板**重新装箱（通过的面板不再放入图集），每页一次 img2img → 拆图 → 双向验证

**retry-2：换策略**
- 过度清理 → 保持 `is_segmentation=false`，prompt 追加：`保持面板背景底板完整。只移除交互子元素。`（AI 保留全部内容后仅移除交互元素，移除区域填充关键色）
- 清理不足 → 保持 `is_segmentation=false`，减少每页项数（每页最多 2 项，给 AI 更多空间处理细节）
- 同样仅装失败面板，拆图后重新双向验证

**retry-2 仍失败**：标记 `source=ai_generate`、`crop_path=text2img_fallback`——该 shell 降级为文生图回退，下游 ui-image-generator 以面板的 `visual_description` + 组件树语义信息文生图生成外壳图（无参考图输入，纯 prompt 驱动）。在输出中列出降级面板及失败原因（过度清理/清理不足）。

AI 图生图路径（走图集批处理）：`generation_atlas.py pack` → img2img → `generation_atlas.py split --clean`。所有配方使用 `is_segmentation=false` + 关键色，AI 输出含关键色背景的 RGB 图，`split --clean` 统一将关键色转换为透明 alpha 通道、清理噪声、缩放→透明 PNG。`composite_shell` 节点同样必须经 `split --clean` 处理——`--clean` 会将关键色转换为透明、裁剪透明边距并缩放到 `export_size`，跳过会导致产出保持原始尺寸、含大量关键色边距。

**核心脚本**：
- `generation_atlas.py pack`：将多个组件裁剪图装箱到 2048×2048 关键色画布
- `generation_atlas.py split --clean`：从 AI 输出页拆回独立组件图，关键色→透明转换、清 alpha 噪声、缩放→透明 PNG

所有需要图片的节点按处理配方分组，每组独立装箱、每页一次图生图。全屏背景保持 Step 8.5 独立流程。

> 图集输入准备、拆图清理参数等关键细节**读** `references/final-generation.md`（§9.1-9.7）。

**§9.4 配方 prompt 表**（leaf 节点默认直接裁剪关键色隔离图，仅质量不达标时 fallback 到 AI 图生图；composite shell 必须走 AI 图生图）。完整 prompt、`is_segmentation` 参数和 prompt 调整方向**读** `references/final-generation.md` §9.4。每页 prompt 末尾追加 `每个槽位相互独立。保持每个槽位内的位置和大小。不要跨槽位移动内容。`

**§9.5 透明度验证**：拆图清理后，对每张产出文件执行验证：① alpha 通道存在且透明像素占比 > 0（关键色已在 `--clean` 中转换为 alpha=0）；② 图片尺寸匹配 manifest 中的 `export_size`。任一失败 → 禁止进入写回，回退到失败补图。所有配方（含 `composite_shell`）均使用 `is_segmentation=false` + 关键色，经 `--clean` 转换后须验证 alpha 通道存在且透明像素占比 > 0。

## 完成后

1. 确认所有 `direct_use`/`ai_generate` 节点产物存在（shell 节点为 `shell_transparent.png`，其余为 `final_transparent.png`）
2. 确认 §9a 路由一致性校验通过
3. 确认 background 节点存在
4. 更新 `pipeline-state.json`：
   - `05-final-generation` → `completed`
   - `06-output` → `in_progress`
5. orchestrator 派发下一阶段 subagent（`stages/06-output/SKILL.md`）
