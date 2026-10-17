# UXML Brick 组合规则与复合部件模式

> 本文件是 ui-panel-builder SKILL.md 步骤 6 的配套参考，包含 Brick 组合的通用规则、复合部件 UXML 实现模式和填充元素尺寸规则。步骤 6 编写 UXML 时按需读取。

## 通用规则（两种引用方式均适用）

1. **Brick 组合约束校验**：组合 Brick 前必须检查步骤 1 读取的 `definition.json` 中的 `compositionPolicy`：
   - `allowedChildren`：声明该 Brick 可以包含哪些子 Brick（如 `"none"` = 不允许子元素，`["layout.*", "primitive.*"]` = 允许 Layout 和 Primitive 层 Brick）
   - `canNestUnder`：声明该 Brick 可以嵌套在哪些父 Brick 下（如 `"none"` = 不可嵌套，`["layout.*"]` = 仅可嵌套在 Layout 层下）
   - 违反约束会导致 UXML 结构无效，运行时元素不可见或布局错误
   - **Brick 不支持嵌套时的降级策略**：若 Brick 的 `allowedChildren` 不允许子元素或 `canNestUnder` 不允许当前父级，且组件树要求嵌套，按以下顺序降级：①优先查找支持嵌套的替代 Brick（同类别、`allowedChildren` 允许子元素的 Brick）；②无替代时跳过 Brick，使用原生 `ui:VisualElement` 容器并手写 USS 复刻 Brick 视觉（`background-image`、`border-radius` 等）；③仍无法满足时停止并报告，不静默降级。不允许嵌套的 Brick 外包一层兼容容器，不强行把子项塞进不可嵌套 Brick。
2. **布局契约参考**：`layoutContract.self.widthMode` / `heightMode` 声明 Brick 的尺寸模式（如 `["fill"]` = 撑满父容器，`["hug"]` = 包裹内容），用于预估 UXML 布局是否合理
3. **Button text 属性**：从 UI设计文档.md §11 的 `显示文字` 列（或 §12.2 的 `display_text` 列）读取每个按钮的实际显示文字，写入 UXML 的 `text` 属性。有文字的按钮（如"本地模式"）必须设置 `text="本地模式"`；图标按钮（`display_text` 为空）设置 `text=""`。禁止留空需要显示文字的按钮——文字由 UXML `text` 属性显示（图片不含文字，见 `modules/ui-image-generator` 核心规则2）
4. **纯文字标签渲染**：UI设计文档.md §12.2 中 `裁剪图路径` 为 `skip_generation` 的组件（`componentType: Label`），在 UXML 中渲染为 `<ui:Label text="{display_text}" />`，**不是** `<ui:VisualElement>` + 背景图。这些组件的位置尺寸从 §12.2 的 X%/Y%/宽%/高% 列读取，写入 USS 定位样式。Label 的文字样式（颜色、字号、对齐）由 USS 定义
5. **艺术字底板+文字拆分行**：UI设计文档.md §12.2 中 `text_type == art_text` 的组件可能拆为两行——`{组件名}_plate`（底板，`source=direct_use`）和 `{组件名}_text`（艺术字，`source=ai_generate`）。两行共享相同的布局位置和尺寸。在 UXML 中：
   - 底板行（`_plate`）：创建一个 `<ui:VisualElement>`（或 `<ui:Button>`），USS `background-image` 指向底板 PNG
   - 艺术字行（`_text`）：在底板元素**内部嵌套**一个 `<ui:VisualElement>` 子元素，USS `background-image` 指向艺术字 PNG
   - **USS 必须项**：文字层 `position: absolute; left:5%; top:5%; width:90%; height:90%`（略小于父元素，留出内边距防止文字贴边/超出底板边缘）；`background-color: rgba(0,0,0,0)`（透明底色）；`-unity-background-scale-mode: scale-to-fit`（等比缩放居中在底板上）
   - **多行艺术字**：若 `display_text` 含 `\n`（多行文字），AI 生成的艺术字 PNG 宽高比会与单行不同（更接近正方形）。`scale-to-fit` 会自动等比缩放适配底板，无需额外处理。但需注意底板宽高比与文字宽高比差异较大时，文字会缩小居中——这是预期行为，确保文字完整不裁切
   - **Button 专用（仅 `text_type=art_text`）**：当按钮的 `text_type == art_text` 时，文字已烤入艺术字图片，UXML `text` 属性必须置空（`text=""`）且 USS 设 `color: rgba(0,0,0,0); font-size: 0`（避免默认文字样式与图片文字重叠）。艺术字子元素是普通 `VisualElement`，默认不拦截点击，无需额外设置 pickingMode
   - ⚠️ **`text_type=label` 的按钮禁止隐藏文字**：文字用 UXML `text` 显示；USS 的 `font_weight` 写入 `-unity-font-style`（默认 `bold`），并设置可见 `color`、`font-size` 与 `-unity-text-align`。禁止透明文字或 `font-size: 0`。
   - 若只有 `_text` 行无 `_plate` 行（优先级 1b，无底板艺术字）：整体创建一个 `<ui:VisualElement>`，USS `background-image` 指向艺术字 PNG 即可，无嵌套
   - ⚠️ **无参考图路径（无 §12.2）时的 `art_text` 处理**：UI设计文档.md §11 元素列表的 `text_type` 列标注为 `art_text` 的元素，文字已烘焙进图片（如艺术字标题），UXML 必须用 `<ui:VisualElement>` + USS `background-image`，**禁止**用 `<ui:Label text="{显示文字}">`（会导致文字重叠）。只有 `_text` 行无 `_plate` 行时同理：整体创建 `<ui:VisualElement>`，USS `background-image` 指向艺术字 PNG。`text_type` 列在参考图路线从 §12.2 读取，AI 直接规划路线由 ui-doc-builder 根据图片是否含文字自行标注
