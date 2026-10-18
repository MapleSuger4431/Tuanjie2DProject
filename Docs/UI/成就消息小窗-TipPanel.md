# 关卡内 · 成就消息小窗（Tip Panel）设计

> 产物：`Assets/Resources/UIPrefabs/LevelTipPanel.prefab`（Panel 形态，Top 层）
> 真源：生成器 `Assets/Editor/LevelTipPanelBuilder.cs`（菜单 `Tools/关卡/重建成就消息小窗`）
> 风格依据：[`Docs/ArtReference-PurpleWitch/紫色女巫法师-美术与主题参考.md`](../ArtReference-PurpleWitch/紫色女巫法师-美术与主题参考.md)
> 需求关联：`Docs/Requirements/04-成就系统需求.md` AC-04（解锁通知）

---

## 1. 定位

关卡内右下角弹出的一块**做旧魔法便签**，用于播报「成就解锁 / 新纪录 / 元素变化」一类瞬时消息：

- 出现后**停留 5 秒**自动消失（淡入 0.25s → 停留 4.5s → 淡出 0.25s）；
- **不阻断操作**：整条链路 `raycastTarget=false`，不可点击、不抢输入焦点，鼠标照常放置方块；
- 多条消息**排队依次播放**，不叠加、不重叠（AC-04 的排队要求）；
- 与 04 篇 AC-04 的差异：**位置由「屏幕上方滑入」改为「右下角弹出」**（2026-10-18 用户拍板），其余（图标 + 标题 + 正文、约 3~5 秒、排队、不阻断）保持一致。

| 项 | 04 篇原文 | 本设计 |
|---|---|---|
| 位置 | 屏幕上方滑入 | **屏幕右下角弹出**（角锚定） |
| 停留 | 约 3 秒 | **5 秒**（用户拍板） |
| 排队 | 依次播放 | 同 |
| 可交互 | 不可点击 | 同（全部 raycastTarget=false） |

---

## 2. 视觉风格

主线取参考文档第二节第 4 套 **「旧魔导书 Grimoire」**（复古羊皮纸 + 褪色紫，自带旧书页气质，长文本可读性最好），
并按第九节的主题锚点 `Assets/Art/Character/Character_Portrait.png`（**黑底 + 紫 + 品红 + 白**）压一层品红辉光，
使小窗在深紫 HUD（`BoardRuntimePanel` 的 `BarColor #140B23`）上能被一眼看到，又不撞棋盘区的橙/青。

### 2.1 配色

| 用途 | HEX | 来源 |
|---|---|---|
| 便签纸底（羊皮纸） | `#EFE8C3` | Grimoire 中性色 |
| 做旧紫晕染 | `#773971` | Grimoire 主色 |
| 深紫包边 | `#40263E` | Grimoire 背景 |
| 暗金描边 / 铆钉 | `#C9A54E` | Hades 紫金法则（金 ≤ 10%） |
| 品红辉光（主题锚点） | `#FF4FD9` | AI 小人 aiColor |
| 标题文字 | `#2A1436` | 深紫近黑，纸底上对比 12:1 |
| 正文文字 | `#4B3352` | 同上，稍浅一档 |
| 卡片外投影 | `#0A0512` @ 0.45 | 与深紫 HUD 拉开层次 |

配色遵循参考文档 5.3 的两条：紫底上**不放红字/青字**（会糊）；成功案例都靠**米白/深色纸面 + 金描边**保证可读性。
这里纸面是浅色，故文字走深紫而非白。

### 2.2 质感关键词

陈旧（weathered parchment）、褪色紫晕染（faded violet wash）、磨损卷边（worn curling edges）、
茶渍霉斑（foxing speckles）、紫水晶铆钉（amethyst rivets）、暗金细描边（antique gold outline）。
**禁止**：霓虹发光、玻璃拟态、现代扁平色块、任何文字与字母。

---

## 3. 布局（1920×1080 设计分辨率）

右下角角锚定，尺寸固定不随父级拉伸：

