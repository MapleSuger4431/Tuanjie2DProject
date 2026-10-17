---
name: ui-layout-intent-designer
description: 使用 LayoutIntentBoard 工作流，让用户在同一项目中切换并手动设计多个 Unity UI 界面（支持 UGUI 和 UI Toolkit 两种模式），也可用外部结构化组件树生成可编辑初稿。采集框选范围、元素名称、组件类型、父子层级和可选来源引用，执行确认前校验，以 LayoutIntent/layout_intent.json 作为独立、稳定的用户布局输出契约。用于用户选择“画板手动设计”、要求在空白画板规划 HUD/菜单/弹窗、恢复或修改已有布局意图，或其他 skill 需要调用画板采集人工布局时。
---

# UI Layout Intent Designer

将用户在 `LayoutIntentBoard` 中确认的布局作为权威几何输入。优先根据已有需求或调用方提供的结构化组件树生成可编辑草稿，再让用户调整和确认。只采集和标准化布局意图；不编排调用方的后续阶段，也不生成或修改参考图分析、设计文档、图片、UXML、USS 或 Panel 脚本。

## 输入与输出

读取：

- `context/games/{GameName}/UI需求.md`（必需）
- `context/games/{GameName}/UI设计文档.md`（恢复或修改时可选）
- 调用方提供的 `component-tree.json`（参考图布局校正模式可选；作为结构化初稿的唯一几何与层级输入，必须包含稳定的 `nodes[].node_id`、`parent_id` 与结构化坐标；本 skill 不推断其生产者，也不解析 Markdown 补数据）
- 调用方提供的参考图路径（参考图布局校正模式可选，仅作为画板底图）
- `context/games/{GameName}/LayoutIntent/layout_intent.json`（恢复、修改或增量预生成时可选）

所有路线写入：

- `context/games/{GameName}/LayoutIntent/layout_intent.json`

路线 A 同时写入并交付给 `ui-doc-builder`：

- `context/games/{GameName}/UI布局意图.md`

路线 B 不以该 Markdown 作为接口；只有用户明确要求预览时才可选生成。

编辑期间还会按保存检查点维护以下未确认草稿；它们不是下游正式输入：

- `context/games/{GameName}/LayoutIntent/layout_intent.draft.json`
- `context/games/{GameName}/LayoutIntent/drafts/index.json`
- `context/games/{GameName}/LayoutIntent/drafts/version-*.json`（恢复入口展示全部已保存版本，无条数上限）

详细字段见 [references/layout-intent-schema.md](references/layout-intent-schema.md)。确认规则见 [references/layout-validation-rules.md](references/layout-validation-rules.md)。

## 工作流

### 流程路线图

本模块有两条独立输入路线，并在 Step 0 汇合判定。图中 `[A]` 表示画板手动设计/需求预生成，`[B]` 表示参考图布局校正；路线 B 的输入机制是由调用方提供结构化组件树初稿和参考图。未标路线的节点为共同步骤。两条路线共享布局采集，但按调用来源分别回流。

```text
[路线 A：画板手动设计 / 根据需求预生成]          [路线 B：参考图布局校正]
UI需求.md                                         UI需求.md
可选增强：UI设计文档.md                           component-tree.json（调用方提供）
                   │                                           │
                   └─────────────────────┬─────────────────────┘
                                         ▼
                              Step 0 入口守卫与模式判定
                                         │
    ┌────────────────────────────────────┼────────────────────────────────────┐
    │                                    │                                    │
    ▼                                    ▼                                    ▼
已有正式 layout_intent.json          [A] 无正式布局                    [B] 无正式布局且校正开启
    │                                    │                                    │
    ▼                                    ▼                                    ▼
Step 1A 加载/重新编辑              Step 1B 需求预生成              Step 1C 参考图校正初稿
    │                                    │                                    │
    │                                    └────────────────┬───────────────────┘
    │                                                     ▼
    │                                           Step 1D 项目 JSON 共同约束
    │                                                     │
    └────────────────────────────┬────────────────────────┘
                                 ▼
                    Step 1E 统一发送前校验
                                 │
                                 ▼
                          Step 2 打开画板
              （Step 2A EditorWindow / Step 2B 降级）
                                 │
                                 ▼
                      Step 3 用户设计 / 校正
                                 │
                                 ▼
                       Step 4 确认与校验
                                 │
                                 ▼
                     Step 5 生成 UI布局意图.md
                        确认正式 layout_intent.json
                                 │
                                 ▼
                       Step 6 按调用来源回流
              ┌──────────────────┴──────────────────┐
              ▼                                     ▼
   [A] ui-doc-builder Step 1/2          [B] 返回调用方 Stage 04.5
```

