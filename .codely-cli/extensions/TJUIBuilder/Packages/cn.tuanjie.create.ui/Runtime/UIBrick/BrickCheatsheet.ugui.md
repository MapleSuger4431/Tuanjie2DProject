# UGUI Brick 速查卡（自动生成，勿手改）

> 由 `gen_brick_artifacts_ugui.py` 从 `UGUI/{Controls,Layout,Container}/Definitions/ugui.*.definition.json` 生成。
> Brick 增删/参数修改后重跑该脚本刷新（BrickIndex.ugui.json 同步更新）。

## 传参规则（全部 Brick 通用，务必先读）

- **只传需要覆盖的参数**：`CreateWithParams` 的 `Dictionary<string, object>` 中未传的参数保持 Prefab/definition 默认值
- **参数顺序无关**：工厂内部按依赖分层应用（SetActive 创建可选子节点 → Ensure*/Load* 挂组件与资产 → 其余），`icon.active` 与 `icon.spritePath` 谁先谁后都正确
- **Color**：接受 `"#RRGGBB[AA]"` 字符串、`[r,g,b,a]` 数组或命名色（white/black/red/…）；**Vector2/Vector3** 接受数组；**枚举**（Image.Type、TextAnchor、FontStyle、Selectable.Transition…）传字符串名
- **资产路径参数**（`backgroundSprite`、`icon.spritePath`、`customFontPath`、`*SoundPath` 等）传 `Assets/` 开头路径字符串；`type:"Sprite"` 的参数（ugui.panel 的 `sprite` 等）同样支持路径字符串或直接传 `Sprite` 引用；路径无效时告警并跳过
- **optional 子节点预创建但默认 inactive**：childStructure 中标 `optional` 的子节点（Button 的 Icon、progress_bar 的 HandleArea、list_item 的 Value 等）会预创建进 Prefab 但默认不激活；`*.active` 参数（icon.active / handle.active / value.active 等）传 `true` 激活。Dropdown 的 Scrollbar 默认 active（可由 scrollbar.active 关闭）
- **assemblyOnly Brick**：`assemblyOnly: true` 的 brick（panel、list_item、modal）无 Prefab 文件，工厂直接从 definition 构建等效层级（Editor 与 Runtime 一致）
- **PostProcess 自动处理**：Button.targetGraphic 连线、Slider.fillRect/handleRect/targetGraphic、Toggle.graphic/targetGraphic、ScrollRect.viewport/content、Dropdown.template（含隐藏 + Template ScrollRect/Scrollbar/Item Toggle 接线 + 默认 Option A/B/C）、Text 默认字体、无 sprite 的 Icon 自动隐藏（Image.enabled=false）、Selectable.navigation 默认 Automatic（与编辑器一致）
- **面板级 API**：`BrickPanelBuilder.BuildBrickTrees(treesJsonPath, prefabDir)` 一次调用输出 N 个 Prefab + 元素 SafeFind 路径清单 + 组合校验警告，Canvas 配置与保存修复（防黑屏）已内置

## 总览

