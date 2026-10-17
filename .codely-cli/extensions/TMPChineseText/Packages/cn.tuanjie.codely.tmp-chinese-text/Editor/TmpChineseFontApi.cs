using System;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontApi
    {
        internal static Func<string, string> TmpReplaceFromSource;
        internal static Func<Component, string> TmpApply;
        internal static Func<GameObject, string> TmpApplyTo;

        public static string Ensure()
        {
            return TmpChineseFontInstaller.Ensure();
        }

        public static string Apply(Component text)
        {
            return TmpApply == null
                ? TmpChineseFontStatus.PackageMissing
                : TmpApply(text);
        }

        public static string ApplyTo(GameObject gameObject)
        {
            return TmpApplyTo == null
                ? TmpChineseFontStatus.PackageMissing
                : TmpApplyTo(gameObject);
        }

        public static string ReplaceFromSource(string sourceFontPath)
        {
            if (!TmpChineseFontEditMode.CanWrite(out var gate))
                return gate;

            var root = TmpChineseFontPaths.ProjectRoot();
            var invalid = TmpChineseFontPaths.ValidateSourceFontPath(sourceFontPath, root);
            if (invalid != null)
                return invalid;

            return TmpReplaceFromSource == null
                ? TmpChineseFontStatus.PackageMissing
                : TmpReplaceFromSource(sourceFontPath);
        }

        public static string ToUnityAssetPath(string fullOrAssetPath, string projectRoot)
        {
            var normalized = fullOrAssetPath.Replace("\\", "/");
            var assetsIndex = normalized.IndexOf(
                "/Assets/",
                StringComparison.OrdinalIgnoreCase);
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return normalized;
            if (assetsIndex >= 0)
                return normalized.Substring(assetsIndex + 1);
            return null;
        }
    }
}