若调用方或用户明确要求放弃已有正式布局并从新的参考图结构化输入重建，路线 B 可从 Step 0 显式转入 Step 1C；生成结果必须先作为未确认草稿，不得直接覆盖正式 JSON。

### Step 0：入口守卫与模式判定

1. 确认目标面板已登记在 `UI需求.md`；未登记时停止，回到 `ui-doc-builder` Step 0 追加面板及视觉输入方式。
2. 从需求读取 `gameId`，并为全部目标界面读取 `uiViewId`、`uiViewType` 和设计分辨率。
3. 一个游戏只打开一个 `LayoutIntentProjectData`；每个目标界面对应其中一个 `LayoutIntentCanvasData`。
4. 检查正式 `LayoutIntent/layout_intent.json` 是否存在，并读取本次调用是否明确请求“参考图布局校正”及其 component tree、面板映射和参考图路径。本模块不读取或管理其他 skill 的阶段状态、校正开关或流水线文件。
5. 按以下优先级选择 Step 1 的数据准备分支：
   - 正式 `layout_intent.json` 已存在：默认加载已有正式布局；用户要求修改或恢复时进入重新编辑语义。
   - 正式 JSON 不存在，且调用方明确请求参考图布局校正并提供结构化初稿：从 component tree 准备草稿。
   - 其余情况：根据需求预生成普通布局；若输入只能确定界面列表，则准备完整的最小空画布项目。

正式 `layout_intent.json` 的用户布局优先级最高。即使调用方提供了新的参考图结构化初稿，也不得静默覆盖正式布局；只有用户明确要求放弃已有布局并从参考图重建时，才可改走路线 B，并且先生成未确认草稿而不是直接覆盖正式文件。

### Step 1：准备项目数据

本步骤只产出一份将交给画板的完整项目 JSON。先查找 Unity 包 `cn.tuanjie.create.ui` 的实际源码根目录：优先搜索其 `package.json` 中的 `name` 字段，必要时再检查项目或 extension 的包映射。不要假定包源码物理位于项目根的 `Packages/` 目录，也不要把 extension 的物理存放路径写进 Unity 资源路径。找到后记为 `{uiPackageRoot}`。

读取以下 `Data` 类型核对 JSON 格式的代码真相：

- `{uiPackageRoot}/Runtime/Boards/LayoutIntentBoard/Data/LayoutIntentProjectData.cs`
- `{uiPackageRoot}/Runtime/Boards/LayoutIntentBoard/Data/LayoutIntentCanvasData.cs`
- `{uiPackageRoot}/Runtime/Boards/LayoutIntentBoard/Data/LayoutIntentRegionData.cs`

#### Step 1A：重新编辑已有正式布局

当 `layout_intent.json` 已存在（无论是否已确认）时默认执行本分支；用户要求修改或恢复布局时，以下流程即为重新编辑模式：

1. 读取 `context/games/{GameName}/LayoutIntent/layout_intent.json`，保留包括 `isConfirmed` 在内的原始项目数据，不为了重新编辑而预先改成 `false`。
2. Step 1E 校验通过后，将这份原始项目 JSON 原样交给 Step 2 的 `OpenWithData`；不得用需求推断或新的参考图分析补充、重建或覆盖后再发送。
3. 用户在画板中的首次实际修改由 `LayoutIntentBoardManager.MarkChanged()` 自动将 `isConfirmed` 转回 `false`，agent 不需要手动干预。
4. 用户不修改任何内容直接再次确认时，Step 4 的校验和保存仍正常执行，`isConfirmed` 保持 `true`。
5. 重新确认后按 Step 5 验证正式 `layout_intent.json`，再按 Step 6 的模式路由。