| brickId | 用途 | 典型用例 |
|---------|------|---------|
| ugui.button | 按钮（带完整视听参数化） | 开始游戏、确认、暂停 |
| ugui.label | 静态文字标签 | 标题、栏目名、说明文字 |
| ugui.value_text | 动态数值文字 | 分数、金币、倒计时（动态数值显示） |
| ugui.icon | 小图标（可点击，支持音效/缩放动效/交互着色） | 功能图标、状态图标、可点击的返回/关闭图标 |
| ugui.sprite | 大图/精灵图（可填充区域） | 头像、背景图、物品展示 |
| ugui.slider | 滑块（双向绑定，可选居中数值文字） | 音量、亮度、难度；数值文字默认 inactive 不渲染，开启须 label.active:true 与 text 成对传（同 icon.active↔spritePath 惯例），文字参数集同 progress_bar；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层 |
| ugui.toggle | 开关（带勾选框和文字） | 音效/音乐开关；根挂 flex（Background 固定方 20×20 + Label 吃剩余，间距 3）——改勾选框大小传 backgroundWidth/Height（自动转 LE preferred，文本让位）；挪列配方=paddingLeft+label.width+label.flexibleWidth:0+checkbox.margin（label.width 须伴随非空 text）+checkbox.position:"right"；checkbox.position 控勾选框左/右/上（right 走 reverseOrder、打包安全），childAlignment 控整列打包；rect 直改类参数已删除（组驱动下直改 rect 会被布局 pass 覆写，操作 LayoutElement.preferred 与组 padding 是正道） |
| ugui.dropdown | 下拉选择框 | 语言、分辨率、画质选择；caption 对收在 Caption Area 包装层内挂 flex（Label 吃剩余 + Arrow 固定 20×20，间距 5）——root/Template 无布局组（引擎交接面），arrow.width/height 自动让位（组下转 LE preferred）；Template 子树结构勿动（Dropdown.Show 手动排布依赖） |
| ugui.progress_bar | 进度条（无手柄，可带居中百分比文字） | 加载、血量、经验；进度百分比传 text:'50%'（默认空串不渲染，运行时由游戏代码更新），文字参数集同 label brick；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层 |
| ugui.input_field | 输入框 | 玩家昵称输入、搜索框；自定义光标色须 customCaretColor:true 与 caretColor 成对传——uGUI 的 caretColor getter 被 customCaretColor 开关门控，单传 caretColor 只写入存储、运行时光标仍用文本色（不可见生效），与 shadowEnabled↔shadowColor 伴随使能同惯例 |
| ugui.stack_vertical | 垂直布局 | 设置面板、主菜单按钮组；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill |
| ugui.stack_horizontal | 水平布局 | HUD 底部栏、工具栏；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill |
| ugui.grid | 网格布局 | 背包、关卡选择网格 |
| ugui.scroll_view | 滚动/列表容器 | 设置列表滚动、商店滚动；Content 可直接挂 ugui.list_item / stack / grid（排行榜、好友列表用 ugui.list_item 动态填充）；背景配色传 viewportColor、隐藏背景传 viewportShowMaskGraphic:false（保留滚动遮罩）——勿直接改 Viewport 的 Image.alpha（alpha=0 会挖空 stencil，子物体全部隐形）或 Mask 组件值（装配时按默认/参数重置） |
| ugui.list_item | 列表条目（图标+标签+值三段式） | 排行榜行、好友列表行、邮件列表行 |
| ugui.panel | 面板容器（9-slice 拉伸） | 面板容器、导航栏底色、标题栏背景、卡片面板、排队容器下的自由布局壳；assemblyOnly 的 preferredHeight 实测仅 10（占位假值）——挂入 stack / scroll_view(contentLayout) 等 preferred 排队场景必须显式传 height，否则被排成 10px 扁条；grid 不受影响（cellSize 硬驱动不读 preferred）；装配默认带白色 9-slice 底图，当透明壳用请换 sprite 或改用纯容器节点（省略 brickId） |
| ugui.modal | 弹窗壳体（独立 Canvas 层） | 确认弹窗、设置弹窗（独立 Canvas 层） |

## 详细参数

