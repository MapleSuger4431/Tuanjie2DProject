# UGUI Panel Prefab 构建参考

> 本文件是 `01-ugui-prefab` Stage 的必读参考。
> **构建模型**：skill 只写声明式 **trees.json**（什么样的界面，`write_file` 纯文本），`BrickPanelBuilder.BuildBrickTrees()` 输出 N 个 Prefab + 元素 SafeFind 路径清单 + 校验警告。每棵树的根节点是一个 brick（`ugui.panel` / `ugui.list_item` 等），根据 definition 决定根结构（panel/modal→Canvas 根，list_item→无 Canvas）。
> 参数名/类型/默认值查询 `BrickCheatsheet.ugui.md`（由 `gen_brick_artifacts_ugui.py` 自动生成，始终与 definition 同步）。
> ⚠️ **路径注意**：包通过 `file:../` 引用，物理文件在 `.codely-cli/extensions/TJUIBuilder/Packages/` 下而非 `Packages/` 下。`read_file`/`glob` 必须使用物理路径；C# 内 `AssetDatabase.LoadAssetAtPath` 使用 Unity 虚拟路径 `Packages/cn.tuanjie.create.ui/Runtime/UIBrick/...`。

---

## 1. 构建总则

- 每个界面 = **一份 trees.json（write_file）+ 一次 `BuildBrickTrees(treesJsonPath, outputPrefabDir)` 调用（execute_csharp_script 固定 runner，见 §5）**
- 一份 trees.json 包含 N 棵 brick 树，每棵树根节点是一个 brick（`ugui.panel` / `ugui.modal` / `ugui.list_item` 等），输出 N 个 Prefab
- 每棵树产出三件套：Prefab（`Assets/Games/{GameName}/UI/Prefabs/{TreeName}.prefab`）、`result.elementPaths`（元素 → SafeFind 路径，写入 state file 供 Stage 2）、`result.warnings`（**必须处理，见 §6**）
- 全部带 `name` 的元素自动进入 `elementPaths`，无需手动记录
- ⛔ 禁止用 `new GameObject` 手写任何有对应 Brick 的组件（Brick 携带框架增强脚本）；无 Brick 的元素省略 `brickId`（纯容器节点）
- ⛔ 禁止移除/替换 Brick 实例上的任何组件

### 1.1 根节点类型与根结构对应表

| 根节点 brickId | 根结构 | 说明 |
|---------------|--------|------|
| `ugui.panel` | Canvas + CanvasScaler + GraphicRaycaster + Image | 面板根，带 Canvas 渲染；防黑屏修复内置 |
| `ugui.modal` | Canvas + CanvasScaler + GraphicRaycaster + Image(遮罩) + ModalPanel(Slots) | 弹窗壳体根，独立 Canvas 层（container 类 assemblyOnly，可作树根）；防黑屏修复内置 |
| `ugui.list_item` | Button + Image + Icon + Label + Value（无 Canvas） | 列表条目模板，供运行时 ListView 克隆用 |
| 其他 brick | 从 definition 构建，无 Canvas | 按需使用 |

> `CanvasRootBricks` 集合在 `BrickPanelBuilder.cs` 中维护，目前含 `ugui.panel`、`ugui.modal`。如需新增 Canvas 根 brick，在集合中添加即可。

### 1.2 库内置行为（skill 无需关心，仅供理解）

