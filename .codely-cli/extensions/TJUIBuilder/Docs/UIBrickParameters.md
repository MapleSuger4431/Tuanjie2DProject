# UIBrick UGUI 参数参考（AI 版）

> 本文档由 `gen_brick_param_docs.py` 从 17 个 `ugui.*.definition.json` **自动生成**（2026-09-15 20:06），请勿手改；definition 变更后重跑生成脚本。
> 用途：为 AI 代理编写 / 校验 trees.json 的 `parameters` 提供权威参数面（含全部扩展参数与必要性分级）。

**参数总量**：889 项 / 17 bricks（Controls 676 · Layout 148 · Container 65）——🔴 必传 35 · 🟡 建议 169 · 🟢 可选 685

## 参考图生 UI 对接指南（三级必要性契约）

| 标记 | 级别 | 契约 |
|---|---|---|
| 🔴 必传 | REQUIRED | 中间大模型**必须**从设计稿/参考图提取写入 trees.json；缺失 = 占位文本、引擎白底图、尺寸被布局压扁/错乱 |
| 🟡 建议 | RECOMMENDED | 有引擎默认（通用风格）；参考图生 UI 场景通常应由中间大模型按设计稿覆盖 |
| 🟢 可选 | OPTIONAL | 微调/特效/过渡/运行态，省略安全 |

**对接职责切分**：

- **外部（参考图/图生图产物）提供的有限输入**：各 UI 元素的 Sprite 图片资产（对应 image_requirements.json 清单），以项目路径形式填入各 brick 的**资源类参数**（`*Sprite` / `spritePath` 等）。
- **中间大模型必须补齐**：文本内容（text/value）、颜色、字号、对齐、显式尺寸（width/height/spacing/cellSize/panelWidth…）、层级 sortingOrder、滚动内容布局 contentLayout 等——即全部 🔴 项与绝大多数 🟡 项。
- 每个 brick 章节末尾给出**最小参数集**（必传清单 + 建议清单 + 可直接复制的最小 trees.json 片段）。
- 各 brick 参数表按 **🔴 必传 → 🟡 建议 → 🟢 可选** 分组排列，同级内保持 definition 顺序——扫表即得『哪些必须传』。

## trees.json 使用约定

节点结构（`BrickNode`）：

```json
{ "brickId": "ugui.button", "name": "btn_start", "slot": "ButtonGroup",
  "parameters": { "text": "开始游戏", "fontSize": 32, "borderRadius": 16 },
  "children": [ /* 子节点，结构同上 */ ] }
```

- `parameters` 的键 = 本文档各表的**参数**列；类型 / 默认值照表；未传的参数走定义默认值。
- `slot`：显式挂载点（相对父 brick 根的路径）。省略时按推理规则：ScrollRect → `Viewport/Content`；根挂 LayoutGroup（stack/grid）→ 根；否则挂父根自身。modal 的子元素用 `ModalPanel/ModalHeaderSlot`、`ModalPanel/ModalContentSlot`、`ModalPanel/ModalFooterSlot`。
- **组合规则（双向 AND）**：子可挂到父下 ⇔ 子的 `canNestUnder` 含父 brickId **且** 父的 `allowedChildren` 含子 brickId。各 brick 的两表见其章节。
- `ugui.panel` 树根：构建（`BrickPanelBuilder.BuildBrickTrees`）时自动加 Canvas + CanvasScaler（ScaleWithScreenSize）+ GraphicRaycaster 并做防黑屏拉伸修复；`ugui.modal` 树根同样处理。
- **树规格字段（`BrickTreeSpec`，树根级而非 brick 参数）**：`sortingOrder`（Canvas 层级，缺省 10，惯例 10 全屏 / 20 弹窗 / 30 确认框）、`referenceWidth` / `referenceHeight`（参考分辨率，缺省 1920×1080，CanvasScaler=ScaleWithScreenSize + MatchWidthOrHeight 0.5）。仅 panel/modal 树根生效；树根保存后强制全屏拉伸，`width`/`height` 参数实际作用于嵌套 panel。
- 资源类参数（Sprite / Font / AnimatorController / AudioClip）传**项目内路径**（如 `Assets/Games/xxx/UI/Images/btn.png`、包内 `Icons/RoundedRectBorder`）。

### handler 机制详解（机制列的 `handler:` 前缀）

- **应用管线**：BuildBrickTrees / CreateWithParams → ApplyParameters：每个参数按 definition 声明分派——有 `handler` 走内建处理程序注册表（共 31 个，其中 3 个为占位 no-op），无 handler 而有 `target` 走反射赋值；值先按 `type` 转换（type:"Sprite" 支持直接引用或项目路径字符串）。
- **依赖分层，键序无关**：tier 0 `SetActive`（创建/显隐可选子节点，如 icon.active）→ tier 1 `Ensure*` / `LoadSprite` / `LoadFont` / `LoadAudioClip` / `LoadAnimatorController` / `SetBackgroundEnabled` / `SetScaleEffect` / `SetContentLayout`（补挂组件、加载资源、在 Content 挂 LayoutGroup——tier 2 的 contentSpacing 等属性要写到它上面，必须先存在）→ tier 2 其余全部（属性写入/几何/皮肤）。同层保持 JSON 键序，trees.json 参数书写顺序不影响结果。
- **几何缓存 + PostProcess 统一应用**：`SetModalSize` / `SetModalPadding` / `SetModalSlotHeight` / `SetPadding` 只缓存值，整树装配完成后由 PostProcess（ApplyModalLayout 等）按固定顺序统一应用——任意 JSON 键序下布局一致；一个 modal 几何参数都没传时保持 definition 默认 stretch 不动。
- **作用点**：参数可声明 `child`（子物体路径，支持 `A/B` 斜杠）；表中『作用点』列的 根 / 子:Icon 即此信息。执行前先解析作用点，找不到子物体则告警跳过该参数。
- **SetSizeDelta 布局感知**：目标处于 LayoutGroup 子级时改写 `LayoutElement.preferredWidth/Height`（缺件自动补挂），并同步 sizeDelta 供布局外读取；`LayoutElement.ignoreLayout=true` 的目标直写 sizeDelta；`axis` 缺省 x。
- **LoadSprite**：目标无 Image 时自动补挂且 enabled=true；路径支持项目路径（`Assets/...`）与包内相对路径（`Icons/xxx`）。
- **反射赋值（target: 前缀）**：① `GetComponent<X>().属性` —— 作用点上取 X，缺失自动 AddComponent；② `GetComponentInChildren<X>().属性` —— 深搜子树（含未激活）取首个，缺失仅告警不自动建；③ 裸属性名 —— 扫作用点全部组件，首个可写同名属性收值。均支持 `a.b.c` 嵌套属性路径。
- **组件类型短名**：definition 中的 `Button` / `Image` / `CanvasScaler` 等短名经 `ShortNameMap` 映射为完整类型（Type.GetType 失败再全程序集扫描兜底）。