#### ugui.button
- 用途: 按钮（带完整视听参数化）
- UGUI 组件: `Button + Image(背景) + Text(文字) + Image(图标,可选)`
- Prefab: `UGUI/Controls/Prefabs/ButtonBase.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); text(string,Button); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor); lineSpacing(float); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); customFontPath(string,路径); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); interactable(bool)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
  - icon.*: icon.spritePath(string,路径); icon.width(int); icon.height(int); icon.position(string); icon.margin(int); icon.color(Color); icon.preserveAspect(bool); icon.active(bool)
  - effects.*: effects.enabled(bool); effects.pressScale(Vector3); effects.pressScaleDuration(float); effects.hoverScale(Vector3); effects.hoverScaleDuration(float)
- BindingProps: text(string); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.modal,ugui.panel
- Events: clicked(ui_event)
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 开始游戏、确认、暂停

#### ugui.label
- 用途: 静态文字标签
- UGUI 组件: `Text`
- Prefab: `UGUI/Controls/Prefabs/Label.prefab`
- Parameters:
  - 基础: text(string,Label); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor,MiddleLeft); lineSpacing(float); customFontPath(string,路径); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); outlineEnabled(bool); outlineColor(Color); outlineDistance(Vector2); gradientEnabled(bool); gradientTopColor(Color); gradientBottomColor(Color); underlineEnabled(bool); underlineColor(Color); strikethroughEnabled(bool); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); paddingLeft(float); paddingRight(float); paddingTop(float); paddingBottom(float); borderRadius(int); width(float); height(float); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); sizeDelta(Vector2); offsetMin(Vector2); offsetMax(Vector2); localScale(Vector3); localRotation(Vector3); opacity(float)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
- BindingProps: text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.modal,ugui.panel
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug
- 用例: 标题、栏目名、说明文字

#### ugui.value_text
- 用途: 动态数值文字
- UGUI 组件: `Text`
- Prefab: `UGUI/Controls/Prefabs/ValueText.prefab`
- Parameters:
  - 基础: width(float); height(float); text(string,0); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor,MiddleLeft); lineSpacing(float); customFontPath(string,路径); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); paddingLeft(float); paddingRight(float); paddingTop(float); paddingBottom(float); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); gradientEnabled(bool); gradientTopColor(Color); gradientBottomColor(Color); underlineEnabled(bool); underlineColor(Color); strikethroughEnabled(bool); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
- BindingProps: text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: —
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 分数、金币、倒计时（动态数值显示）

#### ugui.icon
- 用途: 小图标（可点击，支持音效/缩放动效/交互着色）
- UGUI 组件: `Image + Button(可点击)`
- Prefab: `UGUI/Controls/Prefabs/Icon.prefab`
- Parameters:
  - 基础: spritePath(string,路径); color(Color); width(float); height(float); imageType(Image.Type); preserveAspect(bool); raycastTarget(bool); opacity(float); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); clickSoundPath(string,路径); hoverSoundPath(string,路径); audioVolume(float); clickScale(Vector3); clickScaleDuration(float); hoverScale(Vector3); hoverScaleDuration(float); interactable(bool); clickable(bool,false); transitionMode(Selectable.Transition); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
- BindingProps: visible(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: clicked(ui_event)
- Layout: widthMode=hug; heightMode=hug
- 用例: 功能图标、状态图标、可点击的返回/关闭图标

#### ugui.sprite
- 用途: 大图/精灵图（可填充区域）
- UGUI 组件: `Image`
- Prefab: `UGUI/Controls/Prefabs/Sprite.prefab`
- Parameters:
  - 基础: spritePath(string,路径); color(Color); width(float); height(float); imageType(Image.Type); preserveAspect(bool); raycastTarget(bool); opacity(float); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); showSoundPath(string,路径); audioVolume(float); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3)
- BindingProps: visible(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill
- 用例: 头像、背景图、物品展示

#### ugui.slider
- 用途: 滑块（双向绑定，可选居中数值文字）
- UGUI 组件: `Slider + Image×3(背景/填充/手柄) + Label/Text(可选)`
- Prefab: `UGUI/Controls/Prefabs/SliderBase.prefab`
- Parameters:
  - 基础: width(float); height(float); direction(Slider.Direction); minValue(float); maxValue(float); value(float); wholeNumbers(bool); backgroundColor(Color); backgroundSprite(string,路径); backgroundType(Image.Type); backgroundPreserveAspect(bool); backgroundFillCenter(bool); backgroundPixelsPerUnitMultiplier(float); fillColor(Color); fillSprite(string,路径); fillType(Image.Type,Filled); fillMethod(Image.FillMethod,Horizontal); fillOrigin(int,0); fillPreserveAspect(bool); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); text(string,); fontSize(int,14); fontStyle(FontStyle); textColor(Color,0.196x0.196x0.196x1); textAlignment(TextAnchor,MiddleCenter); lineSpacing(float); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); customFontPath(string,路径); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3); interactable(bool)
  - handle.*: handle.spritePath(string,路径); handle.color(Color); handle.width(int); handle.height(int); handle.preserveAspect(bool); handle.type(Image.Type)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
  - label.*: label.active(bool,false)
- BindingProps: value(float); text(string); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: changed(ui_event)
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 音量、亮度、难度；数值文字默认 inactive 不渲染，开启须 label.active:true 与 text 成对传（同 icon.active↔spritePath 惯例），文字参数集同 progress_bar；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层

#### ugui.toggle
- 用途: 开关（带勾选框和文字）
- UGUI 组件: `Toggle + Image×2(背景/勾选) + Text(标签) + UGUIFlexLayoutGroup(根,声明式)`
- Prefab: `UGUI/Controls/Prefabs/ToggleBase.prefab`
- Parameters:
  - 基础: width(float); height(float); isOn(bool,true); toggleTransition(Toggle.ToggleTransition); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); backgroundPreserveAspect(bool); backgroundFillCenter(bool); backgroundPixelsPerUnitMultiplier(float); backgroundWidth(int); backgroundHeight(int); childAlignment(TextAnchor); borderRadius(int); checkmarkSprite(string,路径); checkmarkColor(Color); checkmarkWidth(int); checkmarkHeight(int); checkmarkPreserveAspect(bool); text(string,Toggle); paddingLeft(float); paddingRight(float); paddingTop(float); paddingBottom(float); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor,MiddleLeft); lineSpacing(float); customFontPath(string,路径); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3); interactable(bool)
  - checkbox.*: checkbox.position(string); checkbox.margin(int)
  - label.*: label.width(float); label.flexibleWidth(float); label.minWidth(float)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
- BindingProps: isOn(bool); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: changed(ui_event)
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 音效/音乐开关；根挂 flex（Background 固定方 20×20 + Label 吃剩余，间距 3）——改勾选框大小传 backgroundWidth/Height（自动转 LE preferred，文本让位）；挪列配方=paddingLeft+label.width+label.flexibleWidth:0+checkbox.margin（label.width 须伴随非空 text）+checkbox.position:"right"；checkbox.position 控勾选框左/右/上（right 走 reverseOrder、打包安全），childAlignment 控整列打包；rect 直改类参数已删除（组驱动下直改 rect 会被布局 pass 覆写，操作 LayoutElement.preferred 与组 padding 是正道）

#### ugui.dropdown
- 用途: 下拉选择框
- UGUI 组件: `Dropdown + Caption Area[Text(当前值)+Image(箭头)+UGUIFlexLayoutGroup] + Template(展开面板)`
- Prefab: `UGUI/Controls/Prefabs/DropdownBase.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); value(int); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor); lineSpacing(float); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); customFontPath(string,路径); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); templateHeight(int); itemHeight(int); templateBackgroundColor(Color); templateBackgroundSprite(string,路径); templateBackgroundType(Image.Type); itemBackgroundColor(Color,1x1x1x0.05); itemBackgroundSprite(string,路径); itemBackgroundType(Image.Type); itemTextColor(Color); itemFontSize(int); itemCheckmarkSprite(string,路径); itemCheckmarkColor(Color); itemCheckmarkType(Image.Type); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3); interactable(bool)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
  - arrow.*: arrow.spritePath(string,路径); arrow.color(Color); arrow.type(Image.Type); arrow.width(int); arrow.height(int)
  - itemBackground.*: itemBackground.active(bool,true)
  - scrollbar.*: scrollbar.active(bool,true); scrollbar.color(Color); scrollbar.spritePath(string,路径); scrollbar.type(Image.Type); scrollbar.handleColor(Color); scrollbar.handleSprite(string,路径); scrollbar.handleType(Image.Type)
