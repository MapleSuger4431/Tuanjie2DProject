# Layout Intent Schema

## 项目

| 字段 | 类型 | 说明 |
|---|---|---|
| `schema` | string | 数据源标识，当前为 `ui-layout-intent/v5` |
| `gameId` | string | 游戏标识 |
| `canvases` | array | 项目中的全部设计画布；数组顺序即顶部标签顺序 |
| `activeCanvasId` | string | 当前显示画布的 ID |
| `isConfirmed` | bool | 通过校验且持久化成功 |
| `lastModifiedUtc` | string | ISO-8601 UTC 时间 |

## 画布

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 8 位随机 ID，供 `activeCanvasId` 引用 |
| `uiViewId` | string | UI 视图唯一标识，在同一项目内不可重复 |
| `uiViewType` | string | `hud`、`menu`、`modal`、`game-over`、`settings` 或自定义 |
| `width`、`height` | int |
| `backgroundImageDescription` | string |
| `overallUiDescription` | string |
| `referenceImagePath` | string | 参考图校正模式的项目根相对原图路径；仅作为画板不可交互的底层参考图，不生成区域、不进入 component tree |
| `referenceImageVisible` | bool | 是否显示参考底图，默认 true |
| `referenceImageOpacity` | float | 参考底图透明度，范围 0.1..1，默认 0.35 |
| `layoutSource` | string | `reference_analysis` 表示由参考图分析生成初稿 |
| `referencePanel` | string | 对应 `UIReferences/Processing/{Panel}` 的稳定面板名；校正模式不可编辑，删除全部来源区域后仍用于定位 component tree |
| `referenceAnalysisHash` | string | 对应 `component-tree.json` 的 SHA-256 |
| `regions` | array |

## 区域

| 字段 | 默认值 | 下游用途 |
|---|---|---|
| `id` | 8 位随机 ID | 父子引用 |
| `name` | `未命名区域` | 下游元素命名候选 |
| `description` | 空 | 功能说明 |
| `rect` | `0,0,100,100` | 画布全局像素坐标（根区域和子区域均使用画布全局坐标，非相对父级偏移） |
| `parentId` | 空 | 引用父区域 ID；空表示根级 |
| `visible` | true | 设计时显隐 |
| `locked` | false | 是否禁止移动和缩放 |
| `overlayColor` | 调色板自动分配 | 画板框选范围颜色；仅画板显示，不参与下游映射 |
| `componentType` | `custom` | 下游组件映射 |
| `sourceReferenceNodeId` | 空 | 参考图校正模式中对应的组件树节点 ID；用户新建/复制区域为空 |
| `sourceReferencePanel` | 空 | 来源参考面板/图片名 |
| `previewImagePath` | 空 | 对应组件裁剪图的项目根相对路径；仅供画板框内预览，缺失时回退为纯色区域 |

`regions` 数组顺序就是区域树中从上到下的显示顺序（数组第一个 = 树顶，最后一个 = 树底），不再单独保存 `order`。在画布渲染中，数组越靠前的区域越先绘制（越靠底层），越靠后的越后绘制（越靠上层）。执行层级拖放、父级修改或子树复制后，数组规范化为父节点优先的深度优先顺序，每棵子树连续；同级区域在数组中的先后顺序就是树顺序和绘制顺序。旧数据在第一次层级编辑前沿用原数组顺序解释。

`id`、`parentId`、`uiViewId` 和画布宽高是持久化协议字段。已有画布只读展示 `uiViewId` 和设计尺寸；“+ 新增界面”创建画布时输入一次唯一 `uiViewId`，尺寸沿用当前画布。通过组件类型与父级区域下拉列表维护语义，不要求用户输入其它内部 ID。

组件类型下拉优先写入 `custom`、`container`、`background` 或标准控件类型名（如 `Button`、`Label`、`Image`、`TextField`、`ListView`、`ScrollView`、`grid`、`Slider`、`ProgressBar`、`Toggle`）；旧数据中的其它值保持原样，显示为兼容类型。