| 内置行为 | 说明 |
|---------|------|
| Canvas 配置 | 根 brick 为 Canvas 根（`ugui.panel`、`ugui.modal`）时自动配置：ScreenSpaceOverlay + CanvasScaler(ScaleWithScreenSize, refRes, match 0.5) + GraphicRaycaster，sortingOrder 从树根字段读取 |
| Brick 构建 | `assemblyOnly: true` 的 brick（panel、list_item、modal）无 Prefab 文件，工厂**直接从 definition 构建等效层级**——这是设计行为，非降级 |
| 防黑屏 | `SaveAsPrefabAsset` 会把 Canvas 根 scale 重置为 0，BuildBrickTree 保存后自动用 SerializedObject 修复并验证（Canvas 根：panel、modal） |
| PostProcess | Button.targetGraphic、Slider.fillRect/handleRect/targetGraphic、Toggle.graphic/targetGraphic、ScrollRect.viewport/content、Dropdown.template（含模板接线 + 默认选项）、Text 默认字体、无 sprite 的 Icon 隐藏（Image.enabled=false）、Selectable.navigation 保持系统默认 Automatic（与编辑器一致） |
| 组合校验 | 每个嵌套关系按 definition 的 `compositionPolicy`（allowedChildren/canNestUnder）校验，违规写入 warnings |

### 1.3 list_item 的两种用法

| 场景 | 用法 | 是否独立 Prefab |
|------|------|----------------|
| scroll_view 内的列表条目 | 作为父树的 `children` 出现，`slot: "Viewport/Content"` | 否（内嵌在父树 Prefab 中） |
| ListView 的 itemPrefab 模板 | 作为独立树（`trees[]` 中单独一棵） | 是（供运行时 `Object.Instantiate` 克隆填充） |

> 区分依据：如果 list_item 在 scroll_view 内静态填充（条目数量固定），直接作为子节点；如果条目数量运行时动态决定，需作为独立模板 Prefab。

## 2. trees.json 编写指南

**字段语义**（完整 JSON 模板见 §5 第 1 步）：

| 层级 | 字段 | JSON 形式 | 语义 |
|------|------|----------|------|
| 顶层 | `interfaceName` | string | 界面名称（仅标识用） |
| 顶层 | `trees` | array | brick 树数组，每棵树根节点是一个 brick |
| 树根 | `name` | string | 树名称 = Prefab 文件名 + 根 GameObject 名 + SafeFind 键 |
| 树根 | `brickId` | string | 根节点 brick（如 `"ugui.panel"`、`"ugui.modal"`、`"ugui.list_item"`） |
| 树根 | `parameters` | object | 根 brick 参数（键名/取值规则见 §3） |
| 树根 | `sortingOrder` | number | Canvas 层级（仅 Canvas 根有效：panel、modal；约定见下表，缺省 10） |
| 树根 | `referenceWidth` / `referenceHeight` | number | 设计分辨率（仅 Canvas 根有效：panel、modal；缺省 1920×1080） |
| 树根 | `children` | array | 子节点树（递归 BrickTreeNode） |
| 子节点 | `name` | string | **树内唯一**；GameObject 名 + SafeFind 键 |
| 子节点 | `brickId` | string | 如 `"ugui.button"`；省略 = 纯 RectTransform 容器节点 |
| 子节点 | `parameters` | object | brick 参数（键名/取值规则见 §3） |
| 子节点 | `layout` | object | 锚点定位（成员与省略规则见 §2.2） |
| 子节点 | `slot` | string | 挂载到父内具名槽位（如 `"ModalHeaderSlot"`），缺省按 §2.1 自动解析 |
| 子节点 | `children` | array | 递归嵌套子元素 |

**sortingOrder 约定**：全屏面板（MainMenu, HUD）= 10；弹窗（Pause, Settings）= 20；确认对话框 = 30。

> ⚠️ **modal 弹窗树根必须显式写 `sortingOrder`**：modal 作树根时，Canvas 层级**只**由树根 `sortingOrder` 字段决定——builder 无条件用它覆盖 definition 参数的默认 100，字段缺省 10 → 弹窗与全屏面板同层，可能盖不住宿主面板。弹窗树根写 20、确认框树根写 30。

### 2.1 子元素挂载规则（自动解析，无需手动 Find）