| 参数 | 值 | 说明 |
|---|---|---|
| 层级 | `UILayer.Top` | 压在 Popup（结算面板）之上，结算时解锁的成就也能看到 |
| 卡片尺寸 | 520 × 104 | 一行标题 + 一行正文 + 左侧图标 |
| 锚点 | `anchorMin = anchorMax = pivot = (1, 0)` | 右下角 |
| 位置 | `anchoredPosition = (-28, 8)` | 距右 28、距底 8 |
| 图标 | 88 × 88，左内边距 16，垂直居中 | 成就徽章 |
| 标题 | TMP 30，左上，距顶 14 | 如「成就解锁」 |
| 正文 | TMP 24，标题下方，距顶 52 | 具体名称/数值 |

**为什么是这个高度**：右侧面板 `RightPanel` 底边在屏幕 y=48（`reserveBottom`），其中「出发」按钮底边在
y≈166（`PlayTop = 858`，面板高 976）。卡片 y 区间 8~112，**完全落在出发按钮下方**，5 秒遮挡期间
也不压住任何可点元素；且因不拦截射线，即使视觉重叠也不影响点击。

面板根 Stretch 全屏但**没有 Mask、没有背景 Image**（否则会吃掉棋盘点击），只有这张右下角小卡片带背景图。

---

## 4. 结构树

```
LevelTipPanel                      RectTransform（Stretch 全屏，无 Image）
└── TipCard                        Image（做旧便签底图，Sliced）+ CanvasGroup + raycastTarget=false
    ├── Icon                       Image（成就徽章，raycastTarget=false）
    ├── TitleText                  TMP（raycastTarget=false）
    └── MessageText                TMP（raycastTarget=false）
```

约束（与项目其它面板一致）：

- **Panel 形态**：根只有 RectTransform，**不带 Canvas / CanvasScaler / GraphicRaycaster**（由 UIRoot 层画布提供）；
- 常驻打开（随关卡由 `BoardRuntimeDriver` 拉起），**不调用 `HideSelf`**：`destroyOnHide` 默认 true，反复 Show/Hide 会销毁重建；
  显隐只切 `TipCard` 的 `gameObject.SetActive` 与 `CanvasGroup.alpha`；
- 切场景由 `SceneTransition` 的 `sceneLoaded` 钩子统一收掉，本面板不写清理代码；
- 文案真源在 GameText 表，预制体文本节点一律留空。

---

## 5. 行为与生命周期

```text
idle ──收到消息──> fadeIn(0.25s) ──> hold(4.5s) ──> fadeOut(0.25s) ──> idle ──队列还有?──> fadeIn
```

- 播放中收到新消息 → 入队，当前条播完立即播下一条（队列上限 5 条，超出丢弃最旧的）；
- 同一条消息重复到达（如同一成就重复解锁）不重复播（由**发送方**保证，本面板不判重）；
- 淡入淡出用 `CanvasGroup.alpha` 在 `Update` 里按 `unscaledDeltaTime` 推进（暂停/慢放时表现一致）；
- 空闲时 `TipCard.SetActive(false)`，零开销。

---