`overlayColor` 是画板为新创建区域自动分配的颜色，取自内置调色板（按区域创建顺序循环取色），格式为十六进制颜色字符串（如 `#4FC3F7`）。agent 预生成 JSON 时不需要填写此字段——画板加载时会为缺失 `overlayColor` 的区域自动分配。存在有效 `previewImagePath` 时，区域内部显示裁剪图片并只保留彩色边框；图片缺失、纯文字节点或用户新建区域继续使用纯色填充。该字段仅用于画板中区分不同区域，不影响下游界面生成与组件映射。

`previewImagePath` 由参考图转换脚本根据节点产物确定：普通组件使用 `final_transparent.png`，复合外壳优先使用 `shell_transparent.png`，`reuse_from:*` 节点复用代表节点图片。纹理由画布统一缓存和释放，区域移动、缩放以及父级缩放预览时，预览图随区域几何同步变换。该字段不写入 `UI布局意图.md`，也不作为最终 UI 资产来源。

父子区域仍统一保存画布全局像素 `rect`，子区域 `rect` 不是相对父级的偏移坐标，而是子区域在画布中的绝对位置。例如：父区域 `rect` 为 `{x:1000, y:130, width:840, height:480}`，子区域在父区域内偏移 `(30, 20)`，则子区域 `rect` 应写 `{x:1030, y:150, width:300, height:40}` 而非 `{x:30, y:20, width:300, height:40}`。编辑行为具有结构语义：

- 移动父级时平移整棵子树。
- 缩放父级时按父级旧/新矩形对全部后代做相同比例变换。
- 子级的直接移动和缩放受父级边界约束。
- 在树中改变父级时保留整棵子树尺寸，仅做满足新父级包含关系的最小平移；放不下时拒绝操作。
- 删除父级时删除整棵子树，复制父级时复制整棵子树并重建内部 ID 引用；两者均可作为单次操作撤销。
- `visible` 和 `locked` 保存本地值。父级隐藏会使后代有效隐藏，父级锁定会使后代有效锁定，但不会改写后代字段。
- 新框选区域的 `parentId` 默认为空；父级只由用户通过区域树拖放或父级下拉明确指定。
- 最大安全深度为 64；缺失父级或循环引用可以保存在未确认草稿中以供修复，但不能进入正式确认结果。

## 可读摘要生成

`LayoutIntent/layout_intent.json` 是用户布局权威。路线 A 必须通过部署型工具 `Create.GameFramework.TJLayoutMarkdown.Run("{GameName}")`（包 `Editor/UIBuildTools`，`execute_csharp_script` 一行调用）生成非权威但可供 `ui-doc-builder` 读取的 `UI布局意图.md`；路线 B 直接把正式 JSON 返回调用方，Markdown 仅按需预览：

- 根区域百分比：`x/画布宽`、`y/画布高`、`width/画布宽`、`height/画布高`。
- 子区域相对父级百分比：`(child.x-parent.x)/parent.width`、`(child.y-parent.y)/parent.height`、`child.width/parent.width`、`child.height/parent.height`。
- 区域深度：沿 `parentId` 链向上计数，根级为 0。

这些值在 resolver 或最终构建时从权威像素框计算，不回写正式项目 JSON。Markdown 不再是数据接口。

## 编辑期草稿

`layout_intent.draft.json` 与版本文件沿用项目 schema，但保存时强制 `isConfirmed=false`。`drafts/index.json` 使用独立的 manifest schema：

- `schema`：当前为 `ui-layout-intent-draft-manifest/v1`。
- `gameId`：草稿所属游戏。
- `nextRevision`：下一次保存检查点使用的递增版本号。
- `versions`：最近 5 个版本；每项包含 `revision`、`savedUtc` 和不可变 `assetPath`。

版本内容先写入，manifest 最后提交。恢复草稿不得直接修改正式 `layout_intent.json`。

草稿不按时间或操作次数保存。保存检查点仅由“保存草稿”创建；关闭画板（窗口“×”或关闭弹框的“保存/不保存”）不触发任何草稿写入。没有未保存修改时跳过写入。下拉框切换草稿版本只读取所选版本，不先保存当前修改。
