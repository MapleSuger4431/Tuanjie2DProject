# UXML→USS 完整性补全示例

> 本文件是 unity-ui-builder SKILL.md 步骤 7 的配套参考，展示查表补全（非推断）的完整 USS 产出示例。

## MyGame MainMenu 查表补全示例

步骤 1 映射表中已确定：
- `{GameName}-main-menu-bg` → 页面根容器的 `backgroundImage` → image_requirements `main_menu_bg`
- `{GameName}-game-title` → 无 Brick 手写区域 → image_requirements `game_title`（uxml_element=`TitleImage`）
- `{GameName}-action-button` → brick `primitive.input.button_base` 的 `iconAsset` → image_requirements `button_style_1`
- `{GameName}-volume-btn` → 无 Brick 手写元素 → image_requirements `volume_icon`（uxml_element=`volume-btn`）
- `{GameName}-center-panel` → 无 Brick 手写布局 → 无图片需求

根据查表结果直接写 USS：

```uss
/* ===== MainMenu Panel ===== */

.{GameName}-main-menu-shell {
    flex-grow: 1;
}

.{GameName}-main-menu-bg {
    background-image: url("Images/main_menu_bg.png");  /* ← 查表: 页面根容器 backgroundImage → main_menu_bg */
    -unity-background-scale-mode: scale-and-crop;
}

.{GameName}-center-panel {
    /* ← 查表: 无 Brick 手写布局，无图片 */
    position: relative;
    align-self: center;
    width: 40%;            /* ← 百分比，不用 px */
    height: 100%;           /* ← 百分比 */
    background-color: rgb(46, 125, 50);
    border-radius: 18px;    /* ← 例外：圆角可用 px */
    flex-direction: column;
    align-items: center;
    justify-content: center;
}

.{GameName}-title-area {
    position: absolute;
    top: 8%;               /* ← 百分比 */
    left: 0;
    width: 100%;
    align-items: center;
}

.{GameName}-game-title {
    width: 53%;            /* ← 百分比，从 UI设计文档.md §12.2 的宽% 列读取 */
    height: 44%;           /* ← 百分比 */
    background-image: url("Images/game_title.png");  /* ← 查表: image_requirements game_title */
    -unity-background-scale-mode: scale-to-fit;
    align-self: center;
}

.{GameName}-action-button {
    background-color: rgba(0, 0, 0, 0);
    background-image: url("Images/button_style_1.png");  /* ← 查表: primitive.input.button_base.iconAsset → button_style_1 */
    -unity-background-scale-mode: scale-to-fit;
    border-width: 0;       /* ← 例外：边框可用 px */
    color: rgb(255, 255, 255);
    -unity-font-style: bold;
    font-size: 20px;       /* ← 例外：字号可用 px */
    transition: opacity 0.1s;
}

.{GameName}-action-button:hover {
    opacity: 0.9;
}

.{GameName}-action-button:active {
    opacity: 0.8;
}

.{GameName}-action-button--last {
    margin-bottom: 0;
}

.{GameName}-volume-btn {
    width: 7%;             /* ← 百分比 */
    height: 12%;           /* ← 百分比 */
    background-color: rgba(0, 0, 0, 0);
    background-image: url("Images/volume_icon.png");  /* ← 查表: image_requirements volume_icon */
    -unity-background-scale-mode: scale-to-fit;
    border-width: 0;
}

.{GameName}-footer {
    flex-direction: row;
    justify-content: center;
    align-items: center;
}

.{GameName}-high-score-label {
    color: rgb(255, 215, 0);
    font-size: 16px;       /* ← 例外：字号可用 px */
    -unity-font-style: bold;
    -unity-text-outline-width: 1px;  /* ← 例外：描边可用 px */
    -unity-text-outline-color: rgb(0, 0, 0);
}
```
