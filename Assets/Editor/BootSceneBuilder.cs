#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 创建 Aesir 启动场景：空场景 + 唯一物体 GameBootstrap（挂启动脚本与 AudioListener），
    /// 登记 Build Settings index 0。幂等，可重复执行。
    /// </summary>
    public static class BootSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("Tools/Game/Create Boot Scene")]
        public static void CreateBootScene()
        {
            // 尽力保存当前已打开场景（未命名场景不弹窗、自动跳过）
            EditorSceneManager.SaveOpenScenes();

            // 空场景：无 Main Camera、无 AudioListener、无灯光
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 唯一物体：启动脚本 + AudioListener（场景无相机，音频监听必须手动补，且恰好一个）
            var bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<GameBootstrap>();
            bootstrap.AddComponent<AudioListener>();

            // 保存场景（SaveScene 不自动建父目录，须先建目录并刷新）
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[BootSceneBuilder] 启动场景保存失败：{ScenePath}");
                return;
            }

            // Build Settings 登记为 index 0（幂等：先移除同路径旧条目再前插）
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            Debug.Log($"[BootSceneBuilder] 启动场景已创建并置为 Build Settings index 0：{ScenePath}");
        }
    }
}
#endif
