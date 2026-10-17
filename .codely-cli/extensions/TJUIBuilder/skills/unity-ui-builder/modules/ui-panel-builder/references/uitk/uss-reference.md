# USS 编码参考

> unity-ui-builder SKILL.md 配套参考，USS 编写核心规则。

## 1. 禁止使用的属性和伪类

**禁止属性**（Unity 2022.3 不支持，写入后报错或不生效）：
`text-shadow`、`box-shadow`、`transform`、`rotate`、`scale`、`translate`、`picking-mode`（以上属性在 Tuanjie 2022.3 USS 中不被解析，不生效；旋转/缩放/点击穿透须在 C# 中设置：`element.transform.rotation`、`element.transform.scale`、`element.pickingMode`）、`font-family`（用 `-unity-font-definition` 替代）、`animation`、`filter`、`backdrop-filter`、`outline`、`float`、`z-index`、`line-height`、`pointer-events`、`user-select`、`cursor: default`（用 `arrow`）、`cursor: pointer`（用 `link`）、`align-items: baseline`（用 `center`）、`text-align`（用 `-unity-text-align` 替代，值见 §7 映射表）、`white-space`（用 `-unity-white-space` 替代）

**禁止伪类**：`:last-child`、`:first-child`、`:nth-child` → 用手动添加 class 替代

**禁止选择器**：`[name="..."]`、`[class="..."]` 等属性选择器（Unity USS 不支持，静默无效）→ 用独立 class 替代（如 `.btn-resume { top: 23.21%; }` 而非 `.pause-btn[name="btn-resume"] { top: 23.21%; }`）

## 2. 路径规则（USS `url()` 和 UXML `src` 统一）

两者都是**相对于文件自身位置**解析，不是从项目根目录：

| 场景 | ✅ 正确 | ❌ 错误 |
|------|--------|--------|
| USS 引图片 | `url("Images/xxx.png")` | `url("Assets/Games/.../Images/xxx.png")` |
| UXML 引 Brick USS | `src="project:Packages/cn.tuanjie.create.ui/Runtime/UIBrick/USS/Primitive/Input/ButtonBase.uss"` | `src="Assets/UIBrick/USS/..."` |
| UXML 引同目录文件 | `src="MainMenuPanel.uss"` | `src="Assets/Games/MyGame/UI/MainMenuPanel.uss"` |

⚠️ `Assets/...` 开头的路径会被当作相对路径，拼接为 `{文件所在目录}/Assets/...`，导致路径不存在。Brick USS/UXML 位于 Package 内，必须用 `project:` 前缀引用（从项目根解析）。面板私有 USS/UXML 用相对路径（同目录直接用文件名）。

## 3. `background-image` 相关规则

### 3.1 必须清除默认背景色

UI Toolkit 的 Button 元素有默认灰色 `background-color`。所有使用 `background-image` 的元素必须设 `background-color: rgba(0, 0, 0, 0)`，否则默认灰色会透过图片透明区域。

```css
/* ✅ 正确 */     .my-btn { background-color: rgba(0,0,0,0); background-image: url("Images/btn.png"); border-width: 0; }
/* ❌ 错误 */     .my-btn { background-image: url("Images/btn.png"); border-width: 0; }  /* 灰色透过 */
```

### 3.2 按钮交互状态必须重申透明背景

`br-button-base:hover` 设了灰色背景 `rgb(92, 104, 126)`。即使默认状态已清除背景色，`:hover` 状态仍会触发 Brick 灰色。**游戏 USS 中按钮的 `:hover` 和 `:active` 必须显式重设 `background-color: rgba(0, 0, 0, 0)`**：

```css
.my-btn { background-color: rgba(0,0,0,0); background-image: url("Images/btn.png"); -unity-background-scale-mode: scale-to-fit; border-width: 0; }
.my-btn:hover  { background-color: rgba(0,0,0,0); opacity: 0.9; }   /* ← 必须！scale 在 USS 中静默忽略，如需缩放效果在 C# 中设 element.transform.scale */
.my-btn:active { opacity: 0.8; }   /* ← 必须！同上 */
```

