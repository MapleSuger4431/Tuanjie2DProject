# Stage 01: UGUI Canvas Prefab 构建

> ⛔ **模式守卫**：本 Stage 仅适用于 `ui_mode == "ugui"`。`ui_mode == "ui_toolkit"` 时走 `01-uxml-uss`。

Steps 1-8：Brick 映射 → Canvas 配置 → 图片验证 → Prefab 构建 → 验证 → 部署 → 写状态文件。

## 步骤契约

| 项 | 内容 |
|----|------|
| 输入 | `UI设计文档.md`、`image_requirements.json` |
| 输出 | `brick-mapping.md`、Tree Prefab 文件（panel 树 + list_item 模板树）、`ui-panel-builder-state.json` |
| 前置门禁 | UI设计文档.md 存在；image_requirements.json 存在 |
| 完成判据 | Prefab 完整性验证通过 + ui-panel-builder-state.json 已写入（含完整元素清单 + Transform 路径） |
| 必读 references | 见模块 SKILL.md「Stage References 矩阵」UGUI 模式部分 |
| 必须激活技能 | unity-operator——BuildBrickTrees 经 execute_csharp_script 构建/改写 Prefab；subagent 第 0 步先激活再读文档 |

> **共享契约**：框架守卫、路径守卫、UIGameInstaller 门禁、gameId namespace、Panel 注册名约定、状态文件 schema、阶段路由规则见 `shared/contracts.md`。

## 本阶段核心规则

1. **UGUI Panel Prefab 直接创建到 `Assets/Games/{GameName}/UI/Prefabs/`**（通过 `execute_csharp_script` 调用 `BrickPanelBuilder.BuildBrickTrees`，Prefab 是二进制资产无需 context 中间层）
2. **UI 图片由 ui-image-generator 生成**，本阶段步骤 3 验证图片已存在
3. **首次执行必须顺序执行全部步骤**。中断恢复时检查已有产出文件，从首个缺失产出的步骤继续
4. **声明式构建（trees.json → BuildBrickTrees）**：每个界面 = 一份 trees.json（`write_file` 声明式 brick 树数组 + 参数 + 布局）+ 一次 `BuildBrickTrees` 调用——根节点 brick 决定根结构（panel/modal→Canvas 根，list_item→无 Canvas）、Brick 实例化、防黑屏修复、元素路径提取全部由库内置，skill 不写 C# 组装代码。`assemblyOnly: true` 的 brick 从 definition 构建；无对应 Brick 的元素省略 `brickId`（纯容器节点）
5. **全屏面板背景检查**：如果某面板的 `panel_type` 为 `full_screen`，其根节点下必须有全屏背景 Image 子元素

## 起草优先原则（防超时核心策略）

> ⚠️ subagent 超时的根因是试图一次性读完所有 references 再动手——这会导致认知超载。以下原则是硬性要求，不是建议。

1. **最小必读即可动笔**：只需读 `shared/contracts.md` + 本 SKILL.md 即可开始写代码。`references/` 下的文件是**边写边查的工具书**，在对应步骤执行时按需查阅，不需要预读
2. **边写边查**：步骤 5 写 trees.json / 构建时查阅 `ugui-prefab-construction.md`（trees.json schema、传参规则、布局换算、构建 runner、警告处理）；步骤 6 验证时查阅 `game-doctor-checks.md`
3. **拿不准的先写**：按最合理写法写，BuildBrickTrees warnings + 步骤 6 validate 兜底
4. **连续 3 轮只读不写 = 过度准备**，立即起草
5. **元素 >10 个时分段写 trees.json**（write_file 主体 + replace 追加），runner 单次调用不超时
6. **每构建 1 棵树立即落盘**：布局数值边算边写进 trees.json（禁止先在 thinking 中推演全部锚点/尺寸再一次性输出）；多界面场景每写完一棵树可先构建验证该树，再写下一棵

## 中断恢复

