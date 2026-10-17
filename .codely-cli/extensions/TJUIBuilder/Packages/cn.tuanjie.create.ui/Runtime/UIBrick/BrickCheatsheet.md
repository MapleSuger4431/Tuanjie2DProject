# UIBrick 速查卡 — UIToolkit（17 Bricks，勿手改）

> 由 `gen_brick_artifacts.py` 从 `Definitions/**/*.definition.json` 自动提取。Brick 增删/改后重跑该脚本刷新（BrickIndex.json 同步更新）。

> UGUI 模式见 `BrickCheatsheet.ugui.md`。

> **用法：写 UXML/USS/Panel 时 `grep` 你要用的 Brick 的 brickId 即可命中其全部参数。**

> 简单 Brick = 内联元素 + 引 USS；复杂 Brick = Template/Instance。详见 panel-builder SKILL.md。


## layout

### layout.grid.basic
- Layer: layout | Category: grid
- AssetSlots: —
- StyleProps: columns[2,3,4,5,6]; gap[xs,sm,md,lg]
- BindingProps: visible(bool)
- Composition: allowedChildren=primitive.*; canNestUnder=layout.*
- Events: —
- Layout: widthMode=fill; heightMode=hug,fill

### layout.list.item
- Layer: layout | Category: list
- AssetSlots: iconAsset (asset?)
- StyleProps: variant[default,selected,highlighted]
- BindingProps: text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.scroll.view
- Events: clicked(ui_event)
- Layout: widthMode=fill; heightMode=hug,fixed

### layout.modal.shell
- Layer: layout | Category: modal
- AssetSlots: backgroundImage (asset?)
- StyleProps: size[sm,md,lg]
- BindingProps: visible(bool)
- Composition: allowedChildren=layout.*,primitive.*; canNestUnder=none
- Events: —
- Layout: widthMode=normal; heightMode=normal

### layout.scroll.view
- Layer: layout | Category: scroll
- AssetSlots: —
- StyleProps: padding[none,sm,md,lg]; listMode[off,on]
- BindingProps: visible(bool)
- Composition: allowedChildren=layout.*,layout.list.item; canNestUnder=layout.*
- Events: —
- Layout: widthMode=fill; heightMode=hug,fill

### layout.stack.horizontal
- Layer: layout | Category: stack
- AssetSlots: —
- StyleProps: gap[xs,sm,md,lg,xl]; padding[none,sm,md,lg]; align[start,center,end,stretch]
- BindingProps: visible(bool)
- Composition: allowedChildren=layout.*,primitive.*; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill

### layout.stack.vertical
- Layer: layout | Category: stack
- AssetSlots: —
- StyleProps: gap[xs,sm,md,lg,xl]; padding[none,sm,md,lg]; align[start,center,end,stretch]
- BindingProps: visible(bool)
- Composition: allowedChildren=layout.*,primitive.*; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill


## primitive

### primitive.container.panel
- Layer: primitive | Category: container
- AssetSlots: backgroundImage (asset?)
- StyleProps: variant[solid,gradient,raised,sunken]; imageType[simple,sliced,tiled,filled]
- BindingProps: visible(bool)
- Composition: allowedChildren=any; canNestUnder=layout.*
- Events: —
- Layout: widthMode=fill; heightMode=fill,hug,fixed

### primitive.image.icon
- Layer: primitive | Category: image
- AssetSlots: iconAsset (asset?)
- StyleProps: size[sm,md,lg]
- BindingProps: visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug; heightMode=hug

### primitive.image.sprite
- Layer: primitive | Category: image
- AssetSlots: spriteAsset (asset?)
- StyleProps: fitMode[cover,contain,fill]
- BindingProps: visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug,fill; heightMode=hug,fill

### primitive.input.button_base
- Layer: primitive | Category: input
- AssetSlots: iconAsset (asset?)
- StyleProps: size[sm,md,lg]; state[disabled,loading]
- BindingProps: text(string); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: clicked(ui_event)
- Layout: widthMode=hug,normal,wide,fill; heightMode=auto; grow=number?; shrink=number?

### primitive.input.dropdown_base
- Layer: primitive | Category: input
- AssetSlots: —
- StyleProps: state[disabled]
- BindingProps: value(number); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: changed(ui_event)
- Layout: widthMode=hug,fill; heightMode=hug

### primitive.input.progress_bar
- Layer: primitive | Category: input
- AssetSlots: —
- StyleProps: variant[accent,danger,success]; size[sm,md,lg]
- BindingProps: value(number); visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: —
- Layout: widthMode=fill; heightMode=hug

### primitive.input.slider_base
- Layer: primitive | Category: input
- AssetSlots: —
- StyleProps: state[disabled]
- BindingProps: value(number); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: changed(ui_event)
- Layout: widthMode=hug,fill; heightMode=hug

### primitive.input.text_field
- Layer: primitive | Category: input
- AssetSlots: —
- StyleProps: state[disabled]
- BindingProps: value(string); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: changed(ui_event)
- Layout: widthMode=hug,fill; heightMode=hug

### primitive.input.toggle_base
- Layer: primitive | Category: input
- AssetSlots: —
- StyleProps: state[disabled]
- BindingProps: value(bool); visible(bool); enabled(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: changed(ui_event)
- Layout: widthMode=hug; heightMode=hug

### primitive.text.label
- Layer: primitive | Category: text
- AssetSlots: —
- StyleProps: variant[muted,disabled]; size[xs,sm,md,lg,xl]
- BindingProps: text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug,fill; heightMode=auto

### primitive.text.value_text
- Layer: primitive | Category: text
- AssetSlots: —
- StyleProps: variant[muted,disabled,accent]; size[sm,md,lg,xl]
- BindingProps: text(string); visible(bool)
- Composition: allowedChildren=none; canNestUnder=layout.*
- Events: —
- Layout: widthMode=hug,fill; heightMode=auto