#### Step 1B：根据需求预生成普通布局

1. 已知 `gameId`、全部目标 `uiViewId`，且需求或设计文档描述了界面内容、区域或功能时，允许 AI 推断初始层级、组件类型和近似几何，生成完整的 `ui-layout-intent/v5` 项目 JSON。
2. 输入只能确定界面列表、无法形成有意义的区域草稿时，仍生成包含全部已知画布且 `regions` 为空的最小 `ui-layout-intent/v5` 项目 JSON；始终使用该完整 JSON，不使用另一种空白画板入口。
3. 根据需求填写 `uiViewType`、`backgroundImageDescription`、`overallUiDescription`、`name`、`description` 和 `componentType`；信息未明确时允许 AI 给出合理的可编辑初稿。
4. 预生成坐标必须做垂直居中计算：先算内容块总高度，再以 `(画布高度 - 内容块总高度) / 2` 作为起点 y 值；HUD 类全屏贴边布局不受此约束。
5. 生成结果必须设置 `schema="ui-layout-intent/v5"`、`isConfirmed=false`，然后进入 Step 1E。

#### Step 1C：从参考图结构化组件树准备校正草稿

仅当 Step 0 判定调用方明确请求参考图布局校正并提供结构化初稿时执行。本 skill 不负责决定调用方为什么需要校正，也不读取或修改调用方的流水线状态。

1. 确认调用方声明的每个目标面板都有 `component-tree.json`；不额外要求 `UI参考图分析.md` 或任何调用方阶段文件存在。
2. 使用部署型工具 `Create.GameFramework.TJLayoutIntentDraft.Run(...)`（包 `Editor/UIBuildTools`，经 `execute_csharp_script` 一行调用）从 `component-tree.json` 的结构化组件树（`nodes[]` 数组含 `node_id`、`parent_id`、`root_box`/`relative_parent_box` 坐标和 `semantic` 语义）生成 `ui-layout-intent/v5` 未确认草稿。`referenceAnalysisHash` 对 `component-tree.json` 字节计算。工具输出区域按**父节点优先的深度优先顺序**排列，`background` 类型根区域排在最前（底层先绘制），每个父区域后紧跟其全部子区域。Stage 04.5 时点 Stage 05 尚未执行，预览图按以下顺序回退：已有 `final_transparent.png` → `shell_transparent.png` → `nodes/{nodeId}/original_crop.png` → 节点 `file` 字段指向的检测裁剪图（`root-extract/` / `child-extract/`）→ 空串（纯色区域）；`reuse_from:*` 节点解析为代表节点的预览图。**禁止反向解析 Markdown 推断几何**。标准调用如下，面板名与 `uiViewId` / `uiViewType` 不一致时追加对应映射参数：

   ```csharp
   Create.GameFramework.TJLayoutIntentDraft.Run("<gameDir 绝对或项目相对路径>", gameId: "{gameId}", width: {设计宽度}, height: {设计高度},
       uiViewIds: new Dictionary<string,string> { { "{PanelName}", "{uiViewId}" } },
       uiViewTypes: new Dictionary<string,string> { { "{PanelName}", "{uiViewType}" } });
   ```
   成功输出 `REFERENCE_LAYOUT_READY <path>`；`gameDir` 为空 gameId 时默认取目录名，`gameDir` 默认 `context/games/{GameName}`（工具内基于 `Application.dataPath` 解析项目根，调用方传项目相对路径即可）。
