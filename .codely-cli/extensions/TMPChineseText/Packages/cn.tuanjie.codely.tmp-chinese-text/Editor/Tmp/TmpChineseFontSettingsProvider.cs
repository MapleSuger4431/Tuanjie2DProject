#if TMP_PRESENT
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontSettingsProvider
    {
        internal const string ButtonLabelZh = "配置中文显示";
        internal const string ButtonLabelEn = "Set Up Chinese Text";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(
                "Project/Codely/TMP Chinese Font",
                SettingsScope.Project)
            {
                label = "TMP Chinese Font",
                guiHandler = DrawSettings
            };
        }

        private static void DrawSettings(string searchContext)
        {
            var registry = TmpChineseFontRegistry.LoadOrCreate();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                "Designated Font",
                registry.designated,
                typeof(TMP_FontAsset),
                false);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            var useAsDefault = EditorGUILayout.Toggle(
                "Use As Default Font",
                registry.useAsDefaultFont);
            if (EditorGUI.EndChangeCheck())
            {
                registry.useAsDefaultFont = useAsDefault;
                TmpChineseFontRegistry.Save(registry);
                if (useAsDefault)
                {
                    TmpChineseFontSettingsWriter.ApplyDefaultIfNeeded(
                        registry.designated,
                        true);
                }
            }

            var chineseUi = PreferChineseUi();
            var button = new GUIContent(
                chineseUi ? ButtonLabelZh : ButtonLabelEn,
                chineseUi
                    ? "检查并补齐 TextMeshPro、Essentials 和中文兜底。已经配好时不会覆盖你的字体。"
                    : "Check and complete TextMeshPro, Essentials, and the Chinese fallback. Already set up? This will not overwrite your fonts.");
            if (GUILayout.Button(button))
                Debug.Log(TmpChineseFontApi.Ensure());
        }

        internal static bool PreferChineseUi()
        {
            if (EditorPrefs.HasKey("Editor.kEnableEditorLocalization") &&
                !EditorPrefs.GetBool("Editor.kEnableEditorLocalization"))
                return false;

            if (TryGetCurrentEditorLanguage(out var language))
                return IsChineseEditorLanguage(language);

            return LocalePrefRequestsChinese(
                EditorPrefs.GetString("Editor.kEditorLocale", string.Empty));
        }

        internal static bool IsChineseEditorLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool LocalePrefRequestsChinese(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return false;
            return locale.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                || locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetCurrentEditorLanguage(out SystemLanguage language)
        {
            language = SystemLanguage.English;
            var type = typeof(Editor).Assembly.GetType("UnityEditor.LocalizationDatabase");
            var property = type?.GetProperty(
                "currentEditorLanguage",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(SystemLanguage))
                return false;
            language = (SystemLanguage)property.GetValue(null, null);
            return true;
        }
    }
}
#endif