- BindingProps: value(int); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: changed(ui_event)
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 语言、分辨率、画质选择；caption 对收在 Caption Area 包装层内挂 flex（Label 吃剩余 + Arrow 固定 20×20，间距 5）——root/Template 无布局组（引擎交接面），arrow.width/height 自动让位（组下转 LE preferred）；Template 子树结构勿动（Dropdown.Show 手动排布依赖）

#### ugui.progress_bar
- 用途: 进度条（无手柄，可带居中百分比文字）
- UGUI 组件: `Slider + Image×2(背景/填充) + Label/Text(居中文字)`
- Prefab: `UGUI/Controls/Prefabs/ProgressBar.prefab`
- Parameters:
  - 基础: width(float); height(float); direction(Slider.Direction); minValue(float); maxValue(float); value(float); wholeNumbers(bool); backgroundColor(Color); backgroundSprite(string,路径); backgroundType(Image.Type); backgroundPreserveAspect(bool); backgroundFillCenter(bool); backgroundPixelsPerUnitMultiplier(float); fillColor(Color); fillSprite(string,路径); fillType(Image.Type,Filled); fillMethod(Image.FillMethod,Horizontal); fillOrigin(int,0); fillPreserveAspect(bool); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); text(string,); fontSize(int,14); fontStyle(FontStyle); textColor(Color,0.196x0.196x0.196x1); textAlignment(TextAnchor,MiddleCenter); lineSpacing(float); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); customFontPath(string,路径); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3); interactable(bool)
  - handle.*: handle.active(bool,false); handle.preserveAspect(bool); handle.width(int); handle.height(int)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
  - label.*: label.active(bool,true)
