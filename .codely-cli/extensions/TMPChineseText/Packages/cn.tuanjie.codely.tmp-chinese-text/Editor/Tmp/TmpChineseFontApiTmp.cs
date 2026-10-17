#if TMP_PRESENT
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontApiTmp
    {
        static TmpChineseFontApiTmp()
        {
            Register();
        }

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            Register();
        }

        private static void Register()
        {
            TmpChineseFontApi.TmpReplaceFromSource = ReplaceFromSource;
            TmpChineseFontApi.TmpApply = Apply;
            TmpChineseFontApi.TmpApplyTo = TmpChineseFontApplicator.ApplyTo;
        }

        private static string ReplaceFromSource(string sourceFontPath)
        {
            var ensure = TmpChineseFontInstaller.Ensure();
            if (ensure != TmpChineseFontStatus.Ready)
                return ensure;

            var root = TmpChineseFontPaths.ProjectRoot();
            var unityPath = TmpChineseFontApi.ToUnityAssetPath(sourceFontPath, root);
            if (string.IsNullOrEmpty(unityPath))
                return TmpChineseFontStatus.FontPathNotInProject;

            AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceSynchronousImport);

            var fileName = Path.GetFileNameWithoutExtension(unityPath);
            var sdfPath = TmpChineseFontPaths.ProjectFontsFolder + "/" + fileName + " SDF.asset";
            if (sdfPath == TmpChineseFontPaths.NotoSdf)
            {
                sdfPath = TmpChineseFontPaths.ProjectFontsFolder + "/" +
                          fileName + "-Custom SDF.asset";
            }

            var created = TmpChineseFontFactory.CreateOrReuse(
                unityPath,
                sdfPath,
                out var newFont);
            if (created != TmpChineseFontStatus.Ready)
                return created;

            var registry = TmpChineseFontRegistry.LoadOrCreate();
            var previous = registry.designated;
            var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                TmpChineseFontPaths.NotoSdf);

            TmpChineseFontSettingsWriter.ReplaceDesignated(previous, newFont, noto);
            var fallbackStatus = TmpChineseFontSettingsWriter.AddFallback(newFont);
            if (fallbackStatus != TmpChineseFontStatus.Ready)
                return fallbackStatus;
            TmpChineseFontSettingsWriter.ApplyDefaultIfNeeded(
                newFont,
                registry.useAsDefaultFont);

            registry.previous = previous;
            registry.designated = newFont;
            TmpChineseFontRegistry.Save(registry);

            var replace = TmpChineseFontReplacer.Replace(
                previous,
                newFont,
                new[] { "Assets" });
            return replace.ToString();
        }

        private static string Apply(Component text)
        {
            if (text is TMP_InputField inputField)
                return TmpChineseFontApplicator.Apply(inputField);
            return TmpChineseFontApplicator.Apply(text as TMP_Text);
        }
    }
}
#endif
