---
name: unity-addressables
description: Unity Addressables asset management system expert — asset groups, async load/unload, preloading, remote CDN distribution, hot updates. Use when managing large asset libraries, implementing DLC/downloadable content, optimizing memory on mobile, configuring remote asset updates, or writing Addressables.LoadAssetAsync<T> code.
---

# Unity Addressables

## Core Concept

Addressables decouples asset reference from location. You load by address (string key), not by path. Runtime decides local vs remote.

## Loading Assets

```csharp
// Single asset
var handle = Addressables.LoadAssetAsync<GameObject>("Prefabs/Enemy");
await handle.Task;
GameObject prefab = handle.Result;

// Instantiate directly (tracks instance for auto-release)
var instHandle = Addressables.InstantiateAsync("Prefabs/Enemy");
await instHandle.Task;

// Load all in a label
var handles = Addressables.LoadAssetsAsync<Sprite>("UI_Icons", null);
await handles.Task;

// Scene loading
var sceneHandle = Addressables.LoadSceneAsync("Levels/BossArena", LoadSceneMode.Additive);
```

## Releasing

```csharp
// Release handle (decrements ref count, frees memory when count=0)
Addressables.Release(handle);

// Release instance (for InstantiateAsync results)
Addressables.ReleaseInstance(gameObjectInstance);
```

**Critical:** Every `LoadAssetAsync` MUST be paired with `Release`. Leaked handles = memory leak.

## Groups & Labels

- **Group**: Folder of assets sharing build/storage settings (local/remote, compression).
- **Label**: Tag for batch loading (e.g., "UI_Icons", "Level_1_Assets").
- **Schema**: Group settings (BundledAssetGroupSchema for packing, ContentUpdateGroupSchema for remote updates).

## Preloading & Memory

```csharp
// Preload all assets with a label, then instantiate as needed
var preloadHandle = Addressables.LoadAssetsAsync<GameObject>("Pool_Enemies", null);
await preloadHandle.Task;
// Now instantiations from this group are instant (already in memory)

// Check memory / force unload
Addressables.ResourceManager.Cleanup(false);
```

## Remote Distribution

1. Set group to "Remote" (Build Path = Server, Load Path = URL).
2. Build: Window > Asset Management > Addressables > Groups > Build > New Build > Default Build.
3. Upload `ServerData/[Platform]` to CDN.
4. Set `Addressables.RuntimePath` or catalog URL at runtime.

## Hot Updates (Content Update)

1. Mark group with "Static Content" = false (ContentUpdateGroupSchema).
2. Build initial build.
3. Modify assets, then: Groups > Build > Update a Previous Build.
4. Only changed bundles re-upload — delta update.

## Initialization

```csharp
// Custom catalog URL (for hot updates from your server)
Addressables.InitializeAsync(false); // false = don't auto-load catalog
await Addressables.LoadContentCatalogAsync("https://cdn.example.com/catalog_1.0.json");
```

## Key Gotchas

1. `Addressables.InitializeAsync()` runs automatically on first load — but you can call it early to warm up.
2. Reference counting: loading the same key twice returns the same handle, ref count = 2. Must release twice.
3. `InstantiateAsync` results MUST be released via `ReleaseInstance`, NOT `Destroy` — it tracks the instance.
4. Scene loaded via Addressables must be unloaded via `Addressables.UnloadSceneAsync`, NOT `SceneManager.UnloadScene`.
5. Editor simulation mode loads assets directly (no bundles) — fast iteration but different from runtime behavior.
6. Build size: each group = one bundle. Too many tiny groups = overhead. Too few = large downloads.
