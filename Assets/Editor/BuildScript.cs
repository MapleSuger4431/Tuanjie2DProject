using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行打包脚本（赛前验证 / 比赛期间快速出包都用它）
/// 用法示例：
///   Tuanjie.exe -batchmode -projectPath "&lt;工程路径&gt;" -executeMethod BuildScript.BuildAndroid -logFile "&lt;日志路径&gt;"
/// </summary>
public static class BuildScript
{
    public static void BuildAndroid()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outDir = Path.Combine(projectRoot, "Builds", "Android");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "2DProject.apk");

        // 包名必须显式设置：
        // 工程里 companyName=DefaultCompany、productName=2DProject，Unity 会推导出
        // com.DefaultCompany.2DProject —— 其中 "2DProject" 以数字开头，不是合法包名，
        // 直接打包会报 "Package Name has not been set up correctly"。
        // 注意：必须按平台取/设（GetApplicationIdentifier 取的是「当前激活平台」的包名，
        // 批处理模式下当前平台不是 Android，所以这里显式指定 Android）。
        string appId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        Debug.Log("[BuildScript] 当前 Android 包名 = [" + appId + "]");
        if (string.IsNullOrEmpty(appId) || appId.StartsWith("com.DefaultCompany"))
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.maplesuger.tuanjie2d");
            Debug.Log("[BuildScript] 已将 Android 包名设为 com.maplesuger.tuanjie2d（比赛前请改成正式包名）");
            AssetDatabase.SaveAssets();
        }

        // 收集场景：优先用 Build Settings 里启用的；若为空，自动收 Assets/Scenes 下所有 .scene
        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled) scenes.Add(s.path);
        }
        if (scenes.Count == 0)
        {
            foreach (var f in Directory.GetFiles("Assets/Scenes", "*.scene", SearchOption.AllDirectories))
            {
                scenes.Add(f.Replace('\\', '/'));
            }
        }
        Debug.Log("[BuildScript] scenes = " + string.Join(", ", scenes));

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log(string.Format(
            "[BuildScript] RESULT={0} ERRORS={1} WARNINGS={2} SIZE={3} OUT={4}",
            summary.result, summary.totalErrors, summary.totalWarnings, summary.totalSize, outPath));

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildScript] BUILD FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildScript] BUILD OK");
            EditorApplication.Exit(0);
        }
    }
}