| handler | 附加键 | 语义 | 典型参数（全 definitions 自动汇总） |
|---|---|---|---|
| `SetSizeDelta` | axis | 写尺寸（axis 指定 x/y）。目标处于 LayoutGroup 子级时自动改写 LayoutElement preferred 尺寸，防被布局覆写；否则写 sizeDelta。 | `width`、`height`、`background.width`、`background.height`、`icon.width`、`icon.height` …共16项 |
| `LoadSprite` | — | 从项目路径加载 Sprite 到目标 Image（目标无 Image 时自动补挂并 enabled=true）。路径形如 Assets/.../x.png 或包内相对 Icons/xxx。 | `backgroundSprite`、`icon.spritePath`、`checkmarkSprite`、`fillSprite`、`handle.spritePath`、`arrow.spritePath` …共18项 |
| `LoadFont` | — | 从项目路径加载 Font 到目标 Text。 | `customFontPath` |
| `LoadAudioClip` | prop | 从项目路径加载 AudioClip（配合 UIAudioFeedback）。 | `clickSoundPath`、`hoverSoundPath`、`showSoundPath` |
| `SetAudioProp` | prop | 设置 UIAudioFeedback 的属性。 | `audioVolume` |
| `EnsureShadow` | — | 确保目标挂 Shadow 组件（缺失补挂）。 | `shadowEnabled`、`textShadowEnabled`、`label.textShadowEnabled` |
| `SetShadowProp` | prop | 设置 Shadow 组件属性（prop 指定属性名）。 | `shadowColor`、`shadowDistance`、`textShadowColor`、`textShadowDistance`、`label.textShadowColor`、`label.textShadowDistance` |
| `EnsureOutline` | — | 确保目标挂 Outline 组件（缺失补挂）。 | `textOutlineEnabled`、`label.textOutlineEnabled`、`outlineEnabled` |
| `SetOutlineProp` | prop | 设置 Outline 组件属性（prop 指定属性名）。 | `textOutlineColor`、`textOutlineDistance`、`label.textOutlineColor`、`label.textOutlineDistance`、`outlineColor`、`outlineDistance` |
| `EnsureGradient` | — | 确保目标挂 UGUITextGradient（缺失补挂）。 | `gradientEnabled` |
| `SetGradientProp` | prop | 设置 UGUITextGradient 渐变属性。 | `gradientTopColor`、`gradientBottomColor` |
| `EnsureUnderline` | — | 确保下划线装饰（UGUITextDecoration）。 | `underlineEnabled` |
| `SetUnderlineProp` | prop | 设置下划线装饰属性。 | `underlineColor` |
| `EnsureStrikethrough` | — | 确保删除线装饰（UGUITextDecoration）。 | `strikethroughEnabled` |
| `SetBackgroundEnabled` | — | 背景子物体启用开关（backgroundEnabled）。 | `backgroundEnabled` |
| `SetIconLayout` | prop | 按钮图标的布局参数（位置/边距驱动的布局处理）。 | `icon.position`、`icon.margin` |
| `SetFlexTextProp` | prop | 弹性文本属性（自动排版适配）。 | `label.width`、`label.flexibleWidth`、`label.minWidth`、`value.flexibleWidth` |
| `SetScaleEffect` | prop | 按钮按压缩放反馈（ButtonScaleEffect：pressScale/hoverScale 等）。 | `effects.enabled`、`effects.pressScale`、`effects.pressScaleDuration`、`effects.hoverScale`、`effects.hoverScaleDuration`、`clickScale` …共9项 |
| `SetBorderRadius` | — | 圆角：以 Icons/RoundedRectBorder 九宫格替换目标 Image 的 sprite 并切 Sliced。 | `borderRadius` |
| `SetPadding` | prop | 布局容器 padding（缓存后统一应用）。 | `paddingLeft`、`paddingRight`、`paddingTop`、`paddingBottom` |
| `SetActive` | component | 子物体显隐开关（component 指定须存在的组件）。 | `icon.active`、`label.active`、`handle.active`、`itemBackground.active`、`scrollbar.active`、`verticalScrollbar.active` …共8项 |
| `EnsureAnimator` | — | 确保目标挂 Animator（缺失补挂）。 | `animation.animatorEnabled` |
| `LoadAnimatorController` | — | 从项目路径加载 AnimatorController。 | `animation.animatorControllerPath` |
| `SetContentLayout` | — | 在 scroll_view 的 Viewport/Content 上挂指定 LayoutGroup（vertical\|horizontal\|grid\|none）。 | `contentLayout` |
| `SetContentLayoutProp` | prop | 设置该 Content LayoutGroup 的属性（prop：spacing/padding/cellSize）。 | `contentSpacing`、`contentPadding`、`contentCellSize` |
| `SetModalSize` | axis | modal 面板宽/高（axis 区分；缓存后在 PostProcess 按序统一应用，任意 JSON 键序安全）。 | `panelWidth`、`panelHeight` |
| `SetModalPadding` | — | modal 面板内边距（缓存后统一应用）。 | `panelPadding` |
| `SetModalSlotHeight` | prop | modal 头/脚槽高度（prop=header/footer；缓存后统一应用）。 | `headerHeight`、`footerHeight` |

> 另有 3 个内建 handler 为占位 no-op（`SetBorderWidth`、`EnsureInlineSprite`、`SetInlineSpriteFormat`），同样无 definition 引用，不入速查表。

`target:` 前缀 = 直接反射赋值表达式（`GetComponent<X>()` 在作用点解析；`GetComponentInChildren<X>()` 取作用点下首个匹配；裸属性名写在作用点组件上）。

## 2. Controls 控件类

### ugui.button（81 参数 · 🔴4 🟡11 🟢66）