3. 默认输出 `LayoutIntent/layout_intent.draft.json`。文件已存在时不得静默覆盖；改用显式临时路径或让用户选择恢复现有草稿。
4. 原始参考图通过画布级 `referenceImagePath` 以默认 0.35 透明度固定显示在最底层，仅用于位置对照；该层不可交互，不生成区域，也不进入 resolver 或 Stage 05。其上有裁剪图的区域显示 `previewImagePath` 图片，图片随区域移动和缩放；缺图区域回退为纯色。
5. 生成结果必须设置 `schema="ui-layout-intent/v5"`、`isConfirmed=false`，然后进入 Step 1E。

#### Step 1D：项目 JSON 共同约束

新生成和参考图转换的数据遵守：

- 为项目内每个画布和区域生成非空、唯一且稳定的 `id`，并让 `activeCanvasId`、`parentId` 指向这些 ID。
- `canvases` 顺序采用目标界面顺序；`regions` 使用父节点优先的深度优先顺序，同级区域的数组顺序就是区域树中从上到下的显示顺序（数组第一个 = 树顶，最后一个 = 树底）。预生成时应按用户期望的树顺序排列同级区域；垂直按钮组通常按屏幕从上到下排列。
- `rect` 使用画布全局像素坐标；根区域和子区域均使用画布全局坐标，非相对父级偏移。例如父区域为 `{x:1000, y:130, width:840, height:480}`，子区域在父区域内偏移 `(30, 20)`，则子区域应写 `{x:1030, y:150, width:300, height:40}`，不能写 `{x:30, y:20, width:300, height:40}`。
- 所有区域位于画布内，所有子区域完整位于父区域内，最小尺寸为 `20×20`。

这些生成约束不得用于改写 Step 1A 读取的正式项目 JSON；已有正式数据只做 Step 1E 的发送前校验，并由画板在加载时按当前 schema 归一化。

#### Step 1E：统一发送前校验

无论数据来自哪个分支，都根据 [references/layout-intent-schema.md](references/layout-intent-schema.md) 与 [references/layout-validation-rules.md](references/layout-validation-rules.md) 检查：

- JSON 可解析，顶层 `schema` 为 `ui-layout-intent/v5`。
- `UI需求.md` 中的 `gameId` 是权威值，项目 JSON 的 `gameId` 必须与其完全一致；不一致时停止并报告，不能静默改写或发送。
- 全部目标画布存在，`uiViewId` 非空且唯一，`activeCanvasId`、`parentId` 引用有效。
- 画布和区域 ID 非空且唯一，几何位于画布内，子区域完整位于父区域内。

不要把格式错误、引用错误或几何错误当作信息不足而静默退化为空白画板。校验通过后，将这一份完整 JSON 交给 Step 2；不要创建第二种画板初始化数据。

### Step 2：打开画板

`LayoutIntentBoard` 的位置固定为：

- Runtime 源码目录：`{uiPackageRoot}/Runtime/Boards/LayoutIntentBoard`
- EditorWindow 入口：`{uiPackageRoot}/Editor/LayoutIntentBoard/LayoutIntentBoardEditorWindow.cs`
- 命名空间：`Create.Utilities`

#### Step 2A：打开 EditorWindow

不需要打开场景或进入 Play Mode。使用 Step 1 准备并校验通过的完整项目 JSON，通过 `execute_csharp_script` 调用唯一启动入口：

```csharp
Create.Utilities.LayoutIntentBoardEditorWindow.OpenWithData(gameId, json);
```

必须使用 `OpenWithData`，不得调用 `Open` 或自行构造另一份空白数据。每个 `uiViewId` 必须非空且在项目内唯一；画布顺序就是画板顶部的界面标签顺序，打开后仍可通过“+ 新增界面”补充界面。

不要调用、反射探测或依赖 `OpenWithData` 之外的 `LayoutIntentBoardEditorWindow` 成员。

若 `OpenWithData` 调用失败（类型不存在、编译未就绪等），不要停止整个流程，转而执行 Step 2B。

源码存在但类型暂时无法解析时，报告“Unity 编译/程序集尚未就绪”及实际错误；不得据此声称“当前项目不存在 LayoutIntentBoardEditorWindow”或“画板手动设计不可用”。

打开画板后停止当前执行回合，提示用户完成设计并点击“确认布局意图”。等待用户后续输入，不订阅结果事件，不轮询 API 状态。

