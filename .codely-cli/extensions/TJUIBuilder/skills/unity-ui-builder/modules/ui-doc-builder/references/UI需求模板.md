# UI 需求模板

> 本文件是 `context/games/{GameName}/UI需求.md` 的标准模板。
> 由 `ui-doc-builder` 的 Step 0 生成或补全后保留在 context 中，不删除。
> 后续 UI 方向变化时优先更新 `UI需求.md`，再由 `UI需求.md` 重新生成 `UI设计文档.md`。

## 必填字段

### 基本信息

- **GameName**: {游戏英文名，如 MyGame}
- **设计分辨率**: {如 1920×1080}
- **UI 模式**: {ugui / ui_toolkit} — ugui（默认，Canvas + UGUI 组件，面板继承 UGUIPanelBase）或 ui_toolkit（UXML/USS，面板继承 UIPanelBase）。全程不可更改
- **视觉输入方式**: {AI直接规划 / 上传参考图 / AI效果图定稿 / 画板手动设计}
- **参考图布局校正**: {开启/关闭} — 仅上传参考图或 AI 效果图定稿时有效，默认关闭；旧文档缺失时按关闭处理
- **是否有参考图**: {是/否}
- **是否走 AI mockup**: {是/否}

### 面板清单

| 面板名称 | Panel 名称 | 类型 | 触发条件 | 用途说明 |
|---------|-----------|------|---------|---------|
| {如 主菜单} | {MainMenuPanel} | {full_screen} | {游戏启动} | {开始游戏、设置、退出} |
| {如 HUD} | {HUDPanel} | {hud} | {游戏进行中} | {分数、生命值、暂停按钮} |
| {如 游戏结束} | {GameOverPanel} | {popup} | {失败/胜利} | {最终分数、重试、返回主菜单} |

### 导航骨架

```
{MainMenu} → {Play 按钮} → {HUD}
{HUD} → {暂停按钮} → {Pause} → {Pop}
{HUD} → {GameOver 事件} → {GameOver}
{GameOver} → {重试} → {HUD}
{GameOver} → {返回主菜单} → {MainMenu}
```

### 基础状态映射

| 游戏状态 | 显示面板 | 隐藏面板 | 说明 |
|---------|---------|---------|------|
| {Menu} | {MainMenu} | — | {初始状态} |
| {Playing} | {HUD} | {MainMenu} | {游戏进行中} |
| {Paused} | {Pause} | — | {暂停叠加} |
| {GameOver} | {GameOver} | {HUD} | {游戏结束} |

---

## 可选/可补全字段

> 以下字段在有创意方向.md 时可从文档自动推导；允许先留空或写占位说明，待后续补齐。

### 绑定字段

| 面板 | UI 元素 | 绑定属性 | 属性类型 | 绑定方式 |
|------|--------|---------|---------|---------|
| {HUD} | {score-label} | {Score} | {int} | {Bind().ToText<Label>()} |
| {HUD} | {lives-icon} | {Lives} | {int} | {Bind().ToVisibility()} |

### 服务接口

| 注册类型 | 注册方式 | 说明 |
|---------|---------|------|
| {IGameService → GameManager} | {Singleton} | {游戏核心服务} |

### 挂载要求

> UI Toolkit 模式和 UGUI 模式挂载要求不同：

#### UI Toolkit 模式

| 对象 | 挂载脚本 | PanelSettings | UXML Source | sortOrder |
|------|---------|---------------|-------------|-----------|
| {UIRoot} | {UIGameInstaller.cs} | {GamePanelSettings.asset} | {不设置（框架动态加载）} | {0} |

#### UGUI 模式

| 对象 | 挂载脚本 | Canvas | CanvasScaler | sortOrder |
|------|---------|--------|-------------|-----------|
| {UIRoot} | {UIGameInstaller.cs} | {Screen Space - Overlay} | {参考分辨率: 1920×1080} | {0} |

### 具体事件名

| 游戏事件 | 所属脚本 | 触发的 UI 操作 | 传递参数 |
|---------|---------|---------------|---------|
| {OnGameOver} | {GameManager} | {Switch("GameOver")} | {最终分数} |

### 面板间导航关系详情

> 若导航骨架不足以表达复杂导航，可在此补充。

```
{补充导航关系，如有}
```

### UI 风格偏好

- **美术风格**: {如 霓虹赛博、像素风、卡通、写实等}
- **UI 风格关键词**: {如 vibrant cartoon, rounded corners, soft shadows}
- **配色方案**: {如 主色调深蓝，强调色金黄}
- **按钮风格**: {如 药丸形圆角实心按钮}
- **图标风格**: {如 扁平图标，粗线条剪影}

---

## 维护说明

- `UI需求.md` 是正式中间输入文档，生成后保留在 context 中不删除
- 后续 UI 方向或独立项目需求变化时，优先更新 `UI需求.md`
- `UI设计文档.md` 应由更新后的 `UI需求.md` 重新生成，而不是绕过它直接手改 UI 文档
