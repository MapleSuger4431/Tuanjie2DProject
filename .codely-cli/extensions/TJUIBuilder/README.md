# TJUIBuilder

> Unity UI 界面构建扩展，为 Codely CLI 提供 AI 驱动的 UI 全流程构建能力。默认 UGUI，可选 UI Toolkit。

## 简介

TJUIBuilder 是一个 Codely CLI 扩展，通过 AI 自动化完成 Unity UI 界面的完整构建流程：从需求采集、设计文档生成，到面板构建（UGUI Tree Prefab 或 UI Toolkit UXML/USS）、AI 图片生成，再到风格参考提取和画板布局设计。

内置 `cn.tuanjie.create.ui` 运行时框架（含 UIBrick 组件库、UIV 校验工具、LayoutIntentBoard、UGUIPanelBase/UGUIBindingBuilder 等），开箱即用。

## 功能特性

- **UI 文档构建** — 从需求描述生成结构化的 `UI设计文档.md` 和 `image_requirements.json`
- **UI 面板构建** — 按 ui_mode 分流：UGUI 模式（默认）构建 Tree Prefab（panel 树 + list_item 模板树，根节点 brick 决定根结构）+ C# Panel 脚本（继承 UGUIPanelBase/UGUIGameControllerBase）；UI Toolkit 模式构建 UXML/USS + C# Panel 脚本（继承 UIPanelBase），基于 UIBrick 组件库；两种模式均配套生成 UIRoot/UIGameInstaller/PanelRegistry 集成资产
- **AI UI 图片生成** — 调用 AI 生成按钮背景、图标、弹窗背景、全屏背景等游戏 UI 图片
- **风格参考提取** — 从参考截图提取组件布局、配色、图生图配置（6 阶段流水线 + 可选布局校正 Stage 04.5）
- **AI 效果图生成** — 根据需求生成设计分辨率 UI 参考图，用户确认后移交组件提取
- **画板布局设计** — 通过 LayoutIntentBoard 可视化画板采集面板几何与层级关系（支持多界面切换设计）；也可为参考图提取提供布局校正（输出权威 layout_intent.json）

## 安装

```bash
codely extensions install < TJUIBuilder 仓库 URL >
```

安装后重启 Codely CLI 会话即可使用。

### AI 生成依赖：TJGenerators MCP Server

TJUIBuilder 的 AI 图片生成能力由 **TJGenerators MCP Server** 提供。生成阶段不经过 Unity Editor。

使用前确认 `.codely-cli/settings.json`（项目级）或 `~/.codely-cli/settings.json`（用户级）已配置：

```json
{
  "mcpServers": {
    "TJGenerators": { "httpUrl": "https://ai-generator.tuanjie.cn/mcp" }
  }
}
```

未配置时，执行生成任务前会提示补充配置并重启会话。`ui-style-reference` 模块使用 Seedream Pro，其余模块使用 Seedream Lite。

## 快速开始

在 Codely CLI 中执行：

```
/ui-builder 为我的游戏创建一个主菜单界面，包含开始游戏、设置、退出三个按钮
```

或直接描述需求，AI 会自动识别并激活技能：

```
帮我生成一个按钮背景图
按这张参考图做 UI
先看 UI 效果图
在画板上手动设计 UI
```

## 架构

```
TJUIBuilder/
├── commands/
│   └── ui-builder.toml          # /ui-builder 斜杠命令定义
├── docs/
│   └── ugui-test-design.html   # UGUI 测试方案文档
├── skills/
│   ├── unity-ui-builder/         # 核心技能（模块索引中心）
│   │   ├── SKILL.md              # 技能主文档（守卫规则、模块路由）
│   │   ├── modules/              # 六个子模块 + shared 共享契约
│   │   │   ├── ui-doc-builder/
│   │   │   ├── ui-panel-builder/
│   │   │   ├── ui-image-generator/
│   │   │   ├── ui-style-reference/
│   │   │   ├── ui-mockup-generator/
│   │   │   ├── ui-layout-intent-designer/
│   │   │   └── shared/               # 跨模块共享 references（MCP 图片生成、图片预览、image_requirements schema）
│   │   ├── references/           # 共享参考文档
│   │   └── scripts/              # 共享脚本
│   ├── csharp-code-writer/       # C# 代码编写技能
│   └── unity-operator/           # Unity Editor 写操作门禁技能
├── tools/
│   ├── md_table_parser.py        # Markdown 表格解析
│   ├── semantic_id.py            # 语义 ID 单一事实源
│   ├── scene_contract.py         # 场景契约校验
│   ├── ui_image_contract.py      # UI 图片契约校验
│   ├── ui_verify_resolver.py     # UI 校验解析器
│   ├── record_generator_result.py # Generator 结果检查点落盘
│   ├── record_ui_gate_receipt.py # UI 验证 Gate receipt 生成
│   ├── validate_*.py             # Gate/receipt 校验工具
│   ├── RoslynValidator/          # Roslyn 编译验证器
│   └── UIBuilderWorker/          # 构建工作目录（预留）
├── Packages/
│   └── cn.tuanjie.create.ui/     # Unity 运行时包（UI 框架 + UIBrick + 工具）
├── gemini-extension.json         # 扩展清单
└── manifest.json                 # Unity 包依赖声明
```