#### Step 2B：本地启动（EditorWindow 不可用时的降级方案）

当 Step 2A 的 `LayoutIntentBoardEditorWindow` 类型无法解析或调用报错时，回退到 PlayMode 路径。按以下顺序启动：

1. 通过 Unity Editor 打开 `Packages/cn.tuanjie.create.ui/Scenes/LayoutIntentBoard.unity`；不要新建替代场景，也不要修改或保存包内场景。
2. 进入 Play Mode，等待 Unity 完成编译和 Domain Reload。
3. 使用 Step 1 准备并校验通过的完整项目 JSON 调用运行时入口：

```csharp
Create.Utilities.LayoutIntentBoardAPI.OpenWithData(gameId, json);
```

必须使用 `OpenWithData`，不得调用 `Open` 或自行构造另一份空白数据。每个 `uiViewId` 必须非空且在项目内唯一；画布顺序就是画板顶部的界面标签顺序，打开后仍可通过“+ 新增界面”补充界面。

不要调用、反射探测或依赖 `OpenWithData` 之外的 `LayoutIntentBoardAPI` 成员。

若本地启动也失败，按顺序诊断：

1. 检查 Unity Console 是否有编译错误，并等待编译和 Domain Reload 完成。
2. 重新查找 `cn.tuanjie.create.ui` 的实际包根，检查源码目录和 `LayoutIntentBoardAPI.cs` 是否存在。
3. 检查活动场景是否为 `Packages/cn.tuanjie.create.ui/Scenes/LayoutIntentBoard.unity`，Editor 是否已进入 Play Mode。
4. 检查调用是否使用 `Create.Utilities.LayoutIntentBoardAPI.OpenWithData(gameId, json)`，并再次验证完整项目 JSON。

源码存在但类型暂时无法解析时，报告“Unity 编译/程序集尚未就绪”及实际错误；不得据此声称“当前项目不存在 LayoutIntentBoardAPI”或“画板手动设计不可用”。

打开画板后停止当前执行回合，提示用户完成设计并点击“确认布局意图”。等待用户后续输入，不订阅结果事件，不轮询 API 状态。

### Step 3：用户设计

让用户通过画板顶部一行切换不同界面，并在每个画布中绘制、移动、缩放和命名区域。每个有效区域至少填写：

- `name`

画板主体为三栏布局（左侧“界面结构”树 / 中央画布 / 右侧属性面板），三栏常驻同显，无标签页切换：

- 左侧“界面结构”树：按 `parentId` 递归构建的可展开/折叠层级树，子区域作为父节点的真实子节点，提供显隐与选择入口；折叠状态与滚动位置在每次重建后保留，从画布选中折叠节点中的区域时自动展开其祖先并滚动到可见位置。画布本身不作为树根节点显示。左栏支持折叠/恢复与拖动分隔条调宽。
- 中央画布：参考图校正模式下，画布最底层显示低透明度原始参考图，不参与选择、拖动、缩放、层级和最终组件树生成；区域优先显示 `previewImagePath` 对应的裁剪图片，名称、彩色边框和缩放手柄保持在图片上层，图片层不得拦截区域选择、拖动或缩放。
- 右侧属性面板按选择状态切换内容（详见 `docs/IntentBoard右侧属性面板设计.md`）：未选中时显示画布属性，分「基本信息」（当前界面/界面类型/设计尺寸/背景透明度）与「设计说明」（背景说明/UI 说明）两组，字段行统一为固定标签列加弹性控件列，只读值（当前界面、设计尺寸）为无边框文本；单选时分「基本信息」（名称/组件类型/父级区域）、「位置与尺寸」（只读 X/Y/宽/高 四格网格 + px 标记 + 锁定框选范围 + 拖拽调整提示；几何编辑在画布进行）、「意图说明」（编辑区域 `description`，组标题承担字段标签、无单独「描述」标签）三组，锁定位于位置与尺寸组内；多选时标题区为三行（已选 N 个对象 / “仅编辑主选对象” / “主选：名称”，主选名可省略并悬停显示全名）；无可用画布时显示空态「暂无可编辑的画布」。多行说明框按实际折行测量自适应高度（不重建控件），下拉当前值与只读值超长时省略并由 tooltip 提供完整内容。右栏支持折叠/恢复与拖动分隔条调宽（280–440）。
- 背景透明度滑条调节画布参考图的显示不透明度（10%–100%，写入 `canvasData.referenceImageOpacity`，悬停 tooltip 显示当前百分比）。它是纯显示态：不标脏项目、不使已确认的布局失效（区别于背景说明/UI 说明等其他画布属性编辑）。
- 树、画布与属性三处选择状态保持同步：树中选节点联动画布与属性；画布中选区域联动树高亮（含祖先展开）与属性面板。
- 功能区按色区分（`LayoutIntentBoard.uss` 的 `--zone-*` token）：顶栏与状态栏共为深紫灰框架带（`--zone-frame`）；左“界面结构”树为冷蓝（`--zone-tree`，表头更深 `--zone-tree-deep`）；中央画布保持近黑中性以突出区域标注，其顶部工具栏为墨黑（`--zone-canvas-tools`）；右属性面板为黑底中性、不加色调（`--zone-inspector` 对应原面板灰阶，用户决策）；各分区外沿用所属色调边框，侧栏折叠后的窄轨继承所属区色调（`sidebar-rail--left` / `sidebar-rail--right`）。