6. **图片路径映射**：根据步骤 1 Brick 映射表中的 `assetSlots↔image_requirements` 关联，确定每个 Brick asset 槽位对应的实际图片路径。图片路径在步骤 7 中写入游戏 USS（`background-image: url("Images/xxx.png")`），不在 UXML 中内联设置。禁止通过文件名关键词猜测对应关系——**直接查映射表**
7. 对砖块的 `stylePropsSchema` 中声明的变体（如 `size: md`、`state: disabled`），在元素上添加对应的 CSS class（映射规则：`br-{brick}--{value}`，如 `br-button-base--md`、`br-button-base--disabled`）
8. 对砖块的 `slots`（如 `layout.modal.shell` 的内容区），将子砖块放在对应的 slot 容器内

## OpenCV 组件树的容器结构

参考图的 OpenCV 组件树进入 UI设计文档后，按节点的 `parent_component` / `parent_uxml_element`、`depth`、`render_mode` 与 `shell_path` 组合。

1. 每个复合节点写为父容器，内部固定分为 shell/background 与零 padding 的 content layer。复合容器使用 `ref-composite` class，外壳使用 `ref-shell` class，内容层使用 `ref-content` class（这三个 class 的最小样式定义见 `uss-reference.md`「OpenCV 组件树容器样式」章节）：
   ```xml
   <ui:VisualElement name="menu-shell" class="ref-composite">
       <ui:VisualElement name="menu-shell-background" class="ref-shell" />
       <ui:VisualElement name="menu-content" class="ref-content">
           <!-- children use left/top/width/height relative to menu-content -->
       </ui:VisualElement>
   </ui:VisualElement>
   ```
2. `ref-content`（内容层）保持零 padding、与父级同尺寸，使 OpenCV 相对父级百分比能直接用于子项 `left/top/width/height`。正常 `Label`、按钮和其他交互子项属于 content layer，不得同时烘焙进图片资产。
3. 子元素的 absolute `left`、`top`、`width`、`height` 必须使用相对父级百分比。根 X/Y/W/H 百分比只供 overlay/debug 诊断，禁止用于子元素布局。
4. `render_mode` 为图片时，shell/background 引用图片；普通文字仍用 UXML Label。`text_type == art_text` 按 §3.1 艺术字拆分规则执行：底板作为 `ref-shell` 背景层，艺术字 PNG 作为内容层叠加在底板上方。

## 复合部件的 UXML 实现模式

- **复合部件的 UI设计文档.md 标注规则以 `ui-doc-builder` 产出的 `UI设计文档.md` 为权威定义源**，本步骤按文档中的 `component_type` 标注实现 UXML 结构
- 当使用 Primitive Brick（`primitive.input.progress_bar`/`slider_base`/`toggle_base`）时，使用 Brick 定义的原生 UI Toolkit 元素（`<ui:ProgressBar>`/`<ui:Slider>`/`<ui:Toggle>`）
- 当需要自定义美术背景图（无对应 Brick）时，手写 UXML 结构，将静态框架图和动态内容分离：通过 `background-image` 设置静态图片，动态子元素通过 `position: absolute` 叠加

## 复合部件填充元素尺寸规则

- 填充元素必须设置显式 `height`
- 框架图容器 `padding` 必须覆盖视觉边框
- 填充宽度计算公式：`fill_width = frame_width - left_padding - right_padding - danger_width`
- 使用 `flex-shrink: 0` 和 `align-items: stretch`
- 统一垂直对齐方式