- BindingProps: value(float); text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: —
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 加载、血量、经验；进度百分比传 text:'50%'（默认空串不渲染，运行时由游戏代码更新），文字参数集同 label brick；Label 为包装层——Text 组件挂在 Label/Text 上，直挂 Text 到根会触发 AttachDynamicLayoutGroups 给根挂 flex 布局组破坏 Slider 锚点几何，勿拍平该层

#### ugui.input_field
- 用途: 输入框
- UGUI 组件: `InputField + Image(背景) + Text×2(占位符/实际输入)`
- Prefab: `UGUI/Controls/Prefabs/InputFieldBase.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); opacity(float); transitionMode(Selectable.Transition); text(string,); contentType(InputField.ContentType); lineType(InputField.LineType); characterLimit(int); caretColor(Color); customCaretColor(bool); selectionColor(Color); caretBlinkRate(float); caretWidth(int); customFontPath(string,路径); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor,MiddleLeft); lineSpacing(float); richText(bool); raycastTarget(bool); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); textShadowEnabled(bool); textShadowColor(Color); textShadowDistance(Vector2); textOutlineEnabled(bool); textOutlineColor(Color); textOutlineDistance(Vector2); placeholderText(string,Enter text...); placeholderColor(Color); placeholderFontSize(int); anchorMin(Vector2); anchorMax(Vector2); pivot(Vector2); anchoredPosition(Vector2); localScale(Vector3); localRotation(Vector3); interactable(bool)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
  - navigation.*: navigation.mode(Navigation.Mode,Automatic); navigation.wrapAround(bool,false)
  - tint.*: tint.normalColor(Color); tint.highlightedColor(Color); tint.pressedColor(Color); tint.selectedColor(Color); tint.disabledColor(Color); tint.fadeDuration(float)
  - spriteSwap.*: spriteSwap.highlightedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.pressedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.selectedSprite(Sprite,支持传Assets/路径字符串); spriteSwap.disabledSprite(Sprite,支持传Assets/路径字符串)
  - animation.*: animation.normalTrigger(string); animation.highlightedTrigger(string); animation.pressedTrigger(string); animation.selectedTrigger(string); animation.disabledTrigger(string); animation.animatorEnabled(bool); animation.animatorControllerPath(string)
  - label.*: label.active(bool,false); label.position(string,left); label.spacing(float,8); label.text(string,); label.fontSize(int,14); label.fontStyle(FontStyle); label.textColor(Color,0.196x0.196x0.196x1); label.textAlignment(TextAnchor,MiddleCenter); label.lineSpacing(float); label.richText(bool); label.raycastTarget(bool); label.horizontalOverflow(HorizontalWrapMode); label.verticalOverflow(VerticalWrapMode); label.bestFit(bool); label.bestFitMinSize(int); label.bestFitMaxSize(int); label.width(float); label.flexibleWidth(float); label.minWidth(float); label.textShadowEnabled(bool); label.textShadowColor(Color); label.textShadowDistance(Vector2); label.textOutlineEnabled(bool); label.textOutlineColor(Color); label.textOutlineDistance(Vector2)
- BindingProps: text(string); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.panel
- Events: changed(ui_event); valueChanged(ui_event)
- Layout: widthMode=hug,fixed,fill; heightMode=hug,fixed
- 用例: 玩家昵称输入、搜索框；自定义光标色须 customCaretColor:true 与 caretColor 成对传——uGUI 的 caretColor getter 被 customCaretColor 开关门控，单传 caretColor 只写入存储、运行时光标仍用文本色（不可见生效），与 shadowEnabled↔shadowColor 伴随使能同惯例

#### ugui.stack_vertical
- 用途: 垂直布局
- UGUI 组件: `Root[RectTransform+VerticalLayoutGroup]；子元素直挂 Root 自动排列，preferred 高度沿布局链向上传播`
- Prefab: `UGUI/Layout/Prefabs/StackVertical.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); spacing(float,4); childAlignment(TextAnchor,UpperLeft); childControlWidth(bool,true); childControlHeight(bool,true); padding(RectOffset,0x0x0x0); childForceExpandWidth(bool,true); childForceExpandHeight(bool,true); childScaleWidth(bool,false); childScaleHeight(bool,false); reverseArrangement(bool,false); layoutEnabled(bool)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
- BindingProps: —
- Composition: allowedChildren=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.scroll_view,ugui.button,ugui.label,ugui.value_text,ugui.icon,ugui.sprite,ugui.panel,ugui.slider,ugui.toggle,ugui.dropdown,ugui.progress_bar,ugui.input_field; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.scroll_view,ugui.modal,ugui.panel
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill
- 用例: 设置面板、主菜单按钮组；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill

