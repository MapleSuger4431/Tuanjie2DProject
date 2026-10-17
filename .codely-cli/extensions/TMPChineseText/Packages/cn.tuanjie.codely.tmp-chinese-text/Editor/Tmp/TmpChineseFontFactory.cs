#if TMP_PRESENT
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontFactory
    {
        public static string CreateOrReuse(
            string sourceFontAssetPath,
            string sdfAssetPath,
            out TMP_FontAsset fontAsset)
        {
            fontAsset = null;

            string status;
            if (!TmpChineseFontEditMode.CanWrite(out status))
                return status;

            if (!TryNormalizeSdfAssetPath(sdfAssetPath, out sdfAssetPath))
                return TmpChineseFontStatus.FontPathNotInProject;

            AssetDatabase.ImportAsset(
                sourceFontAssetPath,
                ImportAssetOptions.ForceSynchronousImport);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontAssetPath);
            if (sourceFont == null)
                return TmpChineseFontStatus.SourceFontMissing;
            if (!TmpChineseFontCjk.FontHasCjk(sourceFont))
                return TmpChineseFontStatus.SourceFontNoCjk;

            var existingAsset = AssetDatabase.LoadMainAssetAtPath(sdfAssetPath);
            if (existingAsset != null && !(existingAsset is TMP_FontAsset))
                return TmpChineseFontStatus.FontAssetPathConflict;
            var projectRoot = TmpChineseFontPaths.ProjectRoot();
            var sdfFullPath = string.IsNullOrEmpty(projectRoot)
                ? null
                : Path.Combine(projectRoot, sdfAssetPath);
            if (existingAsset == null &&
                !string.IsNullOrEmpty(sdfFullPath) &&
                File.Exists(sdfFullPath))
                return TmpChineseFontStatus.FontAssetPathConflict;

            fontAsset = existingAsset as TMP_FontAsset;
            if (fontAsset != null)
            {
                var changed = false;
                if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                {
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    changed = true;
                }
                if (!fontAsset.isMultiAtlasTexturesEnabled)
                {
                    fontAsset.isMultiAtlasTexturesEnabled = true;
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(fontAsset);
                    AssetDatabase.SaveAssets();
                }
                return TmpChineseFontStatus.Ready;
            }

            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
                return TmpChineseFontStatus.FontCreationFailed;

            fontAsset.name = Path.GetFileNameWithoutExtension(sdfAssetPath);
            fontAsset.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(fontAsset, sdfAssetPath);

            if (fontAsset.material != null && !EditorUtility.IsPersistent(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            foreach (var atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null && !EditorUtility.IsPersistent(atlasTexture))
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            AssetDatabase.SaveAssets();
            return TmpChineseFontStatus.Ready;
        }

        private static bool TryNormalizeSdfAssetPath(string path, out string normalizedPath)
        {
            normalizedPath = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/');
            if (!normalizedPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                return false;

            var segments = normalizedPath.Split('/');
            for (var index = 1; index < segments.Length; index++)
            {
                if (string.IsNullOrEmpty(segments[index]) ||
                    segments[index] == "." ||
                    segments[index] == "..")
                    return false;
            }

            return true;
        }
    }
}
#endif