| 父元素类型 | 子元素实际挂载点 |
|-----------|----------------|
| ScrollRect（ugui.scroll_view） | `Viewport/Content` |
| stack | 父根节点（group-on-Root：LayoutGroup 挂在 Root 上排列其直接子级，preferred 尺寸沿布局链逐级上传；无中间容器层） |
| grid | 父根节点 |
| `ugui.modal`（未指定 `slot`） | modal 根自身——modal 子元素一般应显式 `slot` 到 `ModalHeaderSlot`/`ModalContentSlot`/`ModalFooterSlot`（槽位由 `panelPadding`/`headerHeight`/`footerHeight` 参数排版） |
| 其他（ugui.panel 等） | 父节点自身 |
| 指定 `slot` 字段 | 父内具名槽位（深度查找，如 Modal 的 `ModalHeaderSlot`/`ModalContentSlot`/`ModalFooterSlot`），覆盖上表；⚠️ Editor（trees.json）slot 仅匹配**单名**，不支持斜杠路径（斜杠路径仅 Runtime `AssemblePanel` API 支持，见 §11.1） |

### 2.2 layout 何时传、何时省略

- **LayoutGroup 的子元素**：省略 `layout`（LayoutGroup 自动排列，手动设 anchors 反而冲突）
- **锚点定位元素**（全屏背景、居中弹窗、角落图标）：写 `layout` 对象的 `anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta/offsetMin/offsetMax`（全部可省略，只写需要的）
- ⚠️ **`sizeDelta` 与 `offsetMin/offsetMax` 互斥**：UGUI 原生语义中 offset setter 会重算 sizeDelta，同设时 **offset 为准**（sizeDelta = offsetMax − offsetMin）。二者只选其一
- **HUD 面板透明根**：Canvas 根不设背景元素即可（HUD 面板本就不加 bg）

## 3. 传参规则

- **参数名 = definition.json `parameters` 的键**，点前缀分组（`icon.*`、`tint.*`、`effects.*`、`value.*`）。完整清单查 `BrickCheatsheet.ugui.md`（自动生成，与 definition 同步）
- **参数顺序无关**：工厂内部按依赖分层应用（SetActive 激活可选子节点 → Ensure*/Load* 挂组件与资产 → 其余）
- **Color**：`"#RRGGBB[AA]"`、`[r,g,b,a]` 数组或命名色；**Vector2/Vector3**：数组；**枚举**（Image.Type/TextAnchor/FontStyle/Selectable.Transition…）：字符串名；**float/int/bool**：字面量
- **资产参数**（`backgroundSprite`、`icon.spritePath`、`customFontPath`、`*SoundPath`、`type:"Sprite"` 的 `sprite`）传 `Assets/` 开头路径字符串
- **optional 子部件默认未激活**：`icon.active`/`value.active`/`scrollbar.active` 未传 = 不激活；传 `true` 激活 Prefab 中预建的未激活节点（全量生成模型——可选子节点与参数隐含组件已预建进 Prefab，参数只翻开关）
- **根级 width/height 在父级 LayoutGroup 下经 LayoutElement 生效**：LayoutGroup 容器内的 brick，其根级 `width`/`height` 参数会同步写 `LayoutElement.preferredWidth/Height`（sizeDelta 一并保留）——不传则被布局压成内容 preferred 尺寸（如按钮只剩文字高度≈36px），按钮/卡片须显式传尺寸
- ⚠️ 传入 definition 未声明的参数名 → 告警跳过（不生效），以 BrickCheatsheet 为准

## 4. RectTransform 定位规则（设计文档 → trees.json 换算）

### 常见布局模式

| 布局 | anchorMin | anchorMax | pivot |
|------|-----------|-----------|-------|
| 全屏背景 | (0,0) | (1,1) | (0.5,0.5) |
| 居中元素 | (0.5,0.5) | (0.5,0.5) | (0.5,0.5) |
| 顶部居中 | (0.5,1) | (0.5,1) | (0.5,1) |
| 底部居中 | (0.5,0) | (0.5,0) | (0.5,0) |
| 右侧居中 | (1,0.5) | (1,0.5) | (1,0.5) |
| 水平拉伸 | (0,0.5) | (1,0.5) | (0.5,0.5) |