#### ugui.stack_horizontal
- 用途: 水平布局
- UGUI 组件: `Root[RectTransform+HorizontalLayoutGroup]；子元素直挂 Root 自动排列，preferred 尺寸沿布局链向上传播`
- Prefab: `UGUI/Layout/Prefabs/StackHorizontal.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); spacing(float,4); childAlignment(TextAnchor,UpperLeft); childControlWidth(bool,true); childControlHeight(bool,true); padding(RectOffset,0x0x0x0); childForceExpandWidth(bool,true); childForceExpandHeight(bool,true); childScaleWidth(bool,false); childScaleHeight(bool,false); reverseArrangement(bool,false); layoutEnabled(bool)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
- BindingProps: —
- Composition: allowedChildren=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.scroll_view,ugui.button,ugui.label,ugui.value_text,ugui.icon,ugui.sprite,ugui.panel,ugui.slider,ugui.toggle,ugui.dropdown,ugui.progress_bar,ugui.input_field; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.scroll_view,ugui.modal,ugui.panel
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill
- 用例: HUD 底部栏、工具栏；layoutEnabled:false=纯定位容器——布局组停驱（子元素锚点/位置自由，做参考图百分比布局用，引擎 childControl=false 救不了锚点驱动），但不再向父布局组报告 preferred，须自带 width/height 或锚点 fill

#### ugui.grid
- 用途: 网格布局
- UGUI 组件: `GridLayoutGroup + RectTransform`
- Prefab: `UGUI/Layout/Prefabs/GridBasic.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); cellSize(Vector2,100x100); spacing(Vector2,4x4); startCorner(Corner,UpperLeft); constraint(Constraint,Flexible); constraintCount(int,2); startAxis(Axis,Horizontal); childAlignment(TextAnchor,UpperLeft); padding(RectOffset,0x0x0x0)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
- BindingProps: —
- Composition: allowedChildren=ugui.stack_vertical,ugui.stack_horizontal,ugui.button,ugui.label,ugui.value_text,ugui.icon,ugui.sprite,ugui.slider,ugui.toggle,ugui.dropdown,ugui.progress_bar,ugui.input_field,ugui.panel; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.scroll_view,ugui.panel
- Events: —
- Layout: widthMode=fill; heightMode=hug,fill
- 用例: 背包、关卡选择网格

