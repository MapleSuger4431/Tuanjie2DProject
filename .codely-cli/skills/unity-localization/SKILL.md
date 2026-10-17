---
name: unity-localization
description: Unity Localization package expert — StringTable, AssetTable, Locale selection, smart formatting, runtime language switching, CSV import/export. Use when implementing multi-language systems, configuring locale-specific assets, handling RTL languages, or building translation workflows for global game releases.
---

# Unity Localization

## Setup

1. Install `com.unity.localization` via Package Manager.
2. `Edit > Project Settings > Localization` to initialize.
3. Add locales (e.g., en, zh-CN, ja, ko).
4. Create default Locale Selectors (System Language, Specific Language).

## String Tables

```csharp
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// Get a localized string
string localized = LocalizationSettings.StringDatabase
    .GetLocalizedString("UI", "MainMenu_Title");

// Async (preferred — waits for table load)
var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI", "MainMenu_Title");
yield return op;
string text = op.Result;
```

## StringReference in Inspector

```csharp
using UnityEngine.Localization.Components;

public class LocalizedText : MonoBehaviour
{
    public LocalizedString localizedString; // Set in Inspector: Table=UI, Entry=MainMenu_Title

    TMPro.TextMeshProUGUI _text;

    void OnEnable()
    {
        localizedString.StringChanged += OnStringChanged;
        localizedString.RefreshString(); // Force immediate update
    }

    void OnDisable()
    {
        localizedString.StringChanged -= OnStringChanged;
    }

    void OnStringChanged(string value) => _text.text = value;
}
```

## Smart Strings (Format Arguments)

```
// In String Table:
// Key: "Score"  Value: "Score: {0}"
// Key: "Health" Value: "{0} has {1} HP"
```

```csharp
var args = new object[] { playerName, currentHealth };
string text = LocalizationSettings.StringDatabase
    .GetLocalizedString("UI", "Health", args);
// "Alice has 85 HP"

// Using LocalizedString with arguments
localizedString.Arguments = new object[] { playerName, currentHealth };
localizedString.RefreshString();
```

## Pluralization

```
// String table entry for "Enemies":
// English: {count:plural: one{# enemy} other{# enemies}}
// Chinese: {count:plural: other{# 个敌人}}  // Chinese has no singular/plural
```

## Runtime Language Switch

```csharp
// Switch to a specific locale
var localeOp = LocalizationSettings.InitializationOperation;
yield return localeOp;

LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[1];
// Or by identifier:
var locale = LocalizationSettings.AvailableLocales.GetLocale("zh-CN");
LocalizationSettings.SelectedLocale = locale;
```

## Asset Tables

```csharp
// Localized sprites/audio/textures
var op = LocalizationSettings.AssetDatabase
    .LoadAssetAsync<Sprite>("UI", "Logo");
yield return op;
Sprite logo = op.Result;

// AssetReference in component
public class LocalizedSprite : MonoBehaviour
{
    public LocalizedSprite localizedSprite;
    void OnEnable() => localizedSprite.AssetChanged += OnAssetChanged;
    void OnDisable() => localizedSprite.AssetChanged -= OnAssetChanged;
    void OnAssetChanged(Sprite sprite) => GetComponent<SpriteRenderer>().sprite = sprite;
}
```

## CSV Import/Export

```
Window > Localization > CSV Importer
```

Format:
```
Key,en,zh-CN,ja
MainMenu_Title,Main Menu,主菜单,メインメニュー
Settings,Settings,设置,設定
```

## RTL (Right-to-Left)

```csharp
// For Arabic/Hebrew
using UnityEngine.Localization;
var locale = LocalizationSettings.SelectedLocale;
if (locale.Identifier.Code == "ar")
{
    // TextMeshPro handles RTL if you enable it:
    // TMPro settings → Enable RTL Support
    // Or use ArabicSupport library for text reshaping
}
```

## Key Gotchas

1. Tables load asynchronously on first access — use `GetLocalizedStringAsync` or subscribe to `StringChanged`.
2. Locale identifier codes follow BCP-47: `zh-CN`, not `zh_cn` or `zh-CN` (case varies by platform).
3. `LocalizationSettings.SelectedLocale` change triggers all `StringChanged`/`AssetChanged` callbacks automatically.
4. String table collections are per-locale — missing keys return the key name itself (not empty).
5. CSV export includes all locales — good for sending to translators.
6. `GetLocalizedString` with no locale defaults to `SelectedLocale`. Always check if SelectedLocale is initialized.
7. For fonts: TMP font assets are per-locale (CJK needs different font than Latin). Use `TMP_FontAsset` in Asset Tables.