> ⚠️ **pivot 必须与锚点边对齐**：顶边通栏/角落元素若锚点在边缘（y=1 或 y=0）而 pivot 居中 (0.5,0.5)，`anchoredPosition` 偏移会让元素半截出屏（HUD 顶栏曾因此被裁切一半）——顶边元素 pivot 用 (0.5,1)，底边用 (0.5,0)，随锚点走。

> **全屏背景元素**（HUD 战场背景等）：`anchorMin (0,0)` / `anchorMax (1,1)` 全拉伸 + `raycastTarget` 参数传 `false`（背景不拦截点击；panel/sprite/label 均支持该参数）+ 在 trees.json 中排最先渲染（绘制顺序 = 兄弟节点顺序）。

### 从设计文档百分比转换

参考图模式：UI设计文档.md §12.2 的 `相对父 X%/Y%/宽%/高%` 列；AI 直规划模式：§11 各界面的「布局」描述（对齐/尺寸/位置语义）→ 先换算为 0~1 锚点值，直接写入 trees.json 的 `layout`：

```
x=12.5% → 0.125    y=60% → 0.60    w=75% → 0.75    h=16% → 0.16
```

```json
"layout": {
  "anchorMin": [0.125, 0.60],
  "anchorMax": [0.875, 0.76],
  "offsetMin": [0, 0],
  "offsetMax": [0, 0]
}
```

### Image.Type 选择规则

| 用途 | Image.Type |
|------|-----------|
| 全屏/按钮/弹窗背景、进度条 track/fill | `Sliced`（9-slice，要求 Sprite border 已设置；未设置降级 Simple） |
| 图标、装饰图案 | `Simple` |

## 5. 构建流程：trees.json → BuildBrickTrees（主路径）

skill **不写 C# 组装代码**：`write_file` 直接写声明式 trees.json → 一行调用构建。JSON 是唯一主路径（无编译风险、AI 可精确编辑、可 git diff）。

**存档路径约定**：`context/games/{GameName}/ui-panel-builder/trees/{InterfaceName}.trees.json`

### 第 1 步：写 trees.json（write_file）

对照 UI设计文档.md §3/§11/§12 + brick-mapping.md + BrickCheatsheet.ugui.md（参数名），按下述模板写 JSON：

```json
{
  "interfaceName": "MainMenu",
  "trees": [
    {
      "name": "MainMenuPanel",
      "brickId": "ugui.panel",
      "sortingOrder": 10,
      "referenceWidth": 1920,
      "referenceHeight": 1080,
      "parameters": {
        "sprite": "Assets/Games/MyGame/UI/Images/bg-mainmenu.png",
        "type": "Sliced"
      },
      "children": [
        {
          "name": "ButtonGroup",
          "brickId": "ugui.stack_vertical",
          "parameters": { "spacing": 12.0, "childAlignment": "MiddleCenter" },
          "layout": { "anchorMin": [0.5, 0.5], "anchorMax": [0.5, 0.5], "anchoredPosition": [0, -80], "sizeDelta": [400, 300] },
          "children": [
            {
              "name": "StartBtn",
              "brickId": "ugui.button",
              "parameters": {
                "text": "开始游戏",
                "icon.active": true,
                "icon.spritePath": "Assets/Games/MyGame/UI/Images/icon-play.png",
                "backgroundSprite": "Assets/Games/MyGame/UI/Images/btn-start.png"
              },
              "children": []
            },
            { "name": "SettingsBtn", "brickId": "ugui.button", "parameters": { "text": "设置" }, "children": [] }
          ]
        }
      ]
    },
    {
      "name": "MainMenuItemTemplate",
      "brickId": "ugui.list_item",
      "parameters": { "text": "默认项", "interactable": true },
      "children": []
    }
  ]
}
```