画布工具（网格 / 智能吸附 / 自适应，位于中央画布列顶部；默认开启网格与智能吸附，可分别关闭）：

- 创建、移动或缩放区域时，吸附到网格、画布或父级的边缘与中心，以及其他区域的边缘与中心；关闭网格后同时停止网格显示与网格吸附；吸附命中时显示参考线；关闭智能吸附后保留自由移动和缩放。
- 使用 Shift 或 Ctrl 点击区域进行多选；普通点击恢复单选。
- 左键单击区域为选中；在已有区域上右键拖拽创建其子区域（子区域全程约束在父区域范围内），在空白处右键拖拽创建根级区域；左键拖拽移动区域；左键/中键在空白处拖拽或 Alt+左键在任意位置拖拽平移画布。

界面管理（顶栏）：

- 已有画布的 `uiViewId` 和设计尺寸只读展示，不允许直接修改；界面标签区支持鼠标滚轮横向滚动。标签条末尾的“+”按钮新增界面（新界面同样追加到末尾），点击后要求输入一个新的唯一 `uiViewId`；每个界面标签右上角的“×”可在确认后删除该界面及其区域，但项目必须至少保留一个界面。
- 顶栏右侧依次为“草稿”开关（默认收起；展开后含草稿版本选择与“保存草稿”“列表刷新”按钮，状态栏同步草稿状态）、「?」操作说明入口、“确认布局意图”按钮（唯一实心高亮主操作、顶栏最右）；“取消”按钮保持隐藏。草稿版本下拉最底层为常驻项「正式版本 · MM-dd HH:mm」（内容=已确认的正式布局文件 layout_intent.json）：该文件存在于本地时显示（与草稿版本数无关，“列表刷新”时重检存在性），选中即把该文件内容恢复到画布并置为未确认状态；文件不存在则不显示。
- 「?」入口为 chromeless 形态（与标签条“+”一致）：悬停约 300ms 或 Tab 聚焦时立即展开说明卡片；离开入口与卡片约 200ms、或 Esc（仅收起已打开的卡片，不影响对话框与层级树拖动的 Esc 行为）时收起；收起后须重新悬停/聚焦才会再展开；顶栏重建、界面切换或弹出业务对话框时收起。
- 说明卡片为静态内容（画布操作、键盘快捷键、层级树操作三组，含“右键拖动创建区域，左键拖动移动区域”强调行），不随选择状态变化，无点击固定展开。
- 窗口关闭由 EditorWindow 未保存修改保护统一接管（右上角“×”、停靠标签关闭、画板内关闭同语义）：已确认且无修改时直接关闭；有未保存修改时弹系统三选项——“保存”校验并写正式 layout_intent.json 后关闭（失败则中止关闭、回到画板），“不保存”放弃修改后关闭（不写正式文件、不保存草稿），“取消”留在画板。
- 从中文下拉列表选择界面类型和组件类型；未知组件可保留“自定义区域”。

