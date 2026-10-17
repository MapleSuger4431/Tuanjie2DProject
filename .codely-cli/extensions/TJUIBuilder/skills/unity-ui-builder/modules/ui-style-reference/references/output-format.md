# UI参考图分析.md 输出格式

> 本文件是 Stage 06 SKILL.md Step 10 的配套参考。以下格式在输出 `UI参考图分析.md` 时逐字执行。

## 组件裁剪映射表

每行一个节点（含 UI 组件节点和 text-label 文字标签节点）：

| 列 | 含义 |
|----|------|
| 节点 ID | 组件树节点标识 |
| 父组件 / 父 UXML 元素 | 父级语义名 / UXML 元素名；text-label 节点的父组件为其挂载到的 UI 组件 |
| 深度 | 根为 0 的树深度 |
| 渲染模式 | `direct` / `shell_background` / `text` |
| shell 路径 | 从面板根到节点的容器路径 |
| 组件名 | 语义分类产出（组件节点）或文字内容推导（text-label 节点） |
| display_text | 组件节点：图上可见文字（Step 6 语义分类产出）；text-label 节点：5.5.2 从原图读取的完整文字内容。无文字为空 |
| 组件类型 | 完整枚举见 Stage 02 SKILL.md Step 6 prompt 的 `component_type` 字段定义；text-label 节点固定为 `label` |
| text_type | `art_text` / `label` / `none`；text-label 节点固定为 `label` |
| art_text_style | 艺术字风格关键词（仅 `text_type=art_text`） |
| font_color | 文字主色调 HEX（仅 text-label 节点，`source=uss_only`），从原图提取 |
| font_size | 文字像素字号（仅 text-label 节点，`source=uss_only`），基于设计分辨率估算 |
| font_weight | `normal`/`bold`/`thin`（仅 text-label 节点，`source=uss_only`） |
| font_style | 特殊样式如 `italic`/`outline`/`shadow`（仅 text-label 节点，`source=uss_only`），无则空 |
| 裁剪图路径 | 透明 PNG 路径（相对 game context，解析时拼接 {projectRoot}/context/games/{GameName}/ 前缀）；`skip_generation`=纯文字；`text2img_fallback`=裁剪失败走文生图；`reuse_from:xxx`=引用代表组件 |
| source | `ai_generate` / `direct_use` / `uss_only` / `user_import` |
| quality | `clean` / `contaminated`（缺省视为 clean） |
| contamination_description | 仅 `quality=contaminated` 时存在 |
| functional_type | `slider`/`toggle`/`dropdown`/`text_field`/`progress_bar`/`other`/`—`（null 的表格表示） |
| 来源参考图编号 | 1, 2, ... |
| 保真度 | `exact` / `partial_override` |
| override_dimensions | 尺寸覆盖（保真度为 `partial_override` 时非空）：`{width, height}` 设计像素，生成图按此尺寸覆盖默认尺寸 |
| game_area_list | 面板的游戏内容区域组件名列表（文档级字段而非逐组件列），非游戏区面板为空 |
| 相对父 X%/Y%/宽%/高% | 相对直接父级的百分比（UXML/USS 布局唯一坐标来源） |
| 根 X%/Y%/宽%/高%（诊断） | 相对参考图根的诊断坐标，仅供 overlay/debug |
| style_group | 同风格组件组名（如 `button_style_1`），独立组件为 `—` |
| reuse_from | 非代表 leaf 复用的代表组件名 |
| exclude_overlays | 背景图上叠加的内容层组件名列表 |
| dynamic_content | 动态内容类型描述 |
| generation_hint | 图片生成排除动态内容提示 |

## 面板布局表

**面板布局表**必须分别保留**背景层**和**内容层**。复合父级为 shell/background，子组件为对应 content layer 的子元素。

## 文生图回退风格参数（条件章节）

仅当存在裁剪失败组件（`text2img_fallback`）或用户指定维度调整（`partial_override`）时才包含。`quality=contaminated`（leaf）不需要此章节——图生图以透明 PNG 为输入，风格由透明 PNG 决定。
