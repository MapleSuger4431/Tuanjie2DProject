using TMPro;
using UnityEditor;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontApplicator
    {
        public static string Apply(TMP_Text text)
        {
            if (!TmpChineseFontEditMode.CanWrite(out var gate))
                return gate;
            var ensure = TmpChineseFontInstaller.Ensure();
            if (ensure != TmpChineseFontStatus.Ready)
                return ensure;
            var registry = TmpChineseFontRegistry.LoadOrCreate();
            if (registry.designated == null)
                return TmpChineseFontStatus.DesignatedFontMissing;
            if (text == null)
                return TmpChineseFontStatus.ExistingFontUnsupported;
            Assign(text, registry.designated);
            return TmpChineseFontStatus.Ready;
        }

        public static string Apply(TMP_InputField inputField)
        {
            if (!TmpChineseFontEditMode.CanWrite(out var gate))
                return gate;
            var ensure = TmpChineseFontInstaller.Ensure();
            if (ensure != TmpChineseFontStatus.Ready)
                return ensure;
            var designated = TmpChineseFontRegistry.LoadOrCreate().designated;
            if (designated == null)
                return TmpChineseFontStatus.DesignatedFontMissing;
            if (inputField == null)
                return TmpChineseFontStatus.ExistingFontUnsupported;

            if (inputField.textComponent != null)
                Assign(inputField.textComponent, designated);
            if (inputField.placeholder is TMP_Text placeholder)
                Assign(placeholder, designated);
            return TmpChineseFontStatus.Ready;
        }

        public static string ApplyTo(GameObject go)
        {
            if (!TmpChineseFontEditMode.CanWrite(out var gate))
                return gate;
            var ensure = TmpChineseFontInstaller.Ensure();
            if (ensure != TmpChineseFontStatus.Ready)
                return ensure;
            var designated = TmpChineseFontRegistry.LoadOrCreate().designated;
            if (designated == null)
                return TmpChineseFontStatus.DesignatedFontMissing;
            if (go == null)
                return TmpChineseFontStatus.ExistingFontUnsupported;

            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
                Assign(text, designated);
            return TmpChineseFontStatus.Ready;
        }

        private static void Assign(TMP_Text text, TMP_FontAsset designated)
        {
            text.font = designated;
            EditorUtility.SetDirty(text);
        }
    }
}