**schema 字段**：

- 顶层：`interfaceName`（界面名）、`trees`（brick 树数组）
- 每棵树根：`name`（树名 = Prefab 文件名）、`brickId`（根 brick，如 `ugui.panel`/`ugui.modal`/`ugui.list_item`）、`parameters`（根 brick 参数）、`sortingOrder`/`referenceWidth`/`referenceHeight`（仅 Canvas 根有效：panel、modal）、`children`（子节点树）
- 每个子节点：`name`（**树内唯一**，SafeFind 键）、`brickId`（缺省/null = 纯 RectTransform 容器）、`parameters`、`layout`、`slot`、`children`（递归嵌套）
- 向量一律 `[x, y]` / `[x, y, z]` 数组；省略成员 = null 语义；`sizeDelta` 与 `offsetMin/Max` 互斥（§2.2）

### 第 2 步：构建（execute_csharp_script 固定 runner）

```csharp
using System.Collections.Generic;
using System.Linq;
using Create.GameFramework;
using UnityEngine;
using UnityEditor;

string gameName = "MyGame";
string interfaceName = "MainMenu";
string treesPath = $"context/games/{gameName}/ui-panel-builder/trees/{interfaceName}.trees.json";
string prefabDir = $"Assets/Games/{gameName}/UI/Prefabs/";

var results = BrickPanelBuilder.BuildBrickTrees(treesPath, prefabDir);

// 落盘 elementPaths —— 步骤 8 写 state file 的权威来源（跨 execute_csharp_script 传递）
// trees.json 同目录，{treeName}.paths.json：
foreach (var result in results)
{
    if (!result.success || result.elementPaths.Count == 0) continue;
    var pathsOut = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(treesPath),
        $"{result.treeName}.paths.json");
    System.IO.File.WriteAllText(pathsOut, string.Join("\n",
        result.elementPaths.Select(kv => kv.Key + "\t" + kv.Value)));
}

// 返回值汇总 —— subagent 必须直接读到
var summary = string.Join("\n", results.Select(r =>
    $"[{r.treeName}] success={r.success}, warnings={r.warnings.Count}:\n" +
    string.Join("\n", r.warnings.Select(w => "  - " + w)) + "\n" +
    "  elementPaths:\n" + string.Join("\n", r.elementPaths.Select(kv => $"    {kv.Key} -> {kv.Value}")) +
    $"  prefabPath: {r.prefabPath}"));

var anyFailed = results.Any(r => !r.success || r.warnings.Count > 0);
if (anyFailed) Debug.LogWarning($"[{interfaceName}]\n{summary}");
return summary;   // 直接返回给 subagent —— warnings 非空 = 必须处理（见 §6）
```

⚠️ 防超时：元素 >10 个时 trees.json 可分段组装（`write_file` 写主体后 `replace` 追加），但 **BuildBrickTrees 必须单次完整调用**（不支持对已保存 Prefab 追加）。

⚠️ **禁止并行构建**：`BuildBrickTrees` 会改写包内共享 Brick Prefab；两个任务/subagent 同时构建会互相改写对方引用的包 Prefab，导致已建面板的 sprite override 悬空（合金弹头 Stage 01 事故：8 个包内 Prefab 被改写）。构建必须串行、单任务执行。

## 5.5 修改、重建与中断恢复

trees.json 是界面的持久化源，后续迭代不再写任何 C#：