### 3.3 `-unity-background-scale-mode` 选择

| 值 | 等效 | 适用场景 |
|----|------|---------|
| `scale-to-fit` | `contain` | **按钮、图标、面板/卡片背景**（等比、不变形） |
| `scale-and-crop` | `cover` | 全屏背景（铺满，允许裁边） |
| `stretch-to-fill` | `100% 100%` | 仅限"图案本就该随元素任意拉伸"的纯色/渐变/无定形纹理 |

⚠️ 此属性优先级高于 `background-size`。
- **按钮/图标一律 `scale-to-fit`**：按钮图宽高比很少与元素框完全一致，`stretch-to-fill` 会把图案横向拉扁/纵向压变形。等比缩放才不变形。
- **前提：按钮/图标/HUD 条等 UI 图必须先裁掉外圈透明 padding**（AI 生成的 UI 图常只占画布一小块、四周大片透明 → 图案缩在元素一角、与文字/布局错位）。裁到紧贴不透明内容后，`scale-to-fit` 才能让图案填满元素框。
- 全屏背景用 `scale-and-crop`（铺满无空白，边缘裁切可接受）；重要内容须留在图片安全区（居中），避免被裁。

## 4. HUD 叠加面板规则

HUD 面板叠加在游戏画面之上，游戏内容由 Camera 渲染，UI 层只是浮动控件。

- **根容器**：`background-color: rgba(0, 0, 0, 0)`，禁止全屏 `background-image`
- **游戏内容区域**（`game_area_list`）：`background-color: rgba(0, 0, 0, 0)`，仅保留边框
- **浮动控件**（按钮、分数面板等）：可有局部不透明背景
- **判断标准**：面板需要显示 Camera 渲染的游戏对象？→ 必须透明。主菜单无需 → 全屏背景图正确

```css
/* ✅ 正确：HUD 根容器透明 */
.{GameName}-hud-root { position: absolute; left:0; top:0; right:0; bottom:0; background-color: rgba(0,0,0,0); }
.{GameName}-game-area { background-color: rgba(0,0,0,0); border-width: 4px; border-radius: 12px; border-color: rgb(0,137,123); }

/* ❌ 错误：全屏背景图遮挡 Camera 渲染的游戏画面 */
.{GameName}-hud-root { background-image: url("Images/ingame_background.png"); -unity-background-scale-mode: scale-and-crop; }
```

## 5. 纯色面板/容器的 USS 写法（仅开发者明确要求时适用）

> ⚠️ **禁止自行判定"简约风格=纯USS"跳过图片生成**（ui-panel-builder 核心规则 6 为权威源）。以下写法仅在开发者明确要求纯 USS 时使用。

当开发者明确要求纯 USS 时，纯色、渐变、简单圆角矩形面板用 USS 实现，不图生图。原因：图生图 AI 倾向"补完"内容（加角色、加图标），与"空面板"意图冲突。

适用条件（全满足方可）：纯色/渐变背景 + 纯色边框 + 无装饰纹理

```css
/* 实心面板 */
.xxx-panel { background-color: rgb(27, 94, 32); border-width: 2px; border-radius: 16px; border-color: rgb(76, 175, 80); flex-direction: column; align-items: center; }
/* 游戏内容区域（透明 + 边框） */
.xxx-game-area { background-color: rgba(0,0,0,0); border-width: 4px; border-radius: 12px; border-color: rgb(0, 137, 123); }
```

图生图仍适用：面板有复杂纹理（木栅栏、草地纹理等）且 USS 无法表达时。

## 6. D-Pad 共享图片旋转

多个方向按钮共享同一张箭头图片时，**在 C# Panel 脚本的 `OnBind()` 中设置旋转**（USS `transform`/`rotate` 在 Tuanjie 2022.3 不支持）：