#### ugui.scroll_view
- 用途: 滚动/列表容器
- UGUI 组件: `ScrollRect + Image(Mask) + Scrollbar×2(可选)`
- Prefab: `UGUI/Layout/Prefabs/ScrollView.prefab`
- Parameters:
  - 基础: width(float); height(float); backgroundEnabled(bool); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); borderRadius(int); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); horizontal(bool,false); vertical(bool,true); movementType(ScrollRect.MovementType,Clamped); elasticity(float,0.1); inertia(bool,true); decelerationRate(float,0.135); scrollSensitivity(float,1); viewportSprite(string,路径); viewportColor(Color); viewportType(Image.Type); viewportPreserveAspect(bool); viewportFillCenter(bool); viewportPixelsPerUnitMultiplier(float); viewportShowMaskGraphic(bool); verticalScrollbarSprite(string,路径); verticalScrollbarColor(Color); verticalScrollbarType(Image.Type); verticalScrollbarPreserveAspect(bool); horizontalScrollbarSprite(string,路径); horizontalScrollbarColor(Color); horizontalScrollbarType(Image.Type); horizontalScrollbarPreserveAspect(bool); verticalHandleSprite(string,路径); verticalHandleColor(Color); horizontalHandleSprite(string,路径); horizontalHandleColor(Color); contentLayout(string); contentSpacing(float); contentPadding(RectOffset); contentCellSize(Vector2)
  - background.*: background.preserveAspect(bool); background.fillCenter(bool); background.pixelsPerUnitMultiplier(float); background.anchorMin(Vector2); background.anchorMax(Vector2); background.pivot(Vector2); background.anchoredPosition(Vector2); background.sizeDelta(Vector2); background.offsetMin(Vector2); background.offsetMax(Vector2); background.width(float); background.height(float)
  - verticalScrollbar.*: verticalScrollbar.active(bool)
  - horizontalScrollbar.*: horizontalScrollbar.active(bool)
- BindingProps: visible(bool)
- Composition: allowedChildren=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.list_item,ugui.panel; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.panel
- Events: —
- Layout: widthMode=fill; heightMode=hug,fill
- 用例: 设置列表滚动、商店滚动；Content 可直接挂 ugui.list_item / stack / grid（排行榜、好友列表用 ugui.list_item 动态填充）；背景配色传 viewportColor、隐藏背景传 viewportShowMaskGraphic:false（保留滚动遮罩）——勿直接改 Viewport 的 Image.alpha（alpha=0 会挖空 stencil，子物体全部隐形）或 Mask 组件值（装配时按默认/参数重置）

#### ugui.list_item
- 用途: 列表条目（图标+标签+值三段式）
- UGUI 组件: `Button + Image(背景) + Text(标签) + Text(值,可选) + Image(图标,可选)`
- AssemblyOnly: true (无 Prefab 文件，从 definition 构建)
- Parameters:
  - 基础: text(string,Item); interactable(bool,true); iconSprite(Sprite,支持传Assets/路径字符串); fontSize(int); fontStyle(FontStyle); textColor(Color); textAlignment(TextAnchor); childAlignment(TextAnchor); horizontalOverflow(HorizontalWrapMode); verticalOverflow(VerticalWrapMode); lineSpacing(float); richText(bool); raycastTarget(bool); bestFit(bool); bestFitMinSize(int); bestFitMaxSize(int); backgroundSprite(string,路径); backgroundColor(Color); backgroundType(Image.Type); backgroundPreserveAspect(bool); backgroundFillCenter(bool); backgroundPixelsPerUnitMultiplier(float)
  - icon.*: icon.active(bool); icon.width(int); icon.height(int); icon.margin(int); icon.preserveAspect(bool)
  - value.*: value.active(bool); value.text(string); value.fontSize(int); value.textColor(Color); value.flexibleWidth(float); value.textAlignment(TextAnchor); value.horizontalOverflow(HorizontalWrapMode); value.verticalOverflow(VerticalWrapMode)
  - label.*: label.width(float); label.flexibleWidth(float)