| 场景 | 操作 |
|------|------|
| 修改界面（颜色/字号/位置/增删元素） | `read_file` trees.json → `replace` 编辑字段 → 重跑第 2 步 runner |
| 中断恢复（trees.json 已存、Prefab 未建/验证失败） | 直接重跑第 2 步，跳过重新推导 |
| UI设计文档.md 变更了元素清单/布局 | 以文档为准**重写覆盖** trees.json，再重跑 runner |
| 构建警告/验证失败 | 修 trees.json 本身重建，⛔ 禁止手改 Prefab（见 §6） |
| 重建后 Panel 脚本组件丢失 | BuildBrickTrees 覆盖保存 Prefab，先前挂载的 Panel 组件随重建丢失 → 重跑 Stage 02 步骤 12a-3 重挂并 readback |

**与 UI设计文档.md 的关系**：文档是需求源，trees.json 是构建源。文档大改 → 重写 trees.json；仅视觉微调 → 直接改 trees.json。

## 6. 警告处理（强制）

每个 result 的 `warnings` 中的每一类：

| 警告 | 处理 |
|------|------|
| `Composition: ... allowedChildren / canNestUnder` | **必须修复 trees.json**（换合法父级或换 Brick），不可忽略 |
| `Slot 'X' not found` | 检查 slot 名（查 BrickCheatsheet 的层级/槽位名） |
| `Duplicate element name` | 重命名（SafeFind 要求唯一） |
| `Brick 'X' failed to create` | 检查 brickId 拼写（对照 BrickCheatsheet） |
| `[VERIFY] root scale/anchors wrong` | 重新 BuildBrickTree 一次；仍失败则报告调用方 |

步骤 6 Prefab 完整性验证发现问题时，优先**修改 trees.json 重跑 BuildBrickTrees**（而非手改 Prefab）。

## 7. 命名约定

- Prefab 文件名：`{TreeName}.prefab`（如 `MainMenuPanel.prefab`、`MainMenuItemTemplate.prefab`）
- Prefab 根 GameObject 名：`{TreeName}`（BuildBrickTree 用树根 `name` 命名）
- 子元素名：使用 UI设计文档.md §11 元素名称（如 `PlayButton`、`ScoreLabel`），树内唯一

## 8. 与 UI Toolkit 模式的关键差异

| 维度 | UI Toolkit | UGUI |
|------|-----------|------|
| 产出文件 | UXML + USS（文本） | Panel Prefab（二进制） |
| 构建方式 | `write_file` 写文本 | `write_file` 写 trees.json → 固定 runner 调 `BuildBrickTrees` |
| 功能控件修复 | USS 选择器 + C# 同步 | UGUI 原生处理（无需修复） |
| 验证 | `UIV.Validate()` | BuildBrickTrees warnings + 步骤 6 验证 |
| 部署 | `run_shell_command` 复制文件 | 已在 Assets（BuildBrickTrees 直接产出） |

---

## 11. BrickFactory Runtime 组装 API（AssemblePanel）

> **定位分工**：Editor 面板构建主路径为 `BrickPanelBuilder.BuildBrickTrees`（trees.json → Prefab 资产，防黑屏内置，见 §5）。本节的 `AssemblePanel` 系列是 **Runtime 层 API** — 游戏运行时按配置动态生成 UI（不写 Prefab 资产），如任务面板按存档数据动态拼装。
>
> ⚠️ **Runtime JSON 与 trees.json 格式不同**（有意边界）：Editor 用 trees.json（`trees[]` 树结构，根节点 = brick，通过 `BuildBrickTrees` 构建 Prefab）；Runtime 用 panel.json（`elements[]` 嵌套节点树（每个元素可含 `children`），Canvas 隐式，通过 `AssemblePanelFromSpecJson` 动态组装 GameObject）。两套格式不可混用。
>
> ⚠️ **Runtime 资产路径契约**：Editor trees.json 的 sprite/font/audio 参数传 `Assets/...` 路径（AssetDatabase）；Runtime 组装经 `BrickFactoryRuntime`（`Resources.Load`），同类参数必须传 **Resources 相对路径**（无 `Assets/` 前缀、无扩展名，如 `"UI/button_bg"`）—— 传 `Assets/...` 会告警并跳过该参数。
>
> ⚠️ **运行时列表模板（ToListView itemPrefab 来源）**：无 AssetDatabase 时先用 `factory.Create("ugui.list_item", null, "Template")` 造一个实例作模板传入 `ToListView`（每次 tick 以 `Object.Instantiate` 克隆填充）；模板实例可 `SetActive(false)` 挂在常驻节点下。

