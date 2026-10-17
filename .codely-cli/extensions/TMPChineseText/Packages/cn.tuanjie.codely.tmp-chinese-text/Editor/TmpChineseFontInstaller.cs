using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    internal enum TmpPackageInstallRequestState
    {
        None = 0,
        InFlight = 1,
        Succeeded = 2,
        Failed = 3
    }

    public static class TmpChineseFontInstaller
    {
        public static Func<string> TmpEnsure;
        internal static Func<bool> DetectTmpPackage;
        internal static Func<string> StartTmpPackageInstall;
        internal static Func<TmpPackageInstallRequestState> ProbeTmpPackageInstall;

        private static AddRequest addRequest;
        private static bool ensureScheduled;

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            ScheduleEnsureWhenIdle();
        }

        private static void ScheduleEnsureWhenIdle()
        {
            if (ensureScheduled)
                return;
            ensureScheduled = true;
            EditorApplication.update -= TickEnsureWhenIdle;
            EditorApplication.update += TickEnsureWhenIdle;
        }

        private static void TickEnsureWhenIdle()
        {
            if (TmpPackagePresent() ||
                EditorPrefs.GetBool(
                    TmpChineseFontPaths.PackageInstallFailedPrefKey(), false) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopEnsureTick();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            StopEnsureTick();
            Ensure();
        }

        private static void StopEnsureTick()
        {
            ensureScheduled = false;
            EditorApplication.update -= TickEnsureWhenIdle;
        }

        public static string Ensure()
        {
            string status;
            if (!TmpChineseFontEditMode.CanWrite(out status))
                return status;

            if (!TmpPackagePresent())
            {
                EditorPrefs.DeleteKey(TmpChineseFontPaths.PackageInstallFailedPrefKey());
                return EnsureTmpPackage();
            }

            EditorPrefs.DeleteKey(TmpChineseFontPaths.PackageInstallPendingPrefKey());
            EditorPrefs.DeleteKey(TmpChineseFontPaths.PackageInstallFailedPrefKey());

            if (TmpEnsure == null)
            {
                Debug.Log("TextMesh Pro is installed, waiting for its installer to initialize.");
                return TmpChineseFontStatus.PackageInstallPending;
            }

            return TmpEnsure();
        }

        private static string EnsureTmpPackage()
        {
            if (EditorPrefs.GetBool(
                    TmpChineseFontPaths.PackageInstallPendingPrefKey(), false))
            {
                var state = CurrentInstallRequestState();
                if (state == TmpPackageInstallRequestState.InFlight)
                    return TmpChineseFontStatus.PackageInstallPending;
                if (state == TmpPackageInstallRequestState.Failed)
                {
                    Debug.LogError("Failed to install the TextMesh Pro package.");
                    EditorPrefs.DeleteKey(
                        TmpChineseFontPaths.PackageInstallPendingPrefKey());
                    EditorPrefs.SetBool(
                        TmpChineseFontPaths.PackageInstallFailedPrefKey(),
                        true);
                    addRequest = null;
                    EditorApplication.update -= PollAddRequest;
                    return TmpChineseFontStatus.PackageInstallFailed;
                }

                if (state == TmpPackageInstallRequestState.Succeeded)
                    return TmpChineseFontStatus.PackageInstallPending;
            }

            return StartInstall();
        }

        private static string StartInstall()
        {
            if (StartTmpPackageInstall != null)
                return StartTmpPackageInstall();

            try
            {
                addRequest = Client.Add(TmpChineseFontPaths.TmpPackageId);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Failed to install the TextMesh Pro package: " + exception.Message);
                EditorPrefs.DeleteKey(
                    TmpChineseFontPaths.PackageInstallPendingPrefKey());
                EditorPrefs.SetBool(
                    TmpChineseFontPaths.PackageInstallFailedPrefKey(), true);
                return TmpChineseFontStatus.PackageInstallFailed;
            }

            EditorPrefs.SetBool(
                TmpChineseFontPaths.PackageInstallPendingPrefKey(), true);
            EditorApplication.update -= PollAddRequest;
            EditorApplication.update += PollAddRequest;
            return TmpChineseFontStatus.PackageInstallStarted;
        }

        private static TmpPackageInstallRequestState CurrentInstallRequestState()
        {
            if (ProbeTmpPackageInstall != null)
                return ProbeTmpPackageInstall();
            if (addRequest == null)
                return TmpPackageInstallRequestState.None;
            if (!addRequest.IsCompleted)
                return TmpPackageInstallRequestState.InFlight;
            if (addRequest.Status == StatusCode.Failure)
                return TmpPackageInstallRequestState.Failed;
            return TmpPackageInstallRequestState.Succeeded;
        }

        private static void PollAddRequest()
        {
            if (addRequest == null || !addRequest.IsCompleted)
                return;

            if (addRequest.Status == StatusCode.Failure)
            {
                var detail = addRequest.Error != null
                    ? addRequest.Error.message
                    : "unknown error";
                Debug.LogError("Failed to install the TextMesh Pro package: " + detail);
                EditorPrefs.DeleteKey(TmpChineseFontPaths.PackageInstallPendingPrefKey());
                EditorPrefs.SetBool(TmpChineseFontPaths.PackageInstallFailedPrefKey(), true);
                addRequest = null;
                EditorApplication.update -= PollAddRequest;
                return;
            }

            EditorApplication.update -= PollAddRequest;
            Ensure();
        }

        internal static bool TmpPackagePresent()
        {
            if (DetectTmpPackage != null)
                return DetectTmpPackage();
            return TmpChineseFontPaths.TmpRuntimePresent();
        }
    }
}
