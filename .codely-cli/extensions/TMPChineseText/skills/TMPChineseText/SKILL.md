---
name: TMPChineseText
description: >-
  TMPChineseText: prepares Chinese TextMeshPro rendering via the Codely
  editor package and applies the project's designated CJK TMP Font Asset.
  Use before creating or modifying TMP_Text, TextMeshPro, TextMeshProUGUI,
  or TMP_InputField; when Chinese text is missing, blank, tofu, square, or
  garbled; and when the user wants to replace the UI font with their own
  TTF/OTF. Do not invent Font Asset Creator steps. Prefer this skill over
  the marketplace TMPChineseFont 1.0 skill.
allowedTools:
  - unity_editor
  - unity_package
  - unity_refresh
  - execute_csharp_script
---

# TMP Chinese Text

This is **TMPChineseText**. If the marketplace skill `TMPChineseFont` (1.0)
is also installed, do not use it: do not call `ask_user` for fallback, and
do not run inline Font Asset Creator / ImportPackage scripts.

## 1. Ensure before TMP work

Before creating TMP, run this script verbatim:

```csharp
using Codely.TmpChineseFont;
return TmpChineseFontApi.Ensure();
```

## 2. Handle Ensure status

`TMP_PACKAGE_INSTALL_STARTED` / `TMP_PACKAGE_INSTALL_PENDING` / `TMP_ESSENTIALS_IMPORT_STARTED` / `PENDING` → `unity_refresh` once, Ensure again. Do not busy-wait. `TMP_PACKAGE_INSTALL_FAILED` → report the failure; do not write `Client.Add` or Package Manager scripts; do not use `unity_package` to install TextMeshPro.

## 3. Apply designated font on Ready

On Ready, when creating Chinese UI, assign `TmpChineseFontRegistry.LoadOrCreate().designated` via:

```csharp
using Codely.TmpChineseFont;
using TMPro;
// `text` is the TMP_Text just created
return TmpChineseFontApi.Apply(text);
```

Use `TmpChineseFontApi.ApplyTo(gameObject)` to apply the designated font to all
`TMP_Text` components under a specific GameObject hierarchy.

If the host script cannot capture the instance, load designated from Registry and set `tmp.font` in the same `execute_csharp_script` that creates the object.

## 4. Replace with user's font

User wants their own font: **ask in chat for an in-project `.ttf`/`.otf` path**. Do not download. Do not search. Then:

```csharp
using Codely.TmpChineseFont;
return TmpChineseFontApi.ReplaceFromSource(@"<path>");
```

## 5. Tofu / garbled text

Tofu: Ensure first; then Apply on that component only. Never mass-assign every TMP.

## 6. Regular only

Regular only: if the user hands a Bold file, warn that v1 replaces the whole designated Regular font.

## 7. Never duplicate Factory

Never open TMP Font Asset Creator. Never copy Factory code into the skill.
