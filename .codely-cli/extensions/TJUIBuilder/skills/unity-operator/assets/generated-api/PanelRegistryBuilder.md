# PanelRegistryBuilder Authoring API（自动生成，勿手改）

> 由 `Tools/gen_authoring_api_cheatsheets.py` 从 `Editor/Authoring/PanelRegistryBuilder.cs` 的 `PanelRegistryBuilder` 类自动提取。
> 共 7 个 public 方法；源码路径相对 `cn.tuanjie.create.ui` 包根。

## 最小范式

```csharp
using Create.Utilities;
PanelRegistryBuilder.Create("Assets/Games/MyGame/UI/PanelRegistry.asset").Upsert("MyGame", "HUD", hudVisualTree).Save()
```

## 方法签名

- `public static PanelRegistryBuilder Create(string assetPath)`
- `public static PanelRegistryBuilder Edit(string assetPath)`
- `public PanelRegistryBuilder ReplaceEntries(params EntrySpec[] entries)`
- `public PanelRegistryBuilder ReplaceEntries(IEnumerable<EntrySpec> entries)`
- `public PanelRegistryBuilder Upsert(string gameId, string panelName, VisualTreeAsset visualTreeAsset)`
- `public PanelRegistryBuilder Remove(string gameId, string panelName)`
- `public string Save()`
