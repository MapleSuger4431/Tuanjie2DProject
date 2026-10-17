using System;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontPaths
    {
        public const string ProjectFontsFolder = "Assets/Codely/Fonts";
        public const string NotoOtf = "Assets/Codely/Fonts/NotoSansSC-Regular.otf";
        public const string NotoOfl = "Assets/Codely/Fonts/NotoSansSC-OFL.txt";
        public const string NotoSdf = "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset";
        public const string RegistryAsset = "Assets/Codely/Fonts/TmpChineseFontRegistry.asset";
        public const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string EssentialsPendingPref = "Codely.TmpChineseFont.EssentialsPending";
        public const string TmpPackageId = "com.unity.textmeshpro";
        public const string TmpPackageJson = "Packages/com.unity.textmeshpro/package.json";
        public const string UguiPackageId = "com.unity.ugui";
        public const string UguiPackageJson = "Packages/com.unity.ugui/package.json";
        public const string TmpEssentialsUnityPackageRelative =
            "Package Resources/TMP Essential Resources.unitypackage";
        public const string PackageInstallPendingPref =
            "Codely.TmpChineseFont.PackageInstallPending";
        public const string PackageInstallFailedPref =
            "Codely.TmpChineseFont.PackageInstallFailed";

        public static string PackageInstallPendingPrefKey()
        {
            return PackageInstallPendingPref + "." + ProjectDiscriminator();
        }

        public static string PackageInstallFailedPrefKey()
        {
            return PackageInstallFailedPref + "." + ProjectDiscriminator();
        }

        public static string ValidateSourceFontPath(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
                return TmpChineseFontStatus.FontPathMissing;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return TmpChineseFontStatus.ProjectRootMissing;
            if (!File.Exists(path))
                return TmpChineseFontStatus.SourceFontMissing;

            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = root + Path.DirectorySeparatorChar;
            var altPrefix = root + Path.AltDirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(altPrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return TmpChineseFontStatus.FontPathNotInProject;

            var ext = Path.GetExtension(path);
            if (!ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                return TmpChineseFontStatus.FontNotTtfOrOtf;

            return null;
        }

        public static string BundledOtfFullPath()
        {
            return Path.Combine(PackageRoot(), "Editor", "Fonts", "NotoSansSC-Regular.otf");
        }

        public static string BundledOflFullPath()
        {
            return Path.Combine(PackageRoot(), "Editor", "Fonts", "OFL.txt");
        }

        public static string PackageRoot()
        {
            var info = PackageInfo.FindForAssembly(typeof(TmpChineseFontPaths).Assembly);
            return info != null ? info.resolvedPath : string.Empty;
        }

        public static bool PackageVersionMajorAtLeast(string version, int major)
        {
            if (string.IsNullOrEmpty(version) || major < 0)
                return false;
            var dot = version.IndexOf('.');
            var head = dot < 0 ? version : version.Substring(0, dot);
            int parsed;
            return int.TryParse(head, out parsed) && parsed >= major;
        }

        public static bool TmpRuntimePresent()
        {
            return StandaloneTmpPackagePresent() || UguiBundlesTmp();
        }

        public static bool StandaloneTmpPackagePresent()
        {
            return PackageInfo.FindForAssetPath(TmpPackageJson) != null;
        }

        public static bool UguiBundlesTmp()
        {
            var info = PackageInfo.FindForAssetPath(UguiPackageJson);
            return info != null && PackageVersionMajorAtLeast(info.version, 2);
        }

        public static string ResolvedTmpEssentialsUnityPackage()
        {
            foreach (var json in new[] { TmpPackageJson, UguiPackageJson })
            {
                var info = PackageInfo.FindForAssetPath(json);
                if (info == null || string.IsNullOrEmpty(info.resolvedPath))
                    continue;
                var path = Path.Combine(
                    info.resolvedPath,
                    "Package Resources",
                    "TMP Essential Resources.unitypackage");
                if (File.Exists(path))
                    return path;
            }

            return string.Empty;
        }

        public static string ProjectRoot()
        {
            var parent = Directory.GetParent(Application.dataPath);
            return parent == null ? null : parent.FullName;
        }

        private static string ProjectDiscriminator()
        {
            unchecked
            {
                uint hash = 2166136261;
                var dataPath = Application.dataPath ?? string.Empty;
                for (var i = 0; i < dataPath.Length; i++)
                {
                    hash ^= dataPath[i];
                    hash *= 16777619;
                }

                return hash.ToString("X8");
            }
        }
    }
}