| 检查项 | 产出文件 | 存在则跳到 |
|--------|---------|-----------|
| 步骤 1 完成 | `context/games/{GameName}/ui-panel-builder/brick-mapping.md` | 步骤 2 |
| 步骤 3/4 通过 | `Assets/Games/{GameName}/UI/Images/*.png` 全部存在 | 步骤 5 |
| 步骤 5/6 完成 | `Assets/Games/{GameName}/UI/Prefabs/*.prefab` | 步骤 8 |
| 步骤 5 中断（trees.json 已存、Prefab 未建/验证失败） | `context/games/{GameName}/ui-panel-builder/trees/{InterfaceName}.trees.json` | `BuildBrickTrees` 直接重建（跳过重新推导） |
| 步骤 6 通过 | Prefab 完整性验证零错误 | 写状态文件 |

---

## 步骤索引

| 步骤 | 名称 | 输入 | 产出 |
|------|------|------|------|
| 1 | Brick 映射 | UI设计文档.md, image_requirements.json | brick-mapping.md |
| 2 | Canvas 配置 | UI设计文档.md §1 设计分辨率 | Canvas 配置参数 |
| 3 | 验证 UI 图片 | image_requirements.json, Images/*.png | 校验报告 |
| 4 | 图片完整性校验 | 步骤 3 报告 | 校验报告 |
| 5 | 构建 Panel Prefab | brick-mapping.md, UI设计文档.md §3/§11 | {TreeName}.prefab |
| 6 | Prefab 完整性验证 | Prefab 文件 | 验证报告 |
| 7 | 部署/刷新 | Assets 侧 Prefab | AssetDatabase.Refresh() |
| 8 | 写状态文件 | 全部产出 | ui-panel-builder-state.json |

### 步骤1：阅读 UI设计文档.md + Brick 映射

> 若 `context/games/{GameName}/ui-panel-builder/brick-mapping.md` 已存在（调用方预产出，见 `shared/contracts.md` 步骤 2），跳过本步骤全部内容，直接从步骤 2 开始。

1. 阅读 UI设计文档.md。若文件不存在，向调用方报错并中止。若用户请求的面板不在 UI设计文档.md 的界面清单中，向调用方报错并中止
2. 阅读 UI设计文档.md §9（游戏事件与 UI 面板触发映射）与 §7（DI 注册清单），了解游戏逻辑脚本暴露的事件接口与服务。从 `UI设计文档.md` §6 获取绑定信息
3. **验证 HUD 数据源**：对 HUD 需要绑定的每个 BindableProperty，确认对应游戏脚本已暴露公共访问器/事件
4. 确认每个界面需要绑定哪些 BindableProperty，需要哪些 BindButton
5. 规划任务清单：每个界面可能包含多棵 brick 树（panel + list_item 模板等）。对于 HUD 面板，在 state file 中标记 `is_hud: true`
6. **读取图生图配置**：检查 UI设计文档.md 是否包含 §12 图生图配置章节
7. **读取 image_requirements.json**
8. **Brick 映射**：将 UI设计文档.md 中每个 UI 组件映射到砖块。**优先运行部署型工具一行调用 `Create.GameFramework.TJBrickMappingDraft.Mapping("{GameName}");`（包 `Editor/UIBuildTools`，经 `execute_csharp_script`）生成草稿**（类型→brickId 候选、prefabPath/definitionPath 校验、image_requirements 关联、functional_type 判定已工具化），仅审核「人工复核」行并补充 Layout/Container 级 Brick，禁止人工查 BrickIndex 全文手写映射表；brick-mapping.md 草稿已由调用方预产出时直接审核。映射规则细节读 `references/brick-mapping.md`（含 UGUI 模式 BrickIndex.ugui.json 映射）。⚠️ **禁止用 glob 在 `Packages/` 下搜索**——包是 `file:../` 引用，物理文件在 `.codely-cli/extensions/TJUIBuilder/Packages/` 下
9. **产出 brick-mapping.md**：

```markdown
# Brick Mapping — {GameName}（UGUI 模式）

## 界面: {InterfaceName}

| 树名 | 根 brickId | 组件名 | brickId | image_requirements 条目 | functional_type |
|------|-----------|--------|---------|------------------------|-----------------|
| {MainMenuPanel} | ugui.panel | {PlayButton} | {brickId} | button_style_1 | null |
| {MainMenuPanel} | ugui.panel | {ScoreLabel} | {brickId} | — | null |
| {MainMenuItemTemplate} | ugui.list_item | — | — | — | null |
```

> brickId 和 prefabPath 从 `BrickIndex.ugui.json` 读取，不在此硬编码。每界面可能有多棵树（panel 树 + list_item 模板树等）。

### 步骤2：Canvas 配置

> 设计分辨率来源：UI设计文档.md §1 的「设计分辨率」字段，未指定时回退到 `1920×1080`。

Canvas 配置（renderMode/CanvasScaler/GraphicRaycaster/sortingOrder）由 `BrickPanelBuilder.BuildBrickTrees` 从 trees.json 中每棵 Canvas 根树（panel/modal 根）的字段自动完成，本步骤只需确定两个值（写入 trees.json 树根字段）：

- `referenceWidth` / `referenceHeight`：设计分辨率
- `sortingOrder`：全屏面板 10、弹窗 20、确认框 30

UGUI 模式不创建 PanelSettings（PanelSettings 是 UI Toolkit 专属）。

### 步骤3：验证 UI 图片存在

> UI 图片由 ui-image-generator 独立生成，本步骤只验证图片已存在。

1. `glob Assets/Games/{GameName}/UI/Images/*.png` 列出已存在的图片文件
2. 对每张 `image_requirements` 目标图用 AQ 验证 `TextureImporter.textureType == Sprite`、mipmap 关闭
3. 对照步骤 1 的 `image_requirements` 列表，逐项检查每张标记为 `"ai_generate"` 的图片是否存在
4. **缺失图片处理**：记录缺失列表并在输出中明确报告。不中止后续步骤——继续处理有图片的面板，缺失图片的 Image 组件留 null 并标注 `/* TODO: 图片 {name} 缺失 */`

### 步骤4：图片完整性校验

通过 `execute_csharp_script` **一行调用部署型工具**：`Create.GameFramework.TJValidateAssets.Images();`（自动扫描所有 `Assets/Games/*/UI/Images/` 目录；无需读取/重发脚本全文）。

**校验内容**：文件存在性、TextureImporter 设置、尺寸合理性、来源合法性（防直接复制；`source=direct_use` 条目为合法参考图复用，hash 命中按设计豁免——与 UI Toolkit 模式步骤 3 同规则）、透明 padding。

**校验失败处理**：后处理未执行或缺图片 → 报错并中止。

### 步骤5：构建 Panel Prefab

> UGUI Prefab 构建规则读 `references/ugui/ugui-prefab-construction.md`（trees.json schema、传参规则、布局换算、构建 runner、警告处理）。

> ⚠️ **Brick Prefab 完整性**：`assemblyOnly: true` 的 brick（panel、list_item、modal）无 Prefab 文件，工厂直接从 definition 构建——设计行为，非降级。`BuildBrickTrees` 不会因此失败——库内置行为，无需任何处理。

> ⚠️ **防超时**：元素 >10 个时 trees.json 可分段组装（`write_file` 写主体后 `replace` 追加），但 **BuildBrickTrees 必须单次完整调用**（不支持对已保存 Prefab 追加）。

通过 `write_file` + `execute_csharp_script` 为每个界面构建（**全程不写 C# 组装代码**）：

1. **写 trees.json**（`write_file` 到 `context/games/{GameName}/ui-panel-builder/trees/{InterfaceName}.trees.json`，模板见 ugui-prefab-construction.md §5）：
   - 对照 UI设计文档.md §3/§11/§12 + brick-mapping.md 写声明式 brick 树数组
   - 每棵树根节点写 `brickId`（如 `"ugui.panel"`、`"ugui.modal"` 或 `"ugui.list_item"`）；`parameters` 按元素视觉属性填写（参数名查 BrickCheatsheet.ugui.md；图片一律传 `Assets/Games/{GameName}/UI/Images/...` 路径字符串）
   - `layout`：锚点定位元素换算（参考图模式按 §12.2 百分比列；AI 直规划模式按 §11 各界面「布局」描述），LayoutGroup 子元素省略
   - `slot`：Modal 槽位等具名挂载点；常规元素省略；`brickId` 缺省 = 纯容器节点
2. **构建**（固定 runner，见 ugui-prefab-construction.md §5 第 2 步）：`BrickPanelBuilder.BuildBrickTrees(treesPath, "Assets/Games/{GameName}/UI/Prefabs/")`
3. **处理每个 result 的 `warnings`**（强制）：composition 违规必须改 trees.json 重建；其余按 ugui-prefab-construction.md §6 处理
4. 从每个 result 的 `elementPaths` 取每个元素的 SafeFind 路径（如 `ButtonGroup/StartBtn`），供步骤 8 写 state file

**修改已有界面**：直接编辑 trees.json（`read_file` + `replace`）重跑 runner，不写 C#（见 ugui-prefab-construction.md §5.5）。

> 运行时动态组装（游戏内按配置生成 UI）用 Runtime 层的 `BrickFactory.AssemblePanelFromJson / AssemblePanelDetailed`（返回 GameObject + elementPaths，不写 Prefab 资产）；Editor 构建一律走 `BrickPanelBuilder.BuildBrickTrees`。

⚠️ **全屏背景**：`panel_type == "full_screen"` 的面板，trees.json 中该 panel 树的第一个子元素为全屏背景（anchorMin=(0,0), anchorMax=(1,1)）。

⚠️ **HUD 面板透明根**：HUD 面板的 panel 树不含背景元素（透明叠加在游戏画面上）。

### 步骤6：Prefab 完整性验证

**主判据 = 每个 result 的 `warnings` 为空**。BuildBrickTrees 已内置根节点 scale/anchors 验证（`[VERIFY]` 前缀警告，仅 Canvas 根：panel、modal），工厂 PostProcess 已保证组件接线（Button.targetGraphic / Slider.fillRect+handleRect / Toggle.graphic / Dropdown.template），元素命名已由 elementPaths 记录 —— 警告为空即全部通过。

**检查项**（缺一不可）：
1. 每个 Canvas 根树（panel/modal）的 Prefab 根节点有 Canvas + CanvasScaler + GraphicRaycaster（list_item 树无 Canvas，跳过此项）
2. **Canvas 根 RectTransform scale == (1,1,1)**（防黑屏——SaveAsPrefabAsset bug 会重置为 0）
3. **Canvas 根 RectTransform anchors 为 stretch fill**（anchorMin=(0,0), anchorMax=(1,1)）
4. 所有 brick-mapping.md 中标记的元素在 Prefab 中有对应命名 GameObject
5. 所有 Image 组件引用的 Sprite 不为 null（有图片的元素——缺失图片的允许 null 但需有 TODO 标注）
6. Button 组件有 targetGraphic
7. Slider 有 fillRect；handleRect 仅当存在 HandleArea 时要求（slider 有手柄，progress_bar 默认无手柄）
8. Dropdown 有 captionText 和 template
9. Toggle 有 graphic 和 targetGraphic

**检查项 1-9 的机械执行**：用 `execute_csharp_script` 一行调用部署型工具 `Create.GameFramework.TJValidateAssets.UguiPrefabs();`（自动扫描所有 `Assets/Games/*/UI/Prefabs/`，逐项检查 Canvas 三件套/scale/anchors/组件接线，并把结果写入 `context/games/{GameName}/ui-panel-builder/uiv-validation.json`）。工具输出 ❌ 时按失败处理修复；检查项 4（元素覆盖对照 brick-mapping.md）仍由 agent 结合 `elementPaths`/brick-mapping.md 判断。

**失败处理**：优先**修改 trees.json 后重新 BuildBrickTrees**（而非手改 Prefab）。重新验证最大重试 7 次。

### 步骤7：部署/刷新

UGUI Panel Prefab 已在步骤 5 直接创建到 Assets 目录，无需从 context 复制。执行：

```csharp
AssetDatabase.SaveAssets();
```

> 不执行全局 `AssetDatabase.Refresh()`（unity-operator 横切红线）——Prefab 由 `SaveAsPrefabAsset` 即时导入，图片由 ui-image-generator Stage 03 工具逐资产导入。

### 步骤8：写状态文件

Prefab 完整性验证通过后，产出 `ui-panel-builder-state.json`：

1. **元素清单来源**：优先读步骤 5 落盘的 `{TreeName}.paths.json`（name→path 逐行 TSV，与构建产物零漂移）；缺失时回退扫描 Panel Prefab 提取带 name 的 GameObject
   - `type`：从 UGUI 组件类型映射（Button → `Button`, Text → `Label`, Slider → `Slider`, Toggle → `Toggle`, Dropdown → `DropdownField`, InputField → `TextField`, Image/Transform → `VisualElement`）
   - `functional_type`：从 brick-mapping.md 查表关联（paths.json 仅含 name/path，两字段由本步骤合并）
2. **`has_dropdown` 标记**：扫描是否存在 Dropdown 组件（UGUI 模式下 `has_dropdown` 不触发 popup USS 修复，但标记用于 state file 完整性）
3. **`panel_type` / `is_hud`**：从 UI设计文档.md §1 面板清单读取
4. **`design_resolution`**：从 UI设计文档.md §1 读取
5. **组装 state file**：先读取既有文件再按 `shared/contracts.md` 中的 schema 组装写入——stages 中已有的 `status`/`started_at`/`completed_at` **原样透传**（调用方派发时经 `SetStage` 写入的 `started_at` 必须保留；时钟值只允许透传既有值，禁止新造），最终 `status`/`completed_at` 由第 7 步工具写入
   - `01-ugui-prefab` 的 `uiv_validation_passed: true`
   - `02-ugui-csharp` 设为 `pending`
   - `interfaces[]` 每个界面含 `trees[]`（每棵树含完整元素清单含 `path` 字段和 `prefab_assets_path`）
6. 写入后确认 JSON 格式有效
7. 用部署型工具收尾（禁止手写状态与时钟）：`Create.GameFramework.TJStateHelper.SetStage("<state文件>", "01-ugui-prefab", "completed", timestamps: true);` —— 写入 `status: completed` + `completed_at`（系统时钟，与 schema 格式一致）

---

## Completion Checklist

1. [步骤3] 图片验证已执行
2. [步骤4] 图片完整性校验已执行
3. [步骤5] 全部界面 Tree Prefab 已创建到 `Assets/Games/{GameName}/UI/Prefabs/`（panel 树 + list_item 模板树）
4. [步骤5] 全部界面已通过 `BrickPanelBuilder.BuildBrickTrees` 构建，每个 result 的 `warnings` 已处理（composition 违规零容忍）
5. [步骤5] 全部界面 trees.json 已写入 `context/games/{GameName}/ui-panel-builder/trees/`（构建源，供修改/重建）
5. [步骤5] 全部带 name 的 GameObject 已记录 Transform 路径
6. [步骤6] Prefab 完整性验证通过（Canvas/CanvasScaler/GraphicRaycaster 完整、Image.sprite 非 null、Button.targetGraphic 非 null、Slider.fillRect 非 null、有 HandleArea 的 Slider handleRect 非 null）
7. [步骤8] **ui-panel-builder-state.json 已写入**（含完整元素清单含 path 字段、prefab_assets_path、验证状态）

**以下验证不在此阶段范围内**：
- Panel 脚本编写 — Stage 2
- 编译验证 — 由宿主提供的 C# 编译部署闭环统一做
- Play Mode 验证 — 由集成验证阶段做
- UIRoot 装配 — Stage 2