- BindingProps: —
- Composition: allowedChildren=none; canNestUnder=ugui.scroll_view
- Events: clicked(ui_event)
- Layout: widthMode=fill; heightMode=hug,fixed
- 用例: 排行榜行、好友列表行、邮件列表行

#### ugui.panel
- 用途: 面板容器（9-slice 拉伸）
- UGUI 组件: `Image(Sliced 9-slice)，根无布局组——子节点零驱动（自由摆位层）`
- AssemblyOnly: true (无 Prefab 文件，从 definition 构建)
- Parameters:
  - 基础: sprite(Sprite,支持传Assets/路径字符串); color(Color,#FFFFFFFF); type(Image.Type,Sliced); fillCenter(bool,true); raycastTarget(bool,false); preserveAspect(bool,false); width(float); height(float); shadowEnabled(bool); shadowColor(Color); shadowDistance(Vector2); outlineEnabled(bool); outlineColor(Color); outlineDistance(Vector2); borderRadius(float)
- BindingProps: visible(bool)
- Composition: allowedChildren=any; canNestUnder=ugui.stack_vertical,ugui.stack_horizontal,ugui.grid,ugui.scroll_view,ugui.modal,ugui.panel
- Events: —
- Layout: widthMode=fill; heightMode=fill,hug,fixed
- 用例: 面板容器、导航栏底色、标题栏背景、卡片面板、排队容器下的自由布局壳；assemblyOnly 的 preferredHeight 实测仅 10（占位假值）——挂入 stack / scroll_view(contentLayout) 等 preferred 排队场景必须显式传 height，否则被排成 10px 扁条；grid 不受影响（cellSize 硬驱动不读 preferred）；装配默认带白色 9-slice 底图，当透明壳用请换 sprite 或改用纯容器节点（省略 brickId）

#### ugui.modal
- 用途: 弹窗壳体（独立 Canvas 层）
- UGUI 组件: `Canvas + GraphicRaycaster + Image(遮罩) + Image(面板)`
- AssemblyOnly: true (无 Prefab 文件，从 definition 构建)
- Parameters:
  - 基础: overlayColor(Color,#000000CC); sortingOrder(int,100); panelWidth(float); panelHeight(float); panelPadding(RectOffset); headerHeight(float); footerHeight(float); panelSprite(string,路径); panelColor(Color); panelType(Image.Type); panelPreserveAspect(bool); panelFillCenter(bool); panelPixelsPerUnitMultiplier(float)
- BindingProps: —
- Composition: allowedChildren=ugui.stack_vertical,ugui.stack_horizontal,ugui.button,ugui.label,ugui.panel; canNestUnder=ugui.panel
- Events: —
- Layout: widthMode=fill; heightMode=fill
- 用例: 确认弹窗、设置弹窗（独立 Canvas 层）

## 双模式对照表

| 功能 | UIToolkit brickId | UGUI brickId |
|------|-------------------|-------------|
| 垂直布局 | layout.stack.vertical | ugui.stack_vertical |
| 水平布局 | layout.stack.horizontal | ugui.stack_horizontal |
| 网格布局 | layout.grid.basic | ugui.grid |
| 滚动/列表容器 | layout.scroll.view | ugui.scroll_view |
| 列表条目 | layout.list.item | ugui.list_item |
| 弹窗壳体 | layout.modal.shell | ugui.modal |
| 面板容器 | primitive.container.panel | ugui.panel |
| 按钮 | primitive.input.button_base | ugui.button |
| 标签文字 | primitive.text.label | ugui.label |
| 数值文字 | primitive.text.value_text | ugui.value_text |
| 图标 | primitive.image.icon | ugui.icon |
| 大图 | primitive.image.sprite | ugui.sprite |
| 滑块 | primitive.input.slider_base | ugui.slider |
| 开关 | primitive.input.toggle_base | ugui.toggle |
| 下拉选择 | primitive.input.dropdown_base | ugui.dropdown |
| 进度条 | primitive.input.progress_bar | ugui.progress_bar |
| 输入框 | primitive.input.text_field | ugui.input_field |
