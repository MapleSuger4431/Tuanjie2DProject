#if TMP_PRESENT
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontInstallerTmp
    {
        private static bool essentialsImportCompleted;

        static TmpChineseFontInstallerTmp()
        {
            Register();
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
        }

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            Register();
            EditorApplication.update -= TickEnsureWhenIdle;
            EditorApplication.update += TickEnsureWhenIdle;
        }

        private static void TickEnsureWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= TickEnsureWhenIdle;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= TickEnsureWhenIdle;
            TmpChineseFontInstaller.Ensure();
        }

        public static string Ensure()
        {
            var bundledOtf = TmpChineseFontPaths.BundledOtfFullPath();
            var bundledOfl = TmpChineseFontPaths.BundledOflFullPath();
            if (!File.Exists(bundledOtf) || !File.Exists(bundledOfl))
            {
                Debug.LogError("Bundled Noto Sans SC font or license is missing.");
                return TmpChineseFontStatus.BundledSourceMissing;
            }

            var registry = TmpChineseFontRegistry.LoadOrCreate();
            var copiedOtf = CopyBundledFileIfMissing(
                bundledOtf,
                TmpChineseFontPaths.NotoOtf);
            var copiedOfl = CopyBundledFileIfMissing(
                bundledOfl,
                TmpChineseFontPaths.NotoOfl);
            if (copiedOtf || copiedOfl)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (TmpChineseFontCjk.FontAssetHasCjk(registry.designated))
                return TmpChineseFontStatus.Ready;

            var existing = TmpChineseFontSettingsWriter.FindExistingCjkFont();
            if (existing != null)
            {
                registry.designated = existing;
                registry.previous = null;
                TmpChineseFontRegistry.Save(registry);
                return TmpChineseFontStatus.Ready;
            }

            if (!TmpChineseFontSettingsWriter.TmpSettingsPresent())
                return EnsureTmpEssentials();

            EditorPrefs.DeleteKey(TmpChineseFontPaths.EssentialsPendingPref);
            essentialsImportCompleted = false;

            TMP_FontAsset fontAsset;
            var status = TmpChineseFontFactory.CreateOrReuse(
                TmpChineseFontPaths.NotoOtf,
                TmpChineseFontPaths.NotoSdf,
                out fontAsset);
            if (status != TmpChineseFontStatus.Ready)
                return status;

            status = TmpChineseFontSettingsWriter.AddFallback(fontAsset);
            if (status != TmpChineseFontStatus.Ready)
                return status;

            TmpChineseFontSettingsWriter.ApplyDefaultIfNeeded(
                fontAsset,
                registry.useAsDefaultFont);
            registry.designated = fontAsset;
            registry.previous = null;
            TmpChineseFontRegistry.Save(registry);
            return TmpChineseFontStatus.Ready;
        }

        private static void Register()
        {
            TmpChineseFontInstaller.TmpEnsure = Ensure;
        }

        private static void OnImportPackageCompleted(string packageName)
        {
            if (!EditorPrefs.GetBool(TmpChineseFontPaths.EssentialsPendingPref, false))
                return;

            essentialsImportCompleted = true;
            Register();
            TmpChineseFontInstaller.Ensure();
        }

        private static string EnsureTmpEssentials()
        {
            if (EditorPrefs.GetBool(TmpChineseFontPaths.EssentialsPendingPref, false))
            {
                if (!essentialsImportCompleted)
                    return TmpChineseFontStatus.EssentialsImportPending;

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!TmpChineseFontSettingsWriter.TmpSettingsPresent())
                {
                    Debug.LogError("TMP Essentials import completed without creating TMP Settings.");
                    return TmpChineseFontStatus.EssentialsImportFailed;
                }

                return Ensure();
            }

            CloseTmpResourceImporterWindows();
            var essentialsPath = TmpChineseFontPaths.ResolvedTmpEssentialsUnityPackage();
            if (string.IsNullOrEmpty(essentialsPath) || !File.Exists(essentialsPath))
            {
                Debug.LogError("TMP Essential Resources package is missing.");
                return TmpChineseFontStatus.EssentialsPackageMissing;
            }

            AssetDatabase.ImportPackage(essentialsPath, false);
            EditorPrefs.SetBool(TmpChineseFontPaths.EssentialsPendingPref, true);
            return TmpChineseFontStatus.EssentialsImportStarted;
        }

        private static bool CopyBundledFileIfMissing(string source, string destination)
        {
            var projectRoot = TmpChineseFontPaths.ProjectRoot();
            var destinationFullPath = string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, destination);
            if (!string.IsNullOrEmpty(destinationFullPath) && File.Exists(destinationFullPath))
                return false;

            FileUtil.CopyFileOrDirectory(source, destination);
            return true;
        }

        private static void CloseTmpResourceImporterWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == null)
                    continue;

                var type = window.GetType();
                if (type.FullName == "TMPro.TMP_PackageResourceImporterWindow")
                    window.Close();
            }
        }
    }
}
#endif