### 11.1 PanelDescriptor JSON Schema

```json
{
  "panelName": "SettingsPanel",
  "sortingOrder": 10,
  "referenceResolution": [1920, 1080],
  "root": {
    "brickId": "ugui.stack_vertical",
    "name": "Root",
    "parameters": { "spacing": 8 },
    "children": [
      {
        "brickId": "ugui.label",
        "name": "TitleLabel",
        "parameters": { "text": "设置", "fontSize": 28 }
      },
      {
        "brickId": "ugui.slider",
        "name": "VolumeSlider",
        "parameters": { "minValue": 0, "maxValue": 100, "value": 80 }
      },
      {
        "brickId": "ugui.button",
        "name": "BackButton",
        "parameters": { "text": "返回" }
      }
    ]
  }
}
```

字段说明：
- `panelName`: 面板名称（用于 GameObject 名）
- `sortingOrder`: Canvas sortingOrder（UIRoot=0, fullscreen=10, popup=20, confirm=30）
- `referenceResolution`: `[width, height]` 设计分辨率
- `root`: 根 Brick 节点（通常是 `ugui.stack_vertical`）
- 每个 BrickNode:
  - `brickId`: 从 BrickIndex.ugui.json 查询
  - `name`: GameObject 名（供 SafeFind 查找，同一父级下必须唯一）
  - `parameters`: 参数覆盖（key 匹配 definition.json parameters；也接受 `params` 别名）
  - `slot`: 父 Brick 的具名挂载点名称（null=挂到父根；scroll_view 用 `Viewport/Content`，modal 用 `ModalPanel/ModalContentSlot` 等斜杠路径）
  - `children`: 子 Brick 列表（递归）

### 11.2 compositionPolicy 双向校验

`CanNest(parentBrickId, childBrickId, out reason)` 同时检查两侧（AND 语义，任一拒绝即拒绝）：

- **父侧** `allowedChildren`：父 Brick 接受哪些子 Brick（如 grid 仅接受 button/label/icon/sprite/panel）
- **子侧** `canNestUnder`：子 Brick 允许嵌在哪些父 Brick 下（如 modal 仅可嵌于 `ugui.panel` 下、list_item 仅限 scroll_view；树根不受 canNestUnder 约束——modal 可作独立弹窗树根、list_item 可作独立模板树根）
- 通配符：`ugui.*`（前缀匹配）、`ugui.layout.*`（匹配 layer=layout 的 Brick）、`any`/`*`（全部）、`none`（全部拒绝）

`AddBrick` / `AssemblePanel` / `BrickPanelBuilder.BuildBrickTree` 内部自动调用 CanNest；Runtime 组装违规时 `Debug.LogError` + 跳过该节点（含后代计数报告），BuildBrickTree 则写入 `result.warnings`。

辅助 API：

| API | 说明 |
|-----|------|
| `ValidateDescriptor(PanelDescriptor)` | **预检**全树（不创建 GameObject），返回违规描述列表（空 = 合法）。运行时组装前先调用可避免半成品 |
| `GetAllowedChildren(brickId)` | 父侧策略查询 |
| `GetCanNestUnder(brickId)` | 子侧策略查询 |
| `AssemblePanelDetailed(PanelDescriptor, parent)` | 返回 `PanelAssemblyResult { root, elementPaths, errors, Success }`。`elementPaths` 为 SafeFind 可用路径清单（相对面板根）；`errors` 为结构化错误列表。**运行时组装推荐入口** |