层级建立与约束：

- 优先在区域树中拖放建立层级：落在节点中部成为其子级，落在上下边缘调整同级顺序，落在树的空白根区域成为根级；“父级区域”下拉列表保留为替代入口。不要要求用户查看或输入 `parentId`。
- 改变父级时禁止拖入自身或后代；保持整棵子树尺寸，只做使子树进入新父级的最小平移，子树尺寸大于目标父级时拒绝操作。一次拖放及其几何变化作为一个整体撤销。
- 在画布中新框选的区域不根据空间包含关系自动推断父级：空白处框选为根级，在已有区域上框选为其子级。
- 按需填写 `description`，未知时允许留空。

父子关系契约：

- 移动父区域时，全部后代一起移动，并以整棵子树为单位限制在画布内；缩放父区域时，全部后代按父区域变换等比更新位置和尺寸。
- 直接移动或缩放子区域时，不允许越过父区域边界。
- 删除父区域时先提示后代数量，再同时删除整棵子树；撤销时恢复整棵子树及原绘制顺序。复制父区域时复制整棵子树并重建内部 ID 引用。
- 父级隐藏时后代在画布上一起隐藏，但每个后代的本地 `visible` 值保持不变，树中仍显示并标为继承隐藏；父级锁定时后代均不可直接编辑，但每个后代的本地 `locked` 值保持不变，未锁定父级的移动和缩放仍会带动本地锁定的后代。
- 层级编辑后把 `regions` 规范化为父节点优先的深度优先顺序，使每棵子树连续；同级节点在数组中的先后顺序同时决定树顺序与绘制顺序（数组越靠前 = 越先绘制 = 越靠底层）。
- 缺失父级或循环引用的数据在草稿树的根层显示警告，允许拖回根级或有效父级修复；存在此类错误时阻止正式确认。

草稿机制：

- 编辑操作通过 `LayoutIntentBoardManager.MarkChanged()` 标记草稿存在未保存修改，但不按时间或操作次数自动保存。保存检查点仅由草稿开关展开后经“保存草稿”创建；任何关闭路径（窗口“×”或关闭弹框的“保存/不保存”）都不会自动写草稿——关闭保护只针对正式 `layout_intent.json`，草稿是独立的辅助能力，已保存的草稿版本不受“不保存”影响。草稿在目标目录内通过临时文件原子替换：版本文件先写入，版本清单最后提交；中断保存不得覆盖 `layout_intent.json`，也不得让未完整提交的新版本替代清单中的旧版本。
- 草稿版本选择框按“选中即恢复”语义工作：选择版本后直接读取并替换当前画板，不先保存当前修改；读取所选版本期间禁用选择框，防止并发恢复。恢复后项目必须保持 `isConfirmed=false`、清空本次会话的撤销栈，并重新标记为未保存修改；正式 `layout_intent.json` 只有再次点击“确认布局意图”并保存成功后才会更新。

### Step 4：确认与校验

点击顶栏右侧的“确认布局意图”按钮时，画板先弹出二次确认框，提醒用户当前内容将经过校验并保存为供下游使用的正式布局意图；用户在确认框中继续后才调用运行时 `LayoutIntentValidationService`。Error 阻止确认；Warning 保留在结果中但允许确认。

除父级存在、无自引用和无循环外，所有子区域还必须完整位于父区域内。画板交互负责维持该约束，运行时确认校验再次检查，防止旧数据或外部修改绕过。

不得绕过校验直接把 `isConfirmed` 改为 `true`。保存或上传失败时必须恢复为未确认。完整规则见 [references/layout-validation-rules.md](references/layout-validation-rules.md)。

用户后续告知已确认时，读取：

`context/games/{GameName}/LayoutIntent/layout_intent.json`