## 技能模块

| 模块 | 功能 | 触发场景 |
|------|------|---------|
| **ui-doc-builder** | 需求采集 → `UI需求.md` → `UI设计文档.md` + `image_requirements.json` | 首次建 UI、新增面板、需求整理 |
| **ui-panel-builder** | 根据 `UI设计文档.md` 和 `ui_mode` 构建 UGUI Tree Prefab（panel 树 + list_item 模板树）或 UI Toolkit UXML/USS + C# Panel 脚本（两阶段流水线，最终产出模块） | 创建界面、HUD、菜单、弹窗 |
| **ui-image-generator** | 从 `image_requirements.json` 驱动 AI 生成 UI 图片（3 阶段流水线，与 ui_mode 无关） | 生成按钮图片、背景图、图标 |
| **ui-style-reference** | 从参考截图提取布局与图生图配置（6 阶段流水线 + 可选布局校正 Stage 04.5） | 有参考图、按风格做 UI |
| **ui-mockup-generator** | AI 生成设计分辨率参考图 | 先看效果图、UI mockup |
| **ui-layout-intent-designer** | 通过 LayoutIntentBoard 采集面板几何与层级（画板手动设计/需求预生成 与 参考图布局校正 双路线） | 画板手动设计、布局意图、参考图布局校正 |

## 工作流

```
用户请求
  │
  ▼
ui-doc-builder Step 0（需求采集 + 视觉输入方式选择）
  │
  ├── 上传参考图 ──► ui-style-reference ──► UI参考图分析.md ──┐
  ├── AI 效果图 ───► ui-mockup-generator ─► ui-style-reference ─┤
  ├── 画板设计 ────► ui-layout-intent-designer ──► UI布局意图.md ─┤
  └── AI 直接规划 ──────────────────────────────────────────────┤
                                                                 ▼
                                                         ui-doc-builder
                                                   （生成 UI设计文档.md
                                                    + image_requirements.json）
                                                                 │
                                                                 ▼
                                                         ui-image-generator
                                                         （AI 生成 UI 图片，Stage 01-03）
                                                                 │
                                                                 ▼
                                                  ui-panel-builder（最终产出模块）
                                              （按 ui_mode 构建 UGUI Tree Prefab 或
                                               UXML/USS/Panel + UIRoot/UIGameInstaller/
                                               PanelRegistry 集成资产）
                                                                 │
                                                                 ▼
                                                        UI 资产产出完成
                                                 （场景搭建由宿主负责）
```

## 依赖

### 内置（随扩展提供）

- `cn.tuanjie.create.ui` — 运行时 UI 框架（UGUI + UI Toolkit 双模式）、UIBrick 组件库、UIV 校验工具、LayoutIntentBoard

### 按需配置

- **TJGenerators MCP Server** — AI 图片生成能力（settings.json 配置 MCP server，见上方「AI 生成依赖」）

### 按需

- **Python 3.8+** + Pillow + OpenCV + numpy — 仅 `ui-style-reference` 模块需要（首次使用时自动安装）
- **Python 3.x 标准库** — 仅 `ui-layout-intent-designer` 模块需要（系统自带，无需额外安装）

## 技术栈

- **UI 框架**：Unity UGUI（默认，Canvas + UGUIPanelBase）或 UI Toolkit（UXML / USS / UIDocument + UIPanelBase）
- **运行时框架**：Create.GameFramework（`UGUIPanelBase` / `UGUIGameControllerBase` + `UGUIBindingBuilder` / `UIPanelBase` / `GameUIControllerBase` + `BindingBuilder`）
- **组件库**：UIBrick
- **AI 图片生成**：TJGenerators MCP Server（`generate_image` / `file_upload` / `check_task`，Seedream Lite + Pro）
- **图像处理**：Python + OpenCV + Pillow
- **编辑器集成**：Unity Bridge（`execute_csharp_script`，资产导入与面板构建）

## 许可

© Tuanjie. All rights reserved.