```csharp
// HUDPanel.cs OnBind() 示例
SafeQ<VisualElement>("up-btn").transform.rotation = Quaternion.Euler(0, 0, 270);    // 或 -90
SafeQ<VisualElement>("down-btn").transform.rotation = Quaternion.Euler(0, 0, 90);
SafeQ<VisualElement>("left-btn").transform.rotation = Quaternion.Euler(0, 0, 180);
// right-btn 不旋转
```

⚠️ **图片已含箭头时，UXML `text` 必须留空**（`text=""`），否则 Unicode 箭头字符叠加产生双重箭头。仅当图片为无箭头纯色基底时才用 `text` 显示方向文字。

## 7. 其他规则

- 颜色：`rgb()`/`rgba()` 或十六进制
- 字体大小、`border-width`、`border-radius`、`transition` 数值：可用 `px`
- 布局定位属性（`left`/`top`/`width`/`height`/`padding`/`margin`）：**必须用百分比 %**，禁止硬编码 px。用户上传 OpenCV 组件树的子元素只使用相对父级 X/Y/W/H%；根 X/Y/W/H% 仅供 overlay/debug 诊断，不可作为子元素布局输入。
- 复合节点采用 parent container + shell/background + `padding: 0` content layer；常规 Label 位于 content layer，不重复绘制到图片资产。艺术字仍遵循现有 plate/text 双层约定。
- Unity 特有属性加 `-unity-` 前缀（如 `-unity-font-style`、`-unity-text-outline-width`）
- **自定义字体**：禁止 `font-family`，使用 `-unity-font-definition` 引用 FontAsset：

```css
/* ✅ 正确：通过 -unity-font-definition 引用 FontAsset */
.title-label { -unity-font-definition: url("Fonts/GameTitle-Bold.ttf"); -unity-font-style: bold; }
/* ❌ 错误：font-family 在 Unity 2022.3 USS 中不支持 */
.title-label { font-family: "GameTitle-Bold"; }
```

  > `-unity-font-definition` 的 `url()` 路径相对于 USS 文件自身位置，规则同 §2。FontAsset 需提前在 Unity 中导入并生成 `.asset` 文件。仅用内置字体时无需设置此属性。
- 弹窗面板/按钮背景图：用 `-unity-background-scale-mode: scale-to-fit`（等比，见 §3.3），图先裁掉透明 padding；**元素宽高比应匹配图片宽高比**（否则等比缩放后元素内会留空白边）；`padding ≥ 装饰边框像素宽度`
- 按钮文字字号 ≈ 内容区宽度 ÷ 最长文本字符数 × 0.6~0.7

### CSS→Unity 属性映射（常见易错）

以下标准 CSS 属性在 Unity USS 中不被解析，必须用 `-unity-` 前缀版本：

| 标准 CSS | Unity USS | 合法值 |
|---------|-----------|--------|
| `text-align` | `-unity-text-align` | `middle-left` `middle-center` `middle-right` `upper-left` `upper-center` `upper-right` `lower-left` `lower-center` `lower-right` |
| `font-style` | `-unity-font-style` | `normal` `bold` `italic` `bold-and-italic` |
| `white-space` | `-unity-white-space` | `normal` `nowrap` |

⚠️ `text-align: center` 在 USS 中静默无效（不报错但不生效），必须写 `-unity-text-align: middle-center`。注意值不是 `center` 而是 `middle-center`。

## 8. UI 设计文档处理规则

当 UI设计文档.md 包含 §12 图生图配置章节时，builder 步骤 1 读取并按以下规则处理。

### 核心参数

| 参数 | 来源章节 | 用途 |
|------|---------|------|
| `布局参数` + `面板布局表` | UI设计文档.md §3.1 + §3.2 | UXML 布局、USS 尺寸 |
| `component_crops`（含 `保真度`、`ref_image_index`、`x_pct`/`y_pct`/`w_pct`/`h_pct`、`style_group`、`reuse_from`、`exclude_overlays`、`dynamic_content`） | UI设计文档.md §12.2 组件裁剪映射表 | 步骤 3 传给 ui-image-generator（含 per-component 保真度）；步骤 6 UXML 编写（位置尺寸百分比） |
| 文生图回退风格参数 | UI设计文档.md §13（条件存在） | ① 文生图回退 prompt ② USS 配色/圆角/阴影参考 |

