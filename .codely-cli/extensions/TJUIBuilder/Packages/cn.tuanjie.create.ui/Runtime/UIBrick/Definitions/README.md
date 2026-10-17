# Brick Index — v2

这个目录存放 **正式 Brick 定义**（v2 schema）。

## 入口

- `BrickIndex.json`：机器可读索引，包含所有 Brick 的 brickId、category、路径和 AI 优先级

## v2 核心规则

1. **每个 Brick 必须有独立 definition + UXML + USS**
2. **引用组件，不复制结构**：页面通过 `project:` 前缀引用 Package 内的 Brick 组件——简单 Brick 通过 `<ui:Style>` 引用 Brick USS + 内联元素使用 `br-*` 标准类名；复杂 Brick 通过 `<ui:Template>` + `<ui:Instance>` 引用 Brick UXML（Brick UXML 自包含 USS，实例化时自动加载样式）。不复制 Brick UXML 结构到游戏页面
3. **class 名约定**：`br-{brick}--{value}` 映射规则统一使用
4. **两层模型**：Primitive / Layout
5. **参数模型**：`assetPropsSchema` + `stylePropsSchema`（含 state）+ `bindingPropsSchema`（解耦）

## USS 尺寸约定

- **Brick USS 中的 px 值为组件级参考默认值**（如卡片宽度、格子尺寸、内边距），保证组件自身视觉一致性
- **游戏 USS 必须覆盖布局定位属性**（`left`/`top`/`width`/`height`）为百分比，以适配不同分辨率
- **Brick USS 中的 `border-width`/`border-radius`/`font-size` 可保留 px**（不涉及布局适配）
- 若 Brick 的固定宽度与游戏布局冲突，游戏 USS 通过 `flex-grow`/`width: 100%` 等覆盖

---

## 当前已落地 Brick（15 个）

### Primitive / Text

#### `primitive.text.label`
- 作用：通用文本标签
- 定义：`Primitive/Text/primitive.text.label.definition.json`
- UXML：`UXML/Primitive/Text/Label.uxml`
- USS：`USS/Primitive/Text/Label.uss`

#### `primitive.text.value_text`
- 作用：数值型文本（分数、数量等）
- 定义：`Primitive/Text/primitive.text.value_text.definition.json`
- UXML：`UXML/Primitive/Text/ValueText.uxml`
- USS：`USS/Primitive/Text/ValueText.uss`

### Primitive / Image

#### `primitive.image.icon`
- 作用：小图标容器
- 定义：`Primitive/Image/primitive.image.icon.definition.json`
- UXML：`UXML/Primitive/Image/Icon.uxml`
- USS：`USS/Primitive/Image/Icon.uss`

#### `primitive.image.sprite`
- 作用：图片/精灵容器
- 定义：`Primitive/Image/primitive.image.sprite.definition.json`
- UXML：`UXML/Primitive/Image/Sprite.uxml`
- USS：`USS/Primitive/Image/Sprite.uss`

### Primitive / Input

#### `primitive.input.button_base`
- 作用：通用按钮（含变体、尺寸、状态）
- 定义：`Primitive/Input/primitive.input.button_base.definition.json`
- UXML：`UXML/Primitive/Input/ButtonBase.uxml`
- USS：`USS/Primitive/Input/ButtonBase.uss`

#### `primitive.input.toggle_base`
- 作用：基础开关底座
- 定义：`Primitive/Input/primitive.input.toggle_base.definition.json`
- UXML：`UXML/Primitive/Input/ToggleBase.uxml`
- USS：`USS/Primitive/Input/ToggleBase.uss`

#### `primitive.input.slider_base`
- 作用：基础滑块底座
- 定义：`Primitive/Input/primitive.input.slider_base.definition.json`
- UXML：`UXML/Primitive/Input/SliderBase.uxml`
- USS：`USS/Primitive/Input/SliderBase.uss`