## 6. 数据层与 API

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Board/BoardTipModel.cs` | `ObservableValue<BoardTipMessage>` 当前消息（每次 new 实例，引用变化即触发）+ 公开 `Send(title, body)` |
| `BoardContext` | 注册 `BoardTipModel`（Quick 档，跟其它 Model 一起 `RegisterModel`） |
| `Assets/Scripts/Board/BoardTip.cs` | 静态门面：`BoardTip.Send(Localization.Key title, params object[] args)` / `Send(string title, string body)`，供成就系统与玩法代码一行调用 |

`BoardTipMessage` 是引用类型：`ObservableValue` 比较引用，同文案二次发送也照样触发（不会因「赋同值不触发」丢消息）。

调用示例：

```csharp
BoardTip.Send(Localization.Key.TipAchievement, Localization.Get(Localization.Key.TipNewRecord, time));
```

### 6.1 本版接入的触发点

| 触发 | 位置 | 文案键 |
|---|---|---|
| 通关刷新最短纪录 | `BoardRuntimePanel.OnRunFinished` 的 Win 分支（已有 `isNewRecord`） | `TipNewRecord` |
| AI 元素属性反转（踩到反转格） | `BoardRuntimePanel` 订阅 `_run.Element`（与现有 `OnElementChanged` 同点，首帧初始值不播） | `TipElementSwitched` |

后续成就系统（04 篇）落地时，解锁判定处直接调 `BoardTip.Send` 即可，无需改动本面板。

---

## 7. 素材清单与生成提示词

产出目录：`Assets/Art/Board/UI/`（与 `Panel_DarkPurple.png` / `Card_Rounded.png` 同目录）。

### 7.1 `Tip_Parchment.png` — 做旧便签底板

**用途**：`TipCard` 的 Image 贴图，`Image.Type = Sliced`，九宫格 border(56,56,56,56)。

中文提示词（给阅读用）：

> 游戏 UI 素材，一张做旧的魔法便签底板，横向。米白羊皮纸底，表面覆一层褪色紫的做旧晕染，
> 四周深紫包边与一圈细的暗金描边，四角各有一颗紫水晶铆钉，边缘磨损、轻微卷角、有零星茶渍与霉斑，
> 柔和内阴影。手绘奇幻插画风格，画面干净、中央大面积留白供放文字，不要任何文字、字母与图标，
> 透明背景，主体居中，高清 2D。

英文提示词（**实际投喂给生图模型**）：

```text
game UI asset, a weathered fantasy parchment note panel, wide horizontal rectangle, aged cream
parchment surface washed with faded violet stain, deep purple border with a thin antique gold
outline, four small amethyst gem rivets in the corners, worn frayed edges with slightly curling
corners, subtle tea stains and foxing speckles, soft inner shadow, hand-painted fantasy game UI
illustration, clean empty center reserved for text, absolutely no text, no letters, no numbers,
no icons in the center, transparent background, centered, high detail, 2D
```

参数：`image_size = landscape_16_9`，`is_segmentation = true`（去底，保证 UI 可叠在 HUD 上）。

### 7.2 `Tip_Badge.png` — 成就徽章图标

**用途**：`Icon` 节点，`PreserveAspect = true`，可被 `Image.color` 染色（未解锁置灰）。

中文提示词（给阅读用）：

> 游戏 UI 图标，一枚奇幻成就徽章：紫水晶星芒与新月，外圈暗金桂冠环与褪色紫珐琅，
> 做旧复古质感。透明背景，主体居中，正方形，不要文字。

英文提示词（**实际投喂给生图模型**）：

```text
game UI icon, a fantasy achievement medal badge, amethyst crystal star burst with a crescent moon,
antique gold laurel ring, faded violet enamel, worn vintage finish, soft rim light, centered,
square composition, absolutely no text, no letters, transparent background, high detail, 2D
```

参数：`image_size = square_hd`，`is_segmentation = true`。

### 7.3 导入设置

| 项 | 值 |
|---|---|
| textureType | Sprite（Single） |
| mipmap | 关 |
| filterMode | Bilinear |
| alphaIsTransparency | 开 |
| spriteBorder | 底图 (56,56,56,56)；图标 0 |
| maxTextureSize | 底图 1024；图标 256 |

---

## 8. 验收配方

1. **Edit 模式**：跑 `Tools/关卡/重建成就消息小窗`，预制体字段全回填（生成器自检日志无 `null` 字段）；
2. **Play 模式（Level_01）**：
   - `BoardTip.Send("标题", "正文")` → 卡片 `activeSelf=true`、`CanvasGroup.alpha` 从 0 升到 1；
   - `RaycastAll` 在卡片中心**不命中** tip 任一节点（不拦点击）；
   - 5.2s 后 `activeSelf=false`；
   - 连发 3 条 → 依次播放，不叠加；
3. **真实触发**：铺路通关破纪录 → 右下角自动弹出「新纪录」；AI 踩反转格 → 弹出元素反转；
4. **录屏目检**：右下角便签位于屏幕右下、不压出发按钮、切场景（返回选关）后不残留。