### 组件类型对步骤 6 UXML 的影响

| 组件类型标记 | UXML 处理 |
|-------------|----------|
| `game_area_list` | 使用空 VisualElement（仅边框/背景色/圆角），不引用图片，运行时由 Camera 渲染 |
| `crop_path == "text2img_fallback"` | 步骤 3 中由 ui-image-generator 走文生图生成纯背景（无可用参考图） |
| `quality == "contaminated"`（leaf） | 步骤 3 中由 ui-image-generator 走图生图移除污染内容（以透明 PNG 为输入，prompt 基于 `contamination_description` 构建） |

### style_guidance 在图生图模式下的作用

当存在参考图时，ui-image-generator 的图生图主路径**不使用** `style_guidance`（风格由参考图决定），`style_guidance` 仅服务于裁剪失败组件的文生图回退 prompt。但 builder 自身仍需 `style_guidance` 中的配色/圆角/阴影参数来编写 USS 样式。

## 9. OpenCV 组件树容器样式

用户上传参考图的 OpenCV 组件树需要三个标准 USS class 来构建嵌套容器。这些 class 定义在面板级共享 USS 文件中（与面板 USS 同目录）。

### 最小样式定义

```css
.ref-composite {
    position: absolute;
}

.ref-shell {
    position: absolute;
    left: 0;
    top: 0;
    width: 100%;
    height: 100%;
}

.ref-content {
    position: absolute;
    left: 0;
    top: 0;
    width: 100%;
    height: 100%;
    padding: 0;
}
```

### 使用规则

- `ref-composite`：复合节点的最外层容器，使用 `position: absolute` 以支持相对父级百分比定位。
- `ref-shell`：外壳/背景层，铺满父容器（`width/height: 100%`），通过 `background-image` 引用外壳透明 PNG。
- `ref-content`：内容层，铺满父容器且 `padding: 0`，确保子元素的 `left/top/width/height` 百分比直接对应 OpenCV 相对父级坐标。
- 子元素在 `ref-content` 内使用 `position: absolute` + `left/top/width/height` 百分比定位，值来自组件树的 `relative_parent_x_pct` 等字段。
- `ref-content` 与 `ref-shell` 同级嵌套在 `ref-composite` 内，`ref-shell` 在前（背景层），`ref-content` 在后（内容层）。

### 九宫格（外壳图边框保护）

外壳图（`render_mode=shell_background`）有边框/圆角，直接 `stretch-to-fill` 会拉伸边角变形。必须设置九宫格：

1. **TextureImporter 设置 `spriteBorder`**：`Vector4(left, bottom, right, top)`，值为从图片边缘到圆角/边框结束处的像素数。左右 border 必须完全覆盖圆角区域（圆角通常占图片宽度的 10%-15%），不能只测边框线条宽度。
2. **USS 用 `stretch-to-fill`**：Sprite border 设置后九宫格自动生效——四角不缩放、四边单方向缩放、中心双方向缩放。
3. **UI Toolkit 不支持 USS 的 `border-image-slice`**，九宫格只能通过 Sprite Importer 的 `spriteBorder` 设置。
4. 若外壳图无圆角/边框（纯色底板），不需要九宫格，直接 `stretch-to-fill` 即可。

## 10. 功能控件自定义样式（重要）

⚠️ 当面板含 Slider/Toggle/ProgressBar/DropdownField 功能控件时，读 `references/uitk/functional-control-styles.md` 对应控件章节获取内部元素结构、USS 选择器、C# 同步代码模板和 Tuanjie 2022.3 类名 gotcha。文件按 `##` 标题分章，按需读取。