- **brickId**：`ugui.button` · **layer**：primitive · **category**：input
- **AI**：priority=high selectable=True
- **requiredComponents: Button, Text**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - Icon(Image) [可选]
  - Label(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `text` | string | Button | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本，缺失=占位文案 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `fontSize` | int |  | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor |  | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟡 建议 | `icon.spritePath` | string |  | 子:Icon | `handler:LoadSprite` | 对应子件启用时应传生成图 |  |
| 🟡 建议 | `icon.width` | int |  | 子:Icon | `handler:SetSizeDelta axis=x` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `icon.height` | int |  | 子:Icon | `handler:SetSizeDelta axis=y` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `icon.color` | Color |  | 子:Icon | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Button>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Button>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Button>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Button>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Button>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Button>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<Button>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Button>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Button>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Label | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Label | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Label | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Label | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Label | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Label | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `icon.position` | string |  | 根 | `handler:SetIconLayout prop=position` | 微调项，省略安全 |  |
| 🟢 可选 | `icon.margin` | int |  | 根 | `handler:SetIconLayout prop=margin` | 微调项，省略安全 |  |
| 🟢 可选 | `icon.preserveAspect` | bool |  | 子:Icon | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `icon.active` | bool |  | 子:Icon | `handler:SetActive component=Image` | 微调项，省略安全 |  |
| 🟢 可选 | `effects.enabled` | bool |  | 根 | `handler:SetScaleEffect prop=enabled` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `effects.pressScale` | Vector3 |  | 根 | `handler:SetScaleEffect prop=pressScale` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `effects.pressScaleDuration` | float |  | 根 | `handler:SetScaleEffect prop=pressScaleDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `effects.hoverScale` | Vector3 |  | 根 | `handler:SetScaleEffect prop=hoverScale` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `effects.hoverScaleDuration` | float |  | 根 | `handler:SetScaleEffect prop=hoverScaleDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`、`text`
  - 🟡 建议 → `backgroundColor`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`、`icon.spritePath`、`icon.width`、`icon.height`、`icon.color`

  ```json
  {
    "brickId": "ugui.button",
    "name": "button_demo",
    "parameters": {
      "width": 420,
      "height": 64,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "text": "按钮文本",
      "fontSize": 32,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.modal", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **事件绑定**：{"clicked": "onClick.AddListener"}
- **事件 Schema（ui_event）**：{"clicked": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"text": {"target": "GetComponentInChildren<Text>().text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}, "enabled": {"target": "interactable", "runtimeType": "bool"}}

### ugui.toggle（71 参数 · 🔴3 🟡10 🟢58）

- **brickId**：`ugui.toggle` · **layer**：primitive · **category**：input
- **AI**：priority=high selectable=True
- **requiredComponents: Toggle**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
    - Checkmark(Image)
  - Label(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `checkmarkSprite` | string |  | 子:Checkmark | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `checkmarkColor` | Color |  | 子:Checkmark | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `text` | string | Toggle | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本（有默认占位），按设计稿覆盖 |  |
| 🟡 建议 | `fontSize` | int |  | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleLeft | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟢 可选 | `isOn` | bool | true | 根 | `target: isOn` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `toggleTransition` | Toggle.ToggleTransition |  | 根 | `target: GetComponent<Toggle>().toggleTransition` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPreserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundFillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundWidth` | int |  | 子:Background | `handler:SetSizeDelta axis=x` | 子件尺寸微调 |  |
| 🟢 可选 | `backgroundHeight` | int |  | 子:Background | `handler:SetSizeDelta axis=y` | 子件尺寸微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `checkmarkWidth` | int |  | 子:Checkmark | `handler:SetSizeDelta axis=x` | 子件尺寸微调 |  |
| 🟢 可选 | `checkmarkHeight` | int |  | 子:Checkmark | `handler:SetSizeDelta axis=y` | 子件尺寸微调 |  |
| 🟢 可选 | `checkmarkPreserveAspect` | bool |  | 子:Checkmark | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `label.offsetMin` | Vector2 |  | 子:Label | `target: GetComponent<RectTransform>().offsetMin` | 微调项，省略安全 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Label | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Label | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Label | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Label | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Label | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Label | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Toggle>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Toggle>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Toggle>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Toggle>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Toggle>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Toggle>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<Toggle>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Toggle>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Toggle>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Toggle>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Toggle>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Toggle>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Toggle>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Toggle>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Toggle>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Toggle>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Toggle>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Toggle>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`
  - 🟡 建议 → `backgroundColor`、`checkmarkSprite`、`checkmarkColor`、`text`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`

  ```json
  {
    "brickId": "ugui.toggle",
    "name": "toggle_demo",
    "parameters": {
      "width": 300,
      "height": 64,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **事件绑定**：{"changed": "onValueChanged.AddListener"}
- **事件 Schema（ui_event）**：{"changed": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"isOn": {"target": "isOn", "runtimeType": "bool"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}, "enabled": {"target": "interactable", "runtimeType": "bool"}}

### ugui.slider（77 参数 · 🔴5 🟡9 🟢63）

- **brickId**：`ugui.slider` · **layer**：primitive · **category**：input
- **AI**：priority=high selectable=True
- **requiredComponents: Slider**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - FillArea(RectTransform)
    - Fill(Image)
  - HandleArea(RectTransform)
    - Handle(Image)
  - Label(RectTransform) [可选]
    - Text(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `fillSprite` | string |  | 子:Fill | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `handle.spritePath` | string |  | 子:Handle | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `fillColor` | Color |  | 子:Fill | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `handle.color` | Color |  | 子:Handle | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `fontSize` | int | 14 | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color | [0.196,0.196,0.196,1] | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleCenter | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟢 可选 | `direction` | Slider.Direction |  | 根 | `target: GetComponent<Slider>().direction` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `minValue` | float |  | 根 | `target: GetComponent<Slider>().minValue` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `maxValue` | float |  | 根 | `target: GetComponent<Slider>().maxValue` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `value` | float |  | 根 | `target: GetComponent<Slider>().value` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `wholeNumbers` | bool |  | 根 | `target: GetComponent<Slider>().wholeNumbers` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPreserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundFillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |
| 🟢 可选 | `fillType` | Image.Type | Filled | 子:Fill | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `fillMethod` | Image.FillMethod | Horizontal | 子:Fill | `target: GetComponent<Image>().fillMethod` | 微调项，省略安全 |  |
| 🟢 可选 | `fillOrigin` | int | 0 | 子:Fill | `target: GetComponent<Image>().fillOrigin` | 微调项，省略安全 |  |
| 🟢 可选 | `fillPreserveAspect` | bool |  | 子:Fill | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `handle.width` | int |  | 子:Handle | `handler:SetSizeDelta axis=x` | 微调项，省略安全 |  |
| 🟢 可选 | `handle.height` | int |  | 子:Handle | `handler:SetSizeDelta axis=y` | 微调项，省略安全 |  |
| 🟢 可选 | `handle.preserveAspect` | bool |  | 子:Handle | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Slider>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Slider>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Slider>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Slider>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `label.active` | bool | false | 子:Label | `handler:SetActive component=RectTransform` | 微调项，省略安全 |  |
| 🟢 可选 | `text` | string |  | 根 | `target: GetComponentInChildren<Text>().text` | 微调项，省略安全 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Label/Text | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Label/Text | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Label/Text | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Label/Text | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Label/Text | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Label/Text | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`、`fillSprite`、`handle.spritePath`
  - 🟡 建议 → `backgroundColor`、`fillColor`、`handle.color`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`

  ```json
  {
    "brickId": "ugui.slider",
    "name": "slider_demo",
    "parameters": {
      "width": 420,
      "height": 36,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "fillSprite": "Assets/Games/<GameName>/UI/Images/bar_fill.png",
      "handle.spritePath": "Assets/Games/<GameName>/UI/Images/bar_handle.png",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **事件绑定**：{"changed": "onValueChanged.AddListener"}
- **事件 Schema（ui_event）**：{"changed": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"value": {"target": "value", "runtimeType": "float"}, "text": {"target": "GetComponentInChildren<Text>().text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}, "enabled": {"target": "interactable", "runtimeType": "bool"}}

### ugui.progress_bar（73 参数 · 🔴4 🟡9 🟢60）

- **brickId**：`ugui.progress_bar` · **layer**：primitive · **category**：display
- **AI**：priority=medium selectable=True
- **requiredComponents: Slider**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - FillArea(RectTransform)
    - Fill(Image)
  - HandleArea(RectTransform) [可选]
    - Handle(Image)
  - Label(RectTransform)
    - Text(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `fillSprite` | string |  | 子:Fill | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `fillColor` | Color |  | 子:Fill | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `text` | string |  | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本（有默认占位），按设计稿覆盖 |  |
| 🟡 建议 | `fontSize` | int | 14 | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color | [0.196,0.196,0.196,1] | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleCenter | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟢 可选 | `direction` | Slider.Direction |  | 根 | `target: GetComponent<Slider>().direction` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `minValue` | float |  | 根 | `target: GetComponent<Slider>().minValue` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `maxValue` | float |  | 根 | `target: GetComponent<Slider>().maxValue` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `value` | float |  | 根 | `target: GetComponent<Slider>().value` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `wholeNumbers` | bool |  | 根 | `target: GetComponent<Slider>().wholeNumbers` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPreserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundFillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |
| 🟢 可选 | `fillType` | Image.Type | Filled | 子:Fill | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `fillMethod` | Image.FillMethod | Horizontal | 子:Fill | `target: GetComponent<Image>().fillMethod` | 微调项，省略安全 |  |
| 🟢 可选 | `fillOrigin` | int | 0 | 子:Fill | `target: GetComponent<Image>().fillOrigin` | 微调项，省略安全 |  |
| 🟢 可选 | `fillPreserveAspect` | bool |  | 子:Fill | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `handle.active` | bool | false | 子:HandleArea | `handler:SetActive component=RectTransform` | 微调项，省略安全 |  |
| 🟢 可选 | `handle.preserveAspect` | bool |  | 子:Handle | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Slider>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Slider>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Slider>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Slider>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Slider>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Slider>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Slider>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Label/Text | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Label/Text | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Label/Text | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Label/Text | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Label/Text | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Label/Text | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`、`fillSprite`
  - 🟡 建议 → `backgroundColor`、`fillColor`、`text`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`

  ```json
  {
    "brickId": "ugui.progress_bar",
    "name": "progress_bar_demo",
    "parameters": {
      "width": 420,
      "height": 36,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "fillSprite": "Assets/Games/<GameName>/UI/Images/bar_fill.png",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"value": {"target": "value", "runtimeType": "float"}, "text": {"target": "GetComponentInChildren<Text>().text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

### ugui.input_field（105 参数 · 🔴3 🟡19 🟢83）

- **brickId**：`ugui.input_field` · **layer**：primitive · **category**：input
- **AI**：priority=high selectable=True
- **requiredComponents: InputField**
- **childStructure**（工厂自动创建的内部结构）：
  - Label(Text) [可选]
  - Input Area(RectTransform)
    - Background(Image)
    - Text Area(RectTransform)
      - Placeholder(Text)
      - Text(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `customCaretColor` | bool |  | 根 | `target: customCaretColor` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟡 建议 | `fontSize` | int |  | 子:Text | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 子:Text | `target: GetComponent<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 子:Text | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleLeft | 子:Text | `target: GetComponent<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 子:Text | `target: GetComponent<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `placeholderText` | string | Enter text... | 子:Placeholder | `target: GetComponent<Text>().text` | 内容文本（有默认占位），按设计稿覆盖 |  |
| 🟡 建议 | `placeholderColor` | Color |  | 子:Placeholder | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `placeholderFontSize` | int |  | 子:Placeholder | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.fontSize` | int | 14 | 子:Label | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.fontStyle` | FontStyle |  | 子:Label | `target: GetComponent<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.textColor` | Color | [0.196,0.196,0.196,1] | 子:Label | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.textAlignment` | TextAnchor | MiddleCenter | 子:Label | `target: GetComponent<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.lineSpacing` | float |  | 子:Label | `target: GetComponent<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.width` | float |  | 子:Label | `handler:SetFlexTextProp prop=preferredWidth` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `label.textShadowColor` | Color |  | 子:Label | `handler:SetShadowProp prop=effectColor` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `label.textOutlineColor` | Color |  | 子:Label | `handler:SetOutlineProp prop=effectColor` | 有引擎默认色，应按参考图调色 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<InputField>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<InputField>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<InputField>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<InputField>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<InputField>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<InputField>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<InputField>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<InputField>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<InputField>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<InputField>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<InputField>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<InputField>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<InputField>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<InputField>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<InputField>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<InputField>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<InputField>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<InputField>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `text` | string |  | 根 | `target: text` | 初始内容通常留空，交互后由玩家输入 |  |
| 🟢 可选 | `contentType` | InputField.ContentType |  | 根 | `target: contentType` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `lineType` | InputField.LineType |  | 根 | `target: lineType` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `characterLimit` | int |  | 根 | `target: characterLimit` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `caretColor` | Color |  | 根 | `target: caretColor` | 微调项，省略安全 |  |
| 🟢 可选 | `selectionColor` | Color |  | 根 | `target: selectionColor` | 微调项，省略安全 |  |
| 🟢 可选 | `caretBlinkRate` | float |  | 根 | `target: caretBlinkRate` | 微调项，省略安全 |  |
| 🟢 可选 | `caretWidth` | int |  | 根 | `target: caretWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `richText` | bool |  | 子:Text | `target: GetComponent<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 子:Text | `target: GetComponent<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 子:Text | `target: GetComponent<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 子:Text | `target: GetComponent<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 子:Text | `target: GetComponent<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 子:Text | `target: GetComponent<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 子:Text | `target: GetComponent<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Text | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Text | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Text | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Text | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Text | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Text | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `label.active` | bool | false | 子:Label | `handler:SetActive component=Text` | 微调项，省略安全 |  |
| 🟢 可选 | `label.position` | string | left | 根 | `handler:SetLabelLayout prop=position` | 微调项，省略安全 |  |
| 🟢 可选 | `label.spacing` | float | 8 | 根 | `handler:SetLabelLayout prop=spacing` | 微调项，省略安全 |  |
| 🟢 可选 | `label.text` | string |  | 子:Label | `target: GetComponent<Text>().text` | 微调项，省略安全 |  |
| 🟢 可选 | `label.richText` | bool |  | 子:Label | `target: GetComponent<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `label.raycastTarget` | bool |  | 子:Label | `target: GetComponent<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `label.horizontalOverflow` | HorizontalWrapMode |  | 子:Label | `target: GetComponent<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `label.verticalOverflow` | VerticalWrapMode |  | 子:Label | `target: GetComponent<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `label.bestFit` | bool |  | 子:Label | `target: GetComponent<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `label.bestFitMinSize` | int |  | 子:Label | `target: GetComponent<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `label.bestFitMaxSize` | int |  | 子:Label | `target: GetComponent<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `label.flexibleWidth` | float |  | 子:Label | `handler:SetFlexTextProp prop=flexibleWidth` | 布局权重微调 |  |
| 🟢 可选 | `label.minWidth` | float |  | 子:Label | `handler:SetFlexTextProp prop=minWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `label.textShadowEnabled` | bool |  | 子:Label | `handler:EnsureShadow` | 微调项，省略安全 |  |
| 🟢 可选 | `label.textShadowDistance` | Vector2 |  | 子:Label | `handler:SetShadowProp prop=effectDistance` | 微调项，省略安全 |  |
| 🟢 可选 | `label.textOutlineEnabled` | bool |  | 子:Label | `handler:EnsureOutline` | 微调项，省略安全 |  |
| 🟢 可选 | `label.textOutlineDistance` | Vector2 |  | 子:Label | `handler:SetOutlineProp prop=effectDistance` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`
  - 🟡 建议 → `backgroundColor`、`customCaretColor`、`customFontPath`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`placeholderText`、`placeholderColor`、`placeholderFontSize`、`label.fontSize`、`label.fontStyle`、`label.textColor`、`label.textAlignment`、`label.lineSpacing`、`label.width`、`label.textShadowColor`、`label.textOutlineColor`

  ```json
  {
    "brickId": "ugui.input_field",
    "name": "input_field_demo",
    "parameters": {
      "width": 480,
      "height": 64,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **事件绑定**：{"changed": "onEndEdit.AddListener", "valueChanged": "onValueChanged.AddListener"}
- **事件 Schema（ui_event）**：{"changed": "ui_event", "valueChanged": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"text": {"target": "text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}, "enabled": {"target": "interactable", "runtimeType": "bool"}}

### ugui.dropdown（86 参数 · 🔴3 🟡22 🟢61）

- **brickId**：`ugui.dropdown` · **layer**：primitive · **category**：input
- **AI**：priority=high selectable=True
- **requiredComponents: Dropdown, Image**
- **childStructure**（工厂自动创建的内部结构）：
  - Label(Text)
  - Arrow(Image)
  - Template(Image,ScrollRect)
    - Viewport(Image,Mask)
      - Content(RectTransform)
        - Item(Toggle)
          - ItemBackground(Image) [可选]
          - ItemCheckmark(Image)
          - ItemLabel(Text)
    - Scrollbar(Scrollbar,Image) [可选]
      - SlidingArea(RectTransform)
        - Handle(Image)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `backgroundSprite` | string |  | 根 | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 根 | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `value` | int |  | 根 | `target: value` | 内容文本（有默认占位），按设计稿覆盖 |  |
| 🟡 建议 | `fontSize` | int |  | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor |  | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟡 建议 | `arrow.spritePath` | string |  | 子:Arrow | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `arrow.color` | Color |  | 子:Arrow | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `templateHeight` | int |  | 子:Template | `handler:SetSizeDelta axis=y` | 展开列表几何，字号大时必须调大 |  |
| 🟡 建议 | `itemHeight` | int |  | 子:Item | `handler:SetSizeDelta axis=y` | 展开列表几何，字号大时必须调大 |  |
| 🟡 建议 | `templateBackgroundColor` | Color |  | 子:Template | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `templateBackgroundSprite` | string |  | 子:Template | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `itemBackgroundColor` | Color | [1,1,1,0.05] | 子:ItemBackground | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `itemBackgroundSprite` | string |  | 子:ItemBackground | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `itemTextColor` | Color |  | 子:ItemLabel | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `itemFontSize` | int |  | 子:ItemLabel | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `itemCheckmarkSprite` | string |  | 子:ItemCheckmark | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `itemCheckmarkColor` | Color |  | 子:ItemCheckmark | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `scrollbar.color` | Color |  | 子:Scrollbar | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `scrollbar.handleColor` | Color |  | 子:Template/Scrollbar/SlidingArea/Handle | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 根 | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 根 | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 根 | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 根 | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 根 | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 根 | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 根 | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 根 | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Dropdown>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Dropdown>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Dropdown>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Dropdown>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Dropdown>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Dropdown>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.selectedColor` | Color |  | 根 | `target: GetComponent<Dropdown>().colors.selectedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Dropdown>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Dropdown>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Dropdown>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Dropdown>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Dropdown>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Dropdown>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Dropdown>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Dropdown>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Dropdown>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Dropdown>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Dropdown>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Label | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Label | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Label | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Label | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Label | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Label | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `arrow.type` | Image.Type |  | 子:Arrow | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `arrow.width` | int |  | 子:Arrow | `handler:SetSizeDelta axis=x` | 微调项，省略安全 |  |
| 🟢 可选 | `arrow.height` | int |  | 子:Arrow | `handler:SetSizeDelta axis=y` | 微调项，省略安全 |  |
| 🟢 可选 | `templateBackgroundType` | Image.Type |  | 子:Template | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `itemBackgroundType` | Image.Type |  | 子:ItemBackground | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `itemBackground.active` | bool | true | 子:ItemBackground | `handler:SetActive component=Image` | 微调项，省略安全 |  |
| 🟢 可选 | `itemCheckmarkType` | Image.Type |  | 子:ItemCheckmark | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `scrollbar.active` | bool | true | 子:Scrollbar | `handler:SetActive component=Scrollbar` | 微调项，省略安全 |  |
| 🟢 可选 | `scrollbar.spritePath` | string |  | 子:Scrollbar | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `scrollbar.type` | Image.Type |  | 子:Scrollbar | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `scrollbar.handleSprite` | string |  | 子:Template/Scrollbar/SlidingArea/Handle | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `scrollbar.handleType` | Image.Type |  | 子:Template/Scrollbar/SlidingArea/Handle | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `width`、`height`、`backgroundSprite`
  - 🟡 建议 → `backgroundColor`、`value`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`、`arrow.spritePath`、`arrow.color`、`templateHeight`、`itemHeight`、`templateBackgroundColor`、`templateBackgroundSprite`、`itemBackgroundColor`、`itemBackgroundSprite`、`itemTextColor`、`itemFontSize`、`itemCheckmarkSprite`、`itemCheckmarkColor`、`scrollbar.color`、`scrollbar.handleColor`

  ```json
  {
    "brickId": "ugui.dropdown",
    "name": "dropdown_demo",
    "parameters": {
      "width": 360,
      "height": 64,
      "backgroundSprite": "Assets/Games/<GameName>/UI/Images/btn_primary.png",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **事件绑定**：{"changed": "onValueChanged.AddListener"}
- **事件 Schema（ui_event）**：{"changed": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"value": {"target": "value", "runtimeType": "int"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}, "enabled": {"target": "interactable", "runtimeType": "bool"}}

### ugui.label（62 参数 · 🔴1 🟡13 🟢48）

- **brickId**：`ugui.label` · **layer**：primitive · **category**：text
- **AI**：priority=high selectable=True
- **requiredComponents: Text**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - Text(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `text` | string | Label | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本，缺失=占位文案 |  |
| 🟡 建议 | `fontSize` | int |  | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleLeft | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `paddingLeft` | float |  | 根 | `handler:SetPadding prop=left` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `paddingRight` | float |  | 根 | `handler:SetPadding prop=right` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `paddingTop` | float |  | 根 | `handler:SetPadding prop=top` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `paddingBottom` | float |  | 根 | `handler:SetPadding prop=bottom` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Text | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Text | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Text | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineEnabled` | bool |  | 子:Text | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineColor` | Color |  | 子:Text | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineDistance` | Vector2 |  | 子:Text | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientEnabled` | bool |  | 子:Text | `handler:EnsureGradient` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientTopColor` | Color |  | 子:Text | `handler:SetGradientProp prop=topColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientBottomColor` | Color |  | 子:Text | `handler:SetGradientProp prop=bottomColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `underlineEnabled` | bool |  | 子:Text | `handler:EnsureUnderline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `underlineColor` | Color |  | 子:Text | `handler:SetUnderlineProp prop=color` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `strikethroughEnabled` | bool |  | 子:Text | `handler:EnsureStrikethrough` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 文本底图为可选装饰（backgroundEnabled 配套） |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `sizeDelta` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().sizeDelta` | 微调项，省略安全 |  |
| 🟢 可选 | `offsetMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().offsetMin` | 微调项，省略安全 |  |
| 🟢 可选 | `offsetMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().offsetMax` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |

- **最小参数集**：🔴 必传 → `text`
  - 🟡 建议 → `fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`、`backgroundColor`、`paddingLeft`、`paddingRight`、`paddingTop`、`paddingBottom`、`width`、`height`

  ```json
  {
    "brickId": "ugui.label",
    "name": "label_demo",
    "parameters": {
      "text": "文本内容",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.modal", "ugui.panel"]
- **布局契约**：self: hug/fill/hug
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"text": {"target": "GetComponentInChildren<Text>().text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

### ugui.value_text（55 参数 · 🔴1 🟡9 🟢45）

- **brickId**：`ugui.value_text` · **layer**：primitive · **category**：text
- **AI**：priority=medium selectable=True
- **requiredComponents: Text**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - Text(Text)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `text` | string | 0 | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本，缺失=占位文案 |  |
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `fontSize` | int |  | 根 | `target: GetComponentInChildren<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 根 | `target: GetComponentInChildren<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor | MiddleLeft | 根 | `target: GetComponentInChildren<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `customFontPath` | string |  | 根 | `handler:LoadFont` | 有默认字体，美术字体应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `textShadowEnabled` | bool |  | 子:Text | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowColor` | Color |  | 子:Text | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textShadowDistance` | Vector2 |  | 子:Text | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineEnabled` | bool |  | 子:Text | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineColor` | Color |  | 子:Text | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `textOutlineDistance` | Vector2 |  | 子:Text | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientEnabled` | bool |  | 子:Text | `handler:EnsureGradient` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientTopColor` | Color |  | 子:Text | `handler:SetGradientProp prop=topColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `gradientBottomColor` | Color |  | 子:Text | `handler:SetGradientProp prop=bottomColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `underlineEnabled` | bool |  | 子:Text | `handler:EnsureUnderline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `underlineColor` | Color |  | 子:Text | `handler:SetUnderlineProp prop=color` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `strikethroughEnabled` | bool |  | 子:Text | `handler:EnsureStrikethrough` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 文本底图为可选装饰（backgroundEnabled 配套） |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `text`
  - 🟡 建议 → `width`、`height`、`fontSize`、`fontStyle`、`textColor`、`textAlignment`、`lineSpacing`、`customFontPath`、`backgroundColor`

  ```json
  {
    "brickId": "ugui.value_text",
    "name": "value_text_demo",
    "parameters": {
      "text": "文本内容",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fixed/fill/hug/fixed
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"text": {"target": "GetComponentInChildren<Text>().text", "runtimeType": "string"}, "visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

### ugui.icon（46 参数 · 🔴3 🟡3 🟢40）

- **brickId**：`ugui.icon` · **layer**：primitive · **category**：image
- **AI**：priority=high selectable=True
- **requiredComponents: Image, Button**

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `spritePath` | string |  | 根 | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🟡 建议 | `color` | Color |  | 根 | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `clickSoundPath` | string |  | 根 | `handler:LoadAudioClip prop=clickSound` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `hoverSoundPath` | string |  | 根 | `handler:LoadAudioClip prop=hoverSound` | 资源类参数，参考图有对应素材时应传 |  |
| 🟢 可选 | `imageType` | Image.Type |  | 根 | `target: GetComponent<Image>().type` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `preserveAspect` | bool |  | 根 | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponent<Image>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `borderRadius` | int |  | 根 | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 根 | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 根 | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 根 | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `audioVolume` | float |  | 根 | `handler:SetAudioProp prop=volume` | 微调项，省略安全 |  |
| 🟢 可选 | `clickScale` | Vector3 |  | 根 | `handler:SetScaleEffect prop=pressScale` | 微调项，省略安全 |  |
| 🟢 可选 | `clickScaleDuration` | float |  | 根 | `handler:SetScaleEffect prop=pressScaleDuration` | 微调项，省略安全 |  |
| 🟢 可选 | `hoverScale` | Vector3 |  | 根 | `handler:SetScaleEffect prop=hoverScale` | 微调项，省略安全 |  |
| 🟢 可选 | `hoverScaleDuration` | float |  | 根 | `handler:SetScaleEffect prop=hoverScaleDuration` | 微调项，省略安全 |  |
| 🟢 可选 | `interactable` | bool |  | 根 | `target: GetComponent<Button>().interactable` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `clickable` | bool | false | 根 | `target: GetComponent<Button>().enabled` | 微调项，省略安全 |  |
| 🟢 可选 | `navigation.mode` | Navigation.Mode | Automatic | 根 | `target: GetComponent<Button>().navigation.mode` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `navigation.wrapAround` | bool | false | 根 | `target: GetComponent<Button>().navigation.wrapAround` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `transitionMode` | Selectable.Transition |  | 根 | `target: GetComponent<Button>().transition` | 微调项，省略安全 |  |
| 🟢 可选 | `tint.normalColor` | Color |  | 根 | `target: GetComponent<Button>().colors.normalColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.highlightedColor` | Color |  | 根 | `target: GetComponent<Button>().colors.highlightedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.pressedColor` | Color |  | 根 | `target: GetComponent<Button>().colors.pressedColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.disabledColor` | Color |  | 根 | `target: GetComponent<Button>().colors.disabledColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `tint.fadeDuration` | float |  | 根 | `target: GetComponent<Button>().colors.fadeDuration` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.highlightedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.highlightedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.pressedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.pressedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.selectedSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.selectedSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `spriteSwap.disabledSprite` | Sprite |  | 根 | `target: GetComponent<Button>().spriteState.disabledSprite` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.normalTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.normalTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.highlightedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.highlightedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.pressedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.pressedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.selectedTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.selectedTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.disabledTrigger` | string |  | 根 | `target: GetComponent<Button>().animationTriggers.disabledTrigger` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorEnabled` | bool |  | 根 | `handler:EnsureAnimator` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `animation.animatorControllerPath` | string |  | 根 | `handler:LoadAnimatorController` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `spritePath`、`width`、`height`
  - 🟡 建议 → `color`、`clickSoundPath`、`hoverSoundPath`

  ```json
  {
    "brickId": "ugui.icon",
    "name": "icon_demo",
    "parameters": {
      "spritePath": "Assets/Games/<GameName>/UI/Images/icon_default.png",
      "width": 64,
      "height": 64
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/hug
- **事件绑定**：{"clicked": "onClick.AddListener"}
- **事件 Schema（ui_event）**：{"clicked": "ui_event"}
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

### ugui.sprite（20 参数 · 🔴3 🟡2 🟢15）

- **brickId**：`ugui.sprite` · **layer**：primitive · **category**：image
- **AI**：priority=medium selectable=True
- **requiredComponents: Image**

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `spritePath` | string |  | 根 | `handler:LoadSprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🔴 必传 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🔴 必传 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 布局链需显式 preferred 尺寸，缺失会被内容尺寸压扁/拉伸 |  |
| 🟡 建议 | `color` | Color |  | 根 | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `showSoundPath` | string |  | 根 | `handler:LoadAudioClip prop=showSound` | 资源类参数，参考图有对应素材时应传 |  |
| 🟢 可选 | `imageType` | Image.Type |  | 根 | `target: GetComponent<Image>().type` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `preserveAspect` | bool |  | 根 | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponent<Image>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `opacity` | float |  | 根 | `target: GetComponent<CanvasGroup>().alpha` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `borderRadius` | int |  | 根 | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 根 | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 根 | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 根 | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `audioVolume` | float |  | 根 | `handler:SetAudioProp prop=volume` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMin` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMin` | 微调项，省略安全 |  |
| 🟢 可选 | `anchorMax` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchorMax` | 微调项，省略安全 |  |
| 🟢 可选 | `pivot` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().pivot` | 微调项，省略安全 |  |
| 🟢 可选 | `anchoredPosition` | Vector2 |  | 根 | `target: GetComponent<RectTransform>().anchoredPosition` | 微调项，省略安全 |  |
| 🟢 可选 | `localScale` | Vector3 |  | 根 | `target: GetComponent<Transform>().localScale` | 微调项，省略安全 |  |
| 🟢 可选 | `localRotation` | Vector3 |  | 根 | `target: GetComponent<Transform>().localEulerAngles` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `spritePath`、`width`、`height`
  - 🟡 建议 → `color`、`showSoundPath`

  ```json
  {
    "brickId": "ugui.sprite",
    "name": "sprite_demo",
    "parameters": {
      "spritePath": "Assets/Games/<GameName>/UI/Images/icon_default.png",
      "width": 128,
      "height": 128
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.panel"]
- **布局契约**：self: hug/fill/hug/fill
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

## 3. Layout 布局类

### ugui.stack_vertical（32 参数 · 🔴0 🟡7 🟢25）

- **brickId**：`ugui.stack_vertical` · **layer**：layout · **category**：stack
- **requiredComponents: VerticalLayoutGroup**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `spacing` | float | 4 | 根 | `target: spacing` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `childAlignment` | TextAnchor | UpperLeft | 根 | `target: childAlignment` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `padding` | RectOffset | [0,0,0,0] | 根 | `target: padding` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `childControlWidth` | bool | true | 根 | `target: childControlWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childControlHeight` | bool | true | 根 | `target: childControlHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `childForceExpandWidth` | bool | true | 根 | `target: childForceExpandWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childForceExpandHeight` | bool | true | 根 | `target: childForceExpandHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `childScaleWidth` | bool | false | 根 | `target: childScaleWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childScaleHeight` | bool | false | 根 | `target: childScaleHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `reverseArrangement` | bool | false | 根 | `target: reverseArrangement` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → （无）
  - 🟡 建议 → `width`、`height`、`backgroundSprite`、`backgroundColor`、`spacing`、`childAlignment`、`padding`

  ```json
  {
    "brickId": "ugui.stack_vertical",
    "name": "stack_vertical_demo",
    "parameters": {}
  }
  ```

- **组合策略**：allowedChildren=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view", "ugui.button", "ugui.label", "ugui.value_text", "ugui.icon", "ugui.sprite", "ugui.panel", "ugui.slider", "ugui.toggle", "ugui.dropdown", "ugui.progress_bar", "ugui.input_field"] · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view", "ugui.modal", "ugui.panel"]
- **布局契约**：self: hug/fill/hug/fill

### ugui.stack_horizontal（32 参数 · 🔴0 🟡7 🟢25）

- **brickId**：`ugui.stack_horizontal` · **layer**：layout · **category**：stack
- **requiredComponents: HorizontalLayoutGroup**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `spacing` | float | 4 | 根 | `target: spacing` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `childAlignment` | TextAnchor | UpperLeft | 根 | `target: childAlignment` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `padding` | RectOffset | [0,0,0,0] | 根 | `target: padding` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `childControlWidth` | bool | true | 根 | `target: childControlWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childControlHeight` | bool | true | 根 | `target: childControlHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `childForceExpandWidth` | bool | true | 根 | `target: childForceExpandWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childForceExpandHeight` | bool | true | 根 | `target: childForceExpandHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `childScaleWidth` | bool | false | 根 | `target: childScaleWidth` | 微调项，省略安全 |  |
| 🟢 可选 | `childScaleHeight` | bool | false | 根 | `target: childScaleHeight` | 微调项，省略安全 |  |
| 🟢 可选 | `reverseArrangement` | bool | false | 根 | `target: reverseArrangement` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → （无）
  - 🟡 建议 → `width`、`height`、`backgroundSprite`、`backgroundColor`、`spacing`、`childAlignment`、`padding`

  ```json
  {
    "brickId": "ugui.stack_horizontal",
    "name": "stack_horizontal_demo",
    "parameters": {}
  }
  ```

- **组合策略**：allowedChildren=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view", "ugui.button", "ugui.label", "ugui.value_text", "ugui.icon", "ugui.sprite", "ugui.panel", "ugui.slider", "ugui.toggle", "ugui.dropdown", "ugui.progress_bar", "ugui.input_field"] · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view", "ugui.modal", "ugui.panel"]
- **布局契约**：self: hug/fill/hug/fill

### ugui.grid（30 参数 · 🔴0 🟡8 🟢22）

- **brickId**：`ugui.grid` · **layer**：layout · **category**：grid
- **requiredComponents: GridLayoutGroup**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `cellSize` | Vector2 | [100,100] | 根 | `target: cellSize` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `spacing` | Vector2 | [4,4] | 根 | `target: spacing` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `childAlignment` | TextAnchor | UpperLeft | 根 | `target: childAlignment` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `padding` | RectOffset | [0,0,0,0] | 根 | `target: padding` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `startCorner` | Corner | UpperLeft | 根 | `target: startCorner` | 微调项，省略安全 |  |
| 🟢 可选 | `constraint` | Constraint | Flexible | 根 | `target: constraint` | 微调项，省略安全 |  |
| 🟢 可选 | `constraintCount` | int | 2 | 根 | `target: constraintCount` | 微调项，省略安全 |  |
| 🟢 可选 | `startAxis` | Axis | Horizontal | 根 | `target: startAxis` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → （无）
  - 🟡 建议 → `width`、`height`、`backgroundSprite`、`backgroundColor`、`cellSize`、`spacing`、`childAlignment`、`padding`

  ```json
  {
    "brickId": "ugui.grid",
    "name": "grid_demo",
    "parameters": {}
  }
  ```

- **组合策略**：allowedChildren=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.button", "ugui.label", "ugui.value_text", "ugui.icon", "ugui.sprite", "ugui.slider", "ugui.toggle", "ugui.dropdown", "ugui.progress_bar", "ugui.input_field", "ugui.panel"] · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.scroll_view", "ugui.panel"]
- **布局契约**：self: fill/hug/fill

### ugui.scroll_view（54 参数 · 🔴1 🟡13 🟢40）

- **brickId**：`ugui.scroll_view` · **layer**：layout · **category**：scroll
- **requiredComponents: ScrollRect**
- **childStructure**（工厂自动创建的内部结构）：
  - Background(Image)
  - Viewport(Image,Mask)
    - Content(RectTransform)
  - ScrollbarVertical(Scrollbar,Image) [可选]
    - SlidingArea(RectTransform)
      - Handle(Image)
  - ScrollbarHorizontal(Scrollbar,Image) [可选]
    - SlidingArea(RectTransform)
      - Handle(Image)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `contentLayout` | string |  | 根 | `handler:SetContentLayout` | 缺失则 Content 无 LayoutGroup、子项重叠 | Layout group mounted on Viewport/Content: vertical\|horizontal\|grid\|none |
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `backgroundSprite` | string |  | 子:Background | `handler:LoadSprite` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 子:Background | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `viewportSprite` | string |  | 子:Viewport | `handler:LoadSprite` | 资源类参数，参考图有对应素材时应传 |  |
| 🟡 建议 | `viewportColor` | Color |  | 子:Viewport | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `verticalScrollbarColor` | Color |  | 子:ScrollbarVertical | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `horizontalScrollbarColor` | Color |  | 子:ScrollbarHorizontal | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `verticalHandleColor` | Color |  | 子:ScrollbarVertical/SlidingArea/Handle | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `horizontalHandleColor` | Color |  | 子:ScrollbarHorizontal/SlidingArea/Handle | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `contentSpacing` | float |  | 根 | `handler:SetContentLayoutProp prop=spacing` | 内容布局观感，参考图应给出 |  |
| 🟡 建议 | `contentPadding` | RectOffset |  | 根 | `handler:SetContentLayoutProp prop=padding` | 内容布局观感，参考图应给出 |  |
| 🟡 建议 | `contentCellSize` | Vector2 |  | 根 | `handler:SetContentLayoutProp prop=cellSize` | 内容布局观感，参考图应给出 |  |
| 🟢 可选 | `backgroundEnabled` | bool |  | 子:Background | `handler:SetBackgroundEnabled` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 子:Background | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `background.preserveAspect` | bool |  | 子:Background | `target: GetComponent<Image>().preserveAspect` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.fillCenter` | bool |  | 子:Background | `target: GetComponent<Image>().fillCenter` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pixelsPerUnitMultiplier` | float |  | 子:Background | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchorMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchorMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.pivot` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().pivot` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.anchoredPosition` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().anchoredPosition` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.sizeDelta` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().sizeDelta` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMin` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMin` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.offsetMax` | Vector2 |  | 子:Background | `target: GetComponent<RectTransform>().offsetMax` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.width` | float |  | 子:Background | `handler:SetSizeDelta axis=x` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `background.height` | float |  | 子:Background | `handler:SetSizeDelta axis=y` | Background 子物体几何/渲染微调 |  |
| 🟢 可选 | `borderRadius` | int |  | 子:Background | `handler:SetBorderRadius` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 子:Background | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 子:Background | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 子:Background | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `horizontal` | bool | false | 根 | `target: horizontal` | 微调项，省略安全 |  |
| 🟢 可选 | `vertical` | bool | true | 根 | `target: vertical` | 微调项，省略安全 |  |
| 🟢 可选 | `movementType` | ScrollRect.MovementType | Clamped | 根 | `target: movementType` | 微调项，省略安全 |  |
| 🟢 可选 | `elasticity` | float | 0.1 | 根 | `target: elasticity` | 微调项，省略安全 |  |
| 🟢 可选 | `inertia` | bool | true | 根 | `target: inertia` | 微调项，省略安全 |  |
| 🟢 可选 | `decelerationRate` | float | 0.135 | 根 | `target: decelerationRate` | 微调项，省略安全 |  |
| 🟢 可选 | `scrollSensitivity` | float | 1 | 根 | `target: scrollSensitivity` | 微调项，省略安全 |  |
| 🟢 可选 | `viewportType` | Image.Type |  | 子:Viewport | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `viewportPreserveAspect` | bool |  | 子:Viewport | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `viewportFillCenter` | bool |  | 子:Viewport | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `viewportPixelsPerUnitMultiplier` | float |  | 子:Viewport | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |
| 🟢 可选 | `viewportShowMaskGraphic` | bool |  | 子:Viewport | `target: GetComponent<Mask>().showMaskGraphic` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalScrollbarSprite` | string |  | 子:ScrollbarVertical | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `verticalScrollbarType` | Image.Type |  | 子:ScrollbarVertical | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalScrollbarPreserveAspect` | bool |  | 子:ScrollbarVertical | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalScrollbarSprite` | string |  | 子:ScrollbarHorizontal | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `horizontalScrollbarType` | Image.Type |  | 子:ScrollbarHorizontal | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalScrollbarPreserveAspect` | bool |  | 子:ScrollbarHorizontal | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalHandleSprite` | string |  | 子:ScrollbarVertical/SlidingArea/Handle | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `horizontalHandleSprite` | string |  | 子:ScrollbarHorizontal/SlidingArea/Handle | `handler:LoadSprite` | 滚动条皮肤，默认样式可用 |  |
| 🟢 可选 | `verticalScrollbar.active` | bool |  | 子:ScrollbarVertical | `handler:SetActive component=Scrollbar` | 微调项，省略安全 |  |
| 🟢 可选 | `horizontalScrollbar.active` | bool |  | 子:ScrollbarHorizontal | `handler:SetActive component=Scrollbar` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `contentLayout`
  - 🟡 建议 → `width`、`height`、`backgroundSprite`、`backgroundColor`、`viewportSprite`、`viewportColor`、`verticalScrollbarColor`、`horizontalScrollbarColor`、`verticalHandleColor`、`horizontalHandleColor`、`contentSpacing`、`contentPadding`、`contentCellSize`

  ```json
  {
    "brickId": "ugui.scroll_view",
    "name": "scroll_view_demo",
    "parameters": {
      "contentLayout": "vertical"
    }
  }
  ```

- **组合策略**：allowedChildren=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.list_item", "ugui.panel"] · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.panel"]
- **布局契约**：self: fill/hug/fill
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

## 4. Container 容器类（面板树根 / 弹窗 / 列表模板）

### ugui.panel（15 参数 · 🔴1 🟡3 🟢11）

- **brickId**：`ugui.panel` · **layer**：primitive · **category**：container · **assemblyOnly**（纯装配、无源 Prefab）
- **requiredComponents: Image, RectTransform**

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `sprite` | Sprite |  | 根 | `target: sprite` | 视觉皮肤，缺失=引擎白底/无图 |  |
| 🟡 建议 | `color` | Color | #FFFFFFFF | 根 | `target: color` | 有引擎默认色，应按参考图调色 |  |
| 🟡 建议 | `width` | float |  | 根 | `handler:SetSizeDelta axis=x` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟡 建议 | `height` | float |  | 根 | `handler:SetSizeDelta axis=y` | 容器有 fill/hug 语义，具体布局通常应显式给 |  |
| 🟢 可选 | `type` | Image.Type | Sliced | 根 | `target: type` | 微调项，省略安全 |  |
| 🟢 可选 | `fillCenter` | bool | true | 根 | `target: fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool | false | 根 | `target: raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `preserveAspect` | bool | false | 根 | `target: preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `shadowEnabled` | bool |  | 根 | `handler:EnsureShadow` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowColor` | Color |  | 根 | `handler:SetShadowProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `shadowDistance` | Vector2 |  | 根 | `handler:SetShadowProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineEnabled` | bool |  | 根 | `handler:EnsureOutline` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineColor` | Color |  | 根 | `handler:SetOutlineProp prop=effectColor` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `outlineDistance` | Vector2 |  | 根 | `handler:SetOutlineProp prop=effectDistance` | 特效/过渡/导航族，有默认 |  |
| 🟢 可选 | `borderRadius` | float |  | 根 | `handler:SetBorderRadius` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `sprite`
  - 🟡 建议 → `color`、`width`、`height`

  ```json
  {
    "brickId": "ugui.panel",
    "name": "panel_demo",
    "parameters": {
      "sprite": "Assets/Games/<GameName>/UI/Images/panel_card.png"
    }
  }
  ```

- **组合策略**：allowedChildren="any" · canNestUnder=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.grid", "ugui.scroll_view", "ugui.modal", "ugui.panel"]
- **布局契约**：self: fill/fill/hug/fixed
- **运行时绑定属性**（UIBindingBuilder 用，非 trees.json 参数）：{"visible": {"target": "gameObject.SetActive", "runtimeType": "bool"}}

### ugui.modal（13 参数 · 🔴2 🟡8 🟢3）

- **brickId**：`ugui.modal` · **layer**：primitive · **category**：container · **assemblyOnly**（纯装配、无源 Prefab）
- **requiredComponents: Canvas, Image, GraphicRaycaster, CanvasScaler**
- **childStructure**（工厂自动创建的内部结构）：
  - ModalPanel(Image)
    - ModalHeaderSlot(RectTransform)
    - ModalContentSlot(RectTransform)
    - ModalFooterSlot(RectTransform)

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `panelWidth` | float |  | 根 | `handler:SetModalSize axis=x` | 缺失则 ModalPanel 全屏拉伸、不像弹窗 |  |
| 🔴 必传 | `panelHeight` | float |  | 根 | `handler:SetModalSize axis=y` | 缺失则 ModalPanel 全屏拉伸、不像弹窗 |  |
| 🟡 建议 | `overlayColor` | Color | #000000CC | 根 | `target: GetComponent<Image>().color` | 有引擎默认，应按设计稿覆盖 |  |
| 🟡 建议 | `sortingOrder` | int | 100 | 根 | `target: GetComponent<Canvas>().sortingOrder` | 有引擎默认，应按设计稿覆盖 |  |
| 🟡 建议 | `panelPadding` | RectOffset |  | 根 | `handler:SetModalPadding` | 有兜底布局，弹窗版式应按设计稿给 |  |
| 🟡 建议 | `headerHeight` | float |  | 根 | `handler:SetModalSlotHeight prop=header` | 有兜底布局，弹窗版式应按设计稿给 |  |
| 🟡 建议 | `footerHeight` | float |  | 根 | `handler:SetModalSlotHeight prop=footer` | 有兜底布局，弹窗版式应按设计稿给 |  |
| 🟡 建议 | `panelSprite` | string |  | 子:ModalPanel | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `panelColor` | Color |  | 子:ModalPanel | `target: GetComponent<Image>().color` | 有引擎默认，应按设计稿覆盖 |  |
| 🟡 建议 | `panelType` | Image.Type |  | 子:ModalPanel | `target: GetComponent<Image>().type` | 有引擎默认，应按设计稿覆盖 |  |
| 🟢 可选 | `panelPreserveAspect` | bool |  | 子:ModalPanel | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `panelFillCenter` | bool |  | 子:ModalPanel | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `panelPixelsPerUnitMultiplier` | float |  | 子:ModalPanel | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `panelWidth`、`panelHeight`
  - 🟡 建议 → `overlayColor`、`sortingOrder`、`panelPadding`、`headerHeight`、`footerHeight`、`panelSprite`、`panelColor`、`panelType`

  ```json
  {
    "brickId": "ugui.modal",
    "name": "modal_demo",
    "parameters": {
      "panelWidth": 520,
      "panelHeight": 380
    }
  }
  ```

- **组合策略**：allowedChildren=["ugui.stack_vertical", "ugui.stack_horizontal", "ugui.button", "ugui.label", "ugui.panel"] · canNestUnder=["ugui.panel"]
- **布局契约**：self: fill/fill

### ugui.list_item（37 参数 · 🔴1 🟡16 🟢20）

- **brickId**：`ugui.list_item` · **layer**：primitive · **category**：container · **assemblyOnly**（纯装配、无源 Prefab）
- **requiredComponents: Button, Image**
- **childStructure**（工厂自动创建的内部结构）：
  - Icon(Image) [可选]
  - Label(Text)
  - Value(Text) [可选]

| 必要性 | 参数 | 类型 | 默认值 | 作用点 | 机制 | 分级理由 | 说明 |
|---|---|---|---|---|---|---|---|
| 🔴 必传 | `text` | string | Item | 根 | `target: GetComponentInChildren<Text>().text` | 内容文本，缺失=占位文案 |  |
| 🟡 建议 | `iconSprite` | Sprite |  | 子:Icon | `target: GetComponent<Image>().sprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `icon.width` | int |  | 子:Icon | `handler:SetSizeDelta axis=x` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `icon.height` | int |  | 子:Icon | `handler:SetSizeDelta axis=y` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `value.text` | string |  | 子:Value | `target: GetComponent<Text>().text` | 内容文本（有默认占位），按设计稿覆盖 |  |
| 🟡 建议 | `fontSize` | int |  | 子:Label | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `fontStyle` | FontStyle |  | 根 | `target: GetComponentInChildren<Text>().fontStyle` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `textColor` | Color |  | 子:Label | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `label.width` | float |  | 子:Label | `handler:SetFlexTextProp prop=preferredWidth` | 行内件尺寸，模板观感应覆盖 |  |
| 🟡 建议 | `textAlignment` | TextAnchor |  | 子:Label | `target: GetComponent<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `childAlignment` | TextAnchor |  | 根 | `target: GetComponent<LayoutGroup>().childAlignment` | 有通用默认（spacing=4/padding=0），版式应按参考图给 |  |
| 🟡 建议 | `lineSpacing` | float |  | 根 | `target: GetComponentInChildren<Text>().lineSpacing` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `value.fontSize` | int |  | 子:Value | `target: GetComponent<Text>().fontSize` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `value.textColor` | Color |  | 子:Value | `target: GetComponent<Text>().color` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `value.textAlignment` | TextAnchor |  | 子:Value | `target: GetComponent<Text>().alignment` | 有引擎默认（通用风格），参考图场景应覆盖 |  |
| 🟡 建议 | `backgroundSprite` | string |  | 根 | `handler:LoadSprite` | 有引擎默认图，参考图皮肤应替换 |  |
| 🟡 建议 | `backgroundColor` | Color |  | 根 | `target: GetComponent<Image>().color` | 有引擎默认色，应按参考图调色 |  |
| 🟢 可选 | `interactable` | bool | true | 根 | `target: interactable` | 运行态/行为开关，默认可用 |  |
| 🟢 可选 | `icon.active` | bool |  | 子:Icon | `handler:SetActive component=Image` | 微调项，省略安全 |  |
| 🟢 可选 | `icon.margin` | int |  | 根 | `handler:SetIconLayout prop=margin` | 微调项，省略安全 |  |
| 🟢 可选 | `value.active` | bool |  | 子:Value | `handler:SetActive component=Text` | 微调项，省略安全 |  |
| 🟢 可选 | `label.flexibleWidth` | float |  | 子:Label | `handler:SetFlexTextProp prop=flexibleWidth` | 布局权重微调 |  |
| 🟢 可选 | `horizontalOverflow` | HorizontalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `verticalOverflow` | VerticalWrapMode |  | 根 | `target: GetComponentInChildren<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `richText` | bool |  | 根 | `target: GetComponentInChildren<Text>().supportRichText` | 微调项，省略安全 |  |
| 🟢 可选 | `raycastTarget` | bool |  | 根 | `target: GetComponentInChildren<Text>().raycastTarget` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFit` | bool |  | 根 | `target: GetComponentInChildren<Text>().resizeTextForBestFit` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMinSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMinSize` | 微调项，省略安全 |  |
| 🟢 可选 | `bestFitMaxSize` | int |  | 根 | `target: GetComponentInChildren<Text>().resizeTextMaxSize` | 微调项，省略安全 |  |
| 🟢 可选 | `value.flexibleWidth` | float |  | 子:Value | `handler:SetFlexTextProp prop=flexibleWidth` | 布局权重微调 |  |
| 🟢 可选 | `value.horizontalOverflow` | HorizontalWrapMode |  | 子:Value | `target: GetComponent<Text>().horizontalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `value.verticalOverflow` | VerticalWrapMode |  | 子:Value | `target: GetComponent<Text>().verticalOverflow` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundType` | Image.Type |  | 根 | `target: GetComponent<Image>().type` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPreserveAspect` | bool |  | 根 | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundFillCenter` | bool |  | 根 | `target: GetComponent<Image>().fillCenter` | 微调项，省略安全 |  |
| 🟢 可选 | `backgroundPixelsPerUnitMultiplier` | float |  | 根 | `target: GetComponent<Image>().pixelsPerUnitMultiplier` | 微调项，省略安全 |  |
| 🟢 可选 | `icon.preserveAspect` | bool |  | 子:Icon | `target: GetComponent<Image>().preserveAspect` | 微调项，省略安全 |  |

- **最小参数集**：🔴 必传 → `text`
  - 🟡 建议 → `iconSprite`、`icon.width`、`icon.height`、`value.text`、`fontSize`、`fontStyle`、`textColor`、`label.width`、`textAlignment`、`childAlignment`、`lineSpacing`、`value.fontSize`、`value.textColor`、`value.textAlignment`、`backgroundSprite`、`backgroundColor`

  ```json
  {
    "brickId": "ugui.list_item",
    "name": "list_item_demo",
    "parameters": {
      "text": "文本内容",
      "fontSize": 28,
      "fontStyle": "Bold",
      "textColor": "#FFFFFFFF"
    }
  }
  ```

- **组合策略**：allowedChildren="none" · canNestUnder=["ugui.scroll_view"]
- **布局契约**：self: fill/hug/fixed
- **事件绑定**：{"clicked": "onClick.AddListener"}

---

*生成时间：2026-09-15 20:06 · 生成器：`Docs/gen_brick_param_docs.py`*