#### `primitive.input.progress_bar`
- 作用：基础进度条
- 定义：`Primitive/Input/primitive.input.progress_bar.definition.json`
- UXML：`UXML/Primitive/Input/ProgressBar.uxml`
- USS：`USS/Primitive/Input/ProgressBar.uss`

### Primitive / Container

#### `primitive.container.surface`
- 作用：通用表面容器
- 定义：`Primitive/Container/primitive.container.surface.definition.json`
- UXML：`UXML/Primitive/Container/Surface.uxml`
- USS：`USS/Primitive/Container/Surface.uss`

### Layout / Stack

#### `layout.stack.vertical`
- 作用：垂直堆叠布局
- 定义：`Layout/Stack/layout.stack.vertical.definition.json`
- UXML：`UXML/Layout/Stack/StackVertical.uxml`
- USS：`USS/Layout/Stack/StackVertical.uss`

#### `layout.stack.horizontal`
- 作用：水平堆叠布局
- 定义：`Layout/Stack/layout.stack.horizontal.definition.json`
- UXML：`UXML/Layout/Stack/StackHorizontal.uxml`
- USS：`USS/Layout/Stack/StackHorizontal.uss`

### Layout / Grid

#### `layout.grid.basic`
- 作用：基础网格布局
- 定义：`Layout/Grid/layout.grid.basic.definition.json`
- UXML：`UXML/Layout/Grid/GridBasic.uxml`
- USS：`USS/Layout/Grid/GridBasic.uss`

### Layout / Scroll

#### `layout.scroll.view`
- 作用：可滚动区域容器
- 定义：`Layout/Scroll/layout.scroll.view.definition.json`
- UXML：`UXML/Layout/Scroll/ScrollView.uxml`
- USS：`USS/Layout/Scroll/ScrollView.uss`

### Layout / Modal

#### `layout.modal.shell`
- 作用：弹窗壳体
- 定义：`Layout/Modal/layout.modal.shell.definition.json`
- UXML：`UXML/Layout/Modal/ModalShell.uxml`
- USS：`USS/Layout/Modal/ModalShell.uss`

---

## 目录结构

```text
UIBrick/Definitions/
├─ Primitive/
│  ├─ Text/
│  ├─ Image/
│  ├─ Input/
│  └─ Container/
└─ Layout/
   ├─ Stack/
   ├─ Grid/
   ├─ Scroll/
   └─ Modal/
```

同样的分法已同步到 `UXML/` 和 `USS/`。

---

## 常见 Brick 组合模式

> UIBrick 是组件参考库，不规定页面级结构。AI 应根据 GDD 的实际布局自由组合 Primitive 砖块，页面根容器由手写 USS 控制。

### 主菜单

```text
手写页面根容器
├─ 背景图 (background-image)
├─ 居中内容容器 (position: absolute)
│  ├─ primitive.text.label (标题)
│  ├─ 卡片面板 (手写容器)
│  │  ├─ primitive.input.button_base
│  │  └─ ...
│  └─ 浮动元素 (position: absolute, 角落图标)
```

### 背包

```text
手写页面根容器
├─ [header] (primitive.text.label)
├─ content
│  └─ layout.scroll.view
│     └─ layout.grid.basic
│        └─ primitive.image.icon + primitive.text.label (手写组合)
└─ footer
   └─ layout.stack.horizontal
      └─ primitive.input.button_base
```

### 设置

```text
手写页面根容器
├─ [header] (primitive.text.label)
├─ content
│  └─ layout.scroll.view
│     └─ layout.stack.vertical
│        ├─ primitive.text.label + primitive.input.toggle_base (手写组合)
│        ├─ primitive.text.label + primitive.input.slider_base (手写组合)
│        └─ primitive.text.label + primitive.input.dropdown_base (手写组合)
└─ footer
```

AI 从 Brick Catalog 中根据 compositionPolicy 和 slots 约束自由组合，无需固定模板。
