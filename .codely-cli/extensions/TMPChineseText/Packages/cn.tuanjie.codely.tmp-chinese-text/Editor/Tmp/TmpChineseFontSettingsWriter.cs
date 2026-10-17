#if TMP_PRESENT
using TMPro;
using UnityEditor;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontSettingsWriter
    {
        public static string AddFallback(TMP_FontAsset font)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
                return TmpChineseFontStatus.SettingsMissing;
            if (TMP_Settings.fallbackFontAssets == null)
                return TmpChineseFontStatus.FallbackListMissing;

            if (!TMP_Settings.fallbackFontAssets.Contains(font))
            {
                TMP_Settings.fallbackFontAssets.Add(font);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return TmpChineseFontStatus.Ready;
        }

        public static void ApplyDefaultIfNeeded(TMP_FontAsset font, bool useAsDefault)
        {
            if (!useAsDefault)
                return;

            var settings = TMP_Settings.instance;
            if (settings == null || TMP_Settings.defaultFontAsset == font)
                return;

            SetDefault(settings, font);
            Save(settings);
        }

        public static void ReplaceDesignated(
            TMP_FontAsset previous,
            TMP_FontAsset current,
            TMP_FontAsset bundledNoto)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
                return;

            var changed = false;
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null)
            {
                for (var index = 0; index < fallbacks.Count; index++)
                {
                    if (previous != null && fallbacks[index] == previous)
                    {
                        fallbacks[index] = current;
                        changed = true;
                    }
                }

                if (bundledNoto != null && bundledNoto != current)
                {
                    while (fallbacks.Remove(bundledNoto))
                        changed = true;
                }
            }

            var replacementDefault = TMP_Settings.defaultFontAsset;
            var registry = AssetDatabase.LoadAssetAtPath<TmpChineseFontRegistry>(
                TmpChineseFontPaths.RegistryAsset);
            if (previous != null &&
                replacementDefault == previous &&
                registry != null &&
                registry.useAsDefaultFont)
            {
                replacementDefault = current;
            }

            if (bundledNoto != null &&
                bundledNoto != current &&
                replacementDefault == bundledNoto)
            {
                replacementDefault = null;
            }

            if (replacementDefault != TMP_Settings.defaultFontAsset)
            {
                SetDefault(settings, replacementDefault);
                changed = true;
            }

            if (changed)
                Save(settings);
        }

        public static TMP_FontAsset FindExistingCjkFont()
        {
            if (TMP_Settings.instance == null)
                return null;

            var defaultFont = TMP_Settings.defaultFontAsset;
            if (TmpChineseFontCjk.FontAssetHasCjk(defaultFont))
                return defaultFont;

            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null)
                return null;

            foreach (var fallback in fallbacks)
            {
                if (TmpChineseFontCjk.FontAssetHasCjk(fallback))
                    return fallback;
            }

            return null;
        }

        public static bool TmpPackagePresent()
        {
            return TmpChineseFontInstaller.TmpPackagePresent();
        }

        public static bool TmpSettingsPresent()
        {
            return AssetDatabase.LoadMainAssetAtPath(TmpChineseFontPaths.TmpSettings) != null;
        }

        private static void SetDefault(TMP_Settings settings, TMP_FontAsset font)
        {
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(TMP_Settings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
