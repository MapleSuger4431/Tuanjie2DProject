# 图片内联展示

用 `unity_screenshot capture_ui_toolkit` 将 2D 图片直接渲染到 CLI，用户无需点击外部链接即可看到图片。

## 适用场景

- AI 效果图路线生成参考图后展示给用户确认
- UI 图片生成后展示预览
- 任何需要让用户在 CLI 内直接查看 2D 图片的场景

## 限制

- `capture_asset` 不适用：它将 Texture2D 贴到 3D mesh 上渲染，2D 图片会严重失真
- 目标图片必须在 `Assets/` 下（UXML 的 `background-image` 路径相对于 UXML 文件位置解析）。若图片在 `context/` 下，需先复制到 `Assets/` 临时目录

## 预览 UXML

统一使用 `Assets/Games/_preview/_ImagePreview.uxml` 作为预览文件，全项目共用一个，不按游戏隔离。

- 展示前把目标图片复制到 `Assets/Games/_preview/` 下（UXML 的 `background-image` 相对 UXML 文件位置解析，图片必须与其同目录）
- 展示完成后可保留副本供下次覆盖复用

## 工作流

### 1. 创建预览 UXML（仅首次）

首次使用时创建 UXML，后续跳过此步直接覆写复用：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement style="width: {W}px; height: {H}px; background-image: url('{图片文件名}'); -unity-background-scale-mode: scale-to-fit;" />
</ui:UXML>
```

创建后执行一次 `AssetDatabase.Refresh()` 让 Unity 导入 UXML：

```csharp
UnityEditor.AssetDatabase.Refresh();
```

Refresh 可能触发 Domain Reload。若后续 `capture_ui_toolkit` 报 UXML not found，等 ~3s 后调 `unity_refresh` 重连再重试。

### 2. 展示图片

每次展示新图片时，先把目标图片复制到 `Assets/Games/_preview/` 下，再用 `write_file` 覆写 UXML（换 `url` 文件名和 `width`/`height`），然后截图：

```
复制图片到 Assets/Games/_preview/{图片文件名}

write_file → 覆写 Assets/Games/_preview/_ImagePreview.uxml（换图片文件名和尺寸）

unity_screenshot capture_ui_toolkit
  document_path: "Assets/Games/_preview/_ImagePreview.uxml"
  width: {W}
  height: {H}
```

截图结果返回 `path`，用户可在 CLI 中直接看到图片。

> 覆写 UXML 后无需再次 `AssetDatabase.Refresh()`——`capture_ui_toolkit` 每次调用都重新读取 UXML 文件内容。前提是图片本身已被 Unity 导入（图片在 `Assets/` 下且非本次新增）。