只有文件存在、JSON 可解析、`isConfirmed` 为 `true`，且 `canvases` 包含全部目标 `uiViewId`，才把本次布局设计视为完成。文件缺失、仍未确认或画布不完整时，说明具体状态并让用户回到画板修正；不要通过其他 API 导出、保存或查询状态。

### Step 5：确认正式布局产物

所有目标面板确认后，重新读取 `LayoutIntent/layout_intent.json` 并执行 Step 4 的完整校验。该 JSON 是用户布局权威，必须无损保留像素几何、父子引用和数组顺序；不得先转换成 Markdown 再交给下游。

随后按路线处理：

- **路线 A**：必须执行部署型工具 `Create.GameFramework.TJLayoutMarkdown.Run("{GameName}")`（包 `Editor/UIBuildTools`，经 `execute_csharp_script` 一行调用），从已确认 JSON 生成 `UI布局意图.md`。该 Markdown 是 `ui-doc-builder` 的可读布局输入，也是路线 A 的完成门禁；JSON 仍是用户布局权威，Markdown 不得反向覆盖它。
- **路线 B**：不要求生成 `UI布局意图.md`，正式 `layout_intent.json` 就是返回给调用方的机器接口；用户明确要求预览时才可选生成 Markdown。

### Step 6：按调用来源回流

- **路线 A：画板手动设计 / 根据需求预生成**：确认 `UI布局意图.md` 已由正式 JSON 成功生成，然后回流 `ui-doc-builder` Step 1/2；`ui-doc-builder` 读取该 Markdown，并在需要精确结构化几何时以正式 JSON 为权威。
- **路线 B：参考图布局校正**：把正式 `layout_intent.json` 的路径、`gameId`、目标 `uiViewId` 列表和 `isConfirmed=true` 返回给调用本 skill 的 `ui-style-reference` Stage 04.5。不得由本 skill 修改 Stage 04.5 的 `pipeline-state.json`、启动 resolver 或进入其他下游；Stage 04.5 收到返回结果后自行恢复。

调用方需要语义增强时可读取 [prompts/enrich-region-semantics.md](prompts/enrich-region-semantics.md) 生成建议，但任何建议都不得回写已确认的原始 JSON。

## 硬约束

- 保留 `LayoutIntent/layout_intent.json` 原文件；它是下游唯一结构化数据真相，任何摘要生成都不得回写用户几何。
- 草稿、草稿版本和版本清单始终是未确认编辑状态，不得被下游当作正式输入。
- 不把同一项目的画布重新拆成多个原始 JSON；正式 layout intent 始终是结构化权威，路线 A 的 Markdown 只是其确定性可读投影。
- 根级布局使用画布百分比，子级布局使用相对父级百分比。
- `id`、`parentId` 和画布尺寸不作为用户手填项；已有画布的 `uiViewId` 不可编辑，只有新增界面时输入一次。
- 删除界面必须经过用户确认，并禁止删除项目中的最后一个界面。
- 父级变换必须保持后代的相对布局；不得只移动父框而留下子区域。
- `rect` 是用户确认的权威像素几何；百分比只在确定性 resolver 或最终构建时计算，不以 Markdown 作为数据通道。
- 不在本模块生成 `image_requirements.json`；其唯一生成者是 `ui-doc-builder` Step 3（`ui-image-generator` Stage 01-03 消费）。
- 不在本模块创建 UXML/USS/C#；交给 `ui-panel-builder`。

## 完成检查

- 项目 JSON 已确认，且每个目标界面都有一个对应画布。
- 运行时校验无 Error。
- 正式 `layout_intent.json` 已确认；路线 A 的 `UI布局意图.md` 已生成，或路线 B 已返回稳定 JSON 输出契约。
- 正式项目 JSON 中各画布的 `uiViewId` 唯一。
- 所有子区域 parentId 有效、无循环且几何完整位于父区域内。
- 路线 A 已回流 `ui-doc-builder`；路线 B 仅返回 Stage 04.5 调用方且未修改其阶段状态。